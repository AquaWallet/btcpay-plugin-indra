using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Xunit;

namespace BTCPayServer.Plugins.Indra.Tests;

public class IndraClientTests
{
    [Fact]
    public async Task TranslatesGrpcCancellationWhenCallerCancelled()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var rpcException = CreateCancelledRpcException();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            IndraClient.TranslateCancellation(
                Task.FromException<int>(rpcException),
                source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Same(rpcException, exception.InnerException);
    }

    [Fact]
    public async Task PreservesGrpcCancellationWhenCallerDidNotCancel()
    {
        var rpcException = CreateCancelledRpcException();

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            IndraClient.TranslateCancellation(
                Task.FromException<int>(rpcException),
                CancellationToken.None));

        Assert.Same(rpcException, exception);
    }

    [Fact]
    public async Task PreservesDeadlineExceeded()
    {
        var rpcException = new RpcException(
            new Status(StatusCode.DeadlineExceeded, "Deadline exceeded."));

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            IndraClient.TranslateCancellation(
                Task.FromException<int>(rpcException),
                CancellationToken.None));

        Assert.Same(rpcException, exception);
    }

    private static RpcException CreateCancelledRpcException()
    {
        return new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client."));
    }
}
