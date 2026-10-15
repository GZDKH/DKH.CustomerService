namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed class DeliveryContactRequestValidator : AbstractValidator<DeliveryContactRequest>
{
    public DeliveryContactRequestValidator()
    {
        RuleFor(request => request.Actor).NotNull();
        RuleFor(request => request.Actor.StorefrontId).NotEmpty();
        RuleFor(request => request.OperationId).NotEmpty();
        RuleFor(request => request.Operation).IsInEnum();
        RuleFor(request => request.Reason).NotNull().MaximumLength(1000);
    }
}
