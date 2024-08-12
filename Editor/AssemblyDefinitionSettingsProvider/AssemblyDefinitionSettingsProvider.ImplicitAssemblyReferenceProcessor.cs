/** Assembly Definitions Manager for Project Settings Panel
 ** (c) 2024 https://github.com/sator-imaging
 ** Licensed under the MIT License
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals.Editor
{
    partial class AssemblyDefinitionSettingsProvider
    {
        public sealed class ImplicitAssemblyReferenceProcessor : AssetPostprocessor
        {
            const string LOG_PREFIX = "[" + nameof(ImplicitAssemblyReferenceProcessor) + "] ";


            /// <summary>Get or set processing order. Smaller value to be invoked earlier.</summary>
            public int PostprocessOrder { get; set; } = 0;

            public override int GetPostprocessOrder() => PostprocessOrder;


            /*  preprocess  ================================================================ */

            // be static. not sure whether processing is done by one instance or multiple instances
            readonly static HashSet<string> _processedFilePathSet = new();

            public void OnPreprocessAsset()
            {
                if (assetImporter is not AssemblyDefinitionImporter)
                    return;

                if (!Prefs.Instance.EnableImplicitRefsOnChanges)
                    return;

                // avoid unexpected loop
                if (!_processedFilePathSet.Add(assetPath))
                {
                    //Debug.Log(LOG_PREFIX + "already processed: " + assetPath);
                    return;
                }

                ResetIntegratedInspector();
                LockIntegratedInspector(true);
                if (!TryAddImplicitReferences(assetPath))
                {
                    // don't require. domain reloading will happen by asset import --> LockIntegratedInspector(false);
                }
            }


            /*  internals  ================================================================ */

            static StringBuilder? cache_sb;

            public static bool TryAddImplicitReferences(string filePath)
            {
                if (!File.Exists(filePath))
                    return false;

                if (!filePath.StartsWith(ASSETS_DIR_SLASH))
                    return false;

                var jsonText = File.ReadAllText(filePath, Encoding.UTF8);
                var jsonObj = JsonUtility.FromJson<AssemDef_JsonLoader>(jsonText);
                var name = jsonObj?.name
                    ?? throw new NotSupportedException("no assembly name: " + filePath);
                var prefs = Prefs.Instance;

                // don't modify implicit refs source
                if (!(prefs.ImplicitReferenceNames?.Length > 0) || prefs.ImplicitReferenceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    return false;

                bool isGuidMode = true == jsonObj.references?.FirstOrDefault()?.StartsWith(GUID_MODE_PREFIX, StringComparison.OrdinalIgnoreCase);
                var implicitRefs = (isGuidMode ? prefs.ImplicitReferenceGUIDs : prefs.ImplicitReferenceNames).AsEnumerable();

                if (jsonObj.references != null)
                {
                    implicitRefs = implicitRefs
                        .Where(x => !jsonObj.references.Contains(x, StringComparer.OrdinalIgnoreCase))  // ignore case for safe
                        ;
                }

                if (!implicitRefs.Any())
                {
                    return false;
                }

            SPECIAL_CASE_FOR_NEWLY_CREATED_FILE:
                var pos_references = jsonText.AsSpan().IndexOf(JSON_REFS_INSERT_POSITION_FINDER, StringComparison.Ordinal);
                if (pos_references < 0)
                {
                    // NOTE: newly created .asmdef has only "name" property. ie. has no ','
                    if (jsonText.Contains(','))
                    {
                        Debug.LogWarning(LOG_PREFIX + "no references property: " + jsonText);
                        return false;
                    }

                    jsonText = jsonText.AsSpan().TrimEnd()[..^1].TrimEnd().ToString().Replace("\t", JSON_INDENT)
                        + "," + Environment.NewLine + JSON_REFS_INSERT_POSITION_FINDER + "]" + Environment.NewLine + '}';
                    goto SPECIAL_CASE_FOR_NEWLY_CREATED_FILE;
                }

                pos_references += JSON_REFS_INSERT_POSITION_FINDER.Length;

                var sb = (cache_sb ??= new());
                sb.Append(jsonText.AsSpan(0, pos_references));
                sb.Append(JSON_REFS_ARRAY_ITEM_OPEN.AsSpan());
                sb.AppendJoin(JSON_REFS_ARRAY_ITEM_CLOSE, implicitRefs);
                sb.Append('"');  // don't forget closing quote. opening quote is included in ITEM OPEN/CLOSE
                if (jsonText[pos_references] != ']')
                {
                    if (jsonObj.references?.Length > 0)
                        sb.Append(',');
                }
                else
                {
                    sb.AppendLine();
                    sb.Append(JSON_INDENT.AsSpan());
                }
                sb.Append(jsonText.AsSpan(pos_references));

                var result = sb.ToString();
                sb.Length = 0;  // don't clear to keep allocated buffer!

                File.WriteAllText(filePath, result, Encoding.UTF8);
                //AssetDatabase.ImportAsset(filePath);

                Debug.Log(LOG_PREFIX + filePath + "...\n" + result);

                return true;
            }


            readonly static Regex re_emptyRef = new(@"^\s+"""",?[\r\n]+", RegexOptions.Compiled | RegexOptions.Multiline);

            public static bool TryRemoveImplicitReferences(string filePath)
            {
                if (!File.Exists(filePath))
                    return false;

                if (!filePath.StartsWith(ASSETS_DIR_SLASH))
                    return false;

                // try remove both guid and name
                var implicitRefs = Prefs.Instance.ImplicitReferenceGUIDs?.Concat(Prefs.Instance.ImplicitReferenceNames);
                if (implicitRefs == null)
                    return false;

                var contentSpan = File.ReadAllText(filePath, Encoding.UTF8).ToCharArray().AsSpan();
                var originalLength = contentSpan.Length;

                // NOTE: remove only references entries
                int pos_references = ((ReadOnlySpan<char>)contentSpan).IndexOf(JSON_REFS_INSERT_POSITION_FINDER, StringComparison.Ordinal);
                if (pos_references < 0)
                    return false;

                int len_references = contentSpan.Slice(pos_references).IndexOf(']');
                if (len_references < 0)
                    return false;

                var consumed = contentSpan.Length;
                int pos;
                foreach (var item in implicitRefs)
                {
                    // not sure why implicit cast operator doesn't work
                    while ((pos = ((ReadOnlySpan<char>)contentSpan).Slice(pos_references, len_references).IndexOf(item, StringComparison.Ordinal)) >= 0)
                    {
                        pos += pos_references;
                        contentSpan.Slice(pos + item.Length).CopyTo(contentSpan.Slice(pos));

                        consumed -= item.Length;
                        len_references -= item.Length;
                    }
                }

                if (originalLength == consumed)
                {
                    return false;
                }

                var result = contentSpan.Slice(0, consumed).ToString();
                result = re_emptyRef.Replace(result, string.Empty);

                File.WriteAllText(filePath, result, Encoding.UTF8);
                //AssetDatabase.ImportAsset(filePath);

                Debug.Log(LOG_PREFIX + filePath + "...\n" + result);

                return true;
            }

        }

    }
}
