﻿namespace BlazorRepl.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Runtime;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.Routing;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.AspNetCore.Razor.Language;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Razor;
    using Microsoft.JSInterop;

    public class CompilationService
    {
        public const string DefaultRootNamespace = "BlazorRepl.UserComponents";

        public static readonly IDictionary<string, string> BaseAssemblyPackageVersionMappings = new Dictionary<string, string>
        {
            ["Microsoft.AspNetCore.Components"] = "5.0.3",
            ["Microsoft.AspNetCore.Components.Forms"] = "5.0.3",
            ["Microsoft.AspNetCore.Components.Web"] = "5.0.3",
            ["Microsoft.AspNetCore.Components.WebAssembly"] = "5.0.3",
            ["Microsoft.Extensions.Configuration"] = "5.0.0",
            ["Microsoft.Extensions.Configuration.Abstractions"] = "5.0.0",
            ["Microsoft.Extensions.Configuration.Json"] = "5.0.0",
            ["Microsoft.Extensions.DependencyInjection"] = "5.0.1",
            ["Microsoft.Extensions.DependencyInjection.Abstractions"] = "5.0.0",
            ["Microsoft.Extensions.Logging"] = "5.0.0",
            ["Microsoft.Extensions.Logging.Abstractions"] = "5.0.0",
            ["Microsoft.Extensions.Options"] = "5.0.0",
            ["Microsoft.Extensions.Primitives"] = "5.0.0",
            ["Microsoft.JSInterop"] = "5.0.3",
            ["Microsoft.JSInterop.WebAssembly"] = "5.0.3",
            ["Microsoft.Win32.Primitives"] = "5.0.0",
            ["System.Collections"] = "4.0.11",
            ["System.Collections.Concurrent"] = "5.0.0",
            ["System.Collections.NonGeneric"] = "5.0.0",
            ["System.ComponentModel"] = "5.0.0",
            ["System.ComponentModel.Annotations"] = "5.0.0",
            ["System.Console"] = "5.0.0",
            ["System.Diagnostics.DiagnosticSource"] = "5.0.1",
            ["System.Diagnostics.Tracing"] = "5.0.0",
            ["System.IO.Compression"] = "5.0.0",
            ["System.IO.Pipelines"] = "5.0.1",
            ["System.Linq"] = "4.1.0",
            ["System.Linq.Expressions"] = "4.1.0",
            ["System.Memory"] = "4.5.4",
            ["System.Net.Http"] = "5.0.0",
            ["System.Net.Http.Json"] = "5.0.0",
            ["System.Net.NameResolution"] = "5.0.0",
            ["System.Net.NetworkInformation"] = "5.0.0",
            ["System.Net.Primitives"] = "5.0.0",
            ["System.Net.Security"] = "5.0.0",
            ["System.Net.Sockets"] = "5.0.0",
            ["System.ObjectModel"] = "4.0.12",
            ["System.Private.Uri"] = "5.0.0",
            ["System.Reflection.Emit"] = "4.0.1",
            ["System.Reflection.Emit.ILGeneration"] = "4.0.1",
            ["System.Reflection.Emit.Lightweight"] = "4.0.1",
            ["System.Reflection.Primitives"] = "4.3.0",
            ["System.Runtime"] = "4.3.0",
            ["System.Runtime.InteropServices"] = "4.1.0",
            ["System.Runtime.InteropServices.RuntimeInformation"] = "5.0.0",
            ["System.Runtime.Loader"] = "4.3.0",
            ["System.Security.Claims"] = "5.0.0",
            ["System.Security.Cryptography.Algorithms"] = "5.0.0",
            ["System.Security.Cryptography.Encoding"] = "5.0.0",
            ["System.Security.Cryptography.Primitives"] = "5.0.0",
            ["System.Security.Cryptography.X509Certificates"] = "5.0.0",
            ["System.Security.Principal.Windows"] = "5.0.0",
            ["System.Text.Encoding.Extensions"] = "4.0.11",
            ["System.Text.Encodings.Web"] = "5.0.0",
            ["System.Text.Json"] = "5.0.1",
            ["System.Threading"] = "4.0.11",
            ["System.Threading.Channels"] = "5.0.0",
        };

        private const string WorkingDirectory = "/BlazorRepl/";
        private const string DefaultImports = @"@using System.ComponentModel.DataAnnotations
@using System.Linq
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop";

        private static readonly CSharpParseOptions CSharpParseOptions = new(LanguageVersion.Preview);
        private static readonly RazorProjectFileSystem RazorProjectFileSystem = new VirtualRazorProjectFileSystem();

        // Creating the initial compilation + reading references is on the order of 250ms without caching
        // so making sure it doesn't happen for each run.
        private static CSharpCompilation baseCompilation;

        public static async Task InitAsync(HttpClient httpClient)
        {
            var basicReferenceAssemblyRoots = new[]
            {
                typeof(Console).Assembly, // System.Console
                typeof(Uri).Assembly, // System.Private.Uri
                typeof(AssemblyTargetedPatchBandAttribute).Assembly, // System.Private.CoreLib
                typeof(NavLink).Assembly, // Microsoft.AspNetCore.Components.Web
                typeof(IQueryable).Assembly, // System.Linq.Expressions
                typeof(HttpClientJsonExtensions).Assembly, // System.Net.Http.Json
                typeof(HttpClient).Assembly, // System.Net.Http
                typeof(IJSRuntime).Assembly, // Microsoft.JSInterop
                typeof(RequiredAttribute).Assembly, // System.ComponentModel.Annotations
                typeof(WebAssemblyHostBuilder).Assembly, // Microsoft.AspNetCore.Components.WebAssembly
            };

            var assemblyNames = basicReferenceAssemblyRoots
                .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat(new[] { assembly.GetName() }))
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            var assemblyStreams = await GetStreamFromHttpAsync(httpClient, assemblyNames);

            var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

            var basicReferenceAssemblies = allReferenceAssemblies
                .Where(a => basicReferenceAssemblyRoots
                    .Select(x => x.GetName().Name)
                    .Union(basicReferenceAssemblyRoots.SelectMany(y => y.GetReferencedAssemblies().Select(z => z.Name)))
                    .Any(n => n == a.Key))
                .Select(a => a.Value)
                .ToList();

            baseCompilation = CSharpCompilation.Create(
                "BlazorRepl.UserComponents",
                references: basicReferenceAssemblies,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    concurrentBuild: false,
                    //// Warnings CS1701 and CS1702 are disabled when compiling in VS too
                    specificDiagnosticOptions: new[]
                    {
                        new KeyValuePair<string, ReportDiagnostic>("CS1701", ReportDiagnostic.Suppress),
                        new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                    }));
        }

        // TODO: think about removal of packages
        public void AddAssemblyReferences(IEnumerable<byte[]> dllsBytes)
        {
            if (dllsBytes == null)
            {
                throw new ArgumentNullException(nameof(dllsBytes));
            }

            var references = dllsBytes.Select(x => MetadataReference.CreateFromImage(x, MetadataReferenceProperties.Assembly));

            baseCompilation = baseCompilation.AddReferences(references);
        }

        public async Task<CompileToAssemblyResult> CompileToAssemblyAsync(
            ICollection<CodeFile> codeFiles,
            Func<string, Task> updateStatusFunc) // TODO: try convert to event
        {
            if (codeFiles == null)
            {
                throw new ArgumentNullException(nameof(codeFiles));
            }

            var cSharpResults = await this.CompileToCSharpAsync(codeFiles, updateStatusFunc);

            var result = CompileToAssembly(cSharpResults);

            return result;
        }

        private static async Task<IDictionary<string, Stream>> GetStreamFromHttpAsync(
            HttpClient httpClient,
            IEnumerable<string> assemblyNames)
        {
            var streams = new ConcurrentDictionary<string, Stream>();

            await Task.WhenAll(
                assemblyNames.Select(async assemblyName =>
                {
                    var result = await httpClient.GetAsync($"/_framework/{assemblyName}.dll");

                    result.EnsureSuccessStatusCode();

                    streams.TryAdd(assemblyName, await result.Content.ReadAsStreamAsync());
                }));

            return streams;
        }

        private static CompileToAssemblyResult CompileToAssembly(IReadOnlyList<CompileToCSharpResult> cSharpResults)
        {
            if (cSharpResults.Any(r => r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)))
            {
                return new CompileToAssemblyResult { Diagnostics = cSharpResults.SelectMany(r => r.Diagnostics).ToList() };
            }

            var syntaxTrees = new SyntaxTree[cSharpResults.Count];
            for (var i = 0; i < cSharpResults.Count; i++)
            {
                var cSharpResult = cSharpResults[i];
                syntaxTrees[i] = CSharpSyntaxTree.ParseText(cSharpResult.Code, CSharpParseOptions, cSharpResult.FilePath);
            }

            var finalCompilation = baseCompilation.AddSyntaxTrees(syntaxTrees);

            var compilationDiagnostics = finalCompilation.GetDiagnostics().Where(d => d.Severity > DiagnosticSeverity.Info);

            var result = new CompileToAssemblyResult
            {
                Compilation = finalCompilation,
                Diagnostics = compilationDiagnostics
                    .Select(CompilationDiagnostic.FromCSharpDiagnostic)
                    .Concat(cSharpResults.SelectMany(r => r.Diagnostics))
                    .ToList(),
            };

            if (result.Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error))
            {
                using var peStream = new MemoryStream();
                finalCompilation.Emit(peStream);

                result.AssemblyBytes = peStream.ToArray();
            }

            return result;
        }

        private static RazorProjectItem CreateRazorProjectItem(string fileName, string fileContent)
        {
            var fullPath = WorkingDirectory + fileName;

            // File paths in Razor are always of the form '/a/b/c.razor'
            var filePath = fileName;
            if (!filePath.StartsWith('/'))
            {
                filePath = '/' + filePath;
            }

            fileContent = fileContent.Replace("\r", string.Empty);

            return new VirtualProjectItem(
                WorkingDirectory,
                filePath,
                fullPath,
                fileName,
                FileKinds.Component,
                Encoding.UTF8.GetBytes(fileContent.TrimStart()));
        }

        private static RazorProjectEngine CreateRazorProjectEngine(IReadOnlyList<MetadataReference> references) =>
            RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem, b =>
            {
                b.SetRootNamespace(DefaultRootNamespace);
                b.AddDefaultImports(DefaultImports);

                // Features that use Roslyn are mandatory for components
                CompilerFeatures.Register(b);

                b.Features.Add(new CompilationTagHelperFeature());
                b.Features.Add(new DefaultMetadataReferenceFeature { References = references });
            });

        private async Task<IReadOnlyList<CompileToCSharpResult>> CompileToCSharpAsync(
            ICollection<CodeFile> codeFiles,
            Func<string, Task> updateStatusFunc)
        {
            await (updateStatusFunc?.Invoke("Preparing Project") ?? Task.CompletedTask);

            // The first phase won't include any metadata references for component discovery. This mirrors what the build does.
            var projectEngine = CreateRazorProjectEngine(Array.Empty<MetadataReference>());

            // Result of generating declarations
            var declarations = new CompileToCSharpResult[codeFiles.Count];
            var index = 0;
            foreach (var codeFile in codeFiles)
            {
                if (codeFile.Type == CodeFileType.Razor)
                {
                    var projectItem = CreateRazorProjectItem(codeFile.Path, codeFile.Content);

                    var codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
                    var cSharpDocument = codeDocument.GetCSharpDocument();

                    declarations[index] = new CompileToCSharpResult
                    {
                        FilePath = codeFile.Path,
                        ProjectItem = projectItem,
                        Code = cSharpDocument.GeneratedCode,
                        Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
                    };
                }
                else
                {
                    declarations[index] = new CompileToCSharpResult
                    {
                        FilePath = codeFile.Path,
                        Code = codeFile.Content,
                        Diagnostics = Enumerable.Empty<CompilationDiagnostic>(), // Will actually be evaluated later
                    };
                }

                index++;
            }

            // Result of doing 'temp' compilation
            var tempAssembly = CompileToAssembly(declarations);
            if (tempAssembly.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return new[] { new CompileToCSharpResult { Diagnostics = tempAssembly.Diagnostics } };
            }

            await (updateStatusFunc?.Invoke("Compiling Assembly") ?? Task.CompletedTask);

            // Add the 'temp' compilation as a metadata reference
            var references = new List<MetadataReference>(baseCompilation.References) { tempAssembly.Compilation.ToMetadataReference() };
            projectEngine = CreateRazorProjectEngine(references);

            var results = new CompileToCSharpResult[declarations.Length];
            for (index = 0; index < declarations.Length; index++)
            {
                var declaration = declarations[index];
                var isRazorDeclaration = declaration.ProjectItem != null;

                if (isRazorDeclaration)
                {
                    var codeDocument = projectEngine.Process(declaration.ProjectItem);
                    var cSharpDocument = codeDocument.GetCSharpDocument();

                    results[index] = new CompileToCSharpResult
                    {
                        FilePath = declaration.FilePath,
                        ProjectItem = declaration.ProjectItem,
                        Code = cSharpDocument.GeneratedCode,
                        Diagnostics = cSharpDocument.Diagnostics.Select(CompilationDiagnostic.FromRazorDiagnostic).ToList(),
                    };
                }
                else
                {
                    results[index] = declaration;
                }
            }

            return results;
        }
    }
}
