using System.Security.Claims;
using DKH.CustomerService.Application.CustomerAccounts;
using DKH.CustomerService.Contracts.Customer.Api.CustomerAccountAdmin.v1;
using DKH.CustomerService.Contracts.Customer.Models.CustomerAccount.v1;
using DKH.Platform.Grpc.Common.Types;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.CustomerAccountAdmin.v1.CustomerAccountAdminService;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.PlatformCustomerAccountAdmin)]
public sealed partial class CustomerAccountAdminGrpcService(
    IMediator mediator,
    ILogger<CustomerAccountAdminGrpcService> logger)
    : ContractsService.CustomerAccountAdminServiceBase
{
    public override async Task<ListCustomerAccountsResponse> ListCustomerAccounts(
        ListCustomerAccountsRequest request,
        ServerCallContext context)
    {
        var response = await ExecuteAsync(() => mediator.Send(
            new ListAdminCustomerAccountsQuery(
                request.Query,
                request.HasStatus ? request.Status : null,
                request.IncludeDeleted,
                request.Page,
                request.PageSize),
            context.CancellationToken));
        if (logger.IsEnabled(LogLevel.Information))
        {
            var actorSubject = Actor(context);
            LogGlobalAccountsRead(
                logger,
                actorSubject,
                response.Items.Count,
                response.TotalCount,
                request.IncludeDeleted);
        }

        return response;
    }

    public override async Task<AdminCustomerAccountModel> GetCustomerAccount(
        GetAdminCustomerAccountRequest request,
        ServerCallContext context)
    {
        var accountId = RequiredGuid(request.AccountId, "account_id");
        var response = await ExecuteAsync(() => mediator.Send(
            new GetAdminCustomerAccountQuery(accountId, request.IncludeDeleted),
            context.CancellationToken));
        if (logger.IsEnabled(LogLevel.Information))
        {
            var actorSubject = Actor(context);
            LogGlobalAccountRead(logger, actorSubject, accountId, request.IncludeDeleted);
        }

        return response;
    }

    public override async Task<ListAccountStorefrontMembershipsResponse> ListAccountStorefrontMemberships(
        ListAccountStorefrontMembershipsRequest request,
        ServerCallContext context)
    {
        var accountId = RequiredGuid(request.AccountId, "account_id");
        var storefrontId = request.StorefrontId is not null
            ? RequiredGuid(request.StorefrontId, "storefront_id")
            : (Guid?)null;
        var response = await ExecuteAsync(() => mediator.Send(
            new ListAdminStorefrontMembershipsQuery(
                accountId,
                storefrontId,
                request.HasStatus ? request.Status : null,
                request.IncludeDeleted,
                request.Page,
                request.PageSize),
            context.CancellationToken));
        if (logger.IsEnabled(LogLevel.Information))
        {
            var actorSubject = Actor(context);
            LogAccountMembershipsRead(
                logger,
                actorSubject,
                accountId,
                storefrontId,
                response.Items.Count,
                response.TotalCount,
                request.IncludeDeleted);
        }

        return response;
    }

    public override async Task<AdminCustomerAccountModel> SetCustomerAccountStatus(
        SetCustomerAccountStatusRequest request,
        ServerCallContext context)
    {
        var accountId = RequiredGuid(request.AccountId, "account_id");
        var reasonCode = RequireReasonCode(request.Reason);
        var response = await ExecuteAsync(() => mediator.Send(
            new SetAdminCustomerAccountStatusCommand(accountId, request.Status),
            context.CancellationToken));
        LogGlobalAccountStatusChanged(logger, Actor(context), accountId, request.Status.ToString(), reasonCode);
        return response;
    }

    public override async Task<AdminStorefrontMembershipModel> SetStorefrontMembershipStatus(
        SetStorefrontMembershipStatusRequest request,
        ServerCallContext context)
    {
        var membershipId = RequiredGuid(request.MembershipId, "membership_id");
        var reasonCode = RequireReasonCode(request.Reason);
        var response = await ExecuteAsync(() => mediator.Send(
            new SetAdminStorefrontMembershipStatusCommand(membershipId, request.Status),
            context.CancellationToken));
        LogMembershipStatusChanged(logger, Actor(context), membershipId, request.Status.ToString(), reasonCode);
        return response;
    }

    public override async Task<ExportCustomerAccountResponse> ExportCustomerAccount(
        ExportCustomerAccountRequest request,
        ServerCallContext context)
    {
        var accountId = RequiredGuid(request.AccountId, "account_id");
        var response = await ExecuteAsync(() => mediator.Send(
            new ExportAdminCustomerAccountQuery(accountId, request.Format),
            context.CancellationToken));
        LogGlobalAccountExported(logger, Actor(context), accountId);
        return response;
    }

    public override async Task<Empty> DeleteCustomerAccount(
        DeleteCustomerAccountRequest request,
        ServerCallContext context)
    {
        var accountId = RequiredGuid(request.AccountId, "account_id");
        var reasonCode = RequireReasonCode(request.Reason);
        await ExecuteAsync(() => mediator.Send(
            new DeleteAdminCustomerAccountCommand(accountId),
            context.CancellationToken));
        LogGlobalAccountDeleted(logger, Actor(context), accountId, reasonCode);
        return new Empty();
    }

    private static Guid RequiredGuid(GuidValue? value, string fieldName)
    {
        var result = value?.ToGuid() ?? Guid.Empty;
        return result != Guid.Empty
            ? result
            : throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                $"{fieldName} must be a non-empty GUID."));
    }

    private static string RequireReasonCode(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "An audit reason is required."));
        }

        var normalized = reason.Trim().ToLowerInvariant();
        if (normalized.Length > 64 ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.'))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Audit reason must be a 1-64 character machine-readable code."));
        }

        return normalized;
    }

    private static string Actor(ServerCallContext context)
        => context.GetHttpContext().User.FindFirstValue("sub") ?? "unknown";

    private static async Task<TResponse> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return await action();
        }
        catch (CustomerAccountNotFoundException exception)
        {
            throw new RpcException(new Status(StatusCode.NotFound, exception.Message));
        }
        catch (CustomerAccountConflictException exception)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
    }

    private static async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (CustomerAccountNotFoundException exception)
        {
            throw new RpcException(new Status(StatusCode.NotFound, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Customer account admin list read by {ActorSubject}; returned {ReturnedCount} of {TotalCount}; includeDeleted={IncludeDeleted}")]
    private static partial void LogGlobalAccountsRead(ILogger logger, string actorSubject, int returnedCount, int totalCount, bool includeDeleted);

    [LoggerMessage(Level = LogLevel.Information, Message = "Customer account {AccountId} read by {ActorSubject}; includeDeleted={IncludeDeleted}")]
    private static partial void LogGlobalAccountRead(ILogger logger, string actorSubject, Guid accountId, bool includeDeleted);

    [LoggerMessage(Level = LogLevel.Information, Message = "Memberships for customer account {AccountId} read by {ActorSubject}; storefront={StorefrontId}; returned {ReturnedCount} of {TotalCount}; includeDeleted={IncludeDeleted}")]
    private static partial void LogAccountMembershipsRead(ILogger logger, string actorSubject, Guid accountId, Guid? storefrontId, int returnedCount, int totalCount, bool includeDeleted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Customer account {AccountId} status changed to {Status} by {ActorSubject}; reason={ReasonCode}")]
    private static partial void LogGlobalAccountStatusChanged(ILogger logger, string actorSubject, Guid accountId, string status, string reasonCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Storefront membership {MembershipId} status changed to {Status} by {ActorSubject}; reason={ReasonCode}")]
    private static partial void LogMembershipStatusChanged(ILogger logger, string actorSubject, Guid membershipId, string status, string reasonCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Customer account {AccountId} exported by {ActorSubject}")]
    private static partial void LogGlobalAccountExported(ILogger logger, string actorSubject, Guid accountId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Customer account {AccountId} anonymized by {ActorSubject}; reason={ReasonCode}")]
    private static partial void LogGlobalAccountDeleted(ILogger logger, string actorSubject, Guid accountId, string reasonCode);
}
