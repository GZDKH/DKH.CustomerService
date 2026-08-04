namespace DKH.CustomerService.Application.CustomerAccounts;

public sealed class CustomerAccountNotFoundException(string message) : Exception(message);

public sealed class CustomerAccountConflictException(string message) : Exception(message);

public sealed class CustomerAccountAccessException(string message) : Exception(message);
