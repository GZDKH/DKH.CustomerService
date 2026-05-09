using DKH.CustomerService.Domain.Authorization;
using DKH.Platform.Authorization.ResourceAccess.EntityFrameworkCore.Configurations;

namespace DKH.CustomerService.Infrastructure.Persistence.Configurations;

internal sealed class CustomerAccessGrantConfiguration
    : ResourceAccessGrantConfigurationBase<CustomerAccessGrantEntity, Guid>
{
    protected override string TableName => "customer_access_grants";
    protected override string? Schema => null;
}
