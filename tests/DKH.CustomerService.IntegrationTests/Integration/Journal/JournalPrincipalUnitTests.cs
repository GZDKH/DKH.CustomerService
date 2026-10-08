using System.Security.Claims;
using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.CustomerAccounts;
using FluentAssertions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

[Trait("Category", "Unit")]
public sealed class JournalPrincipalUnitTests
{
    [Fact]
    public async Task AuthenticatedPrincipalClaims_CannotMintValidatedTokenEvidenceAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Platform:Auth:Keycloak:AuthServerUrl"] = "https://journal.fixture.invalid",
            ["Platform:Auth:Keycloak:Realm"] = "test",
        }).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "non-guid-human"), new Claim("dkh_principal_purpose", "personal:v1")], "injected")),
        };
        var resolver = new CustomerJournalOwnerResolver(new CustomerJournalAccounts(Substitute.For<IAppDbContext>()), configuration);
        var denied = await Assert.ThrowsAsync<RpcException>(() => resolver.ResolveAsync(context));
        denied.StatusCode.Should().Be(StatusCode.PermissionDenied);
    }
}
