using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.DeliveryContacts;
using DKH.CustomerService.Contracts.Customer.Api.DeliveryContact.v2;
using DKH.CustomerService.Domain.Entities.DeliveryContact;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DKH.CustomerService.Api.Services;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = CustomerServiceAuthorizationPolicies.CustomerSelfAccess)]
public sealed class DeliveryContactGrpcService(IMediator mediator, IPlatformStorefrontContext storefrontContext,
    IOptions<PlatformStorefrontContextOptions> storefrontOptions, IConfiguration configuration)
    : DeliveryContactService.DeliveryContactServiceBase
{
    public override Task<DeliveryContactRevisionModel> SealDeliveryContact(SealDeliveryContactRequest request, ServerCallContext context)
        => ExecuteAsync(async () =>
        {
            var result = (DeliveryContactView)await SendAsync(context, request.Command, DeliveryContactOperation.Seal,
                addressId: Id(request.AddressId));
            return new DeliveryContactRevisionModel { Contact = Map(result.Authority), OwnerProfileBinding = GuidValue(result.OwnerProfileId) };
        });

    public override Task<LiabilityContactVerificationModel> ResolveLiabilityContact(ResolveLiabilityContactRequest request, ServerCallContext context)
        => LiabilityAsync(request, context, false);

    public override Task<LiabilityContactVerificationModel> RevalidateLiabilityContact(ResolveLiabilityContactRequest request, ServerCallContext context)
        => LiabilityAsync(request, context, true);

    private Task<LiabilityContactVerificationModel> LiabilityAsync(ResolveLiabilityContactRequest request, ServerCallContext context, bool revalidate)
        => ExecuteAsync(async () =>
        {
            var contact = Ref(request.Contact);
            if (!revalidate && request.VerificationId is not null)
            {
                throw new ArgumentException("Unexpected verification selector.");
            }

            var result = (DeliveryContactAccessView)await SendAsync(context, request.Command, DeliveryContactOperation.ResolveLiability,
                contact: contact, planId: Id(request.IntendedPlanRevisionId), legId: Id(request.LegId),
                accessId: revalidate ? Id(request.VerificationId) : null);
            return new LiabilityContactVerificationModel { Contact = Map(contact), VerificationId = GuidValue(result.Id), VerificationExpiresAt = Timestamp.FromDateTimeOffset(result.ExpiresAtUtc) };
        });

    public override Task<DeliveryContactConsentModel> RecordDeliveryContactConsent(RecordDeliveryContactConsentRequest request, ServerCallContext context)
        => ExecuteAsync(async () => Map((DeliveryContactConsentView)await SendAsync(context, request.Command, DeliveryContactOperation.Consent,
            contact: Ref(request.Contact), scope: Scope(request.Scope), until: request.ValidUntil?.ToDateTimeOffset() ?? throw new ArgumentException("Consent expiry is required."))));

    public override Task<DeliveryContactConsentModel> RevokeDeliveryContactConsent(RevokeDeliveryContactConsentRequest request, ServerCallContext context)
        => ExecuteAsync(async () => Map((DeliveryContactConsentView)await SendAsync(context, request.Command, DeliveryContactOperation.Revoke,
            consentId: Id(request.ConsentId))));

    public override Task<RecipientContactAccessModel> ResolveRecipientContact(ResolveRecipientContactRequest request, ServerCallContext context)
        => RecipientAsync(request, context, false);

    public override Task<RecipientContactAccessModel> RevalidateRecipientContact(ResolveRecipientContactRequest request, ServerCallContext context)
        => RecipientAsync(request, context, true);

    private Task<RecipientContactAccessModel> RecipientAsync(ResolveRecipientContactRequest request, ServerCallContext context, bool revalidate)
        => ExecuteAsync(async () =>
        {
            if (!revalidate && request.ContactAccessId is not null)
            {
                throw new ArgumentException("Unexpected access selector.");
            }

            var result = (DeliveryRecipientContactView)await SendAsync(context, request.Command, DeliveryContactOperation.ResolveRecipient,
                contact: Ref(request.Contact), consentId: Id(request.ConsentId), scope: Scope(request.Scope),
                accessId: revalidate ? Id(request.ContactAccessId) : null);
            var address = new DeliveryPostalAddressModel { Country = result.Country, City = result.City };
            if (result.Province is not null)
            {
                address.Province = result.Province;
            }

            if (result.Street is not null)
            {
                address.Street = result.Street;
            }

            if (result.Building is not null)
            {
                address.Building = result.Building;
            }

            if (result.Apartment is not null)
            {
                address.Apartment = result.Apartment;
            }

            if (result.PostalCode is not null)
            {
                address.PostalCode = result.PostalCode;
            }

            var model = new RecipientContactAccessModel
            {
                ContactAccessId = GuidValue(result.Access.Id),
                RecipientName = result.RecipientName,
                PostalAddress = address,
                ExpiresAt = Timestamp.FromDateTimeOffset(result.Access.ExpiresAtUtc),
                ScopeRevision = result.Access.ScopeRevision
            };
            if (result.Phone is not null)
            {
                model.Phone = result.Phone;
            }

            return model;
        });

    private Task<object> SendAsync(ServerCallContext context, DeliveryContactCommandContextModel? command, DeliveryContactOperation operation,
        Guid? addressId = null, DeliveryContactAuthority? contact = null, DeliveryContactScope? scope = null,
        Guid? consentId = null, DateTimeOffset? until = null, Guid? planId = null, Guid? legId = null, Guid? accessId = null)
    {
        if (command is null || command.ExpectedRevision < 0 || command.Reason.Length > 1000 ||
            operation == DeliveryContactOperation.Revoke && command.ExpectedRevision <= 0 ||
            operation != DeliveryContactOperation.Revoke && command.ExpectedRevision != 0)
        {
            throw new ArgumentException("A valid operation and expected revision are required.");
        }

        var actor = new DeliveryContactActor(CustomerJournalPrincipal.Require(context.GetHttpContext(), configuration), Storefront(context));
        return mediator.Send(new DeliveryContactRequest(actor, operation, Id(command.OperationId), addressId, contact, scope, consentId,
            operation == DeliveryContactOperation.Revoke ? command.ExpectedRevision : null, until, planId, legId, accessId, command.Reason), context.CancellationToken);
    }

    private Guid Storefront(ServerCallContext context)
    {
        var values = context.RequestHeaders.Where(row => row.Key.Equals(storefrontOptions.Value.HeaderName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (values.Length > 1 || values.Length == 1 && (!Guid.TryParse(values[0].Value, out var parsed) || parsed == Guid.Empty ||
            storefrontContext.StorefrontId is { } resolved && resolved != parsed))
        {
            throw new ArgumentException("A single current storefront is required.");
        }

        if (storefrontContext.StorefrontId is { } id && id != Guid.Empty)
        {
            return id;
        }

        return values.Length == 1 && Guid.TryParse(values[0].Value, out var selected) && selected != Guid.Empty
            ? selected : throw new ArgumentException("A current storefront is required.");
    }

    private static DeliveryContactScope Scope(DeliveryContactScopeModel? model)
    {
        if (model is null || model.Purpose != Contracts.Customer.Api.DeliveryContact.v2.DeliveryContactPurpose.LocalShipment)
        {
            throw new ArgumentException("Local shipment purpose is required.");
        }

        var scope = new DeliveryContactScope(Id(model.GroupId), model.AllocationPublicRef, Id(model.PlanRevisionId), model.ScopeRevision,
            Id(model.ChoiceRevisionId), Ref(model.DutyAcceptance));
        scope.Validate();
        return scope;
    }

    private static DeliveryContactAuthority Ref(DeliveryContactAuthorityRefModel? model)
    {
        if (model is null)
        {
            throw new ArgumentException("An exact authority is required.");
        }

        var authority = new DeliveryContactAuthority(model.Authority, Id(model.ResourceId), model.Revision, model.Digest);
        authority.Validate();
        return authority;
    }

    private static Guid Id(GuidValue? value) => value is not null && Guid.TryParse(value.Value, out var id) && id != Guid.Empty
        ? id : throw new ArgumentException("A nonempty UUID is required.");
    private static GuidValue GuidValue(Guid value) => new() { Value = value.ToString("D") };
    private static DeliveryContactAuthorityRefModel Map(DeliveryContactAuthority value) => new()
    { Authority = value.Authority, ResourceId = GuidValue(value.ResourceId), Revision = value.Revision, Digest = value.Digest };
    private static DeliveryContactConsentModel Map(DeliveryContactConsentView value) => new()
    { ConsentId = GuidValue(value.Id), Revision = value.Revision, ValidUntil = Timestamp.FromDateTimeOffset(value.ValidUntilUtc), Revoked = value.Revoked };

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DeliveryContactFailureException exception)
        {
            throw new RpcException(new Status(exception.Code == "AUTHORITY_UNAVAILABLE" ? StatusCode.Unavailable :
                exception.Code == "OPERATION_CONFLICT" ? StatusCode.AlreadyExists : StatusCode.NotFound, exception.Code));
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "INVALID_CONTACT_REQUEST"));
        }
        catch (InvalidOperationException)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "CONTACT_STATE_CHANGED"));
        }
    }
}
