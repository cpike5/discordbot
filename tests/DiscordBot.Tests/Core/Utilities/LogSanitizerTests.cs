using DiscordBot.Core.Utilities;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Utilities;

/// <summary>
/// Unit tests for <see cref="LogSanitizer"/>.
/// </summary>
public class LogSanitizerTests
{
    #region SanitizeString Tests

    [Fact]
    public void SanitizeString_NullInput_ReturnsNull()
    {
        // Act
        var result = LogSanitizer.SanitizeString(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void SanitizeString_EmptyInput_ReturnsEmptyString()
    {
        // Act
        var result = LogSanitizer.SanitizeString(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeString_NoSensitiveData_ReturnsUnchanged()
    {
        // Arrange
        var input = "This is a regular log message with no sensitive data";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Be(input);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("john.doe@company.org")]
    public void SanitizeString_EmailAddresses_AreRedacted(string email)
    {
        // Arrange
        var input = $"User email is {email}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Email);
        result.Should().NotContain(email);
    }

    [Theory]
    [InlineData("123-456-7890")]
    [InlineData("(123) 456-7890")]
    [InlineData("+1 123 456 7890")]
    [InlineData("1234567890")]
    public void SanitizeString_PhoneNumbers_AreRedacted(string phone)
    {
        // Arrange
        var input = $"Contact phone: {phone}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Phone);
        result.Should().NotContain(phone);
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("5500000000000004")]
    public void SanitizeString_CreditCardNumbers_AreRedacted(string card)
    {
        // Arrange
        var input = $"Card number: {card}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.CreditCard);
        result.Should().NotContain(card);
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
    [InlineData("bearer abc123xyz456")]
    public void SanitizeString_BearerTokens_AreRedacted(string token)
    {
        // Arrange
        var input = $"Authorization: {token}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Token);
        result.Should().NotContain(token.Split(' ')[1]); // Check the token part is redacted
    }

    [Fact]
    public void SanitizeString_DiscordTokens_AreRedacted()
    {
        // Arrange - Use clearly fake token pattern that matches Discord format
        // Format: base64(user_id).timestamp.hmac - starts with M or N
        var fakeToken = "MTEST" + new string('x', 20) + ".XXXXXX.XXXXXXXXXXXXXXXXXXXXXXXXXXX";
        var input = $"Discord token: {fakeToken}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.DiscordToken);
        result.Should().NotContain(fakeToken);
    }

    [Theory]
    [InlineData("password=MySecretPassword123")]
    [InlineData("password: supersecret")]
    [InlineData("\"password\": \"hunter2\"")]
    [InlineData("pwd=admin123")]
    public void SanitizeString_Passwords_AreRedacted(string passwordField)
    {
        // Arrange
        var input = $"Config: {passwordField}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Password);
    }

    [Theory]
    [InlineData("api_key=sk_live_abcdefghijklmnopqrstuv", "[API_KEY]")]
    [InlineData("apikey: AIzaSyABCDEFGHIJKLMNOPQRSTU", "[API_KEY]")]
    [InlineData("\"api_key\": \"super_secret_abcdefghijkl\"", "[API_KEY]")]
    public void SanitizeString_ApiKeys_AreRedacted(string apiKeyField, string expectedMarker)
    {
        // Arrange
        var input = $"Settings: {apiKeyField}";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(expectedMarker);
    }

    [Fact]
    public void SanitizeString_MultipleSensitiveDataTypes_AllAreRedacted()
    {
        // Arrange
        var input = "User test@example.com called from 123-456-7890 with password=secret123";

        // Act
        var result = LogSanitizer.SanitizeString(input);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Email);
        result.Should().Contain(LogSanitizer.Markers.Phone);
        result.Should().Contain(LogSanitizer.Markers.Password);
        result.Should().NotContain("test@example.com");
        result.Should().NotContain("123-456-7890");
        result.Should().NotContain("secret123");
    }

    [Fact]
    public void SanitizeString_Performance_UnderFiveMilliseconds()
    {
        // Arrange - Large string with multiple patterns
        var input = string.Join(" ", Enumerable.Range(0, 100).Select(i =>
            $"User user{i}@example.com phone 123-456-{i:D4} password=secret{i}"));

        // Act - Warm up the regex cache
        _ = LogSanitizer.SanitizeString(input);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = LogSanitizer.SanitizeString(input);
        }
        sw.Stop();

        // Assert - Average should be well under 5ms (allowing for CI/CD variance)
        var averageMs = sw.ElapsedMilliseconds / 100.0;
        averageMs.Should().BeLessThan(5, "sanitization should be performant");
    }

    #endregion

    #region SanitizeObject Tests

    [Fact]
    public void SanitizeObject_NullObject_ReturnsEmptyString()
    {
        // Act
        var result = LogSanitizer.SanitizeObject(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeObject_SimpleObject_SanitizesSensitiveFields()
    {
        // Arrange
        var obj = new { Email = "test@example.com", Name = "John" };

        // Act
        var result = LogSanitizer.SanitizeObject(obj);

        // Assert
        result.Should().Contain(LogSanitizer.Markers.Email);
        result.Should().NotContain("test@example.com");
        result.Should().Contain("John");
    }

    [Fact]
    public void SanitizeObject_ObjectWithPassword_SanitizesPassword()
    {
        // Arrange
        var obj = new Dictionary<string, string>
        {
            { "username", "admin" },
            { "password", "secretpassword123" }
        };

        // Act
        var result = LogSanitizer.SanitizeObject(obj);

        // Assert
        result.Should().NotContain("secretpassword123");
        result.Should().Contain("admin");
    }

    #endregion

    #region SanitizeDictionary Tests

    [Fact]
    public void SanitizeDictionary_NullDictionary_ReturnsEmptyDictionary()
    {
        // Act
        var result = LogSanitizer.SanitizeDictionary(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeDictionary_EmptyDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        var dict = new Dictionary<string, string?>();

        // Act
        var result = LogSanitizer.SanitizeDictionary(dict);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeDictionary_SensitiveKeys_AreFullyRedacted()
    {
        // Arrange
        var dict = new Dictionary<string, string?>
        {
            { "password", "supersecret" },
            { "apikey", "sk_live_123456" },
            { "token", "eyJhbGc..." },
            { "username", "testuser" }
        };

        // Act
        var result = LogSanitizer.SanitizeDictionary(dict);

        // Assert
        result["password"].Should().Be(LogSanitizer.Markers.Password);
        result["apikey"].Should().Be(LogSanitizer.Markers.ApiKey);
        result["token"].Should().Be(LogSanitizer.Markers.Token);
        result["username"].Should().Be("testuser");
    }

    [Fact]
    public void SanitizeDictionary_NonSensitiveKeysWithSensitiveValues_ValuesSanitized()
    {
        // Arrange
        var dict = new Dictionary<string, string?>
        {
            { "message", "Contact me at test@example.com" },
            { "details", "Call 123-456-7890" }
        };

        // Act
        var result = LogSanitizer.SanitizeDictionary(dict);

        // Assert
        result["message"].Should().Contain(LogSanitizer.Markers.Email);
        result["details"].Should().Contain(LogSanitizer.Markers.Phone);
    }

    #endregion

    #region Markers Tests

    [Fact]
    public void Markers_AllHaveBracketFormat()
    {
        // Assert - All markers should be in [MARKER] format
        LogSanitizer.Markers.Email.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.Phone.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.CreditCard.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.Token.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.Password.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.DiscordToken.Should().StartWith("[").And.EndWith("]");
        LogSanitizer.Markers.ApiKey.Should().StartWith("[").And.EndWith("]");
    }

    [Fact]
    public void Markers_AreDistinct()
    {
        // Arrange
        var markers = new[]
        {
            LogSanitizer.Markers.Email,
            LogSanitizer.Markers.Phone,
            LogSanitizer.Markers.CreditCard,
            LogSanitizer.Markers.Token,
            LogSanitizer.Markers.Password,
            LogSanitizer.Markers.DiscordToken,
            LogSanitizer.Markers.ApiKey
        };

        // Assert
        markers.Should().OnlyHaveUniqueItems();
    }

    #endregion
}
