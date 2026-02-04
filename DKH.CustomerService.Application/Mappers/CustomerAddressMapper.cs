using DKH.CustomerService.Contracts.Models.V1;
using DKH.CustomerService.Domain.Entities.CustomerAddress;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerAddressMapper
{
    public static CustomerAddress ToContractModel(this CustomerAddressEntity entity)
    {
        var address = new CustomerAddress
        {
            Id = entity.Id.ToString(),
            CustomerId = entity.CustomerId.ToString(),
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
