using DKH.Platform.Authorization.ResourceAccess;
using DKH.Platform.Authorization.ResourceAccess.Domain;

namespace DKH.CustomerService.Domain.Authorization;

public sealed class CustomerAccessGrantEntity : ResourceAccessGrantEntity<Guid>
{
    private static readonly HashSet<string> AllowedResourceTypes = new(StringComparer.Ordinal) { "customer", "storefront" };

    private CustomerAccessGrantEntity() { }

    public CustomerAccessGrantEntity(
        Guid id, string resourceType, Guid resourceId,
        ResourceAccessSubjectType subjectType, string subjectId,
        ResourceAccessPermissions permissions, DateTime? expiresAt, string? grantReason)
        : base(id, resourceType, resourceId, subjectType, subjectId, permissions, expiresAt, grantReason)
    {
        if (!AllowedResourceTypes.Contains(resourceType))
        {
            throw new ArgumentException($"Resource type '{resourceType}' is not managed by CustomerService.", nameof(resourceType));
        }
    }
}
