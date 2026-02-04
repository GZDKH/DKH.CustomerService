using DKH.CustomerService.Domain.Entities.CustomerProfile;
using FluentAssertions;
using Xunit;

namespace DKH.CustomerService.Application.Tests;

public class CustomerProfileTests
{
    [Fact]
    public void Create_WithValidData_CreatesProfile()
    {
        // Arrange
        var storefrontId = Guid.NewGuid();
        var telegramUserId = "123456789";
        var firstName = "John";
        var lastName = "Doe";
        var username = "johndoe";

        // Act
        var profile = CustomerProfileEntity.Create(
            storefrontId,
            telegramUserId,
            firstName,
            lastName,
            username);

        // Assert
        profile.Should().NotBeNull();
        profile.Id.Should().NotBeEmpty();
        profile.StorefrontId.Should().Be(storefrontId);
        profile.TelegramUserId.Should().Be(telegramUserId);
        profile.FirstName.Should().Be(firstName);
        profile.LastName.Should().Be(lastName);
        profile.Username.Should().Be(username);
        profile.AccountStatus.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_WithValidData_UpdatesProfile()
    {
        // Arrange
        var profile = CustomerProfileEntity.Create(
            Guid.NewGuid(),
            "123456789",
            "John");

        // Act
        profile.Update(
            firstName: "Jane",
            lastName: "Smith",
            email: "jane@example.com",
            phone: "+1234567890");

        // Assert
        profile.FirstName.Should().Be("Jane");
        profile.LastName.Should().Be("Smith");
        profile.Email.Should().Be("jane@example.com");
        profile.Phone.Should().Be("+1234567890");
    }

    [Fact]
    public void Block_SetsBlockedStatus()
    {
        // Arrange
        var profile = CustomerProfileEntity.Create(
            Guid.NewGuid(),
            "123456789",
            "John");

        // Act
        profile.AccountStatus.Block("Violation", "admin@example.com");

        // Assert
        profile.AccountStatus.IsBlocked.Should().BeTrue();
        profile.AccountStatus.IsActive.Should().BeFalse();
        profile.AccountStatus.BlockReason.Should().Be("Violation");
        profile.AccountStatus.BlockedBy.Should().Be("admin@example.com");
    }

    [Fact]
    public void Unblock_ResetsToActiveStatus()
    {
        // Arrange
        var profile = CustomerProfileEntity.Create(
            Guid.NewGuid(),
            "123456789",
            "John");
        profile.AccountStatus.Block("Violation", "admin@example.com");

        // Act
        profile.AccountStatus.Unblock();

        // Assert
        profile.AccountStatus.IsActive.Should().BeTrue();
        profile.AccountStatus.IsBlocked.Should().BeFalse();
        profile.AccountStatus.BlockReason.Should().BeNull();
    }
}
