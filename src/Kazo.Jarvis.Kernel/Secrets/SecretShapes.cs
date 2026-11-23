using System.Text.RegularExpressions;

namespace Kazo.Jarvis.Kernel.Secrets;

/// <summary>
/// The shapes a secret has (ADR 0019, section 3): a fact that looks like a key, a token, a password or a card
/// number is rejected by code, whatever the model was told. It errs on the side of rejecting.
/// </summary>
public static partial class SecretShapes
{
    /// <summary>Whether <paramref name="text"/> holds something shaped like a secret.</summary>
    /// <param name="text">A fact.</param>
    /// <returns><see langword="true"/> when it must not be kept.</returns>
    public static bool LooksLikeSecret(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return KnownKey().IsMatch(text)
            || PasswordSaid().IsMatch(text)
            || LongToken().Matches(text).Any(match => match.Value.Any(char.IsAsciiDigit) && match.Value.Any(char.IsAsciiLetter))
            || CardCandidate().Matches(text).Any(match => IsCardNumber(match.Value));
    }

    private static bool IsCardNumber(string candidate)
    {
        var digits = candidate.Where(char.IsAsciiDigit).Select(digit => digit - '0').ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        // Luhn: a phone number or a date rarely passes, a card number always does.
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            var digit = digits[digits.Length - 1 - index];
            if (index % 2 == 1)
            {
                digit *= 2;
                digit = digit > 9 ? digit - 9 : digit;
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|xox[abprs]-[A-Za-z0-9\-]{10,}|AIza[0-9A-Za-z_\-]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY|eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.")]
    private static partial Regex KnownKey();

    [GeneratedRegex(@"\b(mot de passe|mdp|password|passcode|passphrase|code pin|pin code|code nip|nip|cvv|cvc)\b[^.]{0,30}?(\best\b|\bis\b|:|=)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordSaid();

    [GeneratedRegex(@"[A-Za-z0-9+/_=\-]{32,}")]
    private static partial Regex LongToken();

    [GeneratedRegex(@"\b\d(?:[ \-]?\d){12,18}\b")]
    private static partial Regex CardCandidate();
}
