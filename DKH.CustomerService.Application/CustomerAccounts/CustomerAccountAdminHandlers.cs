using System.Text.Json;
using DKH.CustomerService.Application.Mappers;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccountAdmin.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.CustomerService.Domain.Entities.CustomerAccount;
using DKH.CustomerService.Domain.Entities.StorefrontMembership;
using DKH.CustomerService.Domain.Enums;
using Google.Protobuf;
using ContractAccountStatus = DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1.CustomerAccountStatus;
using ContractMembershipStatus = DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1.StorefrontMembershipStatus;

namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed class ListAdminCustomerAccountsQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListAdminCustomerAccountsQuery, ListCustomerAccountsResponse>
{
    public async Task<ListCustomerAccountsResponse> Handle(
        ListAdminCustomerAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize, skip) = CustomerAccountPagination.Normalize(request.Page, request.PageSize);
        var query = CustomerAccountAdminSupport.Accounts(dbContext, request.IncludeDeleted)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var normalized = request.Query.Trim();
            if (normalized.Length > 256)
            {
                throw new ArgumentException("Search query must not exceed 256 characters.", nameof(request));
            }

            normalized = normalized.ToLowerInvariant();
            // Parameterless ToLower/Contains are intentionally used inside the
            // IQueryable expression: EF translates them to SQL LOWER/LIKE,
            // while StringComparison and CultureInfo overloads are not portable.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(account =>
                account.VerifiedEmail.ToLower().Contains(normalized) ||
                (account.FirstName != null && account.FirstName.ToLower().Contains(normalized)) ||
                (account.LastName != null && account.LastName.ToLower().Contains(normalized)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (request.Status is { } status && status != ContractAccountStatus.Unspecified)
        {
            var domainStatus = CustomerAccountAdminSupport.ToDomainStatus(status);
            query = query.Where(account => account.Status == domainStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var accounts = await query
            .OrderByDescending(account => account.CreationTime)
            .ThenBy(account => account.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var models = await CustomerAccountAdminSupport.BuildAccountModelsAsync(
            dbContext,
            accounts,
            cancellationToken);

        var response = new ListCustomerAccountsResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
        response.Items.AddRange(models);
        return response;
    }
}

public sealed class GetAdminCustomerAccountQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<GetAdminCustomerAccountQuery, AdminCustomerAccountModel>
{
    public async Task<AdminCustomerAccountModel> Handle(
        GetAdminCustomerAccountQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            request.AccountId,
            request.IncludeDeleted,
            tracking: false,
            cancellationToken);
        return (await CustomerAccountAdminSupport.BuildAccountModelsAsync(
            dbContext,
            [account],
            cancellationToken))[0];
    }
}

public sealed class ListAdminStorefrontMembershipsQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ListAdminStorefrontMembershipsQuery, ListAccountStorefrontMembershipsResponse>
{
    public async Task<ListAccountStorefrontMembershipsResponse> Handle(
        ListAdminStorefrontMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            request.AccountId,
            request.IncludeDeleted,
            tracking: false,
            cancellationToken);
        var (page, pageSize, skip) = CustomerAccountPagination.Normalize(request.Page, request.PageSize);
        IQueryable<StorefrontMembershipEntity> query = request.IncludeDeleted
            ? dbContext.StorefrontMemberships.IgnoreQueryFilters()
            : dbContext.StorefrontMemberships;
        query = query.AsNoTracking().Where(membership => membership.CustomerAccountId == account.Id);

        if (request.StorefrontId.HasValue)
        {
            query = query.Where(membership => membership.StorefrontId == request.StorefrontId.Value);
        }

        if (request.Status is { } status && status != ContractMembershipStatus.Unspecified)
        {
            var domainStatus = CustomerAccountAdminSupport.ToDomainStatus(status);
            query = query.Where(membership => membership.Status == domainStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var memberships = await query
            .OrderByDescending(membership => membership.LastActivityAt)
            .ThenBy(membership => membership.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var identities = await dbContext.LinkedCustomerIdentities
            .AsNoTracking()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .OrderBy(identity => identity.ProviderKind)
            .ThenBy(identity => identity.Id)
            .ToListAsync(cancellationToken);

        var response = new ListAccountStorefrontMembershipsResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
        response.Items.AddRange(memberships.Select(membership =>
            membership.ToAdminContractModel(account, identities)));
        return response;
    }
}

public sealed class SetAdminCustomerAccountStatusCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<SetAdminCustomerAccountStatusCommand, AdminCustomerAccountModel>
{
    public async Task<AdminCustomerAccountModel> Handle(
        SetAdminCustomerAccountStatusCommand request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            request.AccountId,
            includeDeleted: false,
            tracking: true,
            cancellationToken);
        switch (request.Status)
        {
            case ContractAccountStatus.Active:
                account.Unblock();
                break;
            case ContractAccountStatus.Blocked:
                account.Block();
                break;
            default:
                throw new ArgumentException(
                    "Only active or blocked are valid administrative account statuses.",
                    nameof(request));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await CustomerAccountAdminSupport.BuildAccountModelsAsync(
            dbContext,
            [account],
            cancellationToken))[0];
    }
}

public sealed class SetAdminStorefrontMembershipStatusCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<SetAdminStorefrontMembershipStatusCommand, AdminStorefrontMembershipModel>
{
    public async Task<AdminStorefrontMembershipModel> Handle(
        SetAdminStorefrontMembershipStatusCommand request,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.StorefrontMemberships
            .SingleOrDefaultAsync(candidate => candidate.Id == request.MembershipId, cancellationToken)
            ?? throw new CustomerAccountNotFoundException("Storefront membership was not found.");
        switch (request.Status)
        {
            case ContractMembershipStatus.Active:
                membership.Activate();
                break;
            case ContractMembershipStatus.Blocked:
                membership.Block();
                break;
            case ContractMembershipStatus.Revoked:
                membership.Revoke();
                break;
            default:
                throw new ArgumentException(
                    "Only active, blocked, or revoked are valid administrative membership statuses.",
                    nameof(request));
        }

        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            membership.CustomerAccountId,
            includeDeleted: false,
            tracking: false,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var identities = await dbContext.LinkedCustomerIdentities
            .AsNoTracking()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .ToListAsync(cancellationToken);
        return membership.ToAdminContractModel(account, identities);
    }
}

public sealed class ExportAdminCustomerAccountQueryHandler(IAppDbContext dbContext)
    : IRequestHandler<ExportAdminCustomerAccountQuery, ExportCustomerAccountResponse>
{
    public async Task<ExportCustomerAccountResponse> Handle(
        ExportAdminCustomerAccountQuery request,
        CancellationToken cancellationToken)
    {
        var format = string.IsNullOrWhiteSpace(request.Format)
            ? "json"
            : request.Format.Trim().ToLowerInvariant();
        if (format != "json")
        {
            throw new ArgumentException("Only json export is supported.", nameof(request));
        }

        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            request.AccountId,
            includeDeleted: false,
            tracking: false,
            cancellationToken);
        var memberships = await dbContext.StorefrontMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(membership => membership.CustomerAccountId == account.Id)
            .OrderBy(membership => membership.StorefrontId)
            .ToListAsync(cancellationToken);
        var providers = await dbContext.LinkedCustomerIdentities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .OrderBy(identity => identity.ProviderKind)
            .ToListAsync(cancellationToken);
        var document = new CustomerAccountExportDocument(
            account.Id,
            account.VerifiedEmail,
            account.EmailVerifiedAt,
            account.FirstName,
            account.LastName,
            account.PreferredLocale,
            account.Status.ToString(),
            [.. memberships.Select(item => new MembershipExportDocument(
                item.Id,
                item.StorefrontId,
                item.Status.ToString(),
                item.FirstAuthenticatedAt,
                item.LastAuthenticatedAt,
                item.LastActivityAt))],
            [.. providers.Select(item => new ProviderExportDocument(
                item.ProviderKind,
                item.LinkedAt,
                item.VerifiedAt))]);

        return new ExportCustomerAccountResponse
        {
            Data = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(document)),
            ContentType = "application/json",
            FileName = $"customer-account-{account.Id:N}.json",
        };
    }

    private sealed record CustomerAccountExportDocument(
        Guid AccountId,
        string VerifiedEmail,
        DateTime EmailVerifiedAt,
        string? FirstName,
        string? LastName,
        string PreferredLocale,
        string Status,
        IReadOnlyList<MembershipExportDocument> Memberships,
        IReadOnlyList<ProviderExportDocument> LinkedProviders);

    private sealed record MembershipExportDocument(
        Guid MembershipId,
        Guid StorefrontId,
        string Status,
        DateTime FirstAuthenticatedAt,
        DateTime LastAuthenticatedAt,
        DateTime LastActivityAt);

    private sealed record ProviderExportDocument(
        string ProviderKind,
        DateTime LinkedAt,
        DateTime VerifiedAt);
}

public sealed class DeleteAdminCustomerAccountCommandHandler(IAppDbContext dbContext)
    : IRequestHandler<DeleteAdminCustomerAccountCommand>
{
    public async Task Handle(
        DeleteAdminCustomerAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await CustomerAccountAdminSupport.RequireAccountAsync(
            dbContext,
            request.AccountId,
            includeDeleted: false,
            tracking: true,
            cancellationToken);
        var memberships = await dbContext.StorefrontMemberships
            .IgnoreQueryFilters()
            .Where(membership => membership.CustomerAccountId == account.Id)
            .ToListAsync(cancellationToken);
        var profileIds = memberships
            .Where(membership => membership.LegacyCustomerProfileId.HasValue)
            .Select(membership => membership.LegacyCustomerProfileId!.Value)
            .ToList();
        var profiles = await dbContext.CustomerProfiles
            .IgnoreQueryFilters()
            .Include(profile => profile.Addresses)
            .Include(profile => profile.WishlistItems)
            .Include(profile => profile.ExternalIdentities)
            .Where(profile => profile.CustomerAccountId == account.Id || profileIds.Contains(profile.Id))
            .ToListAsync(cancellationToken);
        var linkedIdentities = await dbContext.LinkedCustomerIdentities
            .IgnoreQueryFilters()
            .Where(identity => identity.CustomerAccountId == account.Id)
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            profile.Anonymize();
        }

        foreach (var membership in memberships)
        {
            membership.RevokeAndDelete();
        }

        foreach (var identity in linkedIdentities)
        {
            identity.AnonymizeForAccountDeletion();
        }

        account.AnonymizeForDeletion(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal static class CustomerAccountAdminSupport
{
    public static IQueryable<CustomerAccountEntity> Accounts(
        IAppDbContext dbContext,
        bool includeDeleted)
        => includeDeleted
            ? dbContext.CustomerAccounts.IgnoreQueryFilters()
            : dbContext.CustomerAccounts;

    public static async Task<CustomerAccountEntity> RequireAccountAsync(
        IAppDbContext dbContext,
        Guid accountId,
        bool includeDeleted,
        bool tracking,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Customer account id must not be empty.", nameof(accountId));
        }

        var query = Accounts(dbContext, includeDeleted);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(account => account.Id == accountId, cancellationToken)
               ?? throw new CustomerAccountNotFoundException("Customer account was not found.");
    }

    public static async Task<IReadOnlyList<AdminCustomerAccountModel>> BuildAccountModelsAsync(
        IAppDbContext dbContext,
        List<CustomerAccountEntity> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return [];
        }

        var accountIds = accounts.Select(account => account.Id).ToArray();
        var subjects = accounts.Select(account => account.IdentitySubject).ToArray();
        var membershipAccountIds = await dbContext.StorefrontMemberships
            .AsNoTracking()
            .Where(membership => accountIds.Contains(membership.CustomerAccountId))
            .Select(membership => membership.CustomerAccountId)
            .ToListAsync(cancellationToken);
        var membershipCounts = membershipAccountIds
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var identities = await dbContext.LinkedCustomerIdentities
            .AsNoTracking()
            .Where(identity => accountIds.Contains(identity.CustomerAccountId))
            .OrderBy(identity => identity.ProviderKind)
            .ThenBy(identity => identity.Id)
            .ToListAsync(cancellationToken);
        var identitiesByAccount = identities
            .GroupBy(identity => identity.CustomerAccountId)
            .ToDictionary(group => group.Key, group => group.AsEnumerable());
        var ambiguousSubjects = await dbContext.CustomerProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(profile =>
                subjects.Contains(profile.UserId) &&
                profile.AccountReconciliationStatus == CustomerAccountReconciliationStatusType.Quarantined)
            .Select(profile => profile.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var ambiguousSubjectSet = ambiguousSubjects.ToHashSet(StringComparer.Ordinal);

        return [.. accounts.Select(account => account.ToAdminContractModel(
                membershipCounts.GetValueOrDefault(account.Id),
                identitiesByAccount.GetValueOrDefault(account.Id) ?? [],
                ambiguousSubjectSet.Contains(account.IdentitySubject)))];
    }

    public static CustomerAccountStatusType ToDomainStatus(ContractAccountStatus status)
        => status switch
        {
            ContractAccountStatus.Active => CustomerAccountStatusType.Active,
            ContractAccountStatus.Blocked => CustomerAccountStatusType.Blocked,
            ContractAccountStatus.DeletionPending => CustomerAccountStatusType.DeletionPending,
            _ => throw new ArgumentException("A concrete customer account status is required.", nameof(status)),
        };

    public static StorefrontMembershipStatusType ToDomainStatus(ContractMembershipStatus status)
        => status switch
        {
            ContractMembershipStatus.Active => StorefrontMembershipStatusType.Active,
            ContractMembershipStatus.Blocked => StorefrontMembershipStatusType.Blocked,
            ContractMembershipStatus.Revoked => StorefrontMembershipStatusType.Revoked,
            _ => throw new ArgumentException("A concrete storefront membership status is required.", nameof(status)),
        };
}
