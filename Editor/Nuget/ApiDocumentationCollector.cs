// (c) 2025 Sator Imaging, all rights reserved.
// Licensed under the Apache License version 2.0
// https://github.com/sator-imaging/Unity-Fundamentals

/**
 * > [!NOTE]
 * > Depending on `NugetClient`
 */

////DEBUG
//#undef UNITY_EDITOR_WIN
//#undef UNITY_EDITOR_OSX

#if UNITY_EDITOR_WIN && UNITY_6000_0_OR_NEWER == false
#define __UNITY_REF_ASSEMBLIES
#endif

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using static SatorImaging.UnityFundamentals.Editor.NugetClient.Logger;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    public static class ApiDocumentationCollector
    {
        public static NugetClient Client { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = NugetClient.Default;

        readonly static string[] FRAMEWORK_PATHS = new[]
        {
            "/netstandard2.1",
            "/netstandard2.0",
            "/netstandard",
            "/netcore",
            "/net10.0",  // TODO: number aware sort cannot be done in .NET standard 2.1
            "/net9.0",
            "/net8.0",
            "/net7.0",
            "/net6.0",
            "/net5.0",
            "/net",     // .net framework
        };

        public readonly static string TARGET_FOLDER =
#if __UNITY_REF_ASSEMBLIES
            @"UnityReferenceAssemblies"
#else
            // for VS Code on macOS
            @"NetStandard"
#endif
            ;
        readonly static string MERGE_TARGET_FOLDER_SLASH =
#if __UNITY_REF_ASSEMBLIES
            "/Facades/"
#else
            // merge all
            "/"
#endif

            ;

        readonly static string EXCLUDED_SUB_DIR = "/unity-engine-api/";

        const string X_DECLARATION = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
        const string X_DOC = "doc";
        const string X_MEMBERS = "members";
        const string X_ASSEMBLY = "assembly";
        const string X_NAME = "name";

        const string EXT_XML = ".xml";
        const string EXT_DLL = ".dll";
        const string EXT_BAT =
#if UNITY_EDITOR_WIN
            ".bat"
#elif UNITY_EDITOR_OSX
            ".command"
#else
            ".platform_not_supported"
#endif
            ;

        readonly static Encoding ENCODER = Encoding.UTF8;
        readonly static EnumerationOptions ENUM_FILES_OPTIONS = new()
        {
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = true,
        };

        public static bool OverwriteExistingFiles { get; set; } = true;

        public static bool DisallowBatchCommandExecution { get; set; } = false;
        public static bool AutoExecuteBatchCommandAfterDownload { get; set; } = true;

        public static string DotNetInstallationPath { get; set; } = "C:/Program Files/dotnet";

        readonly static string[] FALLBACK_PATHS = new[]
        {
            "packs",
            "sdk/NuGetFallbackFolder",
        };

        readonly static string[] SPECIAL_ASSEMBLIES = new[]
        {
            // no '.dll' ext
            "mscorlib",
            "netstandard",
            "System",
            "System.Core",  //LINQ
        };

        // need to get administrator privilege to copy file to 'Program Files'.
        // so extract zip content to temp path and batch copy later.
        readonly struct BatchCommand
        {
            public readonly string TempPath;
            public readonly string OutputPath;

            public BatchCommand(string tempPath, string outputPath)
            {
                // must be posix path even if on windows.
                // DO NOT think that using 'Path.Combine' is better.
                this.TempPath = tempPath.Replace('\\', '/');
                this.OutputPath = outputPath.Replace('\\', '/');
            }

            public string GetBatchCommand()
            {
                var TempPath = this.TempPath;
                var OutputPath = this.OutputPath;

                if (TempPath.StartsWith(Client.TempFolderPath + '/', StringComparison.Ordinal))
                {
                    TempPath = Path.GetFileName(TempPath);
                }

                string result;

#if UNITY_EDITOR_WIN
                TempPath = TempPath.Replace('/', '\\');
                OutputPath = OutputPath.Replace('/', '\\');

                result = $"copy \"{(TempPath + '\"'),-72} \"{OutputPath}\"\n";

#else
                result = $"cp \"{(TempPath + '\"'),-72} \"{OutputPath}\"\n";
#endif

                return result;
            }
        }


        /*  GUI  ================================================================ */

        const string MENU_ROOT = "Tools/C# API Documentation for IDE/";
        const string MENU_PATH = MENU_ROOT + "Download ";
        const int MENU_PRIORITY_DL = int.MaxValue - 3100;
        const int MENU_PRIORITY_EXPLORE = int.MaxValue - 310;

        // Unity 6 automatically sort menu items... need to explicitly specify order by priority.
        [MenuItem(MENU_PATH + "Japanese", priority = MENU_PRIORITY_DL + 0)] static void Download_Documentation_JA() => Download_Confirm_Dialog(TARGET_FOLDER, "ja");
        [MenuItem(MENU_PATH + "English", priority = MENU_PRIORITY_DL + 1)] static void Download_Documentation_EN() => Download_Confirm_Dialog(TARGET_FOLDER, null);
        // following languages are not tested but should work.
        [MenuItem(MENU_PATH + "Deutsch", priority = MENU_PRIORITY_DL + 2)] static void Download_Documentation_DE() => Download_Confirm_Dialog(TARGET_FOLDER, "de");
        [MenuItem(MENU_PATH + "French", priority = MENU_PRIORITY_DL + 3)] static void Download_Documentation_FR() => Download_Confirm_Dialog(TARGET_FOLDER, "fr");
        [MenuItem(MENU_PATH + "Spanish", priority = MENU_PRIORITY_DL + 4)] static void Download_Documentation_ES() => Download_Confirm_Dialog(TARGET_FOLDER, "es");
        [MenuItem(MENU_PATH + "Chinese", priority = MENU_PRIORITY_DL + 5)] static void Download_Documentation_ZH() => Download_Confirm_Dialog(TARGET_FOLDER, "zh");
        [MenuItem(MENU_PATH + "Korean", priority = MENU_PRIORITY_DL + 6)] static void Download_Documentation_KO() => Download_Confirm_Dialog(TARGET_FOLDER, "ko");


        [MenuItem(MENU_ROOT + "Explore Assembly Folder...", priority = MENU_PRIORITY_EXPLORE + 0)]
        static void Explore_Assembly_Folder()
        {
            var path = GetTargetFolderFullPath(TARGET_FOLDER);
            Logging(LogType.Log, path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MENU_ROOT + "Explore Cache Folder...", priority = MENU_PRIORITY_EXPLORE + 1)]
        static void Explore_Cache_Folder()
        {
            var path = Client.TempFolderPath;

            // fix for macOS.
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            Logging(LogType.Log, path);
            EditorUtility.RevealInFinder(path);
        }


        static void Download_Confirm_Dialog(string targetFolder, string? language)
        {
            switch (EditorUtility.DisplayDialogComplex(
                    nameof(ApiDocumentationCollector),
                    "First time download will take so long.\n" +
                    "Would you like to continue?\n" +
                    "\n" +
                    "* Account Privilege Escalation dialog will appear after download is complete.\n" +
                    "\n" +
                    "** Deny escalation will allow you to review batch command WITHOUT executing it.",
                    "Yes, Download and Execute Batch Command", "Cancel", "Yes, but Download Only"))
            {
                case 0: //ok
                    AutoExecuteBatchCommandAfterDownload = true;
                    break;
                case 2: //alt
                    AutoExecuteBatchCommandAfterDownload = false;
                    break;

                case 1: //cancel
                default:
                    return;
            }

            if (!TryDownloadApiDocumentation(targetFolder, language))
            {
                Logging(LogType.Warning, $"[{nameof(ApiDocumentationCollector)}]: Operation canceled.");
            }
        }


        /*  CLI  ================================================================ */

        /// <param name="targetFolder">See <see cref="TARGET_FOLDER"/></param>
        /// <returns><see langword="false"/> if task is canceled.</returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        public static bool TryDownloadApiDocumentation(string targetFolder, string? language = null)
        {
            var targetDirPath = GetTargetFolderFullPath(targetFolder);
            if (!Directory.Exists(targetDirPath))
                throw new DirectoryNotFoundException(targetDirPath);

            string[] fallbackFilePaths = GetFallbackFilePaths();
            var batchList = new List<BatchCommand>(capacity: 32);

            // merge all available xml documents...!!
            var xmlRootMembers = new XElement(X_MEMBERS);

            try
            {
                var dllFilePaths = Directory.EnumerateFiles(targetDirPath, '*' + EXT_DLL, ENUM_FILES_OPTIONS)
                                            .Select(x => x.Replace('\\', '/'))  // normalize!!
                                            .Where(x => !x.Contains(EXCLUDED_SUB_DIR, StringComparison.OrdinalIgnoreCase))  //TODO
                                            ;

                int totalCount = dllFilePaths.Count();
                var missingXmlDocFilePaths = new List<string>(capacity: totalCount / 2);

                int currentIndex = -1;
                foreach (var dllFilePath in dllFilePaths)
                {
                    currentIndex++;

                    var assemblyName = Path.GetFileNameWithoutExtension(dllFilePath);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            nameof(ApiDocumentationCollector),
                            $"[{currentIndex + 1} of {totalCount}] Processing...: {assemblyName}",
                            (float)currentIndex / totalCount))
                    {
                        return false;
                    }

                    var batchCommand = DownloadPackageAndGenerateBatchCommand(dllFilePath, language, fallbackFilePaths);
                    if (batchCommand.HasValue)
                    {
                        batchList.Add(batchCommand.Value);

                        if (dllFilePath.Contains(MERGE_TARGET_FOLDER_SLASH, StringComparison.OrdinalIgnoreCase))
                        {
                            var xdoc = XDocument.Load(batchCommand.Value.TempPath);
                            foreach (var elem in xdoc.Root.Element(X_MEMBERS).Elements())
                            {
                                xmlRootMembers.Add(elem);
                            }
                        }
                    }
                    else
                    {
                        Logging(LogType.Warning, $"[MISSING] {assemblyName} ({dllFilePath})");
                        missingXmlDocFilePaths.Add(dllFilePath);
                    }
                }

                var xdoc_merged = new XDocument();
                xdoc_merged.Add(new XElement(X_DOC));
                xdoc_merged.Root.Add(xmlRootMembers);
                //xdoc_merged.Declaration = new XDeclaration("1.0", "utf-8", "yes");

                if (!TryGenerateMissingApiDocument(xdoc_merged, missingXmlDocFilePaths, batchList))
                {
                    return false;
                }

                RunAsAdministrator(batchList);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return true;
        }

        // NOTE: must be run after all packages were processed.
        /// <returns><see langword="false"/> if task is canceled.</returns>
        static bool TryGenerateMissingApiDocument(XDocument xdoc, List<string> missingXmlDocFilePaths, List<BatchCommand> batchList)
        {
            try
            {
                int totalCount = missingXmlDocFilePaths.Count;

                int currentIndex = -1;
                foreach (var dllFilePath in missingXmlDocFilePaths)
                {
                    currentIndex++;

                    var assemblyName = Path.GetFileNameWithoutExtension(dllFilePath);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            nameof(ApiDocumentationCollector),
                            $"[{currentIndex + 1} of {totalCount}] Finishing...: {assemblyName}",
                            (float)currentIndex / totalCount))
                    {
                        return false;
                    }

                    //if (dllFilePath.Contains(MERGE_TARGET_FOLDER_SLASH, StringComparison.OrdinalIgnoreCase))
                    //    continue;

                    var xAssembly = xdoc.Root.Element(X_ASSEMBLY);
                    if (xAssembly == null)
                    {
                        xAssembly = new XElement(X_ASSEMBLY);
                        xdoc.Root.AddFirst(xAssembly);
                    }

                    var xName = xAssembly.Element(X_NAME);
                    if (xName == null)
                    {
                        xName = new XElement(X_NAME);
                        xAssembly.Add(xName);
                    }

                    xName.Value = assemblyName;

                    var xmlContent = X_DECLARATION + xdoc.ToString(SaveOptions.OmitDuplicateNamespaces)
                                                         .Replace("\r\n", "\n", StringComparison.Ordinal);

                    if (!SPECIAL_ASSEMBLIES.Contains(assemblyName, StringComparer.OrdinalIgnoreCase))
                    {
                        // always has at least one assembly name in Root.assembly.name.
                        var pos = xmlContent.AsSpan().IndexOf(assemblyName, StringComparison.Ordinal);
                        if (pos < 0 || xmlContent.AsSpan(pos + 1).IndexOf(assemblyName, StringComparison.Ordinal) < 0)
                        {
                            Verbose(LogType.Warning, $"Fallback Canceled: {assemblyName}\n{xmlContent}");
                            continue;
                        }
                    }

                    var assemblyFileName = assemblyName + EXT_XML;
                    var tempXmlFilePath = Path.Combine(Client.TempFolderPath, assemblyFileName);

                    File.WriteAllText(tempXmlFilePath, xmlContent);
                    Verbose(LogType.Log, $"[MERGED API DOC] {tempXmlFilePath}\n{xmlContent}");

                    var outputFilePath = Path.Combine(Path.GetDirectoryName(dllFilePath), assemblyFileName);
                    batchList.Add(new(tempXmlFilePath, outputFilePath));

                    // TODO: after exorted, load it and truncate unnecessary node by regex: '[A-Z]:<PACKAGE_NAME>[^\"]+\"'.
                    //       * don't modify 'xdoc' method parameter structure. it is shared across entire process.
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return true;
        }


        /*  internals  ================================================================ */

        static string GetTargetFolderFullPath(string targetFolder) => $"{EditorApplication.applicationContentsPath}/{targetFolder}";

        /// <returns><see langword="null"/> if failed or timeout.</returns>
        static BatchCommand? DownloadPackageAndGenerateBatchCommand(string targetFilePath, string? language, string[] fallbackFilePaths)
        {
            if (!OverwriteExistingFiles)
            {
                if (File.Exists(targetFilePath[..^(EXT_DLL.Length)] + EXT_XML))
                {
                    Verbose(LogType.Warning, $"API Documentation already exists... skipped: {targetFilePath}");
                    return null;
                }
            }

            var targetPackageName = Path.GetFileNameWithoutExtension(targetFilePath);

            Verbose(LogType.Log, $"Target .dll: {targetFilePath}");
            if (!Client.TryGetPackageUrl(targetPackageName, null, out var packageUrl))
            {
                Verbose(LogType.Error, $"Package not found on nuget.org: {targetPackageName}\n");
                return null;
            }

            Verbose(LogType.Log, $"Package url: {packageUrl}");
            if (!Client.TryDownloadPackageFile(packageUrl, out var zipFilePath))
            {
                Verbose(LogType.Error, $"Failed to download package: {packageUrl}");
                return null;
            }

            Verbose(LogType.Log, $"Zip file path: {zipFilePath}");
            using var fs = File.OpenRead(zipFilePath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

            var outputDirPath = Path.GetDirectoryName(targetFilePath);

            BatchCommand? batchCmd = null;
            foreach (var framework in FRAMEWORK_PATHS)
            {
                ZipArchiveEntry? entry = null;

                var candidates = zip.Entries.Where(x => x.Name.Equals($"{targetPackageName}{EXT_XML}", StringComparison.OrdinalIgnoreCase))
                                            .Where(x => x.FullName.Contains(framework, StringComparison.Ordinal))
                                            .OrderBy(x => x.FullName.Length)    // english .dll is shorter path
                                            .ThenByDescending(x => x.FullName)
                                            ;

                if (language != null)
                {
                    entry = candidates.Where(x => x.FullName.Contains('/' + language, StringComparison.Ordinal)).FirstOrDefault();
                }

                // fallback to english
                entry ??= candidates.FirstOrDefault();

                if (entry != null)
                {
                    batchCmd = ExtractFileToTempPath(entry, outputDirPath, language);

                    break;
                }
            }

            if (batchCmd == null)
            {
                const int MIN_XML_DOC_FILE_SIZE = 200;

                string? fallbackPath = null;
                foreach (var framework in FRAMEWORK_PATHS)
                {
                    var candidates = fallbackFilePaths.Where(x => x.EndsWith($"/{targetPackageName}{EXT_XML}", StringComparison.OrdinalIgnoreCase))
                                                      .Where(x => new FileInfo(x).Length > MIN_XML_DOC_FILE_SIZE)
                                                      .Where(x => x.Contains(framework, StringComparison.Ordinal))
                                                      .OrderBy(x => x.Length)    // english .dll is shorter path
                                                      .ThenByDescending(x => x)
                                                      ;

                    if (language != null)
                    {
                        fallbackPath = candidates.Where(x => x.Contains('/' + language, StringComparison.Ordinal)).FirstOrDefault();
                    }

                    fallbackPath ??= candidates.FirstOrDefault();

                    if (fallbackPath != null)
                    {
                        Logging(LogType.Warning, $"[FALLBACK] {targetPackageName} ({fallbackPath})");

                        var tempXmlPath = $"{Client.TempFolderPath}/{targetPackageName}{EXT_XML}";
                        File.Copy(fallbackPath, tempXmlPath, OverwriteExistingFiles);

                        batchCmd = new(tempXmlPath, Path.Combine(outputDirPath, targetPackageName + EXT_XML));

                        break;
                    }
                }
            }

            if (batchCmd == null)
            {
                Verbose(LogType.Error, $"API Documentation not found in zip archive: {zipFilePath}...\n{string.Join("\n", zip.Entries.Select(x => x.FullName))}\n");
                return null;
            }

            Verbose(LogType.Log, $"[FOUND] {Path.GetFileName(batchCmd.Value.OutputPath)}");
            return batchCmd.Value;
        }

        /// <returns><see langword="null"/> if file is not written.</returns>
        static BatchCommand? ExtractFileToTempPath(ZipArchiveEntry entry, string outputDirPath, string? language)
        {
            //// NOTE: do not place localized file correctly to easily swap language file at any time.
            //if (additionalLanguage != null)
            //    outputDirPath = Path.Combine(outputDirPath, additionalLanguage);

            var outputFilePath = Path.Combine(outputDirPath, entry.Name);

            if (!OverwriteExistingFiles)
            {
                if (File.Exists(outputFilePath))
                {
                    Verbose(LogType.Warning, $"API Documentation already exists... skipped: {outputFilePath}");
                    return null;
                }
            }

            var tempPath = Path.Combine(Client.TempFolderPath, entry.Name);
            entry.ExtractToFile(tempPath, true);

            return new(tempPath, outputFilePath);
        }


        static string[] GetFallbackFilePaths()
        {
            IEnumerable<string> filePaths = Array.Empty<string>();

            foreach (var fallbackDir in FALLBACK_PATHS)
            {
                var searchDir = Path.Combine(DotNetInstallationPath, fallbackDir);
                if (!Directory.Exists(searchDir))
                    continue;

                filePaths = filePaths.Concat(
                    Directory.EnumerateFiles(searchDir, '*' + EXT_XML, ENUM_FILES_OPTIONS)  // only System.*
                        .Select(x => x.Replace('\\', '/'))  // normalize!!
                        .Where(x =>
                        {
                            foreach (var framework in FRAMEWORK_PATHS)
                            {
                                if (x.Contains(framework, StringComparison.Ordinal))
                                {
                                    return true;
                                }
                            }

                            return false;
                        })
                        .OrderBy(x => x.Length)    // english .dll is shorter path
                        .ThenByDescending(x => x)
                    );
            }

            var ret = filePaths.ToArray();

            Verbose(LogType.Log, $"[FALLBACK FILE PATHS]\n{string.Join("\n", ret)}");
            return ret;
        }


        static void RunAsAdministrator(List<BatchCommand> batchList)
        {
            var batchFilePath = $"{Client.TempFolderPath}/{nameof(ApiDocumentationCollector)}-Unity{Application.unityVersion}{EXT_BAT}";

            // build batch command
            using (var fs = new FileStream(batchFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {

#if UNITY_EDITOR_WIN == false
                fs.Write(ENCODER.GetBytes("#!/bin/sh\n"));
#endif

                fs.Write(ENCODER.GetBytes($"pushd \"{Client.TempFolderPath}\"\n"));

                foreach (var batch in batchList.OrderBy(x => x.TempPath).ThenBy(x => x.OutputPath))
                {
                    fs.Write(ENCODER.GetBytes(batch.GetBatchCommand()));
                }

                fs.Write(ENCODER.GetBytes(
#if UNITY_EDITOR_WIN
                    "timeout /nobreak -1"
#else
                    "exit"
#endif
                    ));
            }

#if UNITY_EDITOR_WIN == false
            using var CHMOD = Process.Start("/bin/sh", $"-c \"chmod +x \\\"{batchFilePath}\\\"\"");
            if (!CHMOD.HasExited || CHMOD.ExitCode != 0)
            {
                throw new Exception("Failed: /bin/sh chmod +x");
            }
#endif

            // always reveal batch command
            EditorUtility.RevealInFinder(batchFilePath);

            if (DisallowBatchCommandExecution || !AutoExecuteBatchCommandAfterDownload)
            {
                return;
            }

            var procInfo = new ProcessStartInfo
            {
#if UNITY_EDITOR_WIN
                FileName = batchFilePath,
                UseShellExecute = true,
                Verb = "runas",
#elif UNITY_EDITOR_OSX
                FileName = "osascript",
                Arguments = $"-e \"do shell script \\\"{batchFilePath}\\\" with administrator privileges\"",
#endif
                CreateNoWindow = false,
            };

            using var _ = Process.Start(procInfo);
        }

    }
}
