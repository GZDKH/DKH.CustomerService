namespace DKH.CustomerService.Application.Profiles.CreateCustomer;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.StorefrontId)
            .NotEmpty()
            .WithMessage("Storefront ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("User ID is required and must not exceed 64 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("First name is required and must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Last name is required and must not exceed 100 characters");

        RuleFor(x => x.Username)
            .MaximumLength(100)
            .When(x => x.Username is not null)
            .WithMessage("Username must not exceed 100 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .When(x => x.Phone is not null)
            .WithMessage("Phone must not exceed 32 characters");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(256)
            .When(x => x.Email is not null)
            .WithMessage("Email must be valid and not exceed 256 characters");

        RuleFor(x => x.LanguageCode)
            .MaximumLength(10)
            .When(x => x.LanguageCode is not null)
            .WithMessage("Language code must not exceed 10 characters");

        RuleFor(x => x.PhotoUrl)
            .MaximumLength(512)
            .When(x => x.PhotoUrl is not null)
            .WithMessage("Photo URL must not exceed 512 characters");

        RuleFor(x => x.ProviderType)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Provider type is required and must not exceed 50 characters");
    }
}
