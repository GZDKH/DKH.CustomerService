using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Api.V1;
using DKH.CustomerService.Contracts.Models.V1;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using ContractsService = DKH.CustomerService.Contracts.Api.V1.ContactVerificationService;

namespace DKH.CustomerService.Api.Services;

public class ContactVerificationGrpcService(
    IVerificationService verificationService,
    ICustomerRepository customerRepository,
    IPlatformStorefrontContext storefrontContext)
    : ContractsService.ContactVerificationServiceBase
{
    public override async Task<InitiateEmailVerificationResponse> InitiateEmailVerification(InitiateEmailVerificationRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByTelegramUserIdAsync(storefrontId, request.TelegramUserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var (success, errorMessage, expiresIn) = await verificationService.SendEmailVerificationAsync(
            request.Email,
            profile.Id.ToString(),
            context.CancellationToken);

        return new InitiateEmailVerificationResponse
        {
            Result = new VerificationInitiation
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    public override async Task<VerifyEmailResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByTelegramUserIdAsync(storefrontId, request.TelegramUserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var (success, errorMessage, errorCode) = await verificationService.VerifyEmailCodeAsync(
            request.Email,
            profile.Id.ToString(),
            request.Code,
            context.CancellationToken);

        if (success)
        {
            profile.ContactVerification.VerifyEmail();
            await customerRepository.UpdateAsync(profile, context.CancellationToken);
        }

        return new VerifyEmailResponse
        {
            Result = new VerificationResult
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

    public override async Task<InitiatePhoneVerificationResponse> InitiatePhoneVerification(InitiatePhoneVerificationRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByTelegramUserIdAsync(storefrontId, request.TelegramUserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var (success, errorMessage, expiresIn) = await verificationService.SendPhoneVerificationAsync(
            request.Phone,
            profile.Id.ToString(),
            context.CancellationToken);

        return new InitiatePhoneVerificationResponse
        {
            Result = new VerificationInitiation
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    public override async Task<VerifyPhoneResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByTelegramUserIdAsync(storefrontId, request.TelegramUserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));

        var (success, errorMessage, errorCode) = await verificationService.VerifyPhoneCodeAsync(
            request.Phone,
            profile.Id.ToString(),
            request.Code,
            context.CancellationToken);

        if (success)
        {
            profile.ContactVerification.VerifyPhone();
            await customerRepository.UpdateAsync(profile, context.CancellationToken);
        }

        return new VerifyPhoneResponse
        {
            Result = new VerificationResult
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

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
