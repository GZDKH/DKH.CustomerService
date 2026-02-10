namespace DKH.CustomerService.Application.Profiles.GetOrCreateProfile;

public class GetOrCreateProfileCommandValidator : AbstractValidator<GetOrCreateProfileCommand>
{
    public GetOrCreateProfileCommandValidator()
    {
        RuleFor(x => x.StorefrontId)
            .NotEmpty()
            .WithMessage("Storefront ID is required");

        RuleFor(x => x.TelegramUserId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Telegram User ID is required and must not exceed 64 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("First name is required and must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null)
            .WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Username)
            .MaximumLength(100)
            .When(x => x.Username is not null)
            .WithMessage("Username must not exceed 100 characters");

        RuleFor(x => x.LanguageCode)
            .MaximumLength(10)
            .When(x => x.LanguageCode is not null)
            .WithMessage("Language code must not exceed 10 characters");
    }
}
