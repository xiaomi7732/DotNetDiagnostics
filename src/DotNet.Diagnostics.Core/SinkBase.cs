namespace DotNet.Diagnostics.Core;

public abstract class SinkBase<TSource, TData> : ISink<TSource, TData>, IDisposable
{
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    private bool _isEnabled;
    private bool _isDisposed;

    /// <inheritdoc />
    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Do nothing by default but allow the derived class to overwrite.
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isEnabled)
            {
                // Enabled when OnStartAsync finished successfully.
                _isEnabled = await OnStartingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isEnabled)
            {
                if (await OnStoppingAsync(cancellationToken).ConfigureAwait(false))
                {
                    _isEnabled = false;
                }
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public bool Submit(TData data)
    {
        if (_isEnabled)
        {
            return OnSubmit(data);
        }
        return false;
    }

    /// <summary>
    /// Runs upon starting the sink.
    /// </summary>
    /// <returns>True when the sink started successfully. Otherwise, false.</returns>
    protected abstract Task<bool> OnStartingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs upon stopping the sink.
    /// </summary>
    /// <returns>True when the sink stopped successfully. Otherwise, false.</returns>
    protected abstract Task<bool> OnStoppingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Submits data only when the sink is enabled.
    /// </summary>
    /// <param name="data">The payload.</param>
    protected abstract bool OnSubmit(TData data);

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _semaphoreSlim.Dispose();
            }
            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}