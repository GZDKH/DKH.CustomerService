using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.DeliveryService.Contracts.Delivery.Models.v2;
using FluentAssertions;

namespace DKH.CustomerService.IntegrationTests.Integration.DeliveryContacts;

public sealed partial class DeliveryContactOwnerCallbackUnitTests
{
    [Fact]
    public async Task GenuineTypedCompanyActorIsSeparateExactNativeDataNotRecipientProfileAsync()
    {
        var f = await Fixture.CreateAsync();
        var party = new AuthorityRefModel { Authority = "counterparty.party", ResourceId = NewId(), Revision = 17, Digest = new string('a', 64) };
        f.Response.ActingActorRef = new() { CounterpartyAuthorityRef = party };
        var result = await f.ReadAsync();
        result.ActingActor.Should().Be(new DeliveryContactActingActor(null,
            new DeliveryContactAuthority(party.Authority, Id(party.ResourceId), party.Revision, party.Digest)));
        result.RecipientProfileId.Should().Be(Id(f.Response.RecipientProfileBinding));
        result.ActingActor!.CounterpartyAuthority!.ResourceId.Should().NotBe(result.RecipientProfileId);
        f.Invoker.Request!.Scope.Should().BeEquivalentTo(f.Response.Scope);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("none")]
    [InlineData("unknown")]
    [InlineData("service")]
    [InlineData("self-zero")]
    [InlineData("self-malformed")]
    [InlineData("company-wrong-authority")]
    [InlineData("company-zero")]
    [InlineData("company-zero-revision")]
    [InlineData("company-upper-digest")]
    public async Task MissingLegacyServiceOrMalformedNativeActorNeverAuthorizesRecipientAsync(string changed)
    {
        var f = await Fixture.CreateAsync();
        f.Response.ActingActorRef = changed switch
        {
            "missing" => null,
            "none" => new(),
            // Unknown oneof field4 is retained by the real generated parser;
            // it cannot manufacture a recognized self or company actor.
            "unknown" => ActorRefModel.Parser.ParseFrom([0x22, 0x03, 0x61, 0x62, 0x63]),
            "service" => new() { ServiceActor = "not-an-original-human" },
            "self-zero" => new() { CustomerIdentityId = new() { Value = Guid.Empty.ToString("D") } },
            "self-malformed" => new() { CustomerIdentityId = new() { Value = "subject-not-profile" } },
            _ => new()
            {
                CounterpartyAuthorityRef = new AuthorityRefModel
                {
                    Authority = changed == "company-wrong-authority" ? "customer.delivery-contact.v1" : "counterparty.party",
                    ResourceId = changed == "company-zero" ? new() { Value = Guid.Empty.ToString("D") } : NewId(),
                    Revision = changed == "company-zero-revision" ? 0 : 7,
                    Digest = new string(changed == "company-upper-digest" ? 'A' : 'a', 64),
                }
            },
        };
        var refusal = await Assert.ThrowsAsync<DeliveryContactFailureException>(f.ReadAsync);
        refusal.Code.Should().Be("AUTHORITY_UNAVAILABLE");
        f.Invoker.Calls.Should().Be(1);
    }
}
