/** Unity Editor Main Toolbar
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
// add item to left/right area of main toolbar
UnityEditorMainToolbar.Left.Add(new Label("Left"));
UnityEditorMainToolbar.Right.Add(new Label("Right"));

// add item to center area using legacy GUILayout functions
UnityEditorMainToolbar.Center.Add(new IMGUIContainer(() =>  // or .Insert(0, ...)
{
    if (GUILayout.Button("IMGUI Button",
                         EditorStyles.toolbarButton))  // match look with builtin buttons
    {
        EditorUtility.DisplayDialog("DEBUG", "IMGUI Works!!", "Close");
    }
}));
```

 */

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    /// <remarks>
    /// On startup, need to use `EditorApplication.delayCall` or something to wait for Unity Editor initialization.
    /// </remarks>
    public static class UnityEditorMainToolbar
    {
        // NOTE: cannot use static ctor due to changing window layout preset from Unity editor will dispose existing toolbar VisualElement.
        //       there is no way to detect that parent layout is disposed or not. and when VE reference is kept in field, it has never been null.
        //       so always refresh reference and return it.

        /// <inheritdoc cref="UnityEditorMainToolbar"/>
        public static VisualElement Left => Initialize().left;

        /// <inheritdoc cref="UnityEditorMainToolbar"/>
        public static VisualElement Right => Initialize().right;

        /// <inheritdoc cref="UnityEditorMainToolbar"/>
        public static VisualElement Center => Initialize().center;


        static (VisualElement left, VisualElement center, VisualElement right) Initialize()
        {
            //https://qiita.com/iixd_pog/items/ae7220bfeae49750f354
            const string TYPE_TOOLBAR = "UnityEditor.Toolbar";
            var type = typeof(UnityEditor.Editor).Assembly.GetType(TYPE_TOOLBAR)
                ?? throw new NullReferenceException("type not found: " + TYPE_TOOLBAR);

            var objs = Resources.FindObjectsOfTypeAll(type);
            if (objs.Length == 0 || objs[0] == null)
                throw new NullReferenceException("toolbar not found");

            var toolbar = objs[0];

            FieldInfo? m_Root;
            m_Root = toolbar.GetType().GetField(nameof(m_Root), BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new NullReferenceException("field not found: " + nameof(m_Root));

            var root = m_Root.GetValue(toolbar) as VisualElement
                ?? throw new NullReferenceException("root layout not found");

            const string ID_LEFT = "ToolbarZoneLeftAlign";
            const string ID_RIGHT = "ToolbarZoneRightAlign";
            const string ID_CENTER = "ToolbarZonePlayMode";

            return (
                left: root.Q(ID_LEFT) ?? throw new NullReferenceException(ID_LEFT),
                center: root.Q(ID_CENTER) ?? throw new NullReferenceException(ID_CENTER),
                right: root.Q(ID_RIGHT) ?? throw new NullReferenceException(ID_RIGHT)
                );
        }
    }
}




#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.Editor.DEBUG.Unity_Editor_Main_Toolbar  // must be unique. don't reuse existing namespace
{
    public static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(Unity_Editor_Main_Toolbar) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Add_Debug_Items), priority = 0)]
        public static void Add_Debug_Items()
        {
            // add item to left/right area of main toolbar
            UnityEditorMainToolbar.Left.Add(new Label("Left"));
            UnityEditorMainToolbar.Right.Add(new Label("Right"));

            // place at left/right side of play controls in toolbar
            UnityEditorMainToolbar.Center.Add(new Label("Center-Right"));
            UnityEditorMainToolbar.Center.Insert(0, new Label("Center-Left"));

            // use legacy GUILayout functions
            UnityEditorMainToolbar.Center.Add(new IMGUIContainer(() =>
            {
                if (GUILayout.Button("IMGUI Button",
                                     EditorStyles.toolbarButton))  // match look with builtin buttons
                {
                    EditorUtility.DisplayDialog("DEBUG", "IMGUI Works!!", "Close");
                }
            }));
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Delete_All_Toolbar_Items), priority = 0)]
        public static void Delete_All_Toolbar_Items()
        {
            while (UnityEditorMainToolbar.Left.childCount > 0)
                UnityEditorMainToolbar.Left.RemoveAt(0);

            while (UnityEditorMainToolbar.Center.childCount > 0)
                UnityEditorMainToolbar.Center.RemoveAt(0);

            while (UnityEditorMainToolbar.Right.childCount > 0)
                UnityEditorMainToolbar.Right.RemoveAt(0);
        }


        const string PREF_ENABLE_STARTUP_TEST = nameof(SatorImaging) + nameof(UnityEditorMainToolbar) + nameof(PREF_ENABLE_STARTUP_TEST);

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Enable_Startup_Test), priority = 0)]
        public static void Enable_Startup_Test()
        {
            if (!EditorUtility.DisplayDialog(
                "DEBUG",
                "Would you like to register startup test for next Unity session?\n\n* restart required",
                "Yes", "Cancel"))
            {
                return;
            }

            EditorPrefs.SetBool(PREF_ENABLE_STARTUP_TEST, true);
        }


        [UnityEditor.InitializeOnLoadMethod]
        public static void UnityEditor_Initialize()
        {
            if (!EditorPrefs.GetBool(PREF_ENABLE_STARTUP_TEST, false))
                return;

            EditorPrefs.DeleteKey(PREF_ENABLE_STARTUP_TEST);

            // delay required on initialization
            EditorApplication.delayCall += static () =>
            {
                UnityEditorMainToolbar.Left.Add(CreateLegacyLayout("My Left Btn"));
                UnityEditorMainToolbar.Right.Add(CreateLegacyLayout("My Right Btn"));
                UnityEditorMainToolbar.Center.Add(CreateLegacyLayout("My Center-Right Btn"));
                UnityEditorMainToolbar.Center.Insert(0, CreateLegacyLayout("My Center-Left Btn"));
            };
        }

        static IMGUIContainer CreateLegacyLayout(string label)
        {
            return new IMGUIContainer(() =>
            {
                GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.MaxWidth(128));
            });
        }


        /*  TEMPLATE: End of Debug  ================================================================ */
        #endregion    //  TEMPLATE: End of Debug


        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        public static void Basic_Tests()
        {
        }

        */


        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Debug Script...", priority = int.MaxValue - 310)]
        public static void UnityEditorTests_EditDebugScript() => __EditDebugScript();

        static void __EditDebugScript(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
