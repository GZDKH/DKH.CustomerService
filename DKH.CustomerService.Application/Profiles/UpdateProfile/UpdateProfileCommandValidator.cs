namespace DKH.CustomerService.Application.Profiles.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.StorefrontId)
            .NotEmpty()
            .WithMessage("Storefront ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Telegram User ID is required");

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null)
            .WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null)
            .WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(256)
            .When(x => x.Email is not null)
            .WithMessage("Email must be valid and not exceed 256 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .When(x => x.Phone is not null)
            .WithMessage("Phone must not exceed 32 characters");

        RuleFor(x => x.LanguageCode)
            .MaximumLength(10)
            .When(x => x.LanguageCode is not null)
            .WithMessage("Language code must not exceed 10 characters");
    }
}
