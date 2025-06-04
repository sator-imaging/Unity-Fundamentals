// (c) 2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    public static class UpmPackageTestKit
    {
        const string MENU_ROOT = nameof(UnityFundamentals) + "/UPM Package Test Kit/";
        const string SCRIPT_FULL_NAME = nameof(SatorImaging) + "." + nameof(UpmPackageTestKit);

        const string BUILD_OUTPUT_DIR_NAME = "__BUILD";
        const string BUILD_OUTPUT_EXE_REL_PATH = BUILD_OUTPUT_DIR_NAME + "/" + SCRIPT_FULL_NAME + ".exe";

        const string UNITY_EXE_REL_PATH = "Editor/Unity.exe";
        const string BUILD_SCENE_PATH = "Assets/Main.unity";
        const string BUILD_SCRIPT_PATH = "Assets/Editor/BuildScript.cs";  // must be in Editor folder!!

        /// <summary>
        /// </summary>
        const string TMPL_UNITY_SCENE =
@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1286492270
GameObject:
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1286492273}
  - component: {fileID: 1286492272}
  m_Name: Main Camera
  m_TagString: MainCamera
  m_IsActive: 1
--- !u!4 &1286492273
Transform:
  m_GameObject: {fileID: 1286492270}
--- !u!20 &1286492272
Camera:
  m_GameObject: {fileID: 1286492270}
  m_Enabled: 1
  serializedVersion: 2
"
        ;

        /// <summary>
        /// <code>
        /// 0: Package name
        /// 1: File path (ex: `file:***`) or semantic version
        /// </code>
        /// </summary>
        const string TMPL_MANIFEST_JSON =
@"{{
  ""dependencies"": {{
    ""{0}"": ""{1}""
  }}
}}
"
        ;

        /// <summary>
        /// <code>
        /// 0: Version (ex: 6000.0.23f1)
        /// </code>
        /// </summary>
        const string TMPL_PROJECT_VERSION_TXT =
@"m_EditorVersion: {0}
"
        ;

        // NOTE: Unity does NOT correctly report ERRORLEVEL.
        //       MUST check the built app existence.
        /// <summary>
        /// <code>
        /// 0: Target folder path
        /// 1: Display unity version
        /// 2: Unity exe file path
        /// 3: Build output exe file path
        /// </code>
        /// </summary>
        const string TMPL_BATCH_CMD =
@"@echo off

pushd ""{0}""

echo Building App with {1}...

""{2}""  ^
    -batchmode  ^
    -quit  ^
    -logFile ""build.log""  ^
    -projectPath "".""  ^
    -executeMethod ""BuildScript.Build""

if NOT EXIST ""{3}"" (
    echo.
    echo.
    echo.
    echo        ERROR OCCURRED
    echo.
    echo.
    echo.
    timeout /nobreak -1
)
"
        ;

        /// <summary>
        /// <code>
        /// 0: Target batch file name
        /// 1: Batch command file path
        /// </code>
        /// </summary>
        const string TMPL_RUN_ALL_CMD =
@"@echo off

set /p PAUSE=Press 'Enter' to start building test apps...

echo.

for /r %%i in ({0}*.bat) do (
    @call ""%%i""
)

echo.
echo.
echo    Successfully Finished
echo.

explorer /select,""{1}""

timeout /nobreak -1
"
        ;

        /// <summary>
        /// <code>
        /// 0: Unity scene relative path
        /// 1: Built app.exe relative path
        /// </code>
        /// </summary>
        const string TMPL_BUILD_SCRIPT =
@"using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

public class BuildScript
{{
    public static void Build()
    {{
        var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        var named = NamedBuildTarget.FromBuildTargetGroup(group);

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(group, Il2CppCompilerConfiguration.Debug);
#if UNITY_2022_3_OR_NEWER == false
        EditorUserBuildSettings.il2CppCodeGeneration = Il2CppCodeGeneration.OptimizeSize;
#else
        PlayerSettings.SetIl2CppCodeGeneration(named, Il2CppCodeGeneration.OptimizeSize);
#endif

        BuildPipeline.BuildPlayer(
            new[] {{ new EditorBuildSettingsScene(""{0}"", true) }},
            ""{1}"",
            BuildTarget.StandaloneWindows64,
            BuildOptions.None
            );
    }}
}}
"
        ;


        /*  GUI  ================================================================ */

        [Serializable]
        struct PackageJson
        {
            public string name;
            public Repository repository;

            [Serializable]
            public struct Repository
            {
                public string type;
                public string url;
            }
        }


        [MenuItem(MENU_ROOT + "Generate Clean Projects for Selected \"package.json\"")]
        static void Create_Clean_Environments_for_Selected_Package_Json()
        {
            CreateCleanEnvironmentForSelectedPackageJson();
        }

        [MenuItem(MENU_ROOT + "Explore Default Output Folder...")]
        static void Explore_Default_Output_Folder()
        {
            var folderPath = GetDefaultOutputFolderPath();
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            EditorUtility.RevealInFinder(folderPath);
        }


        const string MENU_MIN_VERSION = MENU_ROOT + ">   ";
        const int PRIORITY_MIN_VER = 2000;
        [MenuItem(MENU_ROOT + "Minimum Supported Version", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_Version() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 6.1 ", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_6100() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 6.0 ", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_6000() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 2022", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_2022() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 2021", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_2021() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 2020", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_2020() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 2019", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_2019() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 2018", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_2018() => AutoSetMinimumVersion();
        [MenuItem(MENU_MIN_VERSION + "Unity 5.x ", priority = PRIORITY_MIN_VER)] static void Set_Min_Target_5() => AutoSetMinimumVersion();

        [MenuItem(MENU_MIN_VERSION + "Show Installed Unity Versions", priority = PRIORITY_MIN_VER)]
        static void Show_Installed_Unity_Versions()
        {
            var unityInstalls = GetInstalledUnityversionDirPathsByMajorVersion().OrderBy(x => x.Key).AsEnumerable();
            foreach (var install in unityInstalls)
            {
                UnityEngine.Debug.Log($"[{nameof(UpmPackageTestKit)}] Installed Unity {install.Key}...\n{string.Join("\n", install)}\n");
            }
        }


        public static int MinimumSupportedVersion { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = 2021;

        public static bool UseMinimumInstalledVersion { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = true;


        /*  GUI impl  ================================================================ */

        static void CreateCleanEnvironmentForSelectedPackageJson()
        {
            var unityInstalls = GetInstalledUnityversionDirPathsByMajorVersion().OrderBy(x => x.Key).AsEnumerable();

            const string ERROR_MSG = "select `package.json` file";

            var selected = Selection.GetFiltered<TextAsset>(SelectionMode.Assets);
            if (selected?.Length != 1)
                throw new Exception(ERROR_MSG);

            var packageFilePath = Path.GetFullPath(AssetDatabase.GetAssetPath(selected[0])).ToPosixPath();
            if (!packageFilePath.EndsWith("/package.json", StringComparison.Ordinal))
                throw new Exception(ERROR_MSG);

            var packageData = JsonUtility.FromJson<PackageJson>(File.ReadAllText(packageFilePath));

            var packageFilePathOrGitUrl = "file:" + Path.GetDirectoryName(packageFilePath).ToPosixPath();

            if (packageData.repository.type == "git")
            {
                var gitUrl = packageData.repository.url;
                if (gitUrl.AsSpan().Contains(".git", StringComparison.OrdinalIgnoreCase))
                {
                    if (EditorUtility.DisplayDialog(
                        nameof(UpmPackageTestKit),
                        $"Selected 'package.json' has repository url. Would you like to test with remote package?\n\n{gitUrl}",
                        "Yes, Use 'git' URL", "No, Use Local 'package.json'"))
                    {
                        packageFilePathOrGitUrl = gitUrl;
                    }
                }
            }

            unityInstalls = unityInstalls.Where(x => x.Key >= MinimumSupportedVersion);

            string? outputDirPath = null;
            int envCount = 0;
            foreach (var install in unityInstalls)
            {
                string dirPath = UseMinimumInstalledVersion
                    ? install.First()
                    : install.Last()
                    ;

                var exePath = Path.Combine(dirPath, UNITY_EXE_REL_PATH);

                outputDirPath = GenerateCleanEnvironment(
                    Path.GetFileName(dirPath),
                    packageData.name,
                    packageFilePathOrGitUrl,
                    openWithExplorer: envCount == 0);

                envCount++;

                File.WriteAllText(Path.Combine(outputDirPath, $"{SCRIPT_FULL_NAME}.bat"),
                    string.Format(TMPL_BATCH_CMD, outputDirPath, $"Unity {install.Key}", exePath.ToPosixPath(), BUILD_OUTPUT_EXE_REL_PATH));
            }

            if (outputDirPath != null)
            {
                var parentDirPath = Path.GetDirectoryName(outputDirPath);

                var cmdFilePath = Path.Combine(parentDirPath, "RUN_ALL.bat");
                File.WriteAllText(cmdFilePath,
                    string.Format(TMPL_RUN_ALL_CMD, SCRIPT_FULL_NAME, cmdFilePath));
            }

            UnityEngine.Debug.Log($"[{nameof(UpmPackageTestKit)}] {envCount} environments generated.");
        }


        [InitializeOnLoadMethod]
        static void Editor_InitializeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                MinimumSupportedVersion = PREF_GetMinTargetVersion();
            };
        }


        static void AutoSetMinimumVersion([CallerMemberName] string? takeLastWordAsVersion = null)
        {
            if (int.TryParse(takeLastWordAsVersion.AsSpan(takeLastWordAsVersion.AsSpan().LastIndexOf('_') + 1), out var version))
            {
                MinimumSupportedVersion = version;
                PREF_PutMinTargetVersion();
            }

            UnityEngine.Debug.Log("Minimum Supported Version: " + PREF_GetMinTargetVersion());
        }

        static int PREF_GetMinTargetVersion() => EditorPrefs.GetInt(MENU_ROOT + nameof(MinimumSupportedVersion), MinimumSupportedVersion);
        static void PREF_PutMinTargetVersion() => EditorPrefs.SetInt(MENU_ROOT + nameof(MinimumSupportedVersion), MinimumSupportedVersion);


        /*  CLI  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static string ToPosixPath(this string path) => path.Replace('\\', '/');


        public static ILookup<int, string> GetInstalledUnityversionDirPathsByMajorVersion()
        {
            var dirPaths = GetInstalledUnityVersionDirPaths();
            return dirPaths.ToLookup(x =>
            {
                var version = RetrieveVersionDataFromDirectoryPath(x);
                if (version.Major <= 5)
                {
                    //return (version.Major * 1000) + (version.Minor * 100);
                    return version.Major;
                }
                else if (version.Major >= 6000)
                {
                    return (version.Major) + (version.Minor * 100);
                }
                else
                {
                    return version.Major;
                }
            });
        }


        public static IEnumerable<string> GetInstalledUnityVersionDirPaths()
        {
            var appPath = EditorApplication.applicationPath.ToPosixPath();

            int pos = appPath.LastIndexOf("/Editor/", StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
                throw new Exception("Editor application path cannot be parsed: " + appPath);

            var versionPath = appPath.Substring(0, pos);
            var editorsPath = Path.GetDirectoryName(versionPath);

            if (!Directory.Exists(editorsPath))
                throw new Exception("Editor install folder not found: " + editorsPath);

            var foundEditorDirPaths = Directory.EnumerateDirectories(editorsPath, "*", new EnumerationOptions()
            {
                ReturnSpecialDirectories = false,
                RecurseSubdirectories = false,
            });

            var ordered = foundEditorDirPaths.OrderBy(x => RetrieveVersionDataFromDirectoryPath(x));

            return ordered;
        }


        static Version RetrieveVersionDataFromDirectoryPath(string dirPath)
        {
            var version = Path.GetFileName(dirPath).AsSpan();
            if (version[^2] is >= 'a' and <= 'z')
                version = version[..^2];

            return Version.Parse(version);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static string GetDefaultOutputFolderPath() => Path.Combine(Path.GetTempPath(), SCRIPT_FULL_NAME);


        /// <param name="outputFolderPath"><see langword="null"/> to use <see cref="Path.GetTempPath"/>.</param>
        /// <returns>Resulting output folder path.</returns>
        public static string GenerateCleanEnvironment(
            string fullUnityVersion,
            string packageName,
            string packageVersionOrFilePathOrGitUrl,
            bool openWithExplorer = true,
            string? outputFolderPath = null
            )
        {
            outputFolderPath ??= Path.Combine(
                GetDefaultOutputFolderPath(),
                $"UpmTest--{DateTimeOffset.Now.ToString("O").Replace(':', '-')}--Unity{fullUnityVersion}"
                );

            const string Assets = "Assets";
            const string Packages = "Packages";
            const string ProjectSettings = "ProjectSettings";
            Directory.CreateDirectory(Path.Combine(outputFolderPath, Assets));
            Directory.CreateDirectory(Path.Combine(outputFolderPath, Packages));
            Directory.CreateDirectory(Path.Combine(outputFolderPath, ProjectSettings));
            Directory.CreateDirectory(Path.Combine(outputFolderPath, Path.GetDirectoryName(BUILD_SCRIPT_PATH)));

            File.WriteAllText(Path.Combine(outputFolderPath, BUILD_SCENE_PATH),
                TMPL_UNITY_SCENE);

            File.WriteAllText(Path.Combine(outputFolderPath, BUILD_SCRIPT_PATH),
                string.Format(TMPL_BUILD_SCRIPT, BUILD_SCENE_PATH, BUILD_OUTPUT_EXE_REL_PATH));

            File.WriteAllText(Path.Combine(outputFolderPath, ProjectSettings, "ProjectVersion.txt"),
                string.Format(TMPL_PROJECT_VERSION_TXT, fullUnityVersion));

            File.WriteAllText(Path.Combine(outputFolderPath, Packages, "manifest.json"),
                string.Format(TMPL_MANIFEST_JSON, packageName, packageVersionOrFilePathOrGitUrl));

            if (openWithExplorer)
            {
                EditorUtility.RevealInFinder(outputFolderPath);
            }

            return outputFolderPath;
        }

    }
}
