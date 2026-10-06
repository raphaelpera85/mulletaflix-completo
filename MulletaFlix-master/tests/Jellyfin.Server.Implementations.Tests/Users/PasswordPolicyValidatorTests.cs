using Xunit;
using MulletaFlix.Server.Implementations.Users;

namespace Jellyfin.Server.Implementations.Tests.Users;

public sealed class PasswordPolicyValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidatePassword_EmptyOrWhitespace_ReturnsFalse(string? password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Contains("empty", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("abcdefg")]
    public void ValidatePassword_TooShort_ReturnsFalse(string password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Contains("8 characters", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NoDigits")]     // No digits
    [InlineData("NODIGITS")]
    public void ValidatePassword_NoDigit_ReturnsFalse(string password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Contains("digit", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("nouppercase1")] // Has digit and lowercase, no uppercase
    [InlineData("nouppercase99")] // Has digits and lowercase, no uppercase
    public void ValidatePassword_NoUppercase_ReturnsFalse(string password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Contains("uppercase", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NOLOWERCASE1")] // Has digit and uppercase, no lowercase
    [InlineData("NOLOWERCASE99")] // Has digits and uppercase, no lowercase
    public void ValidatePassword_NoLowercase_ReturnsFalse(string password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Contains("lowercase", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ValidPass1")]
    [InlineData("SecureP@ssw0rd")]
    [InlineData("MyPassword123")]
    [InlineData("TestSecure99")]
    public void ValidatePassword_ValidPassword_ReturnsTrue(string password)
    {
        var result = PasswordPolicyValidator.ValidatePassword(password, out var error);

        Assert.True(result);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidatePassword_WeakAdminPassword_ReturnsFalse()
    {
        // Simulate common weak passwords someone might try
        var weakPasswords = new[]
        {
            "admin",
            "password",
            "12345678",
            "User123",
            "Pass123",
        };

        foreach (var weak in weakPasswords)
        {
            var result = PasswordPolicyValidator.ValidatePassword(weak, out _);
            Assert.False(result, $"Password '{weak}' should have been rejected by policy.");
        }
    }
}
