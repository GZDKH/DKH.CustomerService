using System.Security.Claims;
using DKH.CustomerService.Api.Services;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Contracts.Customer.Api.DeliveryContact.v2;
using DKH.CustomerService.Infrastructure.DeliveryContacts;
using DKH.CustomerService.IntegrationTests.Support;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using FluentAssertions;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

[Trait("Category", "Unit")]
public sealed class DeliveryContactBoundaryUnitTests
{
    [Theory]
    [InlineData(false, StatusCode.Unauthenticated)]
    [InlineData(true, StatusCode.PermissionDenied)]
    public async Task ClaimsOrBodyCannotReplaceValidatedPersonalBearerEvidenceAsync(bool authenticated, StatusCode expected)
    {
        var mediator = Substitute.For<IMediator>();
        var service = Service(mediator);
        var context = new TestServerCallContext();
        context.UserState["__HttpContext"] = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "human:not-a-guid"), new Claim("dkh_principal_purpose", "personal:v1")],
                authenticated ? "injected" : null)),
        };
        var error = await Assert.ThrowsAsync<RpcException>(() => service.SealDeliveryContact(new SealDeliveryContactRequest
        {
            AddressId = Id(),
            Command = new DeliveryContactCommandContextModel { OperationId = Id() },
        }, context));
        error.StatusCode.Should().Be(expected);
        await mediator.DidNotReceiveWithAnyArgs().Send(default(DeliveryContactRequest)!, default);
    }

    [Fact]
    public async Task UnsupportedRecipientPurposeDoesNotReachStoreOrAuthorityAdapterAsync()
    {
        var mediator = Substitute.For<IMediator>();
        var request = new ResolveRecipientContactRequest
        {
            Contact = new DeliveryContactAuthorityRefModel { Authority = "customer.delivery-contact.v1", ResourceId = Id(), Revision = 1, Digest = new string('a', 64) },
            ConsentId = Id(),
            Scope = new DeliveryContactScopeModel(),
            Command = new DeliveryContactCommandContextModel { OperationId = Id() },
        };
        var error = await Assert.ThrowsAsync<RpcException>(() => Service(mediator).ResolveRecipientContact(request, new TestServerCallContext()));
        error.StatusCode.Should().Be(StatusCode.InvalidArgument);
        await mediator.DidNotReceiveWithAnyArgs().Send(default(DeliveryContactRequest)!, default);
    }

    [Fact]
    public async Task ProductionDefaultCannotTurnCallerScopeIntoRecipientAuthorityAsync()
    {
        var scope = new Domain.Entities.DeliveryContact.DeliveryContactScope(Guid.NewGuid(), "allocation", Guid.NewGuid(), 1, Guid.NewGuid(),
            new Domain.Entities.DeliveryContact.DeliveryContactAuthority("delivery.duty.v2", Guid.NewGuid(), 1, new string('a', 64)));
        var error = await Assert.ThrowsAsync<DeliveryContactFailureException>(() => new UnavailableDeliveryContactScopeAuthorizer()
            .RevalidateAsync(Guid.NewGuid(), Guid.NewGuid(), scope, default));
        error.Code.Should().Be("AUTHORITY_UNAVAILABLE");
    }

    private static DeliveryContactGrpcService Service(IMediator mediator)
        => new(mediator, Substitute.For<IPlatformStorefrontContext>(), Options.Create(new PlatformStorefrontContextOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://identity.fixture.invalid",
                ["Platform:Auth:Keycloak:Realm"] = "test",
            }).Build());
    private static GuidValue Id() => new() { Value = Guid.NewGuid().ToString("D") };
}
