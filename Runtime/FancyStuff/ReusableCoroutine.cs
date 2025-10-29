// (c) 2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public class ReusableCoroutine : IEnumerator
    {
#pragma warning disable IDE1006
        sealed class CoroutineRunner : MonoBehaviour
        {
            new public object gameObject => throw new NotSupportedException();
            new public bool enabled => throw new NotSupportedException();
        }
#pragma warning restore IDE1006

        static CoroutineRunner? _coroutineRunner;

        static CoroutineRunner GetCoroutineRunner()
        {
            if (_coroutineRunner == null)
            {
                var go = new GameObject(nameof(ReusableCoroutine) + "_" + nameof(CoroutineRunner));
                _coroutineRunner = go.AddComponent<CoroutineRunner>();

                if (Application.IsPlaying(go))
                {
                    UnityEngine.Object.DontDestroyOnLoad(go);
                }
            }

            return _coroutineRunner;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TEST/" + nameof(ReusableCoroutine) + "/" + nameof(Get_Coroutine_Runner))]
        static void Get_Coroutine_Runner() => GetCoroutineRunner();
#endif

        [DoesNotReturn] static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);


        /*  instance members  ================================================================ */

        readonly Func<bool> onNext;
        readonly Func<ReusableCoroutine, Exception, bool>? onError;
        readonly Action? onComplete;
        readonly Action? onDispose;
        readonly Func<ReusableCoroutine, bool>? onReset;

        /// <param name="onError"><c>args: (this, error) -> bool markCompleted</c></param>
        /// <param name="onReset"><c>args: (this) -> bool allowReset</c></param>
        /// <param name="timing"><see cref="WaitForEndOfFrame"/> or other.</param>
        public ReusableCoroutine(
            Func<bool> onNext,
            Func<ReusableCoroutine, Exception, bool>? onError = null,
            Action? onComplete = null,
            Action? onDispose = null,
            Func<ReusableCoroutine, bool>? onReset = null,
            object? timing = null
            )
        {
            this.onNext = onNext;
            this.onError = onError;
            this.onComplete = onComplete;
            this.onDispose = onDispose;
            this.onReset = onReset;

            this.Current = timing;
        }

        public object? Current { get; }
        public void Dispose() => onDispose?.Invoke();

        public bool MoveNext()
        {
            try
            {
                bool result = onNext.Invoke();
                if (!result)
                {
                    onComplete?.Invoke();
                    _isCompleted = true;
                }

                return result;
            }
            catch (Exception error)
            {
                if (onError == null || onError.Invoke(this, error))
                {
                    _isCompleted = true;
                }

                throw;
            }
        }


        ReusableCoroutine? _activeCoroutine;
        bool _isCompleted;

        public bool IsStarted => _activeCoroutine != null;
        public bool IsCompleted => _isCompleted;

        public void Reset()
        {
            if (onReset == null || onReset.Invoke(this))
            {
                _activeCoroutine = null;
                _isCompleted = false;
            }
        }


        public void Stop(MonoBehaviour? host = null)
        {
            if (_activeCoroutine == null)
            {
                ThrowInvalidOperation("coroutine has not yet started");
            }

            if (host == null)
                host = GetCoroutineRunner();

            host.StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        public void Start(MonoBehaviour? host = null)
        {
            if (_activeCoroutine != null)
            {
                ThrowInvalidOperation("coroutine has already started");
            }

            if (host == null)
                host = GetCoroutineRunner();

            host.StartCoroutine(this);
            _activeCoroutine = this;
        }


        public async Task StartAsync(CancellationToken ct = default)
        {
            if (Current != null)
            {
                ThrowInvalidOperation("coroutine timing is not null");
            }

            if (_activeCoroutine != null)
            {
                ThrowInvalidOperation("coroutine has already started");
            }

            _activeCoroutine = this;

            while (MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

    }
}
