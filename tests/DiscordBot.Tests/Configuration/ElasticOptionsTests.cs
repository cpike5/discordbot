using DiscordBot.Core.Configuration;
using FluentAssertions;

namespace DiscordBot.Tests.Configuration;

/// <summary>
/// Unit tests for ElasticOptions configuration class.
/// Tests configuration binding, default values, and property assignments for Elastic Stack integration.
/// </summary>
public class ElasticOptionsTests
{
    [Fact]
    public void SectionName_HasCorrectValue()
    {
        // Assert
        ElasticOptions.SectionName.Should().Be("Elastic", "section name should match configuration key");
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange
        var options = new ElasticOptions();

        // Assert
        options.CloudId.Should().BeNull("CloudId should be null by default");
        options.ApiKey.Should().BeNull("ApiKey should be null by default");
        options.Endpoints.Should().BeEmpty("Endpoints array should be empty by default");
        options.IndexFormat.Should().Be("discordbot-logs-{0:yyyy.MM.dd}", "default index format should include date placeholder");
        options.ApmServerUrl.Should().BeNull("ApmServerUrl should be null by default");
        options.ApmSecretToken.Should().BeNull("ApmSecretToken should be null by default");
        options.Environment.Should().Be("development", "environment should default to development");
    }

    [Fact]
    public void IndexFormat_HasValidDefaultValue()
    {
        // Arrange
        var options = new ElasticOptions();
        var now = DateTime.UtcNow;

        // Act
        var formattedIndex = string.Format(options.IndexFormat, now);

        // Assert
        formattedIndex.Should().Match("discordbot-logs-????.??.??", "index format should support date formatting");
        formattedIndex.Should().StartWith("discordbot-logs-");
        formattedIndex.Should().HaveLength("discordbot-logs-".Length + 10, "date format should be yyyy.MM.dd");
    }

    [Fact]
    public void CloudId_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();
        var cloudId = "us-west1:dXMtY2VudHJhbDEkNDJlOTQxMDQxZjQ0NTJmNDUwZjhkNzY0YTU4ZGJkZjg=";

        // Act
        options.CloudId = cloudId;

        // Assert
        options.CloudId.Should().Be(cloudId);
    }

    [Fact]
    public void ApiKey_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();
        var apiKey = "VnVhQ2ZHY0JDUDd1YTNjNllIOGluOlJGVDUzVlE4VGdXZVVkQUZfNjVHQQ==";

        // Act
        options.ApiKey = apiKey;

        // Assert
        options.ApiKey.Should().Be(apiKey);
    }

    [Fact]
    public void Endpoints_CanBeSetWithSingleValue()
    {
        // Arrange
        var options = new ElasticOptions();
        var endpoint = "https://elasticsearch.example.com:9200";

        // Act
        options.Endpoints = new[] { endpoint };

        // Assert
        options.Endpoints.Should().HaveCount(1);
        options.Endpoints[0].Should().Be(endpoint);
    }

    [Fact]
    public void Endpoints_CanBeSetWithMultipleValues()
    {
        // Arrange
        var options = new ElasticOptions();
        var endpoints = new[]
        {
            "https://es-node1.example.com:9200",
            "https://es-node2.example.com:9200",
            "https://es-node3.example.com:9200"
        };

        // Act
        options.Endpoints = endpoints;

        // Assert
        options.Endpoints.Should().HaveCount(3);
        options.Endpoints.Should().ContainInOrder(endpoints);
    }

    [Fact]
    public void Endpoints_StartsAsEmptyArray()
    {
        // Arrange & Act
        var options = new ElasticOptions();

        // Assert
        options.Endpoints.Should().NotBeNull("Endpoints should be initialized as empty array");
        options.Endpoints.Should().BeEmpty("Endpoints should start empty");
    }

    [Fact]
    public void IndexFormat_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();
        var customFormat = "logs-discord-bot-{0:yyyy.MM.dd.HH}";

        // Act
        options.IndexFormat = customFormat;

        // Assert
        options.IndexFormat.Should().Be(customFormat);
    }

    [Fact]
    public void ApmServerUrl_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();
        var apmUrl = "https://apm.example.com:8200";

        // Act
        options.ApmServerUrl = apmUrl;

        // Assert
        options.ApmServerUrl.Should().Be(apmUrl);
    }

    [Fact]
    public void ApmSecretToken_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();
        var secretToken = "apm-secret-token-value";

        // Act
        options.ApmSecretToken = secretToken;

        // Assert
        options.ApmSecretToken.Should().Be(secretToken);
    }

    [Fact]
    public void Environment_CanBeSet()
    {
        // Arrange
        var options = new ElasticOptions();

        // Act
        options.Environment = "production";

        // Assert
        options.Environment.Should().Be("production");
    }

    [Fact]
    public void Environment_CanBeSetToMultipleValues()
    {
        // Arrange
        var options = new ElasticOptions();

        // Act & Assert
        options.Environment = "development";
        options.Environment.Should().Be("development");

        options.Environment = "staging";
        options.Environment.Should().Be("staging");

        options.Environment = "production";
        options.Environment.Should().Be("production");
    }

    [Fact]
    public void Properties_CanBeSetViaObjectInitializer()
    {
        // Act
        var options = new ElasticOptions
        {
            CloudId = "us-west1:dXMtY2VudHJhbDEkNDJlOTQxMDQxZjQ0NTJmNDUwZjhkNzY0YTU4ZGJkZjg=",
            ApiKey = "VnVhQ2ZHY0JDUDd1YTNjNllIOGluOlJGVDUzVlE4VGdXZVVkQUZfNjVHQQ==",
            Endpoints = new[] { "https://elasticsearch.example.com:9200" },
            IndexFormat = "logs-discord-{0:yyyy.MM.dd}",
            ApmServerUrl = "https://apm.example.com:8200",
            ApmSecretToken = "apm-token",
            Environment = "production"
        };

        // Assert
        options.CloudId.Should().Be("us-west1:dXMtY2VudHJhbDEkNDJlOTQxMDQxZjQ0NTJmNDUwZjhkNzY0YTU4ZGJkZjg=");
        options.ApiKey.Should().Be("VnVhQ2ZHY0JDUDd1YTNjNllIOGluOlJGVDUzVlE4VGdXZVVkQUZfNjVHQQ==");
        options.Endpoints.Should().HaveCount(1);
        options.Endpoints[0].Should().Be("https://elasticsearch.example.com:9200");
        options.IndexFormat.Should().Be("logs-discord-{0:yyyy.MM.dd}");
        options.ApmServerUrl.Should().Be("https://apm.example.com:8200");
        options.ApmSecretToken.Should().Be("apm-token");
        options.Environment.Should().Be("production");
    }

    [Fact]
    public void AllProperties_CanBeSetIndependently()
    {
        // Arrange
        var options = new ElasticOptions();

        // Act - Set all properties
        options.CloudId = "cloud-id-value";
        options.ApiKey = "api-key-value";
        options.Endpoints = new[] { "endpoint1", "endpoint2" };
        options.IndexFormat = "custom-index-{0:yyyy.MM.dd}";
        options.ApmServerUrl = "apm-url";
        options.ApmSecretToken = "apm-token";
        options.Environment = "staging";

        // Assert - Verify each property independently
        options.CloudId.Should().Be("cloud-id-value");
        options.ApiKey.Should().Be("api-key-value");
        options.Endpoints.Should().HaveCount(2);
        options.IndexFormat.Should().Be("custom-index-{0:yyyy.MM.dd}");
        options.ApmServerUrl.Should().Be("apm-url");
        options.ApmSecretToken.Should().Be("apm-token");
        options.Environment.Should().Be("staging");
    }

    [Fact]
    public void MultipleInstances_AreIndependent()
    {
        // Act
        var options1 = new ElasticOptions { Environment = "development" };
        var options2 = new ElasticOptions { Environment = "production" };

        // Assert
        options1.Environment.Should().Be("development");
        options2.Environment.Should().Be("production", "instances should not share state");
    }

    [Fact]
    public void NullableProperties_CanBeSetToNull()
    {
        // Arrange
        var options = new ElasticOptions
        {
            CloudId = "some-value",
            ApiKey = "some-value",
            ApmServerUrl = "some-value",
            ApmSecretToken = "some-value"
        };

        // Act
        options.CloudId = null;
        options.ApiKey = null;
        options.ApmServerUrl = null;
        options.ApmSecretToken = null;

        // Assert
        options.CloudId.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.ApmServerUrl.Should().BeNull();
        options.ApmSecretToken.Should().BeNull();
    }

    [Fact]
    public void Endpoints_CanBeSetToEmptyArray()
    {
        // Arrange
        var options = new ElasticOptions { Endpoints = new[] { "endpoint1" } };

        // Act
        options.Endpoints = [];

        // Assert
        options.Endpoints.Should().BeEmpty();
    }

    [Theory]
    [InlineData("development")]
    [InlineData("staging")]
    [InlineData("production")]
    public void Environment_AcceptsCommonValues(string environment)
    {
        // Arrange
        var options = new ElasticOptions();

        // Act
        options.Environment = environment;

        // Assert
        options.Environment.Should().Be(environment);
    }

    [Fact]
    public void DefaultConfiguration_IsValidForDevelopment()
    {
        // Arrange & Act
        var options = new ElasticOptions();

        // Assert
        options.Environment.Should().Be("development", "default should be suitable for development");
        options.IndexFormat.Should().NotBeNullOrEmpty("index format must be specified");
        options.Endpoints.Should().BeEmpty("endpoints are optional for cloud deployments");
    }

    [Fact]
    public void IndexFormat_DefaultSupportsDateTimeFormatting()
    {
        // Arrange
        var options = new ElasticOptions();
        var testDate = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var formattedIndex = string.Format(options.IndexFormat, testDate);

        // Assert
        formattedIndex.Should().Be("discordbot-logs-2024.01.15", "index format should create daily indices");
    }

    [Fact]
    public void Endpoints_CanHandleLargeArrays()
    {
        // Arrange
        var endpoints = Enumerable.Range(1, 100)
            .Select(i => $"https://node-{i}.example.com:9200")
            .ToArray();
        var options = new ElasticOptions();

        // Act
        options.Endpoints = endpoints;

        // Assert
        options.Endpoints.Should().HaveCount(100);
        options.Endpoints.Should().ContainInOrder(endpoints);
    }
}
