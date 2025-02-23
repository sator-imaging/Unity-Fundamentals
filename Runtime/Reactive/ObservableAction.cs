/** Observable `event Action<T>` for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

BASIC USAGE
===========
```cs
// ObservableAction<int> is 24 bytes struct. recommend cast to interface once on instantiate.
private event Action<int>? Ev_MyEvent;
IObservableAction<int>? cache_MyEvent;
public IObservableAction<T> MyEvent => cache_MyEvent ??= new ObservableAction<T>(
    cb => Ev_MyEvent += cb,
    cb => Ev_MyEvent -= cb);

MyEvent.Subscribe(val => Debug.Log(val))  // register callback like Rx (reactive extensions)
    .AddTo(collection)  // add subscription to ICollection<IDisposable> for unsubscribing later
    .BindTo(token)      // or, bind to cancellation token to unsubscribe automatically when canceled.

MyEvent.Unsubscribe(existingAction);  // unsubscribe without disposable interface.
```

For parameter-less action events, `VoidObservableAction` can be used for.
Of course you can use `ObservableAction<object?>` or something instead.

```cs
event Action<VoidObservableAction>? Ev_voidEvent;
public ObservableAction<VoidObservableAction> VoidEvent => new(x => Ev_voidEvent += x, x => Ev_voidEvent -= x);

Ev_voidEvent?.Invoke(VoidObservableAction.Default);  // use default to invoke event.
```

 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    // int is most used primitive. use it to eliminate unnecessary type variant generation by IL2CPP.
    public enum VoidObservableAction /*: byte*/ { Default }

    public static class ObservableActionExtensions
    {
        /* =====  ICollection  ===== */

        /// <inheritdoc cref="AddTo{T}(IObservableAction{T}, ICollection{IDisposable})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddTo<T>(this in ObservableAction<T> self, ICollection<IDisposable> collection)
        {
            collection.Add(self);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddTo<T>(this IObservableAction<T> self, ICollection<IDisposable> collection)
        {
            collection.Add(self);
        }


        /* =====  CancellationToken  ===== */

        /// <inheritdoc cref="BindTo{T}(IObservableAction{T}, CancellationToken, bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CancellationTokenRegistration BindTo<T>(this in ObservableAction<T> self, CancellationToken cancellationToken, bool useSynchronizationContext = false)
        {
            return DisposeIfCanceled(self, cancellationToken, useSynchronizationContext);
        }

        /// <summary>Unsubscribe if token is canceled.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CancellationTokenRegistration BindTo<T>(this IObservableAction<T> self, CancellationToken cancellationToken, bool useSynchronizationContext = false)
        {
            return DisposeIfCanceled(self, cancellationToken, useSynchronizationContext);
        }


        readonly static Action<object> InvokeDisposableDispose = static (obj) => ((IDisposable)obj).Dispose();

        // don't make this method generic to achieve devirtualize. anyway it is boxed into object.
        static CancellationTokenRegistration DisposeIfCanceled(IDisposable disposable, CancellationToken cancellationToken, bool useSynchronizationContext = false)
        {
            if (!cancellationToken.CanBeCanceled)
                return default;

            if (ExecutionContext.IsFlowSuppressed())
            {
                return cancellationToken.Register(InvokeDisposableDispose, disposable, useSynchronizationContext);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    return cancellationToken.Register(InvokeDisposableDispose, disposable, useSynchronizationContext);
                }
            }
        }

    }


    public interface IObservableAction<T> : IObservable<T>, IDisposable
    {
        IObservableAction<T> Subscribe(Action<T> act, bool disallowDuplicate = true);
        void Unsubscribe(Action<T> act);
    }


    [StructLayout(LayoutKind.Auto)]
    public readonly struct ObservableAction<T> : IObservableAction<T>, IEquatable<ObservableAction<T>>
    {
        // NOTE: update Equals() and GetHashCode() when changed!!
        readonly Action<Action<T>> sub;
        readonly Action<Action<T>> unsub;
        readonly Action<T>? callback;

        /// <inheritdoc cref="ObservableAction{T}(Action{Action{T}}, Action{Action{T}}, Action{T}, bool)"/>
        public ObservableAction(Action<Action<T>> sub, Action<Action<T>> unsub) : this(sub, unsub, null, false) { }

        /// <summary>
        /// Create <see cref="IObservable{T}"/> with sub (<c>+=</c>) and unsub (<c>-=</c>)
        /// <code>
        /// public ObservableAction&lt;T> MyEvent => new(x => _backingEventField += x, x => _backingEventField -= x);
        /// 
        /// // adding `event` to backing field will make it thread-safe (not only for preventing access from externals)
        /// event Action&lt;T>? _backingEventField;
        /// </code>
        /// </summary>
        /// <param name="disallowDuplicate"><see langword="true"/> to try unsubscribe before subscribe to avoid duplicate registration.</param>
        private ObservableAction(Action<Action<T>> sub, Action<Action<T>> unsub, Action<T>? callback, bool disallowDuplicate)
        {
            this.sub = sub ?? throw new NullReferenceException(nameof(sub));
            this.unsub = unsub ?? throw new NullReferenceException(nameof(unsub));
            this.callback = callback;

            if (callback == null)
                return;

            if (disallowDuplicate)
            {
                unsub.Invoke(callback);
            }

            sub.Invoke(callback);
        }

        public void Dispose()
        {
            if (callback == null)
                return;

            unsub.Invoke(callback);
        }


        /* =====  IEquatable  ===== */

        public override int GetHashCode() => HashCode.Combine(this.sub, this.unsub, this.callback);
        public override bool Equals(object? obj) => obj is ObservableAction<T> other && this.Equals(other);
        public bool Equals(ObservableAction<T> other)
        {
            return EqualityComparer<Action<T>>.Default.Equals((((this.callback!))), (((other.callback!))))
                && EqualityComparer<Action<Action<T>>>.Default.Equals(this.unsub, other.unsub)
                && EqualityComparer<Action<Action<T>>>.Default.Equals(this.sub, other.sub)
                   ;
        }

        public static bool operator ==(in ObservableAction<T> left, in ObservableAction<T> right) => left.Equals(right);
        public static bool operator !=(in ObservableAction<T> left, in ObservableAction<T> right) => !(left == right);


        /* =====  sub/unsub  ===== */

        IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
        {
            return new ObservableAction<T>(sub, unsub, observer.OnNext, false);
        }


        /// <inheritdoc cref="Subscribe(Action{T}, bool)"/>
        IObservableAction<T> IObservableAction<T>.Subscribe(Action<T> act, bool disallowDuplicate)
        {
            return new ObservableAction<T>(sub, unsub, act, disallowDuplicate);
        }

        /// <returns><see cref="IDisposable"/></returns>
        public ObservableAction<T> Subscribe(Action<T> act, bool disallowDuplicate = true)
        {
            return new ObservableAction<T>(sub, unsub, act, disallowDuplicate);
        }


        public void Unsubscribe(Action<T> act)
        {
            unsub.Invoke(act);
        }

    }
}



#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.DEBUG.Observable_Action  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(Observable_Action) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */

        static class TestServer
        {
            static Action<VoidObservableAction>? b_basicEvent;
            static Action<int>? b_intEvent;
            static int _executeCount = 0;

            public static ObservableAction<VoidObservableAction> BasicEvent => new(x => b_basicEvent += x, x => b_basicEvent -= x);
            public static ObservableAction<int> IntEvent => new(x => b_intEvent += x, x => b_intEvent -= x);

            public static readonly List<IDisposable> SubscriptionList = new();

            static CancellationTokenSource? _tokenSource;
            public static void Start()
            {
                if (_tokenSource != null)
                    return;

                _tokenSource ??= new();

                Task.Run(async () =>
                {
                    while (!_tokenSource.Token.IsCancellationRequested)
                    {
                        if (b_basicEvent == null)
                        {
                            UnityEngine.Debug.Log("BasicAction is not set");
                        }
                        else
                        {
                            UnityEngine.Debug.Log("BasicAction callback count: " + b_basicEvent.GetInvocationList().Length);
                            b_basicEvent.Invoke(VoidObservableAction.Default);
                        }

                        if (b_intEvent == null)
                        {
                            UnityEngine.Debug.Log("IntAction is not set");
                        }
                        else
                        {
                            UnityEngine.Debug.Log("IntAction callback count: " + b_intEvent.GetInvocationList().Length);
                            b_intEvent.Invoke(++_executeCount);
                        }

                        await Task.Delay(1000);
                    }
                });
            }

            public static void Stop()
            {
                if (_tokenSource == null)
                    return;

                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _tokenSource = null;
            }

            public static void ClearActions()
            {
                foreach (var d in SubscriptionList)
                    d.Dispose();
            }
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Register_Callback_and_Start_Server), priority = 0)]
        static void Register_Callback_and_Start_Server()
        {
            RegisterCallbacks(true);
            TestServer.Start();
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Register_Callback_and_Start_Server_Allow_Duplicate_Registration), priority = 0)]
        static void Register_Callback_and_Start_Server_Allow_Duplicate_Registration()
        {
            RegisterCallbacks(false);
            TestServer.Start();
        }

        static void RegisterCallbacks(bool disallowDuplicate)
        {
            TestServer.BasicEvent.Subscribe((_) => UnityEngine.Debug.Log("\t Permanent Action"), disallowDuplicate)
                .AddTo(TestServer.SubscriptionList);

            var cts = new CancellationTokenSource(3100);
            TestServer.BasicEvent.Subscribe((_) => UnityEngine.Debug.Log("\t Unsubscribed after 3 seconds"), disallowDuplicate)
                .BindTo(cts.Token);

            TestServer.IntEvent.Subscribe((val) => UnityEngine.Debug.Log("\t Permanent Action: " + val), disallowDuplicate)
                .AddTo(TestServer.SubscriptionList);

            TestServer.IntEvent.Subscribe((val) => UnityEngine.Debug.Log("\t Unsubscribed after 3 seconds: " + val), disallowDuplicate)
                .BindTo(cts.Token);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Clear_Actions), priority = 100)]
        static void Clear_Actions()
        {
            TestServer.ClearActions();
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Stop_Server), priority = 100)]
        static void Stop_Server()
        {
            TestServer.Stop();
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
