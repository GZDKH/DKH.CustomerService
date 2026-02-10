namespace DKH.CustomerService.Application.Wishlists.AddToWishlist;

public class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.StorefrontId)
            .NotEmpty()
            .WithMessage("Storefront ID is required");

        RuleFor(x => x.TelegramUserId)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Telegram User ID is required");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required");

        RuleFor(x => x.Note)
            .MaximumLength(512)
            .When(x => x.Note is not null)
            .WithMessage("Note must not exceed 512 characters");
    }
}
