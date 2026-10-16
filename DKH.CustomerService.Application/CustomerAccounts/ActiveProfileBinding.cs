namespace DKH.CustomerService.Application.CustomerAccounts;

/// <summary>A current self-only observation, not a versioned authority or permission.</summary>
public sealed record ActiveProfileBinding(Guid CustomerProfileId, Guid StorefrontId);
