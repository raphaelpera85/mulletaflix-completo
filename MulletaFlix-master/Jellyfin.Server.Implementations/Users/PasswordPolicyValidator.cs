using System;
using System.Text.RegularExpressions;

namespace MulletaFlix.Server.Implementations.Users;

/// <summary>
/// Validates passwords against MulletaFlix security policy.
/// Default policy: minimum 8 characters, at least 1 digit, 1 uppercase, 1 lowercase.
/// </summary>
public static class PasswordPolicyValidator
{
    /// <summary>
    /// Minimum password length.
    /// </summary>
    private const int MinimumLength = 8;

    /// <summary>
    /// Validates a password against the security policy.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>True if the password meets the policy; false otherwise.</returns>
    public static bool ValidatePassword(string? password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Password must not be empty.";
            return false;
        }

        if (password.Length < MinimumLength)
        {
            errorMessage = $"Password must be at least {MinimumLength} characters long.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            errorMessage = "Password must contain at least one digit.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            errorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            errorMessage = "Password must contain at least one lowercase letter.";
            return false;
        }

        return true;
    }
}
