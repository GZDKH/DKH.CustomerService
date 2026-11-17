using DKH.CustomerService.Application.ContactVerification;
using DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1;
using DKH.Platform.Authentication.Keycloak.Backend;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1.ContactVerificationService;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerSelfAccess)]
public class ContactVerificationGrpcService(IContactVerificationUseCases verification, IPlatformStorefrontContext storefrontContext)
    : ContractsService.ContactVerificationServiceBase
{
    [RequireCallerMatchesClaim("UserId")]
    public override Task<InitiateEmailVerificationResponse> InitiateEmailVerification(InitiateEmailVerificationRequest request, ServerCallContext context)
        => verification.InitiateEmailVerificationAsync(
            ResolveStorefrontId(request.StorefrontId), request.UserId, request.Email, context.CancellationToken);

    [RequireCallerMatchesClaim("UserId")]
    public override Task<VerifyEmailResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
        => verification.VerifyEmailAsync(
            ResolveStorefrontId(request.StorefrontId), request.UserId, request.Email, request.Code, context.CancellationToken);

    [RequireCallerMatchesClaim("UserId")]
    public override Task<InitiatePhoneVerificationResponse> InitiatePhoneVerification(InitiatePhoneVerificationRequest request, ServerCallContext context)
        => verification.InitiatePhoneVerificationAsync(
            ResolveStorefrontId(request.StorefrontId), request.UserId, request.Phone, context.CancellationToken);

    [RequireCallerMatchesClaim("UserId")]
    public override Task<VerifyPhoneResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
        => verification.VerifyPhoneAsync(
            ResolveStorefrontId(request.StorefrontId), request.UserId, request.Phone, request.Code, context.CancellationToken);

    private Guid ResolveStorefrontId(GuidValue? requestStorefrontId)
    {
        if (requestStorefrontId is not null)
        {
            return requestStorefrontId.ToGuid();
        }

        return storefrontContext.StorefrontId
               ?? throw new RpcException(new Status(StatusCode.InvalidArgument, "Storefront ID is required"));
    }
}
