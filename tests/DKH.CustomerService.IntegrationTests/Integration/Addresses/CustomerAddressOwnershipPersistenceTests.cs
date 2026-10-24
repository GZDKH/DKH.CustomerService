using DKH.CustomerService.Application.Addresses.CreateAddress;
using DKH.CustomerService.Application.Addresses.DeleteAddress;
using DKH.CustomerService.Application.Addresses.GetAddress;
using DKH.CustomerService.Application.Addresses.GetDefaultAddress;
using DKH.CustomerService.Application.Addresses.ListAddresses;
using DKH.CustomerService.Application.Addresses.SetDefaultAddress;
using DKH.CustomerService.Application.Addresses.UpdateAddress;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.IntegrationTests.Integration.Journal;
using DKH.Platform.Domain.Enums;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace DKH.CustomerService.IntegrationTests.Integration.Addresses;

// Same existing isolated synthetic PostgreSQL fixture as normal owner persistence checks.
// Compilation/listing does not claim native execution or production authority.
[Trait("Category", "Integration")]
public sealed class CustomerAddressOwnershipPersistenceTests(JournalHostFixture fixture) : IClassFixture<JournalHostFixture>
{
    [Fact]
    public async Task RelationalReadsRequireExactActiveOwnerAndExplicitAddressStateAsync()
    {
        var seed = await SeedAsync();
        await fixture.ReadAsync(async db =>
        {
            var get = new GetAddressQueryHandler(db);
            Assert.True((await get.Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Active.Id), default)).Found);
            Assert.False((await get.Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Foreign.Id), default)).Found);
            Assert.False((await get.Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Inactive.Id), default)).Found);
            var list = new ListAddressesQueryHandler(db);
            Assert.Equal(seed.Active.Id.ToString("D"), Assert.Single((await list.Handle(new(seed.Profile.StorefrontId,
                seed.Profile.UserId), default)).Addresses).Id.Value);
            Assert.Equal(seed.Inactive.Id.ToString("D"), Assert.Single((await list.Handle(new(seed.Profile.StorefrontId,
                seed.Profile.UserId, PlatformSoftDeleteFilter.InactiveOnly), default)).Addresses).Id.Value);
            return true;
        });
        await fixture.ChangeAsync(db => db.CustomerProfiles.Single(row => row.Id == seed.Profile.Id)
            .AccountStatus.Block("Synthetic blocked owner", "Synthetic operator"));
        Assert.False((await fixture.ReadAsync(db => new GetAddressQueryHandler(db)
            .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Active.Id), default))).Found);
    }

    [Fact]
    public async Task RelationalForeignMutationsRefuseAndConserveOriginalAddressAsync()
    {
        var seed = await SeedAsync();
        await fixture.ReadAsync(async db =>
        {
            var denied = await Assert.ThrowsAsync<RpcException>(() => new UpdateAddressCommandHandler(db)
                .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Foreign.Id, "Forbidden synthetic",
                    null, null, null, null, null, null, null, null, null, null, null), default));
            Assert.Equal(StatusCode.NotFound, denied.StatusCode);
            Assert.False((await new DeleteAddressCommandHandler(db)
                .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Foreign.Id), default)).Success);
            var retained = await db.CustomerAddresses.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == seed.Foreign.Id);
            Assert.Equal("Foreign synthetic", retained.Label);
            Assert.False(retained.IsDeleted);
            Assert.False(db.ChangeTracker.HasChanges());
            return true;
        });
    }

    [Fact]
    public async Task RelationalOwnLifecyclePreservesInactiveDefaultHistoryAsync()
    {
        var seed = await SeedAsync();
        await fixture.ReadAsync(async db =>
        {
            var created = await new CreateAddressCommandHandler(db).Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId,
                "New synthetic", "ZZ", "Synthetic", null, null, null, null, null, true, null, null, null, null), default);
            var current = await new GetDefaultAddressQueryHandler(db).Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId), default);
            Assert.Equal(created.Address.Id, current.Address.Id);
            Assert.True((await new SetDefaultAddressCommandHandler(db)
                .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Active.Id), default)).Success);
            Assert.True((await new DeleteAddressCommandHandler(db)
                .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Active.Id), default)).Success);
            Assert.False((await new GetAddressQueryHandler(db)
                .Handle(new(seed.Profile.StorefrontId, seed.Profile.UserId, seed.Active.Id), default)).Found);
            var historical = await db.CustomerAddresses.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == seed.Inactive.Id);
            Assert.True(historical.IsDeleted);
            Assert.True(historical.IsDefault);
            Assert.Equal("Inactive synthetic", historical.Label);
            return true;
        });
    }

    private async Task<Seed> SeedAsync()
    {
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "synthetic-owner:" + Guid.NewGuid().ToString("N"), "Synthetic");
        var other = CustomerProfileEntity.Create(profile.StorefrontId, "synthetic-other:" + Guid.NewGuid().ToString("N"), "Synthetic");
        var active = CustomerAddressEntity.Create(profile.Id, "Active synthetic", "ZZ", "Synthetic", isDefault: true);
        var inactive = CustomerAddressEntity.Create(profile.Id, "Inactive synthetic", "ZZ", "Synthetic", isDefault: true);
        var foreign = CustomerAddressEntity.Create(other.Id, "Foreign synthetic", "ZZ", "Synthetic");
        await fixture.ChangeAsync(db => db.AddRange(profile, other, active, inactive, foreign));
        await fixture.ChangeAsync(db => db.Entry(db.CustomerAddresses.Single(row => row.Id == inactive.Id))
            .Property(row => row.IsDeleted).CurrentValue = true);
        return new(profile, active, inactive, foreign);
    }

    private sealed record Seed(CustomerProfileEntity Profile, CustomerAddressEntity Active, CustomerAddressEntity Inactive, CustomerAddressEntity Foreign);
}
