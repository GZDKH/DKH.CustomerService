using System.Globalization;
using DKH.CustomerService.Contracts.Models.V1;
using DKH.CustomerService.Domain.Entities.CustomerProfile;
using DKH.CustomerService.Domain.Enums;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;

namespace DKH.CustomerService.Application.Mappers;

public static class CustomerProfileMapper
{
    public static CustomerProfile ToContractModel(this CustomerProfileEntity entity)
    {
        var profile = new CustomerProfile
        {
            Id = GuidValue.FromGuid(entity.Id),
            StorefrontId = GuidValue.FromGuid(entity.StorefrontId),
            UserId = entity.UserId,
            FirstName = entity.FirstName,
            LastName = entity.LastName ?? string.Empty,
            Username = entity.Username ?? string.Empty,
            PhotoUrl = entity.PhotoUrl ?? string.Empty,
            Phone = entity.Phone ?? string.Empty,
            Email = entity.Email ?? string.Empty,
            LanguageCode = entity.LanguageCode,
            ProviderType = entity.ProviderType,
            IsPremium = entity.IsPremium,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.CreationTime, DateTimeKind.Utc)),
            IsDeleted = entity.IsDeleted,
        };

        if (entity.LastModificationTime.HasValue)
        {
            profile.UpdatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.LastModificationTime.Value, DateTimeKind.Utc));
        }

        if (entity.DeletionTime.HasValue)
        {
            profile.DeletedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(entity.DeletionTime.Value, DateTimeKind.Utc));
        }

        profile.AccountStatus = entity.AccountStatus.ToContractModel();
        profile.ContactVerification = entity.ContactVerification.ToContractModel();

        return profile;
    }

    public static AccountStatus ToContractModel(this Domain.ValueObjects.AccountStatus status)
    {
        var result = new AccountStatus
        {
            Status = status.Status.ToContractEnum(),
            BlockReason = status.BlockReason ?? string.Empty,
            BlockedBy = status.BlockedBy ?? string.Empty,
            TotalOrdersCount = status.TotalOrdersCount,
            TotalSpent = status.TotalSpent.ToString("F2", CultureInfo.InvariantCulture),
        };

        if (status.BlockedAt.HasValue)
        {
            result.BlockedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(status.BlockedAt.Value, DateTimeKind.Utc));
        }

        if (status.LastLoginAt.HasValue)
        {
            result.LastLoginAt = Timestamp.FromDateTime(DateTime.SpecifyKind(status.LastLoginAt.Value, DateTimeKind.Utc));
        }

        if (status.LastActivityAt.HasValue)
        {
            result.LastActivityAt = Timestamp.FromDateTime(DateTime.SpecifyKind(status.LastActivityAt.Value, DateTimeKind.Utc));
        }

        return result;
    }

    public static ContactVerification ToContractModel(this Domain.ValueObjects.ContactVerification verification)
    {
        var result = new ContactVerification
        {
            EmailVerified = verification.EmailVerified,
            PhoneVerified = verification.PhoneVerified,
        };

        if (verification.EmailVerifiedAt.HasValue)
        {
            result.EmailVerifiedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(verification.EmailVerifiedAt.Value, DateTimeKind.Utc));
        }

        if (verification.PhoneVerifiedAt.HasValue)
        {
            result.PhoneVerifiedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(verification.PhoneVerifiedAt.Value, DateTimeKind.Utc));
        }

        return result;
    }

    public static AccountStatusEnum ToContractEnum(this AccountStatusType status) => status switch
    {
        AccountStatusType.Active => AccountStatusEnum.Active,
        AccountStatusType.Blocked => AccountStatusEnum.Blocked,
        AccountStatusType.Suspended => AccountStatusEnum.Suspended,
        AccountStatusType.Deleted => AccountStatusEnum.Deleted,
        _ => AccountStatusEnum.Unspecified,
    };
}
