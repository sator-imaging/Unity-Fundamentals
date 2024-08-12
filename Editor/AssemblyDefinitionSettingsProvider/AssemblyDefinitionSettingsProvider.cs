/** Assembly Definitions Manager for Project Settings Panel
 ** (c) 2024 https://github.com/sator-imaging
 ** Licensed under the MIT License
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    public sealed partial class AssemblyDefinitionSettingsProvider : SettingsProvider
    {
        const string LOG_PREFIX = "[" + nameof(AssemblyDefinitionSettingsProvider) + "] ";

        readonly static string DISPLAY_NAME = "Assembly Definitions";

        readonly static string GUID_MODE_PREFIX = "GUID:";
        readonly static string ASSETS_DIR_SLASH = "Assets/";
        //readonly static string PACKAGES_DIR_SLASH = "Packages/";
        readonly static string JSON_INDENT = @"    ";
        readonly static string JSON_REFS_INSERT_POSITION_FINDER = JSON_INDENT + @"""references"": [";
        readonly static string JSON_REFS_ARRAY_ITEM_OPEN = Environment.NewLine + JSON_INDENT + JSON_INDENT + "\"";
        readonly static string JSON_REFS_ARRAY_ITEM_CLOSE = "\"," + JSON_REFS_ARRAY_ITEM_OPEN;

        // json representation
        [Serializable] sealed class AssemDef_JsonLoader { public string? name; public string[]? references; }


        public AssemblyDefinitionSettingsProvider(string path, SettingsScope scopes, IEnumerable<string>? keywords = null)
            : base(path, scopes, keywords) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new AssemblyDefinitionSettingsProvider("Project/" + DISPLAY_NAME, SettingsScope.Project, null);
        }


        /*  activate/deactivate  ================================================================ */

        readonly List<AssemblyDefinitionAsset?> _implicitADefList = new();

        [DescriptionAttribute("tuple: (assetPath, fileNameNoExt)")]
        readonly List<(string assetPath, string fileNameNoExt)> _assetsADefInfoList = new();
        readonly List<GUIContent> _assetsADefLabelList = new();

        public override void OnDeactivate()
        {
            base.OnDeactivate();

            //Debug.Log(LOG_PREFIX + nameof(OnDeactivate));
            Prefs.Instance.Save();
        }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);

            if (_implicitADefList.Count == 0)
            {
                var prefs = Prefs.Instance;  // to load data

                var implicitRefAssets = prefs.ImplicitReferenceGUIDs
                    .Select(static x => AssetDatabase.GUIDToAssetPath(x.Substring(GUID_MODE_PREFIX.Length)))
                    .Select(static x => AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(x))
                    .Where(static x => x != null)
                    ;

                _implicitADefList.AddRange(implicitRefAssets);
            }

            RefreshAssetsADefCaches();
        }


        void RefreshAssetsADefCaches()
        {
            var assetsInAssetsFolder = AssetDatabase.FindAssets("t:" + nameof(AssemblyDefinitionAsset))
                .Select(static guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(static path => path.StartsWith(ASSETS_DIR_SLASH))
                .Select(static path =>
                {
                    var fileNameNoExt = Path.GetFileNameWithoutExtension(path);
                    return (path, fileNameNoExt);
                })
                ;

            _assetsADefInfoList.Clear();
            _assetsADefInfoList.AddRange(assetsInAssetsFolder);

            _assetsADefLabelList.Clear();
            _assetsADefLabelList.AddRange(assetsInAssetsFolder.Select(static x => new GUIContent(x.fileNameNoExt, x.path)));
        }


        /*  OnGUI  ================================================================ */

        readonly GUIContent gui_implicitRefsLabel = new("Implicit Assembly References");
        readonly GUIContent gui_enableImplicitRefsLabel = new("Auto Update .asmdef Files on Import Event");
        readonly GUIContent gui_updateBtnLabel = new("Update All <b>.asmdef</b> *", "Only files in `Assets/` folder");
        readonly GUIContent gui_removeBtnLabel = new("Remove from All");
        readonly GUIContent gui_assetsADefListLabel
            = new("Assembly Definitions in Assets *", "Click on selected list item will unveil .asmdef file in Project panel");
        readonly GUIContent gui_updateADefListLabel = new("Reload List");
        readonly GUIContent gui_dialogHelpText = new("[Integrated Inspector] *experimental");
        //readonly Texture gui_searchIcon = EditorGUIUtility.IconContent("d_Search Icon").image;
        ReorderableList? gui_implicitRefsReorderableList;
        GUIStyle? style_largeBtn;
        GUIStyle? style_linkBtn;
        GUIStyle? style_activeLinkBtn;
        GUIStyle? style_sectionHeaderLabel;
        Vector2 gui_assetsADefScrollPos;
        Vector2 gui_miniInspectorScrollPos;

        // fields must be reset if selection changed
        static UnityEditor.Editor? gui_cachedEditor;
        static string? _activeADefAssetPath;
        static bool _asmdefHasModified_last = false;
        static bool _asmdefHasModified_changed = false;

        static bool _modifingAssetFiles = false;


        public override void OnGUI(string searchContext)
        {
            const int MAX_PANEL_WIDTH = 400;

            base.OnGUI(searchContext);

            if (style_largeBtn == null)
            {
                style_largeBtn = new(EditorStyles.miniButton);
                style_largeBtn.fontSize = 14;
                style_largeBtn.richText = true;
                style_largeBtn.fixedHeight = 28;
                style_largeBtn.padding = new(16, 16, 0, 0);
            }

            if (style_linkBtn == null)
            {
                style_linkBtn = new(EditorStyles.linkLabel);
                style_linkBtn.stretchWidth = true;
                style_linkBtn.fontSize = 13;
                style_linkBtn.fixedHeight = 22;
            }

            if (style_activeLinkBtn == null)
            {
                style_activeLinkBtn = new(EditorStyles.selectionRect);
                style_activeLinkBtn.stretchWidth = style_linkBtn.stretchWidth;
                style_activeLinkBtn.fontSize = style_linkBtn.fontSize;
                style_activeLinkBtn.fixedHeight = style_linkBtn.fixedHeight;
                style_activeLinkBtn.margin = new(0, 0, 0, 0);
                style_activeLinkBtn.padding.left = 6;
            }

            if (style_sectionHeaderLabel == null)
            {
                style_sectionHeaderLabel = new(EditorStyles.largeLabel);
                style_sectionHeaderLabel.fontSize = 15;
                style_sectionHeaderLabel.fixedHeight = 32;
                style_sectionHeaderLabel.stretchWidth = true;
            }

            if (gui_implicitRefsReorderableList == null)
            {
                gui_implicitRefsReorderableList = new(_implicitADefList, typeof(AssemblyDefinitionAsset), true, false, true, true);
                gui_implicitRefsReorderableList.drawElementCallback += (rect, index, isActive, isFocused) =>
                {
                    _implicitADefList[index] = EditorGUI.ObjectField(rect, _implicitADefList[index], typeof(AssemblyDefinitionAsset), false)
                        as AssemblyDefinitionAsset;
                };
                gui_implicitRefsReorderableList.onAddCallback = self =>
                {
                    self.list.Add(null);
                };
            }


            // start!!
            var leftPanelWidth = GUILayout.Width(Math.Min(MAX_PANEL_WIDTH, EditorGUIUtility.currentViewWidth * 0.333f));

            using var rootLayout = new EditorGUILayout.HorizontalScope();

            // left side panel
            using (new EditorGUILayout.VerticalScope(leftPanelWidth))
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField(gui_implicitRefsLabel, style_sectionHeaderLabel);

                // TODO: add "apply all changes" button to allow changing multiple .asmdef files at once.
                bool disallowChangeSelection = false;
                if (gui_cachedEditor is AssetImporterEditor currentEditor)
                {
                    var hasModified = currentEditor.HasModified();
                    _asmdefHasModified_changed = hasModified != _asmdefHasModified_last;
                    _asmdefHasModified_last = hasModified;

                    disallowChangeSelection = hasModified;
                }

                using (new EditorGUI.DisabledScope(disallowChangeSelection || _modifingAssetFiles))
                {
                    // implicit refs list
                    using (var cc = new EditorGUI.ChangeCheckScope())
                    {
                        gui_implicitRefsReorderableList.DoLayoutList();

                        if (cc.changed)
                        {
                            var snapshot = _implicitADefList
                                .Where(static x => x != null)
                                .Select(static x =>
                                {
                                    (string name, string guid) result = (string.Empty, string.Empty);

                                    if (x != null && !string.IsNullOrWhiteSpace(x.text))
                                    {
                                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out var guid, out long id))
                                        {
                                            // NOTE: x.name is FILE NAME, reference requires JSON's NAME
                                            var jsonObj = JsonUtility.FromJson<AssemDef_JsonLoader>(x.text);

                                            result.name = jsonObj?.name ?? string.Empty;
                                            result.guid = GUID_MODE_PREFIX + guid;

                                            //Debug.Log(LOG_PREFIX + result.guid + " / " + result.name);
                                        }
                                    }

                                    return result;
                                })
                                .Where(static x => x.name.Length > 0 && x.guid.Length > 0)
                                .Distinct()
                                ;

                            Prefs.Instance.ImplicitReferenceGUIDs = snapshot.Select(static x => x.guid).ToArray();
                            Prefs.Instance.ImplicitReferenceNames = snapshot.Select(static x => x.name).ToArray();
                        }
                    }


                    EditorGUILayout.Space(-EditorGUIUtility.singleLineHeight * 1.0f);

                    using (new GUILayout.HorizontalScope())
                    {
                        Prefs.Instance.EnableImplicitRefsOnChanges
                            = EditorGUILayout.ToggleLeft(gui_enableImplicitRefsLabel, Prefs.Instance.EnableImplicitRefsOnChanges, EditorStyles.largeLabel);

                        EditorGUILayout.Space();
                    }

                    EditorGUILayout.Space();


                    /* =      add/remove buttons      = */
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(gui_updateBtnLabel, style_largeBtn))
                        {
                            LockAndResetIntegratedInspectorThenUpdateAssets(ImplicitAssemblyReferenceProcessor.TryAddImplicitReferences);
                        }

                        if (GUILayout.Button(gui_removeBtnLabel, style_largeBtn))
                        {
                            LockAndResetIntegratedInspectorThenUpdateAssets(ImplicitAssemblyReferenceProcessor.TryRemoveImplicitReferences);
                        }

                        GUILayout.FlexibleSpace();
                    }


                    /* =      assets ADef list      = */

                    EditorGUILayout.Space();

                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(gui_assetsADefListLabel, style_sectionHeaderLabel);

                        if (GUILayout.Button(gui_updateADefListLabel))
                        {
                            RefreshAssetsADefCaches();
                        }
                    }

                    using (var sv_assetsADef = new GUILayout.ScrollViewScope(gui_assetsADefScrollPos))
                    {
                        gui_assetsADefScrollPos = sv_assetsADef.scrollPosition;

                        for (int i = 0; i < _assetsADefLabelList.Count; i++)
                        {
                            var guiLabel = _assetsADefLabelList[i];
                            var fileNameNoExt = guiLabel.text;
                            var assetPath = guiLabel.tooltip;

                            var btnStyle = assetPath == _activeADefAssetPath ? style_activeLinkBtn : style_linkBtn;
                            if (GUILayout.Button(guiLabel, btnStyle))
                            {
                                if (_activeADefAssetPath == assetPath)
                                {
                                    var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
                                    EditorGUIUtility.PingObject(asset);
                                }
                                else
                                {
                                    var importer = AssetImporter.GetAtPath(assetPath) as AssemblyDefinitionImporter;
                                    if (importer != null)
                                    {
                                        ResetIntegratedInspector();
                                        _activeADefAssetPath = assetPath;  // must be set after Reset

                                        UnityEditor.Editor.CreateCachedEditor(importer, null, ref gui_cachedEditor);

                                        if (gui_cachedEditor is AssetImporterEditor)
                                        {
                                            // required to show Apply/Revert button, when not, any changes will be automatically applied and cause domain reloading
                                            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/AssetPipelineEditor/ImportSettings/AssetImporterEditor.cs#L173
                                            MethodInfo InternalSetAssetImporterTargetEditor;
                                            InternalSetAssetImporterTargetEditor = gui_cachedEditor.GetType()
                                                .GetMethod(nameof(InternalSetAssetImporterTargetEditor), BindingFlags.NonPublic | BindingFlags.Instance);

                                            InternalSetAssetImporterTargetEditor.Invoke(gui_cachedEditor, new object[] { gui_cachedEditor });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }


            /* =      right side panel      = */

            //https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/AssemblyDefinitionImporterInspector.cs
            if (gui_cachedEditor != null)
            {
                using (var sv_miniInspector = new EditorGUILayout.ScrollViewScope(gui_miniInspectorScrollPos))
                {
                    gui_miniInspectorScrollPos = sv_miniInspector.scrollPosition;

                    if (_asmdefHasModified_changed)
                        Prefs.Instance.Save();

                    try
                    {
                        // TODO: don't ignore exception...!
                        // NOTE: when 'Apply' button is pressed, stack overflow will happen ...!!
                        //       ...
                        //       ...
                        //       at UnityEditor.AssetImporters.AssetImporterEditor.get_preview
                        //       at UnityEditor.Editor.ReloadPreviewInstances
                        //       at UnityEditor.Editor.ReloadPreviewInstances
                        //       at UnityEditor.Editor.ReloadPreviewInstances...
                        //       ...
                        //       ...
                        gui_cachedEditor.OnInspectorGUI();
                    }
                    catch
                    {
                        EditorGUIUtility.ExitGUI();

                        ResetIntegratedInspector();

                        EditorGUIUtility.ExitGUI();
                    }
                }
            }
        }


        /*
        public override void OnTitleBarGUI()
        {
            if (_activeADefAssetPath != null)
            {
                EditorGUILayout.HelpBox(gui_dialogHelpText.text, MessageType.Info, true);
            }
        }
        */


        /*  integrated inspector  ================================================================ */

        [InitializeOnLoadMethod]
        static void UnityEditor_Initialize()
        {
            // NOTE: don't remove this!!
            //       reset is required before compilation and selection change!!
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            var prefs = Prefs.Instance;
            if (prefs.TurnOnImplicitRefsOnChangesAfterDomainReloading)
            {
                prefs.EnableImplicitRefsOnChanges = true;
                prefs.TurnOnImplicitRefsOnChangesAfterDomainReloading = false;
                prefs.Save();
            }
        }


        public static void LockIntegratedInspector(bool isLocked) => _modifingAssetFiles = isLocked;


        readonly static Action<object> OnCompilationStarted = _ => ResetIntegratedInspector();

        public static void ResetIntegratedInspector()
        {
            // need to clear cached editor to update inspector correctly
            if (gui_cachedEditor != null)
            {
                var so = gui_cachedEditor.serializedObject;
                UnityEditor.Editor.DestroyImmediate(gui_cachedEditor);
                so.Dispose();  // <-- this is important...!?
                so = null;

                gui_cachedEditor = null;
                _activeADefAssetPath = null;

                _asmdefHasModified_last = false;
                _asmdefHasModified_changed = false;
            }
        }


        public static void LockAndResetIntegratedInspectorThenUpdateAssets(Func<string, bool> updateFunc)
        {
            ResetIntegratedInspector();
            LockIntegratedInspector(true);
            // delay is not required but make UX better
            EditorApplication.delayCall += () =>
            {
                bool isUpdated = false;

                try
                {
                    var assetPaths = AssetDatabase.FindAssets("t:" + nameof(AssemblyDefinitionAsset))
                        .Select(static x => AssetDatabase.GUIDToAssetPath(x));

                    foreach (var path in assetPaths)
                    {
                        isUpdated |= updateFunc(path);
                    }

                    if (isUpdated)
                    {
                        var prefs = Prefs.Instance;
                        prefs.TurnOnImplicitRefsOnChangesAfterDomainReloading = prefs.EnableImplicitRefsOnChanges;
                        prefs.EnableImplicitRefsOnChanges = false;
                        prefs.Save();  // prepare for domain reloading

                        var wnd = EditorWindow.focusedWindow;
                        wnd.ShowNotification(new GUIContent("Reloading Assets..."));

                        AssetDatabase.Refresh();
                    }
                }
                finally
                {
                    // domain reloading will happen if updated
                    if (!isUpdated)
                        LockIntegratedInspector(false);
                }
            };
        }

    }
}
