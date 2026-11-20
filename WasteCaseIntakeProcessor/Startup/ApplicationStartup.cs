namespace WasteCaseIntakeProcessor.Startup;

public sealed class ApplicationStartup
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Wait(CancellationToken cancellationToken) => _started.Task.WaitAsync(cancellationToken);

    public void MarkStarted() => _started.TrySetResult();
}
