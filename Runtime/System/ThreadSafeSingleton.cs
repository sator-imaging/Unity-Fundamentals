// (c) 2024-2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

using System.Threading;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Instance is created on first request.
    /// </summary>
    abstract public class ThreadSafeSingleton<TSelf>
            where TSelf : ThreadSafeSingleton<TSelf>, new()
    {
        volatile static TSelf? b_instance;
        public static TSelf Instance
        {
            get => b_instance ?? Interlocked.CompareExchange(ref b_instance, new(), null) ?? b_instance;
        }
    }
}
