using DKH.Platform.Exceptions;

namespace DKH.CustomerService.Api.Grpc.Helpers;

internal static class GrpcValidationHelper
{
    public static PlatformValidationException CreateValidationException(string field, string message)
        => new([new PlatformValidationFailure(field, message)]);
}
