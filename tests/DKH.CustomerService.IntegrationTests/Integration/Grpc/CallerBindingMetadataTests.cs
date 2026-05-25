using System.Reflection;
using DKH.CustomerService.Api.Services;
using DKH.Platform.Authentication.Keycloak.Backend;
using FluentAssertions;

namespace DKH.CustomerService.IntegrationTests.Integration.Grpc;

[Trait("Category", "Integration")]
public sealed class CallerBindingMetadataTests
{
    [Theory]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.GetOrCreateProfile))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.UpdateProfile))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.DeleteProfile))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.ListAddresses))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.GetAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.CreateAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.UpdateAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.DeleteAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.SetDefaultAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.GetDefaultAddress))]
    [InlineData(typeof(CustomerPreferencesGrpcService), nameof(CustomerPreferencesGrpcService.GetPreferences))]
    [InlineData(typeof(CustomerPreferencesGrpcService), nameof(CustomerPreferencesGrpcService.UpdatePreferences))]
    [InlineData(typeof(CustomerPreferencesGrpcService), nameof(CustomerPreferencesGrpcService.UpdateNotificationChannels))]
    [InlineData(typeof(CustomerPreferencesGrpcService), nameof(CustomerPreferencesGrpcService.UpdateNotificationTypes))]
    [InlineData(typeof(ContactVerificationGrpcService), nameof(ContactVerificationGrpcService.InitiateEmailVerification))]
    [InlineData(typeof(ContactVerificationGrpcService), nameof(ContactVerificationGrpcService.VerifyEmail))]
    [InlineData(typeof(ContactVerificationGrpcService), nameof(ContactVerificationGrpcService.InitiatePhoneVerification))]
    [InlineData(typeof(ContactVerificationGrpcService), nameof(ContactVerificationGrpcService.VerifyPhone))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.GetWishlist))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.AddToWishlist))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.RemoveFromWishlist))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.CheckProductInWishlist))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.ClearWishlist))]
    [InlineData(typeof(WishlistGrpcService), nameof(WishlistGrpcService.GetWishlistCount))]
    public void UserScopedMethods_RequireCallerUserIdBinding(Type serviceType, string methodName)
    {
        var attr = GetBinding(serviceType, methodName);

        attr.PropertyName.Should().Be("UserId");
        attr.ClaimType.Should().Be("sub");
        attr.Path.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.GetProfile))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.CreateCustomer))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.UpdateCustomer))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.ExportCustomerData))]
    [InlineData(typeof(CustomerManagementGrpcService), nameof(CustomerManagementGrpcService.DeleteCustomerData))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.SearchCustomers))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.ListCustomers))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.GetCustomerStats))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.BlockCustomer))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.UnblockCustomer))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.SuspendCustomer))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.RestoreCustomerProfile))]
    [InlineData(typeof(CustomerCrudGrpcService), nameof(CustomerCrudGrpcService.PermanentlyDeleteCustomerProfile))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.RestoreAddress))]
    [InlineData(typeof(CustomerAddressGrpcService), nameof(CustomerAddressGrpcService.PermanentlyDeleteAddress))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.LinkIdentity))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.UnlinkIdentity))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.DeleteIdentity))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.ListIdentities))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.FindByExternalIdentity))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.MergeProfiles))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.RestoreIdentity))]
    [InlineData(typeof(IdentityLinkingGrpcService), nameof(IdentityLinkingGrpcService.PermanentlyDeleteIdentity))]
    [InlineData(typeof(ProductCollectionGrpcService), nameof(ProductCollectionGrpcService.AddToCollection))]
    [InlineData(typeof(ProductCollectionGrpcService), nameof(ProductCollectionGrpcService.UpdateCollectionItem))]
    [InlineData(typeof(ProductCollectionGrpcService), nameof(ProductCollectionGrpcService.RemoveFromCollection))]
    [InlineData(typeof(ProductCollectionGrpcService), nameof(ProductCollectionGrpcService.GetCollection))]
    [InlineData(typeof(ProductCollectionGrpcService), nameof(ProductCollectionGrpcService.GetCollectionItem))]
    public void AdminOrCustomerIdScopedMethods_DoNotRequireCallerBinding(Type serviceType, string methodName)
    {
        GetServiceMethod(serviceType, methodName)
            .GetCustomAttribute<RequireCallerMatchesClaimAttribute>()
            .Should().BeNull();
    }

    private static RequireCallerMatchesClaimAttribute GetBinding(Type serviceType, string methodName)
    {
        var attr = GetServiceMethod(serviceType, methodName)
            .GetCustomAttribute<RequireCallerMatchesClaimAttribute>();

        attr.Should().NotBeNull();
        return attr!;
    }

    private static MethodInfo GetServiceMethod(Type serviceType, string methodName)
        => serviceType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            ?? throw new MissingMethodException(serviceType.FullName, methodName);
}
