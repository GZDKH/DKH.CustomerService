using DKH.CustomerService.Application.Abstractions;
using DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1;
using DKH.CustomerService.Contracts.Customer.Models.ContactVerification.v1;
using DKH.Platform.Authentication.Keycloak.Backend;
using DKH.Platform.Grpc.Common.Types;
using DKH.Platform.MultiTenancy;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using ContractsService = DKH.CustomerService.Contracts.Customer.Api.ContactVerification.v1.ContactVerificationService;

namespace DKH.CustomerService.Api.Services;

[Authorize(Policy = CustomerServiceAuthorizationPolicies.CustomerSelfAccess)]
public class ContactVerificationGrpcService(
    IVerificationService verificationService,
    ICustomerRepository customerRepository,
    IPlatformStorefrontContext storefrontContext)
    : ContractsService.ContactVerificationServiceBase
{
    private const string ContactMismatchErrorCode = "contact_mismatch";
    private const string EmailMismatchErrorMessage = "Email must match the current profile email.";
    private const string PhoneMismatchErrorMessage = "Phone must match the current profile phone.";

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<InitiateEmailVerificationResponse> InitiateEmailVerification(InitiateEmailVerificationRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, request.UserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var email = MatchContact(request.Email, profile.Email, StringComparison.OrdinalIgnoreCase);

        if (email is null)
        {
            return new InitiateEmailVerificationResponse
            {
                Result = new VerificationInitiationModel
                {
                    Success = false,
                    ErrorMessage = EmailMismatchErrorMessage,
                    ExpiresInSeconds = 0,
                },
            };
        }

        var (success, errorMessage, expiresIn) = await verificationService.SendEmailVerificationAsync(
            email,
            profile.Id.ToString(),
            context.CancellationToken);

        return new InitiateEmailVerificationResponse
        {
            Result = new VerificationInitiationModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<VerifyEmailResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, request.UserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var email = MatchContact(request.Email, profile.Email, StringComparison.OrdinalIgnoreCase);

        if (email is null)
        {
            return new VerifyEmailResponse
            {
                Result = new VerificationResultModel
                {
                    Success = false,
                    ErrorMessage = EmailMismatchErrorMessage,
                    ErrorCode = ContactMismatchErrorCode,
                },
            };
        }

        var (success, errorMessage, errorCode) = await verificationService.VerifyEmailCodeAsync(
            email,
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
            Result = new VerificationResultModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<InitiatePhoneVerificationResponse> InitiatePhoneVerification(InitiatePhoneVerificationRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, request.UserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var phone = MatchContact(request.Phone, profile.Phone, StringComparison.Ordinal);

        if (phone is null)
        {
            return new InitiatePhoneVerificationResponse
            {
                Result = new VerificationInitiationModel
                {
                    Success = false,
                    ErrorMessage = PhoneMismatchErrorMessage,
                    ExpiresInSeconds = 0,
                },
            };
        }

        var (success, errorMessage, expiresIn) = await verificationService.SendPhoneVerificationAsync(
            phone,
            profile.Id.ToString(),
            context.CancellationToken);

        return new InitiatePhoneVerificationResponse
        {
            Result = new VerificationInitiationModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ExpiresInSeconds = expiresIn,
            },
        };
    }

    [RequireCallerMatchesClaim("UserId")]
    public override async Task<VerifyPhoneResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
    {
        var storefrontId = ResolveStorefrontId(request.StorefrontId);
        var profile = await customerRepository.GetByUserIdAsync(storefrontId, request.UserId, context.CancellationToken) ?? throw new RpcException(new Status(StatusCode.NotFound, "Customer profile not found"));
        var phone = MatchContact(request.Phone, profile.Phone, StringComparison.Ordinal);

        if (phone is null)
        {
            return new VerifyPhoneResponse
            {
                Result = new VerificationResultModel
                {
                    Success = false,
                    ErrorMessage = PhoneMismatchErrorMessage,
                    ErrorCode = ContactMismatchErrorCode,
                },
            };
        }

        var (success, errorMessage, errorCode) = await verificationService.VerifyPhoneCodeAsync(
            phone,
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
            Result = new VerificationResultModel
            {
                Success = success,
                ErrorMessage = errorMessage ?? string.Empty,
                ErrorCode = errorCode ?? string.Empty,
            },
        };
    }

    private static string? MatchContact(string requested, string? current, StringComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(current))
        {
            return null;
        }

        return string.Equals(requested.Trim(), current.Trim(), comparison) ? current : null;
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
