/** Managed Shell Disposer
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Provides utility methods to avoid creating leaked managed shells.

HOW TO USE
==========
```cs
// dispose an object.
ManagedShell.Dispose(ref unityObj);

// dispose objects in collection and collection together.
ManagedShell.Dispose(ref Array_or_Collection);

// dispose objects in collection only. collection keeps alive.
ManagedShell.Dispose(ReadOnly_Array_or_Collection);

// component disposer which is useful when dispose gameObject by transform reference.
// ie) shorthand for `Destroy(component.gameObject); component = null;`
void DisposeGameObject<T>(ref T component) where T : UnityEngine.Component

// same manner with Dispose() methods.
ManagedShell.DisposeGameObject(ref component);
ManagedShell.DisposeGameObject(ref componentArrayOrCollection);
ManagedShell.DisposeGameObject(readonlyComponentCollection);
```

 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class ManagedShell
    {
        const int LAYER_DEFAULT = 0;
        const string TAG_DEFAULT = "Untagged";
        const string NAME_PREFIX = "__MS_DESTROYED__";

        static void Dispose_Internal<T>(T? obj, float t = 0f)
            where T : UnityEngine.Object
        {
            if (obj == null || obj is Transform)
            {
                //UnityEngine.Debug.LogWarning("[ManagedShell] destroying null or transform doesn't make sense: " + obj);
                return;
            }

#if MANAGED_SHELL_DISABLE_DEEP_DESTROY == false
            // NOTE: as of actual object deletion is happened at the end of frame, GameObject.Find(),
            //       GameObject.FindWithTag() or other Unity native functions unexpectedly retrieve
            //       destroyed objects. thus need to "hide" destroyed object from those functions.
            //       * error could occur if IDisposable.Dispose() is depending on modified properties.
            //         however it's edge case and here is only chance to hide object before destroy.
            if (t <= 0)
            {
                // component.name will change gameObject.name. ignore it.
                if (obj is not Component)
                {
                    // 1) must be unique due to obj.name may be used as dictionary key
                    // 2) empty or short string causes problem when slicing or something w/o bounds check

                    Span<char> span = stackalloc char[32];  // max possible: 27 "__MS_DESTROYED__-2147483648"

                    NAME_PREFIX.AsSpan().CopyTo(span);
                    obj.GetHashCode().TryFormat(span.Slice(NAME_PREFIX.Length), out var written);
                    written += NAME_PREFIX.Length;

                    obj.name = new string(span.Slice(0, written))
#if UNITY_EDITOR
                        // for debugging
                        + obj.name
#endif
                        ;
                }

                if (obj is GameObject go)
                {
                    go.SetActive(false);
                    // to hide from GetComponentsInChildren<T>(true)
                    go.transform.SetParent(null, false);
                    go.tag = TAG_DEFAULT;
                    go.layer = LAYER_DEFAULT;
                    //go.hideFlags = ;
                }
            }
#endif

#if MANAGED_SHELL_DISABLE_DISPOSABLE_CHECKS == false
            // mono behaviour might implement IDisposable. super ultra rare case.
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();

                // NOTE: won't be null!!
                //if (obj == null)
                //    return;
            }
#endif

#if UNITY_EDITOR
            if (Application.IsPlaying(obj))
            {
                UnityEngine.Object.Destroy(obj, t);
            }
            else if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(obj)))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
#else
            UnityEngine.Object.Destroy(obj, t);
#endif
        }


        /// <summary>NOTE: Do nothing when obj is null or Transform component.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose<T>([MaybeNull] ref T obj, float t = 0f)
            where T : UnityEngine.Object
        {
            Dispose_Internal(obj, t);
            obj = null;
        }

        /// <summary>
        /// This method ensures variable doesn't have reference to deleted component.
        /// Especially useful when destroying GameObject by Transform reference.
        /// </summary>
        /// <remarks>Shorthand for `Destroy(component.gameObject); component = null;`</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeGameObject<T>([MaybeNull] ref T component, float t = 0f)
            where T : Component
        {
            Dispose_Internal(component.gameObject, t);
            component = null;
        }


        /*  Transform overloads  ================================================================ */

        // NOTE: these overloads are to show error when trying to dispose Transform instance
        const string ERROR_TRANSFORM = "Disposing Transform does nothing. Use DisposeGameObject() instead.";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ThrowOnTransform()
        {
            // TODO: [Obsolete("...", true)] sometimes doesn't show error on Visual Studio.
            //       don't throw for workaround.
            //throw new NotSupportedException(ERROR_TRANSFORM);
        }

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ref Transform transform, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(Transform?[] array, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ref Transform?[] array, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(List<Transform?> list, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ref List<Transform?> list, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ICollection<Transform?> collection, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ref ICollection<Transform?> collection, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(IDictionary<Transform, Transform?> dict, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose(ref IDictionary<Transform, Transform?> dict, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose<T>(IDictionary<T, Transform?> dict, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose<T>(ref IDictionary<T, Transform?> dict, float t = 0f) => ThrowOnTransform();

        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose<T>(IDictionary<Transform, T?> dict, float t = 0f) => ThrowOnTransform();
        [Obsolete(ERROR_TRANSFORM, true)] public static void Dispose<T>(ref IDictionary<Transform, T?> dict, float t = 0f) => ThrowOnTransform();


        /*  Dispose collection overloads  ================================================================ */

        /// <summary>
        /// Dispose objects in collection and collection together.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose<T>([MaybeNull] ref T?[] array, float t = 0f)
            where T : UnityEngine.Object
        {
            Dispose(array, t);
            array = null;
        }

        /// <summary>
        /// For readonly collection to dispose only elements.
        /// <br/>
        /// Use <see langword="ref"/> overload instead to dispose elements and collection together.
        /// </summary>
        public static void Dispose<T>(T?[] array, float t = 0f)
            where T : UnityEngine.Object
        {
            for (int i = 0; i < array.Length; i++)
            {
                Dispose_Internal(array[i], t);
                array[i] = null;
            }
        }


        /// <inheritdoc cref="Dispose{T}(ref T[], float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose<T>([MaybeNull] ref List<T?> list, float t = 0f)
            where T : UnityEngine.Object
        {
            Dispose(list, t);
            list = null;
        }

        /// <inheritdoc cref="Dispose{T}(T[], float)"/>
        public static void Dispose<T>(List<T?> list, float t = 0f)
            where T : UnityEngine.Object
        {
            for (int i = 0; i < list.Count; i++)
            {
                Dispose_Internal(list[i], t);
            }
            list.Clear();
        }


        /// <inheritdoc cref="Dispose{T}(ref T[], float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose<T>([MaybeNull] ref ICollection<T?> collection, float t = 0f)
            where T : UnityEngine.Object
        {
            Dispose(collection, t);
            collection = null;
        }

        /// <inheritdoc cref="Dispose{T}(T[], float)"/>
        public static void Dispose<T>(ICollection<T?> collection, float t = 0f)
            where T : UnityEngine.Object
        {
            foreach (var item in collection)
            {
                Dispose_Internal(item, t);
            }
            collection.Clear();
        }


        /// <inheritdoc cref="Dispose{T}(ref T[], float)"/>
        /// <remarks>
        /// <c>Dispose()</c> will be called if <c>TKey</c> and/or <c>TValue</c> implements <see cref="IDisposable"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose<TKey, TValue>([MaybeNull] ref IDictionary<TKey, TValue?> dict, float t = 0f)
        {
            Dispose(dict, t);
            dict = null;
        }

        /// <inheritdoc cref="Dispose{T}(T[], float)"/>
        /// <inheritdoc cref="Dispose{TKey, TValue}(ref IDictionary{TKey, TValue}, float)"/>
        public static void Dispose<TKey, TValue>(IDictionary<TKey, TValue?> dict, float t = 0f)
        {
            foreach (var key in dict.Keys)
            {
                //value
                if (dict[key] is UnityEngine.Object valOb)
                {
                    Dispose_Internal(valOb, t);
                }
#if MANAGED_SHELL_DISABLE_DISPOSABLE_CHECKS == false
                else if (dict[key] is IDisposable disposable)
                {
                    disposable.Dispose();
                }
#endif

                //key
                if (key is UnityEngine.Object keyOb)
                {
                    Dispose_Internal(keyOb, t);
                }
#if MANAGED_SHELL_DISABLE_DISPOSABLE_CHECKS == false
                else if (key is IDisposable disposable)
                {
                    disposable.Dispose();
                }
#endif
            }
            dict.Clear();
        }


        /*  DisposeGameObject collection overloads  ================================================================ */

        /// <summary>
        /// Dispose objects in collection and collection together.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeGameObject<T>([MaybeNull] ref T?[] array, float t = 0f)
            where T : Component
        {
            DisposeGameObject(array, t);
            array = null;
        }

        /// <summary>
        /// For readonly collection to dispose only elements.
        /// <br/>
        /// Use <see langword="ref"/> overload instead to dispose elements and collection together.
        /// </summary>
        public static void DisposeGameObject<T>(T?[] array, float t = 0f)
            where T : Component
        {
            for (int i = 0; i < array.Length; i++)
            {
                var item = array[i];
                if (item == null)
                    continue;
                Dispose_Internal(item.gameObject, t);

                array[i] = null;
            }
        }


        /// <inheritdoc cref="DisposeGameObject{T}(ref T[], float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeGameObject<T>([MaybeNull] ref List<T?> list, float t = 0f)
            where T : Component
        {
            DisposeGameObject(list, t);
            list = null;
        }

        /// <inheritdoc cref="DisposeGameObject{T}(T[], float)"/>
        public static void DisposeGameObject<T>(List<T?> list, float t = 0f)
            where T : Component
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                    continue;
                Dispose_Internal(item.gameObject, t);
            }
            list.Clear();
        }


        /// <inheritdoc cref="DisposeGameObject{T}(ref T[], float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeGameObject<T>([MaybeNull] ref ICollection<T?> collection, float t = 0f)
            where T : Component
        {
            DisposeGameObject(collection, t);
            collection = null;
        }

        /// <inheritdoc cref="DisposeGameObject{T}(T[], float)"/>
        public static void DisposeGameObject<T>(ICollection<T?> collection, float t = 0f)
            where T : Component
        {
            foreach (var item in collection)
            {
                if (item == null)
                    continue;
                Dispose_Internal(item.gameObject, t);
            }
            collection.Clear();
        }


        /// <inheritdoc cref="DisposeGameObject{T}(ref T[], float)"/>
        /// <remarks>
        /// <c>Dispose()</c> will be called if <c>TKey</c> and/or <c>TValue</c> implements <see cref="IDisposable"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeGameObject<TKey, TValue>([MaybeNull] ref IDictionary<TKey, TValue?> dict, float t = 0f)
        {
            DisposeGameObject(dict, t);
            dict = null;
        }

        /// <inheritdoc cref="DisposeGameObject{T}(T[], float)"/>
        /// <inheritdoc cref="DisposeGameObject{TKey, TValue}(ref IDictionary{TKey, TValue}, float)"/>
        public static void DisposeGameObject<TKey, TValue>(IDictionary<TKey, TValue?> dict, float t = 0f)
        {
            foreach (var key in dict.Keys)
            {
                //value
                if (dict[key] is Component valOb)
                {
                    Dispose_Internal(valOb.gameObject, t);
                }
#if MANAGED_SHELL_DISABLE_DISPOSABLE_CHECKS == false
                else if (dict[key] is IDisposable disposable)
                {
                    disposable.Dispose();
                }
#endif
                //key
                if (key is Component keyOb)
                {
                    Dispose_Internal(keyOb.gameObject, t);
                }
#if MANAGED_SHELL_DISABLE_DISPOSABLE_CHECKS == false
                else if (key is IDisposable disposable)
                {
                    disposable.Dispose();
                }
#endif
            }
            dict.Clear();
        }

    }
}
