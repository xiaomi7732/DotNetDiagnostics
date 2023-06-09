namespace ThreadPoolStarvationExample;

class ThreadEater
{
    // This will be compensated by .NET 6 runtime
    public int StarveBeforeNet6()
    {
        SimulateAsyncIOAsync().GetAwaiter().GetResult();
        return GetUsedThreads();
    }

    /// <summary>
    /// Exhausts the thread time. When called repeatedly, it will drain the thread pool
    /// really quick.
    /// </summary>
    public bool StarveAllTheWay()
    {
        Task delayTask = SimulateAsyncIOAsync();
        while (!delayTask.IsCompleted)
        {
            Thread.Sleep(10);   // Blocking the thread.
            int usedThreads = GetUsedThreads();
            Console.WriteLine("Used thread:" + usedThreads);
        }
        return true;
    }

    /// <summary>
    /// This shall be okay to call.
    /// </summary>
    public async Task NoBlockingAsync()
    {
        await Task.Delay(500);
    }

    private async Task SimulateAsyncIOAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
    }

    private int GetUsedThreads()
    {
        ThreadPool.GetAvailableThreads(out int workerThreads, out int _);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
        int usedThreads = maxWorkerThreads - workerThreads;
        return usedThreads;
    }
}