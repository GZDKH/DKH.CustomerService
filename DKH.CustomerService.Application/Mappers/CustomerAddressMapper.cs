using DKH.CustomerService.Contracts.Customer.Models.CustomerAddress.v1;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerAddressMapper
{
    public static CustomerAddressModel ToContractModel(this CustomerAddressEntity entity)
    {
        var address = new CustomerAddressModel
        {
            Id = GuidValue.FromGuid(entity.Id),
            CustomerId = GuidValue.FromGuid(entity.CustomerId),
            Label = entity.Label,
            Country = entity.Country,
            City = entity.City,
            Street = entity.Street ?? string.Empty,
            Building = entity.Building ?? string.Empty,
            Apartment = entity.Apartment ?? string.Empty,
            PostalCode = entity.PostalCode ?? string.Empty,
            Phone = entity.Phone ?? string.Empty,
            IsDefault = entity.IsDefault,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.CreationTime, DateTimeKind.Utc)),
        };

        if (entity.LastModificationTime.HasValue)
        {
            address.UpdatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.LastModificationTime.Value, DateTimeKind.Utc));
        }

        return address;
    }
}
