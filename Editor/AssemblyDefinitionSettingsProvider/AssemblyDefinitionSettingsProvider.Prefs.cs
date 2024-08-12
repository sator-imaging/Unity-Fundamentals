/** Assembly Definitions Manager for Project Settings Panel
 ** (c) 2024 https://github.com/sator-imaging
 ** Licensed under the MIT License
 */

using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    partial class AssemblyDefinitionSettingsProvider
    {
        [Serializable]
        public sealed class Prefs
        {
            // json fields
            [SerializeField] bool enableImplicitRefsOnChanges = false;
            [SerializeField] bool turnOnImplicitRefsOnChangesAfterDomainReloading = false;
            [SerializeField] string[]? implicitReferenceGUIDs;
            [SerializeField] string[]? implicitReferenceNames;

            //properties
            /// <summary></summary>
            /// <value></value>
            public string[]? ImplicitReferenceNames
            {
                get { return implicitReferenceNames; }
                set { implicitReferenceNames = value; }
            }

            public string[]? ImplicitReferenceGUIDs
            {
                get { return implicitReferenceGUIDs; }
                set { implicitReferenceGUIDs = value; }
            }

            public bool EnableImplicitRefsOnChanges
            {
                get { return enableImplicitRefsOnChanges; }
                set { enableImplicitRefsOnChanges = value; }
            }

            public bool TurnOnImplicitRefsOnChangesAfterDomainReloading
            {
                get { return turnOnImplicitRefsOnChangesAfterDomainReloading; }
                set { turnOnImplicitRefsOnChangesAfterDomainReloading = value; }
            }


            // export path
            readonly static string OUTPUT_PATH = Application.dataPath
                + "/../ProjectSettings/" + nameof(AssemblyDefinitionSettingsProvider) + ".json";

            private Prefs()
            {
                if (File.Exists(OUTPUT_PATH))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(OUTPUT_PATH, Encoding.UTF8), this);
            }

            volatile static Prefs? _instance;
            public static Prefs Instance => _instance ?? Interlocked.CompareExchange(ref _instance, new(), null) ?? _instance;

            public void Save() => File.WriteAllText(OUTPUT_PATH, JsonUtility.ToJson(this, true), Encoding.UTF8);
        }

    }
}
