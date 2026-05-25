using DKH.Platform.Authorization.ResourceAccess;
using DKH.Platform.Authorization.ResourceAccess.Contracts.V1;
using DKH.Platform.Authorization.ResourceAccess.Exceptions;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.Identity;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grant = DKH.Platform.Authorization.ResourceAccess.Models.Grant;
using GrantCommand = DKH.Platform.Authorization.ResourceAccess.Models.GrantCommand;
using ListAllGrantsQuery = DKH.Platform.Authorization.ResourceAccess.Models.ListAllGrantsQuery;
using ListAllGrantsResult = DKH.Platform.Authorization.ResourceAccess.Models.ListAllGrantsResult;
using ProtoGrant = DKH.Platform.Authorization.ResourceAccess.Contracts.V1.Grant;

namespace DKH.CustomerService.Api.Grpc.Services;

/// <summary>
/// gRPC server-side implementation of ResourceAccessGrantsService for CustomerService.
/// Restricts resource_type to "customer" — other resource types are managed by their own services.
/// </summary>
public sealed class CustomerGrantsGrpcService(
    IResourceAccessGrantsService grantsService,
    IResourceAccessChecker checker,
    IPlatformCurrentUser currentUser)
    : ResourceAccessGrantsService.ResourceAccessGrantsServiceBase
{
    private const string AllowedResourceType = "customer";

    /// <inheritdoc/>
    public override async Task<GrantAccessResponse> GrantAccess(GrantAccessRequest request, ServerCallContext context)
    {
        ValidateResourceType(request.ResourceType);
        ValidateRoleGrantAllowed(request.SubjectType);

        try
        {
            var grant = await grantsService.GrantAsync(ToGrantCommand(request), context.CancellationToken);
            return new GrantAccessResponse { Grant = ToProto(grant) };
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    /// <inheritdoc/>
    public override async Task<RevokeAccessResponse> RevokeAccess(RevokeAccessRequest request, ServerCallContext context)
    {
        try
        {
            await grantsService.RevokeAsync(request.GrantId.ToGuid(), context.CancellationToken);
            return new RevokeAccessResponse();
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    /// <inheritdoc/>
    public override async Task<ListGrantsResponse> ListGrants(ListGrantsRequest request, ServerCallContext context)
    {
        ValidateResourceType(request.ResourceType);
        try
        {
            var grants = await grantsService.ListAsync(
                request.ResourceType, request.ResourceId.ToGuid(),
                request.IncludeExpired, context.CancellationToken);
            var response = new ListGrantsResponse();
            response.Grants.AddRange(grants.Select(ToProto));
            return response;
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    /// <inheritdoc/>
    public override async Task<ListAllGrantsResponse> ListAllGrants(ListAllGrantsRequest request, ServerCallContext context)
    {
        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            ValidateResourceType(request.ResourceType);
        }

        try
        {
            var result = await grantsService.ListAllAsync(ToListAllGrantsQuery(request), context.CancellationToken);
            return ToListAllGrantsResponse(result);
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (NotSupportedException ex)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ex.Message));
        }
    }

    /// <inheritdoc/>
    public override async Task<CheckAccessResponse> CheckAccess(CheckAccessRequest request, ServerCallContext context)
    {
        var allowed = await checker.CheckAsync(
            request.ResourceType,
            request.ResourceId.ToGuid(),
            (ResourceAccessPermissions)request.Permission,
            null,
            context.CancellationToken);
        return new CheckAccessResponse { Allowed = allowed };
    }

    /// <inheritdoc/>
    public override async Task<BulkGrantAccessResponse> BulkGrantAccess(BulkGrantAccessRequest request, ServerCallContext context)
    {
        var commands = request.Grants.Select(r =>
        {
            ValidateResourceType(r.ResourceType);
            ValidateRoleGrantAllowed(r.SubjectType);
            return ToGrantCommand(r);
        }).ToList();

        try
        {
            var grants = await grantsService.BulkGrantAsync(commands, context.CancellationToken);
            var response = new BulkGrantAccessResponse();
            response.Grants.AddRange(grants.Select(ToProto));
            return response;
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    /// <inheritdoc/>
    public override async Task<BulkRevokeAccessResponse> BulkRevokeAccess(BulkRevokeAccessRequest request, ServerCallContext context)
    {
        try
        {
            await grantsService.BulkRevokeAsync(
                [.. request.GrantIds.Select(g => g.ToGuid())],
                context.CancellationToken);
            return new BulkRevokeAccessResponse();
        }
        catch (ResourceAccessDeniedException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (resourceType != AllowedResourceType)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                $"Resource type '{resourceType}' is not managed by CustomerService. Only '{AllowedResourceType}' is allowed."));
        }
    }

    private void ValidateRoleGrantAllowed(SubjectType subjectType)
    {
        if (subjectType == SubjectType.Role && !currentUser.IsInRole(ResourceAccessConstants.SuperAdminRole))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "Role-based grants can only be created by super-admin."));
        }
    }

    private static ListAllGrantsQuery ToListAllGrantsQuery(ListAllGrantsRequest r) => new()
    {
        ResourceType = string.IsNullOrWhiteSpace(r.ResourceType) ? null : r.ResourceType,
        SubjectType = r.SubjectType == SubjectType.Unspecified ? null : (ResourceAccessSubjectType)r.SubjectType,
        SubjectId = string.IsNullOrWhiteSpace(r.SubjectId) ? null : r.SubjectId,
        IncludeExpired = r.IncludeExpired,
        Page = r.Page,
        PageSize = r.PageSize,
    };

    private static ListAllGrantsResponse ToListAllGrantsResponse(ListAllGrantsResult result)
    {
        var response = new ListAllGrantsResponse
        {
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        };
        response.Grants.AddRange(result.Grants.Select(ToProto));
        return response;
    }

    private static GrantCommand ToGrantCommand(GrantAccessRequest r) => new()
    {
        ResourceType = r.ResourceType,
        ResourceId = r.ResourceId.ToGuid(),
        SubjectType = (ResourceAccessSubjectType)r.SubjectType,
        SubjectId = r.SubjectId,
        Permissions = (ResourceAccessPermissions)r.Permissions,
        ExpiresAt = r.ExpiresAt?.ToDateTime(),
        GrantReason = string.IsNullOrEmpty(r.GrantReason) ? null : r.GrantReason,
    };

    private static ProtoGrant ToProto(Grant grant) => new()
    {
        Id = GuidValue.FromGuid(grant.Id),
        ResourceType = grant.ResourceType,
        ResourceId = GuidValue.FromGuid(grant.ResourceId),
        SubjectType = (SubjectType)grant.SubjectType,
        SubjectId = grant.SubjectId,
        Permissions = (uint)grant.Permissions,
        ExpiresAt = grant.ExpiresAt is null ? null : Timestamp.FromDateTime(DateTime.SpecifyKind(grant.ExpiresAt.Value, DateTimeKind.Utc)),
        GrantReason = grant.GrantReason ?? string.Empty,
        GrantedById = grant.GrantedById is null ? null : GuidValue.FromGuid(grant.GrantedById.Value),
        GrantedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(grant.GrantedAt, DateTimeKind.Utc)),
    };
}
