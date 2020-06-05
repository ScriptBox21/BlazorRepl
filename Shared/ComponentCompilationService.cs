﻿using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Razor;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations;

namespace BlazorRepl.Shared
{
    public class ComponentCompilationService
    {
        private const string LineEnding = "\n";
        private const string DefaultRootNamespace = "BlazorRepl.UserComponents";
        private const string WorkingDirectory = "/BlazorRepl/";

        public static async Task Init(HttpClient httpClient)
        {
            var basicReferenceAssemblyRoots = new[]
            {
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly, // System.Runtime
                typeof(NavLink).Assembly, // Microsoft.AspNetCore.Components.Web
                typeof(IQueryable).Assembly, // System.Linq
                typeof(HttpClientJsonExtensions).Assembly, // System.Net.Http.Json
                typeof(HttpClient).Assembly, // System.Net.Http
                typeof(IJSRuntime).Assembly, // Microsoft.JSInterop
                typeof(RequiredAttribute).Assembly, // System.ComponentModel.Annotations
            };

            var assemblyNames = basicReferenceAssemblyRoots
                .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat(new[] { assembly.GetName() }))
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            var assemblyStreams = await GetStreamFromHttp(httpClient, assemblyNames);

            var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

            var basicReferenceAssemblies = allReferenceAssemblies
                .Where(a => basicReferenceAssemblyRoots
                    .Select(x => x.GetName().Name)
                    .Union(basicReferenceAssemblyRoots.SelectMany(y => y.GetReferencedAssemblies().Select(z => z.Name)))
                    .Any(n => n == a.Key))
                .Select(a => a.Value)
                .ToList();

            BaseCompilation = CSharpCompilation.Create(
                "BlazorRepl.UserComponent",
                Array.Empty<SyntaxTree>(),
                basicReferenceAssemblies,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            CSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        }

        private static async Task<IDictionary<string, Stream>> GetStreamFromHttp(HttpClient httpClient, IEnumerable<string> assemblyNames)
        {
            var streams = new ConcurrentDictionary<string, Stream>();

            await Task.WhenAll(
                assemblyNames.Select(async assemblyName =>
                {
                    var result = await httpClient.GetAsync($"/_framework/_bin/{assemblyName}.dll");

                    result.EnsureSuccessStatusCode();

                    streams.TryAdd(assemblyName, await result.Content.ReadAsStreamAsync());
                }));

            return streams;
        }

        private static CSharpParseOptions CSharpParseOptions { get; set; }

        private RazorConfiguration Configuration { get; } =
            RazorConfiguration.Create(RazorLanguageVersion.Latest, "MVC-3.0", Array.Empty<RazorExtension>());

        private RazorProjectFileSystem FileSystem { get; } = new VirtualRazorProjectFileSystem();

        // Creating the initial compilation + reading references is on the order of 250ms without caching
        // so making sure it doesn't happen for each run.
        private static CSharpCompilation BaseCompilation;

        public async Task<CompileToAssemblyResult> CompileToAssembly(
            string cshtmlRelativePath,
            string cshtmlContent,
            string preset,
            Func<string, Task> updateStatusFunc)
        {
            var compilation = BaseCompilation;

            var cSharpResult = await this.CompileToCSharp(cshtmlRelativePath, cshtmlContent, compilation, updateStatusFunc);

            await (updateStatusFunc?.Invoke("Compiling Assembly") ?? Task.CompletedTask);
            var result = this.CompileToAssembly(cSharpResult);

            return result;
        }

        public CompileToAssemblyResult CompileToAssembly(CompileToCSharpResult cSharpResult)
        {
            if (cSharpResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return new CompileToAssemblyResult { Diagnostics = cSharpResult.Diagnostics, };
            }

            var syntaxTrees = new[] { Parse(cSharpResult.Code), };

            var compilation = cSharpResult.BaseCompilation.AddSyntaxTrees(syntaxTrees);

            var diagnostics = compilation
                .GetDiagnostics()
                .Where(d => d.Severity > DiagnosticSeverity.Info)
                .ToList();

            var result = new CompileToAssemblyResult
            {
                Compilation = compilation,
                Diagnostics = diagnostics.Select(CompilationDiagnostic.FromCSharpDiagnostic).Concat(cSharpResult.Diagnostics).ToList(),
            };

            if (result.Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error))
            {
                using var peStream = new MemoryStream();
                compilation.Emit(peStream);

                result.AssemblyBytes = peStream.ToArray();
            }

            return result;
        }

        private static CSharpSyntaxTree Parse(string text, string path = null) =>
            (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(text, CSharpParseOptions, path: path);

        private async Task<CompileToCSharpResult> CompileToCSharp(
            string cshtmlRelativePath,
            string cshtmlContent,
            CSharpCompilation compilation,
            Func<string, Task> updateStatusFunc)
        {
            // The first phase won't include any metadata references for component discovery. 
            // This mirrors what the build does.
            var projectEngine = this.CreateProjectEngine(Array.Empty<MetadataReference>());

            // Result of generating declarations
            var projectItem = CreateProjectItem(cshtmlRelativePath, cshtmlContent);

            var codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
            var cSharpDocument = codeDocument.GetCSharpDocument();

            var declaration = new CompileToCSharpResult
            {
                BaseCompilation = compilation,
                Code = cSharpDocument.GeneratedCode,
                Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
            };

            // Result of doing 'temp' compilation
            var tempAssembly = this.CompileToAssembly(declaration);
            if (tempAssembly.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return new CompileToCSharpResult { Diagnostics = tempAssembly.Diagnostics, };
            }

            // Add the 'temp' compilation as a metadata reference
            var references = compilation.References.Concat(new[] { tempAssembly.Compilation.ToMetadataReference() }).ToArray();
            projectEngine = this.CreateProjectEngine(references);

            await (updateStatusFunc?.Invoke("Preparing Project") ?? Task.CompletedTask);

            // Result of real code generation for the document
            codeDocument = projectEngine.Process(projectItem);
            cSharpDocument = codeDocument.GetCSharpDocument();

            return new CompileToCSharpResult
            {
                BaseCompilation = compilation,
                Code = cSharpDocument.GeneratedCode,
                Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
            };
        }

        private static RazorProjectItem CreateProjectItem(string cshtmlRelativePath, string cshtmlContent)
        {
            var fullPath = WorkingDirectory + cshtmlRelativePath;

            // FilePaths in Razor are **always** of the form '/a/b/c.cshtml'
            var filePath = cshtmlRelativePath.Replace('\\', '/');
            if (!filePath.StartsWith('/'))
            {
                filePath = '/' + filePath;
            }

            cshtmlContent = cshtmlContent.Replace("\r", "");

            return new VirtualProjectItem(
                WorkingDirectory,
                filePath,
                fullPath,
                cshtmlRelativePath,
                FileKinds.Component,
                Encoding.UTF8.GetBytes(cshtmlContent.TrimStart()));
        }

        private RazorProjectEngine CreateProjectEngine(IReadOnlyList<MetadataReference> references)
        {
            return RazorProjectEngine.Create(this.Configuration, this.FileSystem, b =>
            {
                b.SetRootNamespace(DefaultRootNamespace);

                // Turn off checksums, we're testing code generation.
                b.Features.Add(new SuppressChecksum());

                b.Phases.Insert(0, new ForceLineEndingPhase(LineEnding));

                // Features that use Roslyn are mandatory for components
                CompilerFeatures.Register(b);

                b.Features.Add(new CompilationTagHelperFeature());
                b.Features.Add(new DefaultMetadataReferenceFeature { References = references, });
            });
        }
    }
}
