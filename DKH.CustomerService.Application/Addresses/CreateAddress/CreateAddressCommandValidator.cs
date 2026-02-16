namespace DKH.CustomerService.Application.Addresses.CreateAddress;

public class CreateAddressCommandValidator : AbstractValidator<CreateAddressCommand>
{
    public CreateAddressCommandValidator()
    {
        RuleFor(x => x.StorefrontId)
            .NotEmpty()
            .WithMessage("Storefront ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Telegram User ID is required");

        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Label is required and must not exceed 64 characters");

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Country is required and must not exceed 100 characters");

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("City is required and must not exceed 100 characters");

        RuleFor(x => x.Street)
            .MaximumLength(256)
            .When(x => x.Street is not null)
            .WithMessage("Street must not exceed 256 characters");

        RuleFor(x => x.Building)
            .MaximumLength(32)
            .When(x => x.Building is not null)
            .WithMessage("Building must not exceed 32 characters");

        RuleFor(x => x.Apartment)
            .MaximumLength(32)
            .When(x => x.Apartment is not null)
            .WithMessage("Apartment must not exceed 32 characters");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20)
            .When(x => x.PostalCode is not null)
            .WithMessage("Postal code must not exceed 20 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .When(x => x.Phone is not null)
            .WithMessage("Phone must not exceed 32 characters");
    }
}
