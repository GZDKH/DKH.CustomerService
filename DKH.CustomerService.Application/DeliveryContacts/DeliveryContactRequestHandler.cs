using System.Security.Cryptography;
using System.Text.Json;

namespace DKH.CustomerService.Application.DeliveryContacts;

// The existing Platform MediatR/outbox behavior owns the local transaction.
public sealed class DeliveryContactRequestHandler(IDeliveryContactStore store) : IRequestHandler<DeliveryContactRequest, object>
{
    public async Task<object> Handle(DeliveryContactRequest request, CancellationToken cancellationToken)
    {
        if (request.Actor.StorefrontId == Guid.Empty || request.OperationId == Guid.Empty || request.Reason.Length > 1000)
        {
            throw new ArgumentException("A storefront and operation are required.");
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Actor.StorefrontId,
            request.Operation,
            request.AddressId,
            request.Contact,
            request.Scope,
            request.ConsentId,
            request.ExpectedRevision,
            request.ValidUntilUtc,
            request.IntendedPlanRevisionId,
            request.LegId,
            request.Reason,
        })));
        return request.Operation switch
        {
            DeliveryContactOperation.Seal when request.AddressId is { } addressId =>
                await store.SealAsync(request.Actor, addressId, request.OperationId, digest, cancellationToken),
            DeliveryContactOperation.ResolveLiability when request.Contact is { } contact &&
                request.IntendedPlanRevisionId is { } plan && request.LegId is { } leg =>
                await store.ResolveLiabilityAsync(request.Actor, contact, plan, leg, request.OperationId, request.AccessId, digest, cancellationToken),
            DeliveryContactOperation.Consent when request.Contact is { } contact && request.Scope is { } scope && request.ValidUntilUtc is { } until =>
                await store.ConsentAsync(request.Actor, contact, scope, until, request.OperationId, digest, cancellationToken),
            DeliveryContactOperation.Revoke when request.ConsentId is { } consent && request.ExpectedRevision is { } revision =>
                await store.RevokeAsync(request.Actor, consent, revision, request.OperationId, digest, cancellationToken),
            DeliveryContactOperation.ResolveRecipient when request.Contact is { } contact && request.Scope is { } scope && request.ConsentId is { } consent =>
                await store.ResolveRecipientAsync(request.Actor, contact, consent, scope, request.OperationId, request.AccessId, digest, cancellationToken),
            _ => throw new ArgumentException("A complete supported contact operation is required."),
        };
    }
}
