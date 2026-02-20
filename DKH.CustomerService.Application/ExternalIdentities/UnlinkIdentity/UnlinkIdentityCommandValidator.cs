namespace DKH.CustomerService.Application.ExternalIdentities.UnlinkIdentity;

public class UnlinkIdentityCommandValidator : AbstractValidator<UnlinkIdentityCommand>
{
    public UnlinkIdentityCommandValidator()
    {
        RuleFor(x => x.StorefrontId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.IdentityId).NotEmpty();
    }
}
