using Grpc.Core;

namespace DKH.CustomerService.IntegrationTests.Support;

public sealed class TestServerCallContext : ServerCallContext
{
    protected override string MethodCore => string.Empty;

    protected override string HostCore => string.Empty;

    protected override string PeerCore => string.Empty;

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore => [];

    protected override CancellationToken CancellationTokenCore => CancellationToken.None;

    protected override Metadata ResponseTrailersCore => [];

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore => new(string.Empty, []);

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
