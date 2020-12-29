﻿namespace BlazorRepl.Core.PackageInstallation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NuGet.DependencyResolver;
    using NuGet.Frameworks;
    using NuGet.LibraryModel;
    using NuGet.Packaging;
    using NuGet.Versioning;

    public class NuGetPackageManager
    {
        private static readonly string LibFolderPrefix = $"lib{Path.DirectorySeparatorChar}";

        private readonly RemoteDependencyWalker remoteDependencyWalker;
        private readonly RemoteDependencyProvider remoteDependencyProvider;
        private readonly HttpClient httpClient;

        public NuGetPackageManager(
            RemoteDependencyWalker remoteDependencyWalker,
            RemoteDependencyProvider remoteDependencyProvider,
            HttpClient httpClient)
        {
            this.remoteDependencyWalker = remoteDependencyWalker;
            this.remoteDependencyProvider = remoteDependencyProvider;
            this.httpClient = httpClient;
        }

        public async Task<IDictionary<string, byte[]>> DownloadPackageContentsAsync(string packageName, string packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentOutOfRangeException(nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(packageVersion));
            }

            var libraryRange = new LibraryRange(
                packageName,
                new VersionRange(new NuGetVersion(packageVersion)),
                LibraryDependencyTarget.Package);

            try
            {
                await this.remoteDependencyWalker.WalkAsync(
                    libraryRange,
                    framework: FrameworkConstants.CommonFrameworks.Net50,
                    runtimeIdentifier: null,
                    runtimeGraph: null,
                    recursive: true);

                var packageContents = new Dictionary<string, byte[]>();

                foreach (var package in this.remoteDependencyProvider.PackagesToInstall)
                {
                    var lib = package.Library;
                    var packageBytes = await this.httpClient.GetByteArrayAsync(
                        $"https://api.nuget.org/v3-flatcontainer/{lib.Name}/{lib.Version}/{lib.Name}.{lib.Version}.nupkg");

                    await using var zippedStream = new MemoryStream(packageBytes);
                    using var archive = new ZipArchive(zippedStream);

                    var dlls = ExtractDlls(archive.Entries, package.Framework);
                    foreach (var (fileName, fileBytes) in dlls)
                    {
                        packageContents.Add(fileName, fileBytes);
                    }

                    var scripts = ExtractStaticContents(archive.Entries, ".js");
                    foreach (var (fileName, fileBytes) in scripts)
                    {
                        packageContents.Add(fileName, fileBytes);
                    }

                    var styles = ExtractStaticContents(archive.Entries, ".css");
                    foreach (var (fileName, fileBytes) in styles)
                    {
                        packageContents.Add(fileName, fileBytes);
                    }
                }

                return packageContents;
            }
            finally
            {
                this.remoteDependencyProvider.ClearPackagesToInstall();
            }
        }

        private static IDictionary<string, byte[]> ExtractDlls(IEnumerable<ZipArchiveEntry> entries, NuGetFramework framework)
        {
            var dllEntries = entries.Where(e =>
            {
                if (Path.GetExtension(e.FullName) != ".dll" ||
                    !e.FullName.StartsWith(LibFolderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var path = e.FullName[LibFolderPrefix.Length..];
                var parsedFramework = FrameworkNameUtility.ParseNuGetFrameworkFolderName(path, strictParsing: true, out _);

                return parsedFramework == framework;
            });

            return GetEntriesContent(dllEntries);
        }

        private static IDictionary<string, byte[]> ExtractStaticContents(IEnumerable<ZipArchiveEntry> entries, string extension)
        {
            var staticContentEntries = entries.Where(e => Path.GetExtension(e.Name) == extension);

            return GetEntriesContent(staticContentEntries);
        }

        private static IDictionary<string, byte[]> GetEntriesContent(IEnumerable<ZipArchiveEntry> entries)
        {
            var result = new Dictionary<string, byte[]>();
            foreach (var entry in entries)
            {
                using var memoryStream = new MemoryStream();
                using var entryStream = entry.Open();

                entryStream.CopyTo(memoryStream);
                var entryBytes = memoryStream.ToArray();

                result.Add(entry.Name, entryBytes);
            }

            return result;
        }
    }
}
