using System.Net;
using System.Text;

namespace WasteCaseIntakeProcessor.Test.WasteObligations;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string body = "")
    {
        _responses.Enqueue(() =>
            new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
        );

        return this;
    }

    public StubHttpMessageHandler RespondWith(Exception exception)
    {
        _responses.Enqueue(() => throw exception);

        return this;
    }

    /// <summary>Sends the headers, then a body that never finishes until the request is cancelled.</summary>
    public StubHttpMessageHandler RespondWithStalledBody()
    {
        _responses.Enqueue(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) }
        );

        return this;
    }

    /// <summary>Sends the headers, then a body that fails part way, as when the connection drops.</summary>
    public StubHttpMessageHandler RespondWithBrokenBody()
    {
        _responses.Enqueue(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream()) }
        );

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Requests.Add(request);

        if (_responses.Count == 0)
            throw new InvalidOperationException("No stub response left for " + request.RequestUri);

        return Task.FromResult(_responses.Dequeue()());
    }
}

internal class StalledStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class BrokenStream : StalledStream
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(new IOException("Connection reset"));
}
