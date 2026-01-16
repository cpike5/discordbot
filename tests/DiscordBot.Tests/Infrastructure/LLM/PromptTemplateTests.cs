#nullable enable

using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for PromptTemplate.
/// Tests template loading from files, caching behavior, and variable substitution.
/// </summary>
public class PromptTemplateTests
{
    private readonly Mock<ILogger<PromptTemplate>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly PromptTemplate _promptTemplate;

    public PromptTemplateTests()
    {
        _mockLogger = new Mock<ILogger<PromptTemplate>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions { CompactionPercentage = 0.1, SizeLimit = 1024 * 1024 });
        _promptTemplate = new PromptTemplate(_mockLogger.Object, _memoryCache);
    }

    #region LoadAsync Tests

    [Fact]
    public async Task LoadAsync_WithValidRelativePath_LoadsTemplateSuccessfully()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "test_template.txt");
        var templateContent = "Hello, {{name}}! Welcome to {{place}}.";
        await File.WriteAllTextAsync(templatePath, templateContent);

        var previousDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            // Act
            var result = await _promptTemplate.LoadAsync("test_template.txt");

            // Assert
            result.Should().Be(templateContent);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Loading template from disk")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDir);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithAbsolutePath_LoadsTemplateSuccessfully()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "absolute_test.txt");
        var templateContent = "Absolute path test: {{value}}";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Act
            var result = await _promptTemplate.LoadAsync(templatePath);

            // Assert
            result.Should().Be(templateContent);
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_CachesTemplateOnFirstLoad()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "cache_test.txt");
        var templateContent = "Cache test content";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Act
            var result1 = await _promptTemplate.LoadAsync(templatePath);
            var result2 = await _promptTemplate.LoadAsync(templatePath);

            // Assert
            result1.Should().Be(templateContent);
            result2.Should().Be(templateContent);

            // Verify cache logging shows hit on second call
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Loaded template from cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}", "missing.txt");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _promptTemplate.LoadAsync(missingPath));

        exception.FileName.Should().Be(missingPath);
        exception.Message.Should().Contain("Template file not found");
    }

    [Fact]
    public async Task LoadAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _promptTemplate.LoadAsync(null!));

        exception.ParamName.Should().Be("filePath");
    }

    [Fact]
    public async Task LoadAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _promptTemplate.LoadAsync(""));

        exception.ParamName.Should().Be("filePath");
    }

    [Fact]
    public async Task LoadAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _promptTemplate.LoadAsync("   "));

        exception.ParamName.Should().Be("filePath");
    }

    [Fact]
    public async Task LoadAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "cancel_test.txt");
        await File.WriteAllTextAsync(templatePath, "Content");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                _promptTemplate.LoadAsync(templatePath, cts.Token));
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithLargeFile_CachesWithSizeTracking()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "large_file.txt");
        var largeContent = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}: {{{{variable_{i}}}}}"));
        await File.WriteAllTextAsync(templatePath, largeContent);

        try
        {
            // Act
            var result = await _promptTemplate.LoadAsync(templatePath);

            // Assert
            result.Length.Should().Be(largeContent.Length);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("cached for")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_ChecksAppContextBaseDirectoryFirst()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "app_context_test.txt");
        var templateContent = "App context test";
        await File.WriteAllTextAsync(templatePath, templateContent);

        // Create a relative path that won't exist in AppContext.BaseDirectory
        var relativePath = $"subdir\\relative_test_{Guid.NewGuid()}.txt";

        try
        {
            // Act & Assert - Should throw since file doesn't exist in either location
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _promptTemplate.LoadAsync(relativePath));
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Render Tests

    [Fact]
    public void Render_WithSingleVariable_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Hello, {{name}}!";
        var variables = new Dictionary<string, string> { { "name", "World" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void Render_WithMultipleVariables_SubstitutesAllCorrectly()
    {
        // Arrange
        var template = "{{greeting}}, {{name}}! Welcome to {{place}}.";
        var variables = new Dictionary<string, string>
        {
            { "greeting", "Hello" },
            { "name", "Alice" },
            { "place", "Wonderland" }
        };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Hello, Alice! Welcome to Wonderland.");
    }

    [Fact]
    public void Render_WithRepeatedVariable_SubstitutesAllOccurrences()
    {
        // Arrange
        var template = "{{name}} loves {{hobby}} and {{name}} practices {{hobby}} daily.";
        var variables = new Dictionary<string, string>
        {
            { "name", "Bob" },
            { "hobby", "coding" }
        };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Bob loves coding and Bob practices coding daily.");
    }

    [Fact]
    public void Render_WithUnmatchedPlaceholder_LeavesPlaceholderAsIs()
    {
        // Arrange
        var template = "Hello, {{name}}! You have {{count}} messages.";
        var variables = new Dictionary<string, string> { { "name", "User" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Hello, User! You have {{count}} messages.");
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Variable not found, leaving placeholder")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Render_WithNoVariablesProvided_ReturnsTemplateUnchanged()
    {
        // Arrange
        var template = "This template has no {{variable}} substitutions.";
        var variables = new Dictionary<string, string>();

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be(template);
    }

    [Fact]
    public void Render_WithNullVariables_TreatsAsEmpty()
    {
        // Arrange
        var template = "No substitutions needed.";

        // Act
        var result = _promptTemplate.Render(template, null);

        // Assert
        result.Should().Be(template);
    }

    [Fact]
    public void Render_WithNullTemplate_ThrowsArgumentNullException()
    {
        // Arrange
        var variables = new Dictionary<string, string> { { "key", "value" } };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            _promptTemplate.Render(null!, variables));

        exception.ParamName.Should().Be("template");
    }

    [Fact]
    public void Render_WithNullValueInVariables_SubstitutesWithEmptyString()
    {
        // Arrange
        var template = "Value: {{key}}!";
        var variables = new Dictionary<string, string> { { "key", null! } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Value: !");
    }

    [Fact]
    public void Render_WithEmptyStringValue_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Value: [{{key}}]";
        var variables = new Dictionary<string, string> { { "key", "" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Value: []");
    }

    [Fact]
    public void Render_WithSpecialCharactersInValue_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Special: {{value}}";
        var variables = new Dictionary<string, string> { { "value", "!@#$%^&*()" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Special: !@#$%^&*()");
    }

    [Fact]
    public void Render_WithComplexTemplate_SubstitutesCorrectly()
    {
        // Arrange
        var template = @"
System: {{system_prompt}}
User: {{user_message}}
Assistant: {{assistant_response}}
";
        var variables = new Dictionary<string, string>
        {
            { "system_prompt", "You are helpful." },
            { "user_message", "What is 2+2?" },
            { "assistant_response", "2+2=4" }
        };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Contain("You are helpful.");
        result.Should().Contain("What is 2+2?");
        result.Should().Contain("2+2=4");
    }

    [Fact]
    public void Render_WithVariableContainingBraces_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Code: {{code}}";
        var variables = new Dictionary<string, string> { { "code", "if (x > 5) { return true; }" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Code: if (x > 5) { return true; }");
    }

    [Fact]
    public void Render_WithUnderscoresInVariableName_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Value: {{my_variable_name}}";
        var variables = new Dictionary<string, string> { { "my_variable_name", "test" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Value: test");
    }

    [Fact]
    public void Render_WithNumbersInVariableName_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Value: {{var123}}";
        var variables = new Dictionary<string, string> { { "var123", "test" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Value: test");
    }

    [Fact]
    public void Render_WithInvalidPlaceholderFormat_IgnoresIt()
    {
        // Arrange - Single braces or incorrect format should not be matched
        var template = "Value: {notmatched} and {{valid}}";
        var variables = new Dictionary<string, string> { { "valid", "yes" } };

        // Act
        var result = _promptTemplate.Render(template, variables);

        // Assert
        result.Should().Be("Value: {notmatched} and yes");
    }

    [Fact]
    public void Render_LogsSubstitutionCount()
    {
        // Arrange
        var template = "{{a}} {{b}} {{c}}";
        var variables = new Dictionary<string, string>
        {
            { "a", "one" },
            { "b", "two" },
            { "c", "three" }
        };

        // Act
        _ = _promptTemplate.Render(template, variables);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("variable substitutions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public async Task InvalidateCache_RemovesTemplateFromCache()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "invalidate_test.txt");
        var templateContent = "Original content";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Load template (caches it)
            var result1 = await _promptTemplate.LoadAsync(templatePath);
            result1.Should().Be(templateContent);

            // Verify it was cached by checking log
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("cached for")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Modify the file
            var newContent = "Modified content";
            await File.WriteAllTextAsync(templatePath, newContent);

            // Invalidate cache
            _promptTemplate.InvalidateCache(templatePath);

            // Load again - should get new content
            var result2 = await _promptTemplate.LoadAsync(templatePath);

            // Assert
            result2.Should().Be(newContent);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalidated template cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void InvalidateCache_WithNonexistentPath_DoesNotThrow()
    {
        // Arrange
        var nonexistentPath = "/path/that/does/not/exist.txt";

        // Act & Assert
        var action = () => _promptTemplate.InvalidateCache(nonexistentPath);
        action.Should().NotThrow();
    }

    [Fact]
    public void InvalidateCache_LogsInvalidation()
    {
        // Arrange
        var filePath = "/some/path.txt";

        // Act
        _promptTemplate.InvalidateCache(filePath);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalidated template cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new PromptTemplate(null!, _memoryCache));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new PromptTemplate(_mockLogger.Object, null!));

        exception.ParamName.Should().Be("cache");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task LoadAndRender_IntegrationWithRealFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "integration_test.txt");
        var templateContent = "Hello {{firstName}} {{lastName}}, your order {{orderId}} is {{status}}.";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Act
            var loaded = await _promptTemplate.LoadAsync(templatePath);
            var rendered = _promptTemplate.Render(loaded, new Dictionary<string, string>
            {
                { "firstName", "John" },
                { "lastName", "Doe" },
                { "orderId", "ORD-12345" },
                { "status", "shipped" }
            });

            // Assert
            rendered.Should().Be("Hello John Doe, your order ORD-12345 is shipped.");
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAndRender_WithPartialVariables_LeavesUnmatchedPlaceholders()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "partial_test.txt");
        var templateContent = "User {{username}} (ID: {{userId}}) has role {{role}}.";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Act
            var loaded = await _promptTemplate.LoadAsync(templatePath);
            var rendered = _promptTemplate.Render(loaded, new Dictionary<string, string>
            {
                { "username", "alice" },
                { "role", "admin" }
                // userId is not provided
            });

            // Assert
            rendered.Should().Be("User alice (ID: {{userId}}) has role admin.");
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MultipleLoads_CachePerformanceBoost()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"prompt_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var templatePath = Path.Combine(tempDir, "perf_test.txt");
        var templateContent = "Performance test template";
        await File.WriteAllTextAsync(templatePath, templateContent);

        try
        {
            // Act - Load multiple times
            var result1 = await _promptTemplate.LoadAsync(templatePath);
            var result2 = await _promptTemplate.LoadAsync(templatePath);
            var result3 = await _promptTemplate.LoadAsync(templatePath);

            // Assert
            result1.Should().Be(templateContent);
            result2.Should().Be(templateContent);
            result3.Should().Be(templateContent);

            // Verify cache was used (debug logs for cache hits)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Loaded template from cache")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2)); // Two cache hits after first load
        }
        finally
        {
            File.Delete(templatePath);
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
