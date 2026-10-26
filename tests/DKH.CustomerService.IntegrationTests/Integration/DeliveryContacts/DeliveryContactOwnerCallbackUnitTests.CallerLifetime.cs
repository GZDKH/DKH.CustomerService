using System.Security.Claims;
using DKH.CustomerService.Application.DeliveryContacts;
using FluentAssertions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

public sealed partial class DeliveryContactOwnerCallbackUnitTests
{
    [Theory]
    [InlineData("principal", false)]
    [InlineData("principal", true)]
    [InlineData("identity", false)]
    [InlineData("identity", true)]
    [InlineData("issuer", false)]
    [InlineData("issuer", true)]
    [InlineData("bearer", false)]
    [InlineData("bearer", true)]
    [InlineData("workspace", false)]
    [InlineData("workspace", true)]
    [InlineData("storefront", false)]
    [InlineData("storefront", true)]
    [InlineData("path", false)]
    [InlineData("path", true)]
    [InlineData("context", false)]
    [InlineData("context", true)]
    public async Task OriginalCallerCannotChangeAcrossActualOwnerCallOrDisposalAsync(string changed, bool onDispose)
    {
        var f = await Fixture.CreateAsync();
        void Mutate()
        {
            switch (changed)
            {
                case "principal":
                    f.Context.User = new ClaimsPrincipal(new ClaimsIdentity(f.Context.User.Claims, f.Context.User.Identity!.AuthenticationType));
                    break;
                case "identity":
                    f.Context.User.AddIdentity(new ClaimsIdentity([new Claim("extra-identity", "changed")], "other"));
                    break;
                case "issuer":
                    var identity = (ClaimsIdentity)f.Context.User.Identity!;
                    identity.RemoveClaim(identity.FindFirst("iss")!);
                    identity.AddClaim(new Claim("iss", "https://different.fixture.invalid/realms/test"));
                    break;
                case "bearer":
                    f.Context.Request.Headers.Authorization = "Bearer different";
                    break;
                case "workspace":
                    f.Context.Request.Headers["X-Workspace-Id"] = Guid.NewGuid().ToString("D");
                    break;
                case "storefront":
                    f.Context.Request.Headers["X-Storefront-Id"] = Guid.NewGuid().ToString("D");
                    break;
                case "path":
                    f.Context.Request.Path = "/different-owner";
                    break;
                case "context":
                    f.Accessor.HttpContext = new DefaultHttpContext { User = f.Context.User };
                    break;
                default:
                    throw new ArgumentException("Unknown test input.");
            }
        }

        if (onDispose)
        {
            f.Invoker.Disposing = Mutate;
        }
        else
        {
            f.Invoker.Completing = Mutate;
        }

        var failure = await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        failure.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        f.Invoker.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalCancellationIsCheckedAfterOwnedDisposalEvenOnOwnerErrorAsync(bool ownerError)
    {
        var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        f.Context.RequestAborted = cancellation.Token;
        if (ownerError)
        {
            f.Invoker.Error = new RpcException(new Status(StatusCode.PermissionDenied, "private-owner-details"));
        }

        f.Invoker.Disposing = cancellation.Cancel;
        await Assert.ThrowsAsync<OperationCanceledException>(f.ReadAsync);
        f.Invoker.Calls.Should().Be(1);
    }

    [Fact]
    public async Task OriginalValidatedTokenCannotExpireDuringCallDisposalAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_ => now);
        var f = await Fixture.CreateAsync(clock);
        f.Invoker.Disposing = () => now = now.AddMinutes(6);
        var failure = await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        failure.Code.Should().Be("AUTHORITY_UNAVAILABLE");
    }

    [Fact]
    public async Task AlreadyCanceledRequestNeverStartsOwnerReadAsync()
    {
        var f = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => f.Authorizer.RevalidateAsync(f.ActorAccountId,
            f.StorefrontId, f.Scope, cancellation.Token));
        f.Invoker.Calls.Should().Be(0);
    }
}
