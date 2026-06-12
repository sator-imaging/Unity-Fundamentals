#:package FUnit@*
#:package FUnit.Directives@*

#warning funit include ../../Runtime/Threading/Fibers.cs

using SatorImaging.UnityFundamentals;

#pragma warning disable IDE0039 // Use local function
#pragma warning disable IDE0028 // Simplify collection initialization
#pragma warning disable IDE0305 // Simplify collection initialization
#pragma warning disable SMA0040

const int DELAY = 42;

return FUnit.Run(args, describe =>
{
    describe("Fibers concurrency level validation", it =>
    {
        it("should throw an error if concurrency level is 0 for Fibers.For", () =>
        {
            Func<int, Task<int>> factory = async (index) => index;
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: 0 (Parameter 'Concurrency')", () => Fibers.For(0, 0, 1, 1, factory));
        });

        it("should throw an error if concurrency level is negative for Fibers.For", () =>
        {
            Func<int, Task<int>> factory = async (index) => index;
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: -1 (Parameter 'Concurrency')", () => Fibers.For(-1, 0, 1, 1, factory));
        });

        it("should throw an error if concurrency level is 0 for Fibers.ForEach", () =>
        {
            IEnumerable<string> items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) => item;
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: 0 (Parameter 'Concurrency')", () => Fibers.ForEach(0, items, factory));
        });

        it("should throw an error if concurrency level is negative for Fibers.ForEach", () =>
        {
            IEnumerable<string> items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) => item;
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: -1 (Parameter 'Concurrency')", () => Fibers.ForEach(-1, items, factory));
        });

        it("should throw an error if concurrency level is set to 0 after instantiation", () =>
        {
            Func<int, Task<int>> factory = async (index) => index;
            var fibers = Fibers.For(1, 0, 1, 1, factory);
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: 0 (Parameter 'Concurrency')", () => fibers.Concurrency = 0);
        });

        it("should throw an error if concurrency level is set to negative after instantiation", () =>
        {
            Func<int, Task<int>> factory = async (index) => index;
            var fibers = Fibers.For(1, 0, 1, 1, factory);
            Must.Throw<ArgumentOutOfRangeException>("Value must be greather than 0: -1 (Parameter 'Concurrency')", () => fibers.Concurrency = -1);
        });

        it("should validate value after setting concurrency level", () =>
        {
            Func<int, Task<int>> factory = async (index) => index;
            var fibers = Fibers.For(1, 0, 1, 1, factory);
            Must.BeEqual(1, fibers.Concurrency);

            fibers.Concurrency = 5;
            Must.BeEqual(5, fibers.Concurrency);

            fibers.Concurrency = 10;
            Must.BeEqual(10, fibers.Concurrency);
        });
    });

    describe("Fibers.For", it =>
    {
        it("should create a Fibers instance and process tasks", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index * 2);
            };

            var fibers = Fibers.For(2, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            var processedResults = new List<int>();
            await foreach (var result in fibers)
            {
                processedResults.Add(result);
            }

            Must.HaveSameSequence(new List<int> { 0, 1, 2, 3, 4 }, results.OrderBy(x => x).ToList());
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should respect concurrency limit", async () =>
        {
            var runningTasks = new HashSet<int>();
            int maxConcurrency = 0;

            Func<int, Task<int>> factory = async (index) =>
            {
                lock (runningTasks)
                {
                    runningTasks.Add(index);
                    maxConcurrency = Math.Max(maxConcurrency, runningTasks.Count);
                }
                await Task.Delay(DELAY);
                lock (runningTasks)
                {
                    runningTasks.Remove(index);
                }
                return index;
            };

            var fibers = Fibers.For(3, 0, 10, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await foreach (var result in fibers)
            {
                // Consume results
            }

            Must.BeTrue(maxConcurrency <= 3);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should process tasks using Start() and await AsTask()", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index * 2);
            };

            var fibers = Fibers.For(2, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start(); // Start the fibers in the background
            Must.BeTrue(fibers.IsRunning);

            await fibers.AsTask(); // Await the completion of all tasks

            Must.HaveSameSequence(new List<int> { 0, 1, 2, 3, 4 }, results.OrderBy(x => x).ToList());
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should throw FiberException if Start() is called before await foreach...in", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index * 2);
            };

            var fibers = Fibers.For(2, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start(); // Start the fibers in the background
            Must.BeTrue(fibers.IsRunning);

            // Attempting to iterate should now throw FiberException
            Must.Throw<FiberException>("Cannot iterate while task is running in background", async () =>
            {
                await foreach (var result in fibers)
                {
                    // This should throw
                }
            });

            // After the error, the fibers should be completed successfully
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed); // next() throws but fibers is completed by start()
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Start() is called inside await foreach...in", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index * 2);
            };

            var fibers = Fibers.For(2, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // This should throw FiberException
            Must.Throw<FiberException>("Cannot start while iterating over fibers", async () =>
            {
                var processedResults = new List<int>();
                await foreach (var result in fibers)
                {
                    processedResults.Add(result);
                    // Calling start() inside the loop should now throw
                    _ = fibers.Start();
                }
            });

            // After the error, the fibers should be completed successfully
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed); // next() throws but fibers is completed by start()
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Stop() is called in await foreach loop", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };

            var fibers = Fibers.For(1, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            Must.Throw<FiberException>("Attempting to stop fibers running by `await foreach`", async () =>
            {
                await foreach (var result in fibers)
                {
                    if (result == 0) // Stop on the first iteration
                    {
                        await fibers.Stop();
                    }
                }
            });

            // After the error, the fibers should be completed successfully
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed); // next() throws but fibers is completed by start()
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Fibers.For is consumed simultaneously", async () =>
        {
            Func<int, Task<int>> factory = async (index) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 5, 1, factory);

            using var signal = new ManualResetEventSlim();

            // Start consumption in the background
            _ = Task.Run(async () =>
            {
                await foreach (var result in fibers)
                {
                    signal.Set();
                }
            });

            signal.Wait();

            Must.Throw<FiberException>("Cannot iterate while other thread is consuming tasks", async () =>
            {
                await foreach (var result in fibers)
                {
                    // This should throw
                }
            });

            await fibers.AsTask(); // Ensure background task completes
        });
    });

    describe("Fibers.ForEach", it =>
    {
        it("should create a Fibers instance and process tasks from an iterable", async () =>
        {
            var items = new List<string> { "a", "b", "c" };
            var results = new List<string>();
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item.ToUpper());
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            var processedResults = new List<string>();
            await foreach (var result in fibers)
            {
                processedResults.Add(result);
            }

            Must.HaveSameSequence(new List<string> { "a", "b", "c" }, results.OrderBy(x => x).ToList());
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should handle empty iterable", async () =>
        {
            var items = new List<string>();
            var results = new List<string>();
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item.ToUpper());
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            var processedResults = new List<string>();
            await foreach (var result in fibers)
            {
                processedResults.Add(result);
            }

            Must.HaveSameSequence(new List<string>(), results);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should process tasks from an iterable using Start() and await AsTask()", async () =>
        {
            var items = new List<string> { "a", "b", "c" };
            var results = new List<string>();
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item.ToUpper());
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);

            await fibers.AsTask();

            Must.HaveSameSequence(new List<string> { "a", "b", "c" }, results.OrderBy(x => x).ToList());
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("should throw FiberException if Start() is called before await foreach...in (forEach)", async () =>
        {
            var items = new List<string> { "a" };
            var results = new List<string>();
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item.ToUpper());
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);

            Must.Throw<FiberException>("Cannot iterate while task is running in background", async () =>
            {
                var processedResults = new List<string>();
                await foreach (var result in fibers)
                {
                    processedResults.Add(result);
                }
            });

            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Start() is called inside await foreach...in (forEach)", async () =>
        {
            var items = new List<string> { "a", "b", "c" };
            var results = new List<string>();
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item.ToUpper());
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            Must.Throw<FiberException>("Cannot start while iterating over fibers", async () =>
            {
                var processedResults = new List<string>();
                await foreach (var result in fibers)
                {
                    processedResults.Add(result);
                    _ = fibers.Start();
                    Must.BeTrue(fibers.IsRunning);
                }
            });

            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Stop() is called in await foreach loop (forEach)", async () =>
        {
            var results = new List<string>();
            var items = new List<string> { "a", "b", "c" };
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };

            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            Must.Throw<FiberException>("Attempting to stop fibers running by `await foreach`", async () =>
            {
                await foreach (var result in fibers)
                {
                    if (result == "a") // Stop on the first iteration
                    {
                        await fibers.Stop();
                    }
                }
            });

            // After the error, the fibers should be completed successfully
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed); // next() throws but fibers is completed by start()
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should throw FiberException if Fibers.ForEach is consumed simultaneously", async () =>
        {
            var items = new List<string> { "a", "b", "c" };
            Func<string, Task<string>> factory = async (item) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            using var signal = new ManualResetEventSlim();

            // Start consumption in the background
            _ = Task.Run(async () =>
            {
                await foreach (var result in fibers)
                {
                    signal.Set();
                }
            });

            signal.Wait();

            Must.Throw<FiberException>("Cannot iterate while other thread is consuming tasks", async () =>
            {
                await foreach (var result in fibers)
                {
                    // This should throw
                }
            });
            await fibers.AsTask(); // Ensure background task completes
        });
    });

    describe("Fibers.For combinations", it =>
    {
        it("verifies no re-processing when started and awaited multiple times", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // First start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<int> { 0 }, results);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Reset results for second attempt
            results.Clear();

            // Second start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<int>(), results); // No new tasks should be processed
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no re-processing when started and awaited, then iterated with await foreach...in", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // First start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<int> { 0 }, results);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Reset results for second attempt
            results.Clear();

            // Attempt iteration with await foreach...in
            var processedResults = new List<int>();
            await foreach (var result in fibers)
            {
                processedResults.Add(result);
            }
            Must.HaveSameSequence(new List<int>(), results); // No new tasks should be processed
            Must.HaveSameSequence(new List<int>(), processedResults);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no error when Start() is called after await foreach...in completes", async () =>
        {
            Func<int, Task<int>> factory = async (index) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await foreach (var _ in fibers)
            {
                // Consume the fiber
            }
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Calling Start() again should not throw and should not restart
            await fibers.Start();
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no re-iteration when await foreach...in is called multiple times", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await foreach (var _ in fibers)
            {
                // First iteration
            }
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            var secondResults = new List<int>();
            await foreach (var result in fibers)
            {
                secondResults.Add(result);
            }

            Must.HaveSameSequence(new List<int> { 0 }, results); // Only the first iteration should have processed tasks
            Must.HaveSameSequence(new List<int>(), secondResults); // Second iteration should yield nothing
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no error when Start() is called multiple times", async () =>
        {
            Func<int, Task<int>> factory = async (index) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };
            var fibers = Fibers.For(1, 0, 1, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // Calling Start() again should not throw and should not change state
            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });
    });

    describe("Fibers.ForEach combinations", it =>
    {
        it("verifies no re-processing when started and awaited multiple times (forEach)", async () =>
        {
            var results = new List<string>();
            var items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // First start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<string> { "a" }, results);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Reset results for second attempt
            results.Clear();

            // Second start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<string>(), results); // No new tasks should be processed
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no re-processing when started and awaited, then iterated with await foreach...in (forEach)", async () =>
        {
            var results = new List<string>();
            var items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // First start and await
            await fibers.Start();
            await fibers.AsTask();
            Must.HaveSameSequence(new List<string> { "a" }, results);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Reset results for second attempt
            results.Clear();

            // Attempt iteration with await foreach...in
            var processedResults = new List<string>();
            await foreach (var result in fibers)
            {
                processedResults.Add(result);
            }
            Must.HaveSameSequence(new List<string>(), results); // No new tasks should be processed
            Must.HaveSameSequence(new List<string>(), processedResults);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no error when Start() is called after await foreach...in completes (forEach)", async () =>
        {
            var items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await foreach (var _ in fibers)
            {
                // Consume the fiber
            }
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            // Calling Start() again should not throw and should not restart
            await fibers.Start();
            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no re-iteration when await foreach...in is called multiple times (forEach)", async () =>
        {
            var results = new List<string>();
            var items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) =>
            {
                results.Add(item);
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await foreach (var _ in fibers)
            {
                // First iteration
            }
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);

            var secondResults = new List<string>();
            await foreach (var result in fibers)
            {
                secondResults.Add(result);
            }

            Must.HaveSameSequence(new List<string> { "a" }, results); // Only the first iteration should have processed tasks
            Must.HaveSameSequence(new List<string>(), secondResults); // Second iteration should yield nothing
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });

        it("verifies no error when Start() is called multiple times (forEach)", async () =>
        {
            var items = new List<string> { "a" };
            Func<string, Task<string>> factory = async (item) =>
            {
                await Task.Delay(DELAY);
                return await Task.FromResult(item);
            };
            var fibers = Fibers.ForEach(1, items, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            // Calling Start() again should not throw and should not change state
            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await fibers.AsTask();
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });
    });

    describe("Fibers error handling", it =>
    {
        it("should stop processing tasks when error handler returns \"Stop\"", async () =>
        {
            var processed = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                if (index == 2)
                {
                    throw new NotSupportedException("Test error - stop");
                }
                processed.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };

            var fibers = Fibers.For(1, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            fibers.SetErrorHandler((e, f, reason) =>
            {
                Must.BeTrue(e is NotSupportedException);
                Must.BeEqual("Test error - stop", e.Message);
                Must.BeEqual(Fibers.ErrorReason.MoveNextAsync, reason);
                return Fibers.ErrorHandlingPolicy.Stop;
            });

            await foreach (var result in fibers)
            {
                // The loop will continue until all tasks are processed or stopped
            }

            // Expect tasks before the error to be processed, and no tasks after
            Must.HaveSameSequence(new List<int> { 0, 1 }, processed);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(fibers.IsFailed);
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should skip erroneous tasks when error handler returns \"Skip\"", async () =>
        {
            var processed = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                if (index == 2)
                {
                    throw new NotSupportedException("Test error - skip");
                }
                processed.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };

            var fibers = Fibers.For(1, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            fibers.SetErrorHandler((e, f, reason) =>
            {
                Must.BeTrue(e is NotSupportedException);
                Must.BeEqual("Test error - skip", e.Message);
                Must.BeEqual(Fibers.ErrorReason.MoveNextAsync, reason);
                return Fibers.ErrorHandlingPolicy.Skip;
            });

            await foreach (var result in fibers)
            {
                // Consume results
            }

            // Expect tasks before and after the error to be processed, but not the erroneous one
            Must.HaveSameSequence(new List<int> { 0, 1, 3, 4 }, processed);
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);
            Must.BeTrue(!fibers.IsRunning);
        });

        it("should re-throw error and mark fibers as failed when error handler returns \"Default\"", async () =>
        {
            var processed = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                if (index == 2)
                {
                    throw new NotSupportedException("Test error - default");
                }
                processed.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };

            var fibers = Fibers.For(1, 0, 5, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            fibers.SetErrorHandler((e, f, reason) =>
            {
                Must.BeTrue(e is NotSupportedException);
                Must.BeEqual("Test error - default", e.Message);
                Must.BeEqual(Fibers.ErrorReason.MoveNextAsync, reason);
                return Fibers.ErrorHandlingPolicy.Default;
            });

            Must.Throw<NotSupportedException>("Test error - default", async () =>
            {
                await foreach (var result in fibers)
                {
                    // This should throw
                }
            });

            // Expect tasks before the error to be processed
            Must.HaveSameSequence(new List<int> { 0, 1 }, processed);
            Must.BeTrue(fibers.IsCompleted); // Fibers should be completed even if failed
            Must.BeTrue(fibers.IsFailed); // Fibers should be marked as failed
            Must.BeTrue(!fibers.IsRunning); // Should not be started after error
        });
    });

    describe("Fibers start/stop", it =>
    {
        it("should restart a stopped fibers and process new tasks", async () =>
        {
            var results = new List<int>();
            Func<int, Task<int>> factory = async (index) =>
            {
                results.Add(index);
                await Task.Delay(DELAY);
                return await Task.FromResult(index);
            };

            var concurrency = 7;
            var arraySize = 310;
            var expected = Enumerable.Range(0, arraySize).ToList();

            var fibers = Fibers.For(concurrency, 0, arraySize, 1, factory);

            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            Must.BeTrue(fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            await fibers.Stop();
            Must.NotHaveSameSequence(expected, results);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsCompleted);
            Must.BeTrue(!fibers.IsFailed);

            _ = fibers.Start();
            await fibers.AsTask();

            Must.HaveSameSequence(expected, results.OrderBy(x => x).ToList());
            Must.BeTrue(fibers.IsCompleted);
            Must.BeTrue(!fibers.IsRunning);
            Must.BeTrue(!fibers.IsFailed);
        });
    });
});
