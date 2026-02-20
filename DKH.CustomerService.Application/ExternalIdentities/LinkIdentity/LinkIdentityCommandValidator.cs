namespace DKH.CustomerService.Application.ExternalIdentities.LinkIdentity;

public class LinkIdentityCommandValidator : AbstractValidator<LinkIdentityCommand>
{
    public LinkIdentityCommandValidator()
    {
        RuleFor(x => x.StorefrontId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProviderUserId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.DisplayName).MaximumLength(200);
    }
}
