using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Entities.WishlistItem;
using DKH.CustomerService.Domain.Enums;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;
using ContractAccountStatus = DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1.CustomerAccountStatus;
using ContractMembershipStatus = DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1.StorefrontMembershipStatus;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerAccountMapper
{
    public static CustomerAccountModel ToContractModel(this CustomerAccountEntity entity)
    {
        var model = new CustomerAccountModel
        {
            Id = GuidValue.FromGuid(entity.Id),
            VerifiedEmail = entity.VerifiedEmail,
            EmailVerifiedAt = ToTimestamp(entity.EmailVerifiedAt),
            FirstName = entity.FirstName ?? string.Empty,
            LastName = entity.LastName ?? string.Empty,
            PreferredLocale = entity.PreferredLocale,
            Status = entity.Status.ToContractStatus(),
            CreatedAt = ToTimestamp(entity.CreationTime),
        };

        if (entity.LastModificationTime.HasValue)
        {
            model.UpdatedAt = ToTimestamp(entity.LastModificationTime.Value);
        }

        return model;
    }

    public static StorefrontMembershipModel ToContractModel(this StorefrontMembershipEntity entity)
        => new()
        {
            Id = GuidValue.FromGuid(entity.Id),
            StorefrontId = GuidValue.FromGuid(entity.StorefrontId),
            FirstAuthenticatedAt = ToTimestamp(entity.FirstAuthenticatedAt),
            LastAuthenticatedAt = ToTimestamp(entity.LastAuthenticatedAt),
            LastActivityAt = ToTimestamp(entity.LastActivityAt),
            Status = entity.Status.ToContractStatus(),
        };

    public static LinkedCustomerIdentityModel ToContractModel(this LinkedCustomerIdentityEntity entity)
        => new()
        {
            Id = GuidValue.FromGuid(entity.Id),
            ProviderKind = entity.ProviderKind,
            DisplayName = entity.DisplayName ?? string.Empty,
            LinkedAt = ToTimestamp(entity.LinkedAt),
            VerifiedAt = ToTimestamp(entity.VerifiedAt),
        };

    public static ConsolidatedWishlistEntryModel ToConsolidatedContractModel(
        this WishlistItemEntity entity,
        Guid storefrontId)
        => new()
        {
            Id = GuidValue.FromGuid(entity.Id),
            StorefrontId = GuidValue.FromGuid(storefrontId),
            ProductId = GuidValue.FromGuid(entity.ProductId),
            ProductSkuId = entity.ProductSkuId.HasValue
                ? GuidValue.FromGuid(entity.ProductSkuId.Value)
                : null,
            AddedAt = ToTimestamp(entity.AddedAt),
            Note = entity.Note ?? string.Empty,
        };

    private static ContractAccountStatus ToContractStatus(this CustomerAccountStatusType status)
        => status switch
        {
            CustomerAccountStatusType.Active => ContractAccountStatus.Active,
            CustomerAccountStatusType.Blocked => ContractAccountStatus.Blocked,
            CustomerAccountStatusType.DeletionPending => ContractAccountStatus.DeletionPending,
            _ => ContractAccountStatus.Unspecified,
        };

    private static ContractMembershipStatus ToContractStatus(this StorefrontMembershipStatusType status)
        => status switch
        {
            StorefrontMembershipStatusType.Active => ContractMembershipStatus.Active,
            StorefrontMembershipStatusType.Blocked => ContractMembershipStatus.Blocked,
            StorefrontMembershipStatusType.Revoked => ContractMembershipStatus.Revoked,
            _ => ContractMembershipStatus.Unspecified,
        };

    private static Timestamp ToTimestamp(DateTime value)
        => Timestamp.FromDateTime(value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        });
}
