using System.Security.Claims;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Contracts.Customer.Api.DeliveryContact.v2;
using DKH.CustomerService.IntegrationTests.Support;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

public sealed partial class DeliveryContactOwnerCallbackUnitTests
{
    [Theory]
    [InlineData("principal")]
    [InlineData("bearer")]
    [InlineData("workspace")]
    [InlineData("resolved-storefront")]
    [InlineData("cancel")]
    public async Task PublicRecipientBodyCannotEscapeChangedCallerAfterMediatorCompletionAsync(string changed)
    {
        var f = await Fixture.CreateAsync();
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = f.Context;
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        storefront.StorefrontId.Returns(f.StorefrontId);
        var mediator = Substitute.For<IMediator>();
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        mediator.Send(Arg.Any<DeliveryContactRequest>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var service = new DeliveryContactGrpcService(mediator, storefront, Options.Create(new PlatformStorefrontContextOptions()), f.Configuration, TimeProvider.System);
        using var canceled = new CancellationTokenSource();
        f.Context.RequestAborted = canceled.Token;
        var pending = service.ResolveRecipientContact(RecipientRequest(f), call);
        pending.IsCompleted.Should().BeFalse();
        switch (changed)
        {
            case "principal":
                f.Context.User = new ClaimsPrincipal(f.Context.User);
                break;
            case "bearer":
                f.Context.Request.Headers.Authorization = "Bearer replaced";
                break;
            case "workspace":
                f.Context.Request.Headers["X-Workspace-Id"] = Guid.NewGuid().ToString("D");
                break;
            case "resolved-storefront":
                storefront.StorefrontId.Returns(Guid.NewGuid());
                break;
            case "cancel":
                canceled.Cancel();
                break;
        }

        // Explicit mediator response fixture, not a claim of a committed DB TX.
        completion.SetResult(PrivateRecipient(DateTimeOffset.UtcNow.AddMinutes(1)));
        if (changed == "cancel")
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        else
        {
            var refusal = await Assert.ThrowsAsync<RpcException>(() => pending);
            refusal.StatusCode.Should().Be(StatusCode.Unavailable);
            refusal.Status.Detail.Should().NotContain("PRIVATE_RECIPIENT");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicRecipientPreservesOriginalErrorOnlyWhileOriginalCallerRemainsCurrentAsync(bool callerChanged)
    {
        var f = await Fixture.CreateAsync();
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = f.Context;
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        storefront.StorefrontId.Returns(f.StorefrontId);
        var mediator = Substitute.For<IMediator>();
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        mediator.Send(Arg.Any<DeliveryContactRequest>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var service = new DeliveryContactGrpcService(mediator, storefront, Options.Create(new PlatformStorefrontContextOptions()), f.Configuration, TimeProvider.System);
        var pending = service.ResolveRecipientContact(RecipientRequest(f), call);
        if (callerChanged)
        {
            f.Context.Request.Headers["X-Workspace-Id"] = Guid.NewGuid().ToString("D");
        }

        var original = new RpcException(new Status(StatusCode.NotFound, "PRIVATE_OWNER_REFUSAL"));
        completion.SetException(original);
        var refusal = await Assert.ThrowsAsync<RpcException>(() => pending);
        if (callerChanged)
        {
            refusal.StatusCode.Should().Be(StatusCode.Unavailable);
            refusal.Status.Detail.Should().NotContain("PRIVATE_OWNER_REFUSAL");
        }
        else
        {
            refusal.Should().BeSameAs(original);
        }
    }

    [Fact]
    public async Task PublicRecipientExpiredLeaseCannotBeReturnedAfterMediatorCompletionAsync()
    {
        var f = await Fixture.CreateAsync();
        var call = new TestServerCallContext();
        call.UserState["__HttpContext"] = f.Context;
        var storefront = Substitute.For<IPlatformStorefrontContext>();
        storefront.StorefrontId.Returns(f.StorefrontId);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeliveryContactRequest>(), Arg.Any<CancellationToken>()).Returns(PrivateRecipient(DateTimeOffset.UtcNow.AddSeconds(-1)));
        var service = new DeliveryContactGrpcService(mediator, storefront, Options.Create(new PlatformStorefrontContextOptions()), f.Configuration, TimeProvider.System);
        var refusal = await Assert.ThrowsAsync<RpcException>(() => service.ResolveRecipientContact(RecipientRequest(f), call));
        refusal.StatusCode.Should().Be(StatusCode.Unavailable);
        refusal.Status.Detail.Should().NotContain("PRIVATE_RECIPIENT");
    }

    private static ResolveRecipientContactRequest RecipientRequest(Fixture f) => new()
    {
        Contact = new() { Authority = "customer.delivery-contact.v1", ResourceId = NewId(), Revision = 1, Digest = new string('a', 64) },
        ConsentId = NewId(),
        Command = new() { OperationId = NewId() },
        Scope = new()
        {
            GroupId = new() { Value = f.Scope.GroupId.ToString("D") },
            AllocationPublicRef = f.Scope.AllocationPublicRef,
            PlanRevisionId = new() { Value = f.Scope.PlanRevisionId.ToString("D") },
            ScopeRevision = f.Scope.ScopeRevision,
            ChoiceRevisionId = new() { Value = f.Scope.ChoiceRevisionId.ToString("D") },
            Purpose = DeliveryContactPurpose.LocalShipment,
            DutyAcceptance = new() { Authority = f.Scope.Duty.Authority, ResourceId = new() { Value = f.Scope.Duty.ResourceId.ToString("D") }, Revision = f.Scope.Duty.Revision, Digest = f.Scope.Duty.Digest },
        },
    };

    private static DeliveryRecipientContactView PrivateRecipient(DateTimeOffset expiry) => new(new(Guid.NewGuid(), expiry, 7),
        "PRIVATE_RECIPIENT", "GB", "PRIVATE_CITY", null, null, null, null, null, null);
}
