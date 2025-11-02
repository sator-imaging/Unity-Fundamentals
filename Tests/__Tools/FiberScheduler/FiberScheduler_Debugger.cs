// (c) 2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

#if DEBUG

using SatorImaging.UnityFundamentals;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

#nullable enable

namespace Tests.SatorImaging.UnityFundamentals.Tools
{
    [DebuggerVisualizer(typeof(FiberScheduler_Debugger))]
    public static class FiberScheduler_Debugger
    {
        const string DEFAULT = "Default Scheduler";
        const string PRIORITY = "Priority Scheduler";

        [Category(DEFAULT)] public static void Concurrency1() => Debug.Log($"Concurrency: {FiberScheduler.Default.RawConcurrencyLevel = 1}");
        [Category(DEFAULT)] public static void ConcurrencyIncrement() => Debug.Log($"Concurrency: {FiberScheduler.Default.AdjustConcurrencyLevel(+1)}");
        [Category(DEFAULT)] public static void ConcurrencyDecrement() => Debug.Log($"Concurrency: {FiberScheduler.Default.AdjustConcurrencyLevel(-1)}");
        [Category(DEFAULT)] public static void Resume() => FiberScheduler.Default.Resume();
        [Category(DEFAULT)] public static void Suspend() => Debug.Log($"Suspended (remaining tasks: {FiberScheduler.Default.Suspend()})");
        [Category(DEFAULT)] public static void Submit3Tasks() => SubmitTasks(3, FiberScheduler.Default);
        [Category(DEFAULT)] public static void Submit10Tasks() => SubmitTasks(10, FiberScheduler.Default);

        [Category(PRIORITY)] public static void Concurrency1_() => Debug.Log($"Concurrency: {FiberScheduler.Priority.RawConcurrencyLevel = 1}");
        [Category(PRIORITY)] public static void ConcurrencyIncrement_() => Debug.Log($"Concurrency: {FiberScheduler.Priority.AdjustConcurrencyLevel(+1)}");
        [Category(PRIORITY)] public static void ConcurrencyDecrement_() => Debug.Log($"Concurrency: {FiberScheduler.Priority.AdjustConcurrencyLevel(-1)}");
        [Category(PRIORITY)] public static void Resume_() => FiberScheduler.Priority.Resume();
        [Category(PRIORITY)] public static void Suspend_() => Debug.Log($"Suspended (remaining tasks: {FiberScheduler.Priority.Suspend()})");
        [Category(PRIORITY)] public static void Submit3Tasks_() => SubmitTasks(3, FiberScheduler.Priority);
        [Category(PRIORITY)] public static void Submit10Tasks_() => SubmitTasks(10, FiberScheduler.Priority);

        const int DELAY = 500;
        static int TaskNo;

        static void SubmitTasks(int count, FiberScheduler scheduler)
        {
            var color = scheduler == FiberScheduler.Priority ? "<color=yellow>" : "<color=lime>";

            for (int i = 0; i < count; i++)
            {
                var number = ++TaskNo;

                scheduler.Submit(new(0, null), async payload =>
                {
                    Debug.Log($"{color}#{number} Starting (thread: {Environment.CurrentManagedThreadId})</color>");
                    await Task.Delay(DELAY);
                    Debug.Log($"{color}#{number} Complete (thread: {Environment.CurrentManagedThreadId}; remaining tasks: {scheduler.RemainingTaskCount})</color>");
                });
            }
        }
    }
}

#endif
