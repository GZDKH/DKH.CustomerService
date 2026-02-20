using DKH.CustomerService.Contracts.Customer.Models.ExternalIdentity.v1;
using DKH.CustomerService.Domain.Entities.ExternalIdentity;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class ExternalIdentityMapper
{
    public static ExternalIdentityModel ToContractModel(this CustomerExternalIdentityEntity entity)
    {
        return new ExternalIdentityModel
        {
            Id = GuidValue.FromGuid(entity.Id),
            CustomerId = GuidValue.FromGuid(entity.CustomerId),
            Provider = entity.Provider,
            ProviderUserId = entity.ProviderUserId,
            Email = entity.Email ?? string.Empty,
            DisplayName = entity.DisplayName ?? string.Empty,
            IsPrimary = entity.IsPrimary,
            LinkedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.LinkedAt, DateTimeKind.Utc)),
        };
    }
}
