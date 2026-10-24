using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Application.Addresses.CreateAddress;
using DKH.CustomerService.Application.Addresses.DeleteAddress;
using DKH.CustomerService.Application.Addresses.GetAddress;
using DKH.CustomerService.Application.Addresses.GetDefaultAddress;
using DKH.CustomerService.Application.Addresses.ListAddresses;
using DKH.CustomerService.Application.Addresses.SetDefaultAddress;
using DKH.CustomerService.Application.Addresses.UpdateAddress;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Infrastructure.Persistence;
using DKH.CustomerService.Infrastructure.Persistence.Repositories;
using DKH.Platform.Domain.Enums;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKH.CustomerService.IntegrationTests.Integration.Addresses;

[Trait("Category", "Unit")]
public sealed class CustomerAddressOwnershipTests
{
    [Theory]
    [InlineData("own", true)]
    [InlineData("foreign-user", false)]
    [InlineData("foreign-storefront", false)]
    [InlineData("deleted-profile", false)]
    [InlineData("blocked-profile", false)]
    [InlineData("suspended-profile", false)]
    [InlineData("deleted-address", false)]
    [InlineData("missing-profile", false)]
    public async Task GetAddressRequiresActualActiveSelectedProfileAndAddressAsync(string scenario, bool found)
    {
        await using var db = CreateContext();
        var storefront = Guid.NewGuid();
        const string user = "synthetic-owner";
        var owner = CustomerProfileEntity.Create(
            scenario == "foreign-storefront" ? Guid.NewGuid() : storefront,
            scenario == "foreign-user" ? "synthetic-other" : user, "Synthetic");
        var address = CustomerAddressEntity.Create(owner.Id, "Synthetic address", "ZZ", "Synthetic city");
        db.AddRange(owner, address);
        await db.SaveChangesAsync();
        if (scenario == "deleted-profile")
        {
            db.Entry(owner).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "deleted-address")
        {
            db.Entry(address).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "blocked-profile")
        {
            owner.AccountStatus.Block("Synthetic block", "Synthetic operator");
        }

        if (scenario == "suspended-profile")
        {
            owner.AccountStatus.Suspend(DateTime.UtcNow.AddHours(1), "Synthetic suspension", "Synthetic operator");
        }

        await db.SaveChangesAsync();
        // Retain tracked entities too: FindAsync must not bypass the actual owner predicate.
        var result = await new GetAddressQueryHandler(db).Handle(
            new(storefront, scenario == "missing-profile" ? "synthetic-missing" : user, address.Id), CancellationToken.None);
        Assert.Equal(found, result.Found);
        if (found)
        {
            Assert.Equal(address.Id.ToString("D"), result.Address.Id.Value);
        }
        else
        {
            Assert.Null(result.Address);
        }
    }

    [Theory]
    [InlineData(PlatformSoftDeleteFilter.ActiveOnly, "active")]
    [InlineData(PlatformSoftDeleteFilter.InactiveOnly, "active")]
    [InlineData(PlatformSoftDeleteFilter.All, "active")]
    [InlineData(PlatformSoftDeleteFilter.ActiveOnly, "deleted")]
    [InlineData(PlatformSoftDeleteFilter.All, "deleted")]
    [InlineData(PlatformSoftDeleteFilter.ActiveOnly, "blocked")]
    [InlineData(PlatformSoftDeleteFilter.All, "blocked")]
    public async Task ListAddressesKeepsExactActiveProfileAndExplicitAddressStateAsync(PlatformSoftDeleteFilter filter, string profileState)
    {
        await using var db = CreateContext();
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "synthetic-owner", "Synthetic");
        var foreign = CustomerProfileEntity.Create(profile.StorefrontId, "synthetic-other", "Synthetic");
        var active = CustomerAddressEntity.Create(profile.Id, "Active own", "ZZ", "Synthetic");
        var inactive = CustomerAddressEntity.Create(profile.Id, "Inactive own", "ZZ", "Synthetic");
        var other = CustomerAddressEntity.Create(foreign.Id, "Foreign", "ZZ", "Synthetic");
        db.AddRange(profile, foreign, active, inactive, other);
        await db.SaveChangesAsync();
        db.Entry(inactive).Property(value => value.IsDeleted).CurrentValue = true;
        if (profileState == "deleted")
        {
            db.Entry(profile).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (profileState == "blocked")
        {
            profile.AccountStatus.Block("Synthetic block", "Synthetic operator");
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await new ListAddressesQueryHandler(db)
            .Handle(new(profile.StorefrontId, profile.UserId, filter), CancellationToken.None);
        string[] expected = profileState != "active" ? [] : filter switch
        {
            PlatformSoftDeleteFilter.ActiveOnly => [active.Id.ToString("D")],
            PlatformSoftDeleteFilter.InactiveOnly => [inactive.Id.ToString("D")],
            _ => [active.Id.ToString("D"), inactive.Id.ToString("D")]
        };
        Assert.Equal(expected.Order(), result.Addresses.Select(value => value.Id.Value).Order());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    public static TheoryData<string, string> MutationCases => new()
    {
        { "update", "own" }, { "update", "foreign-user" }, { "update", "foreign-storefront" },
        { "update", "deleted-profile" }, { "update", "blocked-profile" }, { "update", "deleted-address" }, { "update", "missing-profile" },
        { "delete", "own" }, { "delete", "foreign-user" }, { "delete", "foreign-storefront" },
        { "delete", "deleted-profile" }, { "delete", "blocked-profile" }, { "delete", "deleted-address" }, { "delete", "missing-profile" }
    };

    [Theory]
    [MemberData(nameof(MutationCases))]
    public async Task OrdinaryMutationsCannotChangeAnotherOrInactiveOwnerAddressAsync(string operation, string scenario)
    {
        await using var db = CreateContext();
        var storefront = Guid.NewGuid();
        const string user = "synthetic-owner";
        var profile = CustomerProfileEntity.Create(scenario == "foreign-storefront" ? Guid.NewGuid() : storefront,
            scenario == "foreign-user" ? "synthetic-other" : user, "Synthetic");
        var address = CustomerAddressEntity.Create(profile.Id, "Original synthetic", "ZZ", "Synthetic");
        db.AddRange(profile, address);
        await db.SaveChangesAsync();
        if (scenario == "deleted-profile")
        {
            db.Entry(profile).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "deleted-address")
        {
            db.Entry(address).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "blocked-profile")
        {
            profile.AccountStatus.Block("Synthetic block", "Synthetic operator");
        }

        await db.SaveChangesAsync();
        var selectedUser = scenario == "missing-profile" ? "synthetic-missing" : user;
        if (operation == "update")
        {
            var request = new UpdateAddressCommand(storefront, selectedUser, address.Id, "Changed synthetic",
                null, null, null, null, null, null, null, null, null, null, null);
            var handler = new UpdateAddressCommandHandler(db);
            if (scenario == "own")
            {
                var response = await handler.Handle(request, CancellationToken.None);
                Assert.Equal("Changed synthetic", response.Address.Label);
            }
            else
            {
                var denied = await Assert.ThrowsAsync<RpcException>(() => handler.Handle(request, CancellationToken.None));
                Assert.Equal(StatusCode.NotFound, denied.StatusCode);
                Assert.Equal("Original synthetic", address.Label);
                Assert.False(db.ChangeTracker.HasChanges());
            }
        }
        else
        {
            var response = await new DeleteAddressCommandHandler(db).Handle(new(storefront, selectedUser, address.Id), CancellationToken.None);
            Assert.Equal(scenario == "own", response.Success);
            if (scenario != "own")
            {
                Assert.Equal(scenario == "deleted-address", address.IsDeleted);
                Assert.Equal("Original synthetic", address.Label);
                Assert.False(db.ChangeTracker.HasChanges());
            }
        }
    }

    [Theory]
    [InlineData("get-default", "own")]
    [InlineData("get-default", "deleted-address")]
    [InlineData("get-default", "deleted-profile")]
    [InlineData("get-default", "blocked-profile")]
    [InlineData("set-default", "own")]
    [InlineData("set-default", "deleted-address")]
    [InlineData("set-default", "deleted-profile")]
    [InlineData("set-default", "blocked-profile")]
    [InlineData("create", "own")]
    [InlineData("create", "deleted-address")]
    [InlineData("create", "deleted-profile")]
    [InlineData("create", "blocked-profile")]
    public async Task CreateAndDefaultOperationsRequireActiveOwnerAndPreserveDeletedAddressHistoryAsync(string operation, string scenario)
    {
        await using var db = CreateContext();
        var profile = CustomerProfileEntity.Create(Guid.NewGuid(), "synthetic-owner", "Synthetic");
        var address = CustomerAddressEntity.Create(profile.Id, "Original synthetic", "ZZ", "Synthetic", isDefault: true);
        db.AddRange(profile, address);
        await db.SaveChangesAsync();
        if (scenario == "deleted-address")
        {
            db.Entry(address).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "deleted-profile")
        {
            db.Entry(profile).Property(value => value.IsDeleted).CurrentValue = true;
        }

        if (scenario == "blocked-profile")
        {
            profile.AccountStatus.Block("Synthetic block", "Synthetic operator");
        }

        await db.SaveChangesAsync();
        using var services = new ServiceCollection().AddSingleton<IAppDbContext>(db)
            .AddSingleton<ICustomerRepository>(new CustomerRepository(db)).BuildServiceProvider();
        if (operation == "get-default")
        {
            var result = await ActivatorUtilities.CreateInstance<GetDefaultAddressQueryHandler>(services)
                .Handle(new(profile.StorefrontId, profile.UserId), CancellationToken.None);
            Assert.Equal(scenario == "own", result.Found);
            Assert.Equal(scenario == "own", result.Address is not null);
        }
        else if (operation == "set-default")
        {
            var handler = ActivatorUtilities.CreateInstance<SetDefaultAddressCommandHandler>(services);
            var request = new SetDefaultAddressCommand(profile.StorefrontId, profile.UserId, address.Id);
            if (scenario == "own")
            {
                Assert.True((await handler.Handle(request, CancellationToken.None)).Success);
            }
            else
            {
                var denied = await Assert.ThrowsAsync<RpcException>(() => handler.Handle(request, CancellationToken.None));
                Assert.Equal(StatusCode.NotFound, denied.StatusCode);
            }
        }
        else
        {
            var handler = ActivatorUtilities.CreateInstance<CreateAddressCommandHandler>(services);
            var request = new CreateAddressCommand(profile.StorefrontId, profile.UserId, "New synthetic", "ZZ", "Synthetic",
                null, null, null, null, null, true, null, null, null, null);
            if (scenario is "own" or "deleted-address")
            {
                var result = await handler.Handle(request, CancellationToken.None);
                Assert.True(result.Address.IsDefault);
                Assert.NotEqual(address.Id.ToString("D"), result.Address.Id.Value);
                Assert.Equal(scenario == "deleted-address", address.IsDefault);
            }
            else
            {
                var denied = await Assert.ThrowsAsync<RpcException>(() => handler.Handle(request, CancellationToken.None));
                Assert.Equal(StatusCode.NotFound, denied.StatusCode);
                Assert.Equal(1, await db.CustomerAddresses.CountAsync());
            }
        }

        Assert.Equal(scenario == "deleted-address", address.IsDeleted);
        Assert.Equal("Original synthetic", address.Label);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static TestContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override Guid? GetCurrentUserId() => null;
    }
}
