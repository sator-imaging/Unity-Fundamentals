// (c) 2025 Sator Imaging, all rights reserved.
// Licensed under the Apache License version 2.0
// https://github.com/sator-imaging/Unity-Fundamentals

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    public class NugetClient
    {
        const string MIME_JSON = @"application/json";
        const string MIME_ZIP = @"application/zip";

        readonly static MediaTypeWithQualityHeaderValue HEADER_JSON = new(MIME_JSON);
        readonly static MediaTypeWithQualityHeaderValue HEADER_ZIP = new(MIME_ZIP);

        /// <summary>
        /// <list type="table">
        /// <item>0: package name</item>
        /// </list>
        /// </summary>
        readonly static string NUGET_PACKAGE_EP = @"https://api.nuget.org/v3-flatcontainer/{0}/index.json";

        /// <summary>
        /// <list type="table">
        /// <item>0: package name</item>
        /// <item>1: semantic version</item>
        /// </list>
        /// </summary>
        readonly static string NUGET_DOWNLOAD_URL = @"https://www.nuget.org/api/v2/package/{0}/{1}";

        // TODO: grab files from here: https://github.com/dotnet/dotnet-api-docs/tree/main/xml
        //       or build from source: https://github.com/dotnet/standard/tree/v2.1.0
        readonly static string FALLBACK_PACKAGE_NAME = "SatorImaging.CSharpApiDocumentation";

        readonly static Dictionary<string, string> cache_urlByPackageName = new(capacity: 32);

        [Serializable]
        public sealed class Response
        {
            public string[]? versions;
        }


        public static NugetClient Default { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new();


        /*  logger  ================================================================ */

        public static class Logger
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Conditional("DEBUG")]
            public static void Logging(LogType type, object message)
            {
                switch (type)
                {
                    case LogType.Error:
                        UnityEngine.Debug.LogError(message);
                        break;
                    case LogType.Warning:
                        UnityEngine.Debug.LogWarning(message);
                        break;
                    case LogType.Log:
                        UnityEngine.Debug.Log(message);
                        break;
                    case LogType.Exception:
                        UnityEngine.Debug.LogException(message as Exception);
                        break;
                    case LogType.Assert:
                        UnityEngine.Debug.Assert(true, message);
                        break;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Conditional("__DISABLED__li11li11li11l1l1lii11i1li")]
            public static void Verbose(LogType type, object message) => Logging(type, message);
        }


        /*  instance members  ================================================================ */

        const string TEMP_DIR_NAME = nameof(SatorImaging) + "." + nameof(NugetClient);

        private string b_tempFolderPath = Path.Combine(Path.GetTempPath(), TEMP_DIR_NAME).Replace('\\', '/');
        public string TempFolderPath
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => b_tempFolderPath;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("null or whitespace");
                }

                if (value == b_tempFolderPath)
                    return;

                value = value.Replace('\\', '/');

                if (value[^1] == '/')
                {
                    value = value[..^1];
                }

                if (!value.EndsWith('/' + TEMP_DIR_NAME, StringComparison.Ordinal))
                {
                    value += '/' + TEMP_DIR_NAME;
                }

                b_tempFolderPath = value;
            }
        }

        private HttpClient? b_httpClient;
        public HttpClient HttpClient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => b_httpClient ??= new();
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value == b_httpClient)
                    return;

                if (b_httpClient != null)
                {
                    b_httpClient.CancelPendingRequests();
                    b_httpClient.Dispose();
                }

                b_httpClient = value;
            }
        }

        public TimeSpan Timeout { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = TimeSpan.FromSeconds(10);


        /// <returns><see langword="false"/> if failed.</returns>
        /// <inheritdoc cref="TryGetPackageUrlAsync(string, string?, CancellationToken)"/>
        public bool TryGetPackageUrl(string packageName, string? targetVersion, [NotNullWhen(true)] out string? url)
        {
            using var cts = new CancellationTokenSource(Timeout);
            url = Task.Run(async () => await TryGetPackageUrlAsync(packageName, targetVersion, cts.Token)).ConfigureAwait(false).GetAwaiter().GetResult();
            return url != null;
        }

        /// <returns><see langword="false"/> if failed.</returns>
        public bool TryDownloadPackageFile(string url, [NotNullWhen(true)] out string? tempZipFilePath)
        {
            using var cts = new CancellationTokenSource(Timeout);
            tempZipFilePath = Task.Run(async () => await TryDownloadPackageFileAsync(url, cts.Token)).ConfigureAwait(false).GetAwaiter().GetResult();
            return tempZipFilePath != null;
        }


        /* =====  async  ===== */

        /// <param name="targetVersion"><see langword="null"/> to get latest version.</param>
        /// <returns><see langword="null"/> if package is not found.</returns>
        public async ValueTask<string?> TryGetPackageUrlAsync(string packageName, string? targetVersion, CancellationToken cancellationToken = default)
        {
            if (targetVersion == null)
            {
                if (cache_urlByPackageName.TryGetValue(packageName, out var cachedUrl))
                {
                    return cachedUrl;
                }
            }

            var versions = await TryGetAvailableVersionAsync(packageName, cancellationToken);

            string? foundVersion = null;
            if (versions?.Length > 0)
            {
                foundVersion = (targetVersion == null)
                    ? versions[^1]
                    : versions.FirstOrDefault(x => x == targetVersion)
                    ;
            }

            if (foundVersion == null)
            {
                //// fallback!!
                //return await TryGetPackageUrlAsync(FALLBACK_PACKAGE_NAME, cancellationToken);

                return null;
            }

            var url = string.Format(NUGET_DOWNLOAD_URL, packageName, foundVersion);
            cache_urlByPackageName[packageName] = url;
            return url;
        }


        /// <returns><see langword="null"/> if failed to retrieve package versions.</returns>
        public async ValueTask<string[]?> TryGetAvailableVersionAsync(string packageName, CancellationToken cancellationToken = default)
        {
            var client = HttpClient;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(HEADER_JSON);

            try
            {
                var endpoint = string.Format(NUGET_PACKAGE_EP, packageName);
                Logger.Logging(LogType.Log, $"[NuGet] fetching nuget.org for package information: {packageName}\n{endpoint}\n");

                var GET = await client.GetAsync(endpoint, cancellationToken);
                if (!GET.IsSuccessStatusCode)
                    return null;

                var json = await GET.Content.ReadAsStringAsync();
                var response = JsonUtility.FromJson<Response>(json);

                if (response.versions?.Length == 0)
                {
                    response.versions = null;
                }

                return response.versions;
            }
            catch (Exception error)
            {
                Logger.Logging(LogType.Warning, error);
                return null;
            }
        }


        /// <returns><see langword="null"/> if failed to download otherwise file path to downloaded content.</returns>
        public async ValueTask<string?> TryDownloadPackageFileAsync(string url, CancellationToken cancellationToken = default)
        {
            var cacheDirPath = TempFolderPath;
            var cacheFileName = url.Replace("https://www.", string.Empty, StringComparison.OrdinalIgnoreCase)
                                   .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                                   .Replace("/", "--", StringComparison.Ordinal)
                                   .Replace(':', '-')
                                   + ".zip";

            var outputFilePath = Path.Combine(cacheDirPath, cacheFileName);
            if (File.Exists(outputFilePath))
            {
                Logger.Verbose(LogType.Warning, $"Cache file found: {outputFilePath}");
                return outputFilePath;
            }

            if (!Directory.Exists(cacheDirPath))
                Directory.CreateDirectory(cacheDirPath);

            var data = await TryDownloadPackageAsync(url, cancellationToken);
            if (data == null)
                return null;

            await File.WriteAllBytesAsync(outputFilePath, data, cancellationToken);
            return outputFilePath;
        }


        /// <returns><see langword="null"/> if failed or canceled.</returns>
        public async ValueTask<byte[]?> TryDownloadPackageAsync(string url, CancellationToken cancellationToken = default)
        {
            var client = HttpClient;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(HEADER_ZIP);

            try
            {
                var GET = await client.GetAsync(url, cancellationToken);
                if (!GET.IsSuccessStatusCode)
                    return null;

                var result = await GET.Content.ReadAsByteArrayAsync();
                if (result?.Length == 0)
                    result = null;

                return result;
            }
            catch (Exception error)
            {
                Logger.Logging(LogType.Warning, error);
                return null;
            }
        }

    }
}
