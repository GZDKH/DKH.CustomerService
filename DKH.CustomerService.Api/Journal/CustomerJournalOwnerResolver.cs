using DKH.CustomerService.Application.CustomerAccounts;
using Grpc.Core;

namespace DKH.CustomerService.Api.Journal;

/// <summary>Server-owned adapter for the additive journal RPCs; legacy APIs retain their own policies.</summary>
public sealed class CustomerJournalOwnerResolver(CustomerJournalAccounts accounts, IConfiguration configuration)
{
    public Task<CustomerJournalOwner> ResolveAsync(ServerCallContext context)
        => ResolveAsync(context.GetHttpContext(), context.CancellationToken);

    public Task<CustomerJournalOwner> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
        => ExecuteAsync(() => accounts.ResolveAsync(
            CustomerJournalPrincipal.Require(context, configuration), cancellationToken));

    public async Task<CustomerJournalOwner> RequireOwnerAsync(
        ServerCallContext context,
        Guid resourceOwnerId)
    {
        var owner = await ResolveAsync(context);
        if (owner.AccountId != resourceOwnerId)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Personal resource was not found."));
        }

        return owner;
    }

    public Task<Guid> ResolveLegacyProfileAsync(ServerCallContext context, Guid storefrontId, Guid legacyProfileId)
        => ExecuteAsync(() => accounts.ResolveLegacyProfileAsync(
            CustomerJournalPrincipal.Require(context.GetHttpContext(), configuration),
            storefrontId, legacyProfileId, context.CancellationToken));

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (CustomerAccountNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Personal resource was not found."));
        }
        catch (CustomerAccountAccessException)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Personal account is unavailable."));
        }
    }
}
