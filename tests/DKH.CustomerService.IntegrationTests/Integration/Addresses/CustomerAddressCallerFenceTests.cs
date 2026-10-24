using System.Security.Claims;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application.Addresses.GetAddress;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAddressManagement.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAddress.v1;
using DKH.CustomerService.IntegrationTests.Support;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Addresses;

[Trait("Category", "Unit")]
public sealed class CustomerAddressCallerFenceTests
{
    [Theory]
    [InlineData("principal")]
    [InlineData("claims")]
    [InlineData("authorization")]
    [InlineData("storefront")]
    [InlineData("http-context")]
    [InlineData("cancel")]
    public async Task PendingPrivateAddressCannotEscapeChangedOriginalCallerContextAsync(string change)
    {
        using var cancellation = new CancellationTokenSource();
        var http = Context();
        http.RequestAborted = cancellation.Token;
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = http;
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        var selected = Guid.NewGuid();
        storefront.StorefrontId.Returns(selected);
        var completion = new TaskCompletionSource<GetAddressResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAddressQuery>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var service = new CustomerAddressGrpcService(mediator, storefront);
        var pending = service.GetAddress(Request(selected), call);
        Assert.False(pending.IsCompleted);
        switch (change)
        {
            case "principal":
                http.User = new ClaimsPrincipal(http.User);
                break;
            case "claims":
                ((ClaimsIdentity)http.User.Identity!).AddClaim(new("role", "synthetic-late"));
                break;
            case "authorization":
                http.Request.Headers.Authorization = "Bearer synthetic-replacement";
                break;
            case "storefront":
                storefront.StorefrontId.Returns(Guid.NewGuid());
                break;
            case "http-context":
                call.UserState["__HttpContext"] = new DefaultHttpContext { User = http.User };
                break;
            case "cancel":
                cancellation.Cancel();
                break;
        }

        completion.SetResult(new GetAddressResponse { Found = true, Address = new CustomerAddressModel { Label = "SYNTHETIC_PRIVATE_ADDRESS" } });
        if (change == "cancel")
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        else
        {
            var denied = await Assert.ThrowsAsync<RpcException>(() => pending);
            Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
            Assert.DoesNotContain("SYNTHETIC_PRIVATE_ADDRESS", denied.Status.Detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task BodyStorefrontCannotOverrideAlreadyResolvedCurrentStorefrontAsync()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAddressQuery>(), Arg.Any<CancellationToken>()).Returns(new GetAddressResponse());
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        storefront.StorefrontId.Returns(Guid.NewGuid());
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = Context();
        var service = new CustomerAddressGrpcService(mediator, storefront);
        var denied = await Assert.ThrowsAsync<RpcException>(() => service.GetAddress(Request(Guid.NewGuid()), call));
        Assert.Equal(StatusCode.InvalidArgument, denied.StatusCode);
        Assert.Empty(mediator.ReceivedCalls());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingFailurePreservesAuthorizedErrorButNotChangedCallerDisclosureAsync(bool changeCaller)
    {
        var http = Context();
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = http;
        var selected = Guid.NewGuid();
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        storefront.StorefrontId.Returns(selected);
        var completion = new TaskCompletionSource<GetAddressResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAddressQuery>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var pending = new CustomerAddressGrpcService(mediator, storefront).GetAddress(Request(selected), call);
        if (changeCaller)
        {
            http.User = new ClaimsPrincipal(http.User);
        }

        var original = new RpcException(new Status(StatusCode.NotFound, "SYNTHETIC_PRIVATE_FAILURE"));
        completion.SetException(original);
        var actual = await Assert.ThrowsAsync<RpcException>(() => pending);
        if (changeCaller)
        {
            Assert.Equal(StatusCode.PermissionDenied, actual.StatusCode);
            Assert.DoesNotContain("SYNTHETIC_PRIVATE_FAILURE", actual.Status.Detail, StringComparison.Ordinal);
        }
        else
        {
            Assert.Same(original, actual);
        }
    }

    private static DefaultHttpContext Context()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic-owner")], "Synthetic existing authenticated boundary"))
        };
        http.Request.Path = "/synthetic-address/GetAddress";
        http.Request.Headers.Authorization = "Bearer synthetic-original";
        return http;
    }

    private static GetAddressRequest Request(Guid storefront) => new()
    {
        StorefrontId = GuidValue.FromGuid(storefront),
        UserId = "synthetic-owner",
        AddressId = GuidValue.FromGuid(Guid.NewGuid())
    };
}
