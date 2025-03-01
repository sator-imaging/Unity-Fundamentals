/** Observable `UnityEvent`
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
uGuiButton.onClick.Subscribe(() => UnityEngine.Debug.Log("Button.onClick"))
    .AddTo(disposables);         // add to collection to unregister later
    .BindTo(cancellationToken);  // or, unregister event automatically when token is canceled

slider.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val))
    .BindTo(this.destroyCancellationToken);
```

 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.Events;
using UnityEngine.UI;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class ObservableUnityEvent
    {
        public enum Unit { Default }

        [StructLayout(LayoutKind.Auto)]
        public readonly struct Observable : IDisposable, IObservable<Unit>, IEquatable<Observable>
        {
            readonly UnityEvent target;
            readonly UnityAction? act;

            public Observable(UnityEvent target, UnityAction? act)
            {
                this.target = target;
                this.act = act;

                if (act != null)
                {
                    target.AddListener(act);
                }
            }

            public IDisposable Subscribe(IObserver<Unit> observer) => new Observable(target, () => observer.OnNext(Unit.Default));
            public IDisposable Subscribe(UnityAction act) => new Observable(target, act);

            public void Dispose()
            {
                if (this.act == null)
                    return;

                this.target.RemoveListener(this.act);
            }

            public override int GetHashCode() => HashCode.Combine(this.target, this.act);
            public override bool Equals(object? obj) => obj is Observable other && Equals(other);
            public bool Equals(Observable other) => target == other.target && act == other.act;

            public static bool operator ==(Observable left, Observable right) => left.Equals(right);
            public static bool operator !=(Observable left, Observable right) => !(left == right);
        }

        [StructLayout(LayoutKind.Auto)]
        public readonly struct Observable<T> : IDisposable, IObservable<T>, IEquatable<Observable<T>>
        {
            readonly UnityEvent<T> target;
            readonly UnityAction<T>? act;

            public Observable(UnityEvent<T> target, UnityAction<T>? act)
            {
                this.target = target;
                this.act = act;

                if (act != null)
                {
                    target.AddListener(act);
                }
            }

            public IDisposable Subscribe(IObserver<T> observer) => new Observable<T>(target, observer.OnNext);
            public IDisposable Subscribe(UnityAction<T> act) => new Observable<T>(target, act);

            public void Dispose()
            {
                if (this.act == null)
                    return;

                this.target.RemoveListener(this.act);
            }

            public override int GetHashCode() => HashCode.Combine(this.target, this.act);
            public override bool Equals(object? obj) => obj is Observable<T> other && Equals(other);
            public bool Equals(Observable<T> other) => target == other.target && act == other.act;

            public static bool operator ==(Observable<T> left, Observable<T> right) => left.Equals(right);
            public static bool operator !=(Observable<T> left, Observable<T> right) => !(left == right);
        }


        /*  sub  ================================================================ */

        public static IObservable<Unit> AsObservable(this UnityEvent self) => new Observable(self, null);
        public static Observable Subscribe(this UnityEvent self, UnityAction act) => new Observable(self, act);

        public static IObservable<T> AsObservable<T>(this UnityEvent<T> self) => new Observable<T>(self, null);
        public static Observable<T> Subscribe<T>(this UnityEvent<T> self, UnityAction<T> act) => new Observable<T>(self, act);


        /*  unsub  ================================================================ */

        public static void AddTo(this in ObservableUnityEvent.Observable self, ICollection<IDisposable> collection) => collection.Add(self);
        public static void AddTo<T>(this in ObservableUnityEvent.Observable<T> self, ICollection<IDisposable> collection) => collection.Add(self);

        public static CancellationTokenRegistration BindTo(this in ObservableUnityEvent.Observable self, CancellationToken cancellationToken)
            => BindCore(self, cancellationToken);
        public static CancellationTokenRegistration BindTo<T>(this in ObservableUnityEvent.Observable<T> self, CancellationToken cancellationToken)
            => BindCore(self, cancellationToken);

        static CancellationTokenRegistration BindCore(IDisposable self, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return default;

            if (ExecutionContext.IsFlowSuppressed())
            {
                // NOTE: useSynchronizationContext must be true
                return cancellationToken.Register(InvokeDisposableDispose, self, useSynchronizationContext: true);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    // NOTE: useSynchronizationContext must be true
                    return cancellationToken.Register(InvokeDisposableDispose, self, useSynchronizationContext: true);
                }
            }
        }

        readonly static Action<object> InvokeDisposableDispose = static (obj) =>
        {
#if UNITY_EDITOR
            try
#endif
            {
                ((IDisposable)obj).Dispose();
            }
#if UNITY_EDITOR
            catch (Exception exc)
            {
                UnityEngine.Debug.LogException(exc);
            }
#endif
        };
    }
}




#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006  // naming style
#pragma warning disable CA1861   // avoid constant array

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.DEBUG.Observable_UnityEvent  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(Observable_UnityEvent) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */

        static CancellationTokenSource _tokenSource = new();
        readonly static List<IDisposable> _disposableList = new();

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Register_Events_on_Selected_Hierarchy), priority = 0)]
        static void Register_Events_on_Selected_Hierarchy()
        {
            if (!UnityEditor.EditorApplication.isPlaying)
                throw new Exception("Run only on Play Mode");

            var selections = UnityEditor.Selection.transforms ?? Array.Empty<UnityEngine.Transform>();
            if (selections.Length == 0)
            {
                UnityEngine.Debug.LogWarning("Nothing selected");
                return;
            }

            foreach (var sel in selections)
            {
                foreach (var ctrl in sel.GetComponentsInChildren<Toggle>(true))
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<Slider>(true))
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<Scrollbar>(true))
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<ScrollRect>(true))
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<Button>(true))
                    ctrl.onClick.Subscribe(() => UnityEngine.Debug.Log("Button.onClick")).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<Dropdown>(true))
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();

                foreach (var ctrl in sel.GetComponentsInChildren<InputField>(true))
                {
                    ctrl.onValueChanged.Subscribe(val => UnityEngine.Debug.Log(val)).AddTo_BindTo();
                    ctrl.onEndEdit.Subscribe(val => UnityEngine.Debug.Log("InputField.onEndEdit" + val)).AddTo_BindTo();
                    ctrl.onSubmit.Subscribe(val => UnityEngine.Debug.Log("InputField.onSubmit" + val)).AddTo_BindTo();
                }
            }

            UnityEngine.Debug.Log("Events Registered");
        }

        static void AddTo_BindTo(this in ObservableUnityEvent.Observable self)
        {
            self.AddTo(_disposableList);
            self.BindTo(_tokenSource.Token);
        }
        static void AddTo_BindTo<T>(this in ObservableUnityEvent.Observable<T> self)
        {
            self.AddTo(_disposableList);
            self.BindTo(_tokenSource.Token);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Cancel_Token_to_Unsubscribe), priority = int.MaxValue / 2)]
        static void Cancel_Token_to_Unsubscribe()
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _tokenSource = new();

            UnityEngine.Debug.Log("Unregistered by Cnacellation Token");
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Dispose_Collection_to_Unsubscribe), priority = int.MaxValue / 2)]
        static void Dispose_Collection_to_Unsubscribe()
        {
            if (_disposableList.Count == 0)
            {
                UnityEngine.Debug.LogWarning("Nothing in collection");
                return;
            }

            foreach (var d in _disposableList)
                d.Dispose();
            _disposableList.Clear();

            UnityEngine.Debug.Log("Unregistered by Disposable Collection");
        }


        /*  TEMPLATE: End of Debug  ================================================================ */
        #endregion    //  TEMPLATE: End of Debug


        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        static void Basic_Tests()
        {
        }

        */


        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Debug Script...", priority = int.MaxValue - 310)]
        static void UnityEditorTests_EditDebugScript() => __EditDebugScript();

        static void __EditDebugScript(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
