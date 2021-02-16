﻿namespace BlazorRepl.Client.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Components;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Client.Services;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class Repl : IDisposable
    {
        private const string MainComponentCodePrefix = "@page \"/__main\"\n";
        private const string MainUserPagePath = "/__main";

        private DotNetObjectReference<Repl> dotNetInstance;
        private string errorMessage;
        private CodeFile activeCodeFile;

        [Inject]
        public SnippetsService SnippetsService { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public IJSInProcessRuntime JsRuntime { get; set; }

        [Parameter]
        public string SnippetId { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private CodeEditor CodeEditorComponent { get; set; }

        private IDictionary<string, CodeFile> CodeFiles { get; set; } = new Dictionary<string, CodeFile>();

        private IList<string> CodeFileNames { get; set; } = new List<string>();

        private string CodeEditorPath => this.activeCodeFile?.Path;

        private string CodeEditorContent => this.activeCodeFile?.Content;

        private CodeFileType CodeFileType => this.activeCodeFile?.Type ?? CodeFileType.Razor;

        private ActivityManager ActivityManagerComponent { get; set; }

        private IEnumerable<Package> InstalledPackages =>
            this.ActivityManagerComponent?.GetInstalledPackages() ?? Enumerable.Empty<Package>();

        private ICollection<Package> PackagesToRestore { get; set; } = new List<Package>();

        private StaticAssets StaticAssets { get; } = new();

        private bool SaveSnippetPopupVisible { get; set; }

        private string SplittableContainerClass { get; set; } = "splittable-container-full";

        private IReadOnlyCollection<CompilationDiagnostic> Diagnostics { get; set; } = Array.Empty<CompilationDiagnostic>();

        private bool AreDiagnosticsShown { get; set; }

        private string LoaderText { get; set; }

        private bool ShowLoader { get; set; }

        private string SessionId { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        [JSInvokable]
        public async Task TriggerCompileAsync()
        {
            await this.CompileAsync();

            this.StateHasChanged();
        }

        public void Dispose()
        {
            this.dotNetInstance?.Dispose();

            this.PageNotificationsComponent?.Dispose();

            this.JsRuntime.InvokeVoid("App.Repl.dispose", this.SessionId);
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);

                this.JsRuntime.InvokeVoid(
                    "App.Repl.init",
                    "user-code-editor-container",
                    "user-page-window-container",
                    this.dotNetInstance);
            }

            if (!string.IsNullOrWhiteSpace(this.errorMessage) && this.PageNotificationsComponent != null)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, content: this.errorMessage);

                this.errorMessage = null;
            }

            base.OnAfterRender(firstRender);
        }

        protected override async Task OnInitializedAsync()
        {
            this.PageNotificationsComponent?.Clear();

            if (!string.IsNullOrWhiteSpace(this.SnippetId))
            {
                try
                {
                    var snippetResponse = await this.SnippetsService.GetSnippetContentAsync(this.SnippetId);

                    this.CodeFiles = snippetResponse.Files?.ToDictionary(f => f.Path, f => f) ?? new Dictionary<string, CodeFile>();
                    if (!this.CodeFiles.Any())
                    {
                        this.errorMessage = "No files in snippet.";
                    }
                    else
                    {
                        this.activeCodeFile = this.CodeFiles.First().Value;
                        this.PackagesToRestore = snippetResponse.InstalledPackages?.ToList() ?? new List<Package>();
                        this.StaticAssets.Scripts = snippetResponse.StaticAssets?.Scripts ?? new HashSet<string>();
                        this.StaticAssets.Styles = snippetResponse.StaticAssets?.Styles ?? new HashSet<string>();

                        this.StateHasChanged();
                    }
                }
                catch (ArgumentException)
                {
                    this.errorMessage = "Invalid Snippet ID.";
                }
                catch (Exception)
                {
                    this.errorMessage = "Unable to get snippet content. Please try again later.";
                }
            }

            if (!this.CodeFiles.Any())
            {
                this.activeCodeFile = new CodeFile
                {
                    Path = CoreConstants.MainComponentFilePath,
                    Content = CoreConstants.MainComponentDefaultFileContent,
                };
                this.CodeFiles.Add(CoreConstants.MainComponentFilePath, this.activeCodeFile);
            }

            this.CodeFileNames = this.CodeFiles.Keys.ToList();

            await base.OnInitializedAsync();
        }

        private async Task CompileAsync()
        {
            var sw = Stopwatch.StartNew();

            this.ShowLoader = true;
            this.LoaderText = "Processing";

            await Task.Delay(1); // Ensure rendering has time to be called

            if (this.PackagesToRestore.Any())
            {
                await this.ActivityManagerComponent.RestorePackagesAsync();
            }

            CompileToAssemblyResult compilationResult = null;
            CodeFile mainComponent = null;
            string originalMainComponentContent = null;
            try
            {
                this.UpdateActiveCodeFileContent();

                // Add the necessary main component code prefix and store the original content so we can revert right after compilation.
                if (this.CodeFiles.TryGetValue(CoreConstants.MainComponentFilePath, out mainComponent))
                {
                    originalMainComponentContent = mainComponent.Content;
                    mainComponent.Content = MainComponentCodePrefix + originalMainComponentContent;
                }

                compilationResult = await this.CompilationService.CompileToAssemblyAsync(
                    this.CodeFiles.Values,
                    this.UpdateLoaderTextAsync);

                this.Diagnostics = compilationResult.Diagnostics.OrderByDescending(x => x.Severity).ThenBy(x => x.Code).ToList();
                this.AreDiagnosticsShown = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                this.PageNotificationsComponent.AddNotification(NotificationType.Error, content: "Error while compiling the code.");
            }
            finally
            {
                if (mainComponent != null)
                {
                    mainComponent.Content = originalMainComponentContent;
                }

                this.ShowLoader = false;
            }

            if (compilationResult?.AssemblyBytes?.Length > 0)
            {
                // Make sure the DLL is updated before reloading the user page
                await this.JsRuntime.InvokeVoidAsync("App.CodeExecution.updateUserComponentsDll", compilationResult.AssemblyBytes);

                var userPagePath = this.InstalledPackages.Any() || this.StaticAssets.Scripts.Any() || this.StaticAssets.Styles.Any()
                    ? $"{MainUserPagePath}#{this.SessionId}"
                    : MainUserPagePath;

                // TODO: Add error page in iframe
                this.JsRuntime.InvokeVoid("App.reloadIFrame", "user-page-window", userPagePath);
            }

            Console.WriteLine($"FULL RUN: {sw.Elapsed}");
        }

        private void ShowSaveSnippetPopup()
        {
            this.UpdateActiveCodeFileContent();

            this.SaveSnippetPopupVisible = true;
        }

        private void HandleTabActivate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            this.UpdateActiveCodeFileContent();

            if (this.CodeFiles.TryGetValue(name, out var codeFile))
            {
                this.activeCodeFile = codeFile;

                this.CodeEditorComponent.Focus();
            }
        }

        private void HandleTabClose(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            this.CodeFiles.Remove(name);
        }

        private void HandleTabCreate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(name);

            var newCodeFile = new CodeFile { Path = name };

            newCodeFile.Content = newCodeFile.Type == CodeFileType.CSharp
                ? string.Format(CoreConstants.DefaultCSharpFileContentFormat, nameWithoutExtension)
                : string.Format(CoreConstants.DefaultRazorFileContentFormat, nameWithoutExtension);

            this.CodeFiles.TryAdd(name, newCodeFile);

            // TODO: update method name when refactoring the code editor JS module
            this.JsRuntime.InvokeVoid(
                "App.Repl.setCodeEditorContainerHeight",
                newCodeFile.Type == CodeFileType.CSharp ? "csharp" : "razor");
        }

        private void HandleScaffoldStartupSettingClick()
        {
            this.UpdateActiveCodeFileContent();

            if (!this.CodeFiles.TryGetValue(CoreConstants.StartupClassFilePath, out var startupCodeFile))
            {
                startupCodeFile = new CodeFile
                {
                    Path = CoreConstants.StartupClassFilePath,
                    Content = CoreConstants.StartupClassDefaultFileContent,
                };

                this.CodeFiles.Add(CoreConstants.StartupClassFilePath, startupCodeFile);

                this.CodeFileNames = this.CodeFiles.Keys.ToList();
            }

            this.activeCodeFile = startupCodeFile;

            // TODO: update method name when refactoring the code editor JS module
            this.JsRuntime.InvokeVoid("App.Repl.setCodeEditorContainerHeight", "csharp");
        }

        private async Task HandleActivityManagerVisibleChangedAsync(bool activityManagerVisible)
        {
            this.SplittableContainerClass = activityManagerVisible ? "splittable-container-shrunk" : "splittable-container-full";

            this.StateHasChanged();
            await Task.Delay(1); // Ensure rendering has time to be called

            this.CodeEditorComponent.Resize();
        }

        private void UpdateActiveCodeFileContent()
        {
            if (this.activeCodeFile == null)
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, "No active file to update.");
                return;
            }

            this.activeCodeFile.Content = this.CodeEditorComponent.GetCode();
        }

        private Task UpdateLoaderTextAsync(string loaderText)
        {
            this.LoaderText = loaderText;

            this.StateHasChanged();
            return Task.Delay(1); // Ensure rendering has time to be called
        }
    }
}
