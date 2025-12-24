using System.Text.Json;
using System.Text.RegularExpressions;

namespace DiscordBot.Core.Utilities;

/// <summary>
/// Provides methods for sanitizing sensitive data from log entries.
/// This is a security-critical utility for GDPR/privacy compliance.
/// </summary>
public static partial class LogSanitizer
{
    /// <summary>
    /// Replacement markers for sanitized data.
    /// </summary>
    public static class Markers
    {
        public const string Email = "[EMAIL]";
        public const string Phone = "[PHONE]";
        public const string CreditCard = "[CARD]";
        public const string Token = "[TOKEN]";
        public const string Password = "[PASSWORD]";
        public const string DiscordToken = "[DISCORD_TOKEN]";
        public const string ApiKey = "[API_KEY]";
    }

    // Email pattern: standard email format
    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Phone patterns: various formats including international
    [GeneratedRegex(@"(?:\+?1[-.\s]?)?\(?[0-9]{3}\)?[-.\s]?[0-9]{3}[-.\s]?[0-9]{4}", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    // Credit card patterns: 13-19 digits with optional separators
    [GeneratedRegex(@"\b(?:\d[-\s]?){13,19}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    // Discord tokens: Bot tokens and user tokens (Base64-like with dots)
    [GeneratedRegex(@"[MN][A-Za-z\d]{23,}\.[\w-]{6}\.[\w-]{27,}", RegexOptions.Compiled)]
    private static partial Regex DiscordTokenRegex();

    // Bearer tokens: Authorization header format
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_]+\.?[A-Za-z0-9\-_]*\.?[A-Za-z0-9\-_]*", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    // API keys: Common patterns (alphanumeric with dashes, 20+ chars)
    [GeneratedRegex(@"(?:api[_-]?key|apikey|api_secret|secret[_-]?key)[""']?\s*[:=]\s*[""']?([A-Za-z0-9\-_]{20,})[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyRegex();

    // Password patterns: password fields in various formats
    [GeneratedRegex(@"(?:password|passwd|pwd|secret)[""']?\s*[:=]\s*[""']?([^""'\s,}\]]{1,})[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PasswordRegex();

    // Generic secret/token patterns in key-value format
    [GeneratedRegex(@"(?:token|auth|secret|credential)[""']?\s*[:=]\s*[""']?([A-Za-z0-9\-_\.]{10,})[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenericSecretRegex();

    /// <summary>
    /// Sanitizes a string by redacting sensitive data patterns.
    /// </summary>
    /// <param name="input">The string to sanitize.</param>
    /// <returns>The sanitized string with sensitive data replaced by markers, or null if input was null.</returns>
    public static string? SanitizeString(string? input)
    {
        if (input == null)
        {
            return null;
        }

        if (input.Length == 0)
        {
            return input;
        }

        var result = input;

        // Order matters: more specific patterns first
        result = DiscordTokenRegex().Replace(result, Markers.DiscordToken);
        result = BearerTokenRegex().Replace(result, Markers.Token);
        result = CreditCardRegex().Replace(result, Markers.CreditCard);
        result = EmailRegex().Replace(result, Markers.Email);
        result = PhoneRegex().Replace(result, Markers.Phone);
        result = ApiKeyRegex().Replace(result, m =>
        {
            if (m.Groups.Count > 1 && m.Groups[1].Success)
            {
                return m.Value.Replace(m.Groups[1].Value, Markers.ApiKey);
            }
            return m.Value;
        });
        result = PasswordRegex().Replace(result, m =>
        {
            if (m.Groups.Count > 1 && m.Groups[1].Success)
            {
                return m.Value.Replace(m.Groups[1].Value, Markers.Password);
            }
            return m.Value;
        });
        result = GenericSecretRegex().Replace(result, m =>
        {
            if (m.Groups.Count > 1 && m.Groups[1].Success)
            {
                return m.Value.Replace(m.Groups[1].Value, Markers.Token);
            }
            return m.Value;
        });

        return result;
    }

    /// <summary>
    /// Sanitizes an object by serializing to JSON, sanitizing, and returning the sanitized string.
    /// </summary>
    /// <param name="obj">The object to sanitize.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>The sanitized JSON string representation.</returns>
    public static string SanitizeObject(object? obj, JsonSerializerOptions? options = null)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        try
        {
            var json = JsonSerializer.Serialize(obj, options ?? DefaultJsonOptions);
            return SanitizeString(json);
        }
        catch (JsonException)
        {
            // If serialization fails, try ToString
            return SanitizeString(obj.ToString());
        }
    }

    /// <summary>
    /// Sanitizes a dictionary by redacting sensitive values.
    /// Keys that match sensitive patterns will have their values redacted.
    /// </summary>
    /// <param name="dictionary">The dictionary to sanitize.</param>
    /// <returns>A new dictionary with sensitive values redacted.</returns>
    public static IDictionary<string, string?> SanitizeDictionary(IDictionary<string, string?>? dictionary)
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        var result = new Dictionary<string, string?>(dictionary.Count);

        foreach (var kvp in dictionary)
        {
            var sanitizedValue = IsSensitiveKey(kvp.Key)
                ? GetMarkerForKey(kvp.Key)
                : SanitizeString(kvp.Value);

            result[kvp.Key] = sanitizedValue;
        }

        return result;
    }

    /// <summary>
    /// Determines if a key name indicates sensitive data.
    /// </summary>
    private static bool IsSensitiveKey(string key)
    {
        var lowerKey = key.ToLowerInvariant();
        return lowerKey.Contains("password") ||
               lowerKey.Contains("passwd") ||
               lowerKey.Contains("secret") ||
               lowerKey.Contains("token") ||
               lowerKey.Contains("apikey") ||
               lowerKey.Contains("api_key") ||
               lowerKey.Contains("api-key") ||
               lowerKey.Contains("auth") ||
               lowerKey.Contains("credential") ||
               lowerKey.Contains("credit") ||
               lowerKey.Contains("card");
    }

    /// <summary>
    /// Gets the appropriate marker for a sensitive key.
    /// </summary>
    private static string GetMarkerForKey(string key)
    {
        var lowerKey = key.ToLowerInvariant();

        if (lowerKey.Contains("password") || lowerKey.Contains("passwd"))
            return Markers.Password;
        if (lowerKey.Contains("discord") && lowerKey.Contains("token"))
            return Markers.DiscordToken;
        if (lowerKey.Contains("token"))
            return Markers.Token;
        if (lowerKey.Contains("apikey") || lowerKey.Contains("api_key") || lowerKey.Contains("api-key"))
            return Markers.ApiKey;
        if (lowerKey.Contains("credit") || lowerKey.Contains("card"))
            return Markers.CreditCard;

        return Markers.Token; // Default for other sensitive keys
    }

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false
    };
}
