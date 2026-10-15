namespace DKH.CustomerService.Application.DeliveryContacts;

public sealed class DeliveryContactFailureException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
