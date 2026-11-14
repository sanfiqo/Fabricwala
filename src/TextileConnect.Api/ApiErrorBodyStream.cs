namespace TextileConnect.Api;

// Successful responses (including files) stream directly. Error bodies are suppressed
// so middleware can replace arbitrary endpoint/framework text without buffering it.
internal sealed class ApiErrorBodyStream(Stream inner, HttpResponse response) : Stream
{
    private bool IsError => response.StatusCode >= 400;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { if (!IsError) inner.Flush(); }
    public override Task FlushAsync(CancellationToken cancellationToken) => IsError ? Task.CompletedTask : inner.FlushAsync(cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) { if (!IsError) inner.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { if (!IsError) inner.Write(buffer); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => IsError ? Task.CompletedTask : inner.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => IsError ? ValueTask.CompletedTask : inner.WriteAsync(buffer, cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
