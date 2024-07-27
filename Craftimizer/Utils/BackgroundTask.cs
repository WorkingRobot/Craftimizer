using System;
using System.Threading;
using System.Threading.Tasks;

namespace Craftimizer.Utils;

public sealed class BackgroundTask<T>(Func<CancellationToken, T> func) : IDisposable where T : struct
{
    public T? Result { get; private set; }
    public Exception? Exception { get; private set; }
    public bool Completed { get; private set; }
    public bool Cancelling => !Completed && TokenSource.IsCancellationRequested;

    private CancellationTokenSource TokenSource { get; } = new();
    private Func<CancellationToken, T> Func { get; } = func;

    public void Start()
    {
        var token = TokenSource.Token;
        var task = Task.Run(() => Result = Func(token), token);
        _ = task.ContinueWith(t => Completed = true);
        _ = task.ContinueWith(t =>
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                t.Exception!.Flatten().Handle(ex => ex is TaskCanceledException or OperationCanceledException);
            }
            catch (AggregateException e)
            {
                Exception = e;
                Log.Error(e, "Background task failed");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Cancel() =>
        TokenSource.Cancel();

    public void Dispose() =>
        Cancel();
}
