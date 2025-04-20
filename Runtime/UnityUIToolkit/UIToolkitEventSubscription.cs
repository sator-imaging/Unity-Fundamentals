// (c) 2024-2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

#if STMG_UITOOLKIT_EXISTS

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.UIElements;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>Represents event subscription. <c>Dispose()</c> to unregister event.</summary>
    public interface IUIToolkitEventSubscription : IDisposable
    {
        VisualElement VisualElement { get; }
        Delegate Callback { get; }
        TrickleDown UseTrickleDown { get; }
    }


    /// <inheritdoc cref="IUIToolkitEventSubscription"/>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct UIToolkitEventSubscription<TEventType, TState>
        : IEquatable<UIToolkitEventSubscription<TEventType, TState>>
        , IEqualityComparer<UIToolkitEventSubscription<TEventType, TState>>
        , IUIToolkitEventSubscription
        where TEventType : EventBase<TEventType>, new()
    {
        readonly VisualElement ve;
        readonly EventCallback<TEventType, TState> callback;
        readonly TrickleDown useTrickleDown;

        public UIToolkitEventSubscription(VisualElement ve, EventCallback<TEventType, TState> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
        {
            this.ve = ve;
            this.callback = callback;
            this.useTrickleDown = useTrickleDown;
        }

        readonly public VisualElement VisualElement => this.ve;
        readonly public Delegate Callback => this.callback;
        readonly public TrickleDown UseTrickleDown => this.useTrickleDown;
        readonly public void Dispose()
        {
#if UNITY_EDITOR
            if (ve is UnityEditor.UIElements.GradientField)
            {
                UnityEngine.Debug.LogError("[BUG] cannot unregister callback from 'GradientField' (Unity 2021 LTS)");
            }
#endif
            ve.UnregisterCallback(callback, useTrickleDown);
        }

        readonly public override int GetHashCode() => HashCode.Combine(typeof(UIToolkitEventSubscription<TEventType, TState>), ve, callback);
        readonly public override bool Equals(object? obj) => obj is UIToolkitEventSubscription<TEventType, TState> other && Equals(other);
        readonly public bool Equals(UIToolkitEventSubscription<TEventType, TState> other) => this.ve == other.ve && this.callback == other.callback;

        readonly public bool Equals(UIToolkitEventSubscription<TEventType, TState> x, UIToolkitEventSubscription<TEventType, TState> y) => x.Equals(y);
        readonly public int GetHashCode(UIToolkitEventSubscription<TEventType, TState> obj) => obj.GetHashCode();

        public static bool operator ==(UIToolkitEventSubscription<TEventType, TState> left, UIToolkitEventSubscription<TEventType, TState> right) => left.Equals(right);
        public static bool operator !=(UIToolkitEventSubscription<TEventType, TState> left, UIToolkitEventSubscription<TEventType, TState> right) => !(left == right);

    }


    /// <summary>
    /// Extension methods for <see cref="UIToolkitEventSubscription{TEventType, TState}"/>
    /// </summary>
    public static class UIToolkitEventSubscriptionExtensions
    {
        /// <summary>Register callback and return subscription.</summary>
        /// <returns>Represents event subscription. Call <see cref="IDisposable.Dispose"/> to unregister event.</returns>
        public static UIToolkitEventSubscription<TEventType, TState> RegisterCallbackAsSubscription<TEventType, TState>(
            this VisualElement ve, EventCallback<TEventType, TState> callback, TState userArgs, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            ve.RegisterCallback(callback, userArgs, useTrickleDown);
            return new(ve, callback, useTrickleDown);
        }

        /// <inheritdoc cref="RegisterCallbackAsSubscription{TEventType, TState}(VisualElement, EventCallback{TEventType, TState}, TState, TrickleDown)"/>
        /// <remarks>NOTE: callback 2nd argument (`object?`) is always <see langword="null"/></remarks>
        public static UIToolkitEventSubscription<TEventType, object?> RegisterCallbackAsSubscription<TEventType>(
            this VisualElement ve, EventCallback<TEventType, object?> callback, TrickleDown useTrickleDown = TrickleDown.NoTrickleDown)
            where TEventType : EventBase<TEventType>, new()
        {
            ve.RegisterCallback(callback, null, useTrickleDown);
            return new(ve, callback, useTrickleDown);
        }
    }

}

#endif
