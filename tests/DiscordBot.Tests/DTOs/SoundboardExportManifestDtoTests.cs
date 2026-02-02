using System.Text.Json;
using DiscordBot.Core.DTOs.Soundboard;
using FluentAssertions;

namespace DiscordBot.Tests.DTOs;

/// <summary>
/// Unit tests for <see cref="SoundboardExportManifestDto"/> and <see cref="ExportedSoundDto"/>.
/// </summary>
public class SoundboardExportManifestDtoTests
{
    [Fact]
    public void ExportedSoundDto_SerializesToJson_WithCamelCasePropertyNames()
    {
        // Arrange
        var sound = new ExportedSoundDto
        {
            Id = "123e4567-e89b-12d3-a456-426614174000",
            Name = "Test Sound",
            FileName = "test_sound.mp3",
            OriginalFileName = "original_test.mp3",
            DurationSeconds = 3.5,
            FileSizeBytes = 12345,
            PlayCount = 42,
            UploadedById = "987654321098765432",
            UploadedAt = "2025-01-15T10:30:00.0000000Z"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(sound, options);

        // Assert
        json.Should().Contain("\"id\":", "id property should be camelCase");
        json.Should().Contain("\"name\":", "name property should be camelCase");
        json.Should().Contain("\"fileName\":", "fileName property should be camelCase");
        json.Should().Contain("\"originalFileName\":", "originalFileName property should be camelCase");
        json.Should().Contain("\"durationSeconds\":", "durationSeconds property should be camelCase");
        json.Should().Contain("\"fileSizeBytes\":", "fileSizeBytes property should be camelCase");
        json.Should().Contain("\"playCount\":", "playCount property should be camelCase");
        json.Should().Contain("\"uploadedById\":", "uploadedById property should be camelCase");
        json.Should().Contain("\"uploadedAt\":", "uploadedAt property should be camelCase");
    }

    [Fact]
    public void ExportedSoundDto_DeserializesFromJson_WithCamelCasePropertyNames()
    {
        // Arrange
        var json = """
        {
            "id": "123e4567-e89b-12d3-a456-426614174000",
            "name": "Test Sound",
            "fileName": "test_sound.mp3",
            "originalFileName": "original_test.mp3",
            "durationSeconds": 3.5,
            "fileSizeBytes": 12345,
            "playCount": 42,
            "uploadedById": "987654321098765432",
            "uploadedAt": "2025-01-15T10:30:00.0000000Z"
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var sound = JsonSerializer.Deserialize<ExportedSoundDto>(json, options);

        // Assert
        sound.Should().NotBeNull();
        sound!.Id.Should().Be("123e4567-e89b-12d3-a456-426614174000");
        sound.Name.Should().Be("Test Sound");
        sound.FileName.Should().Be("test_sound.mp3");
        sound.OriginalFileName.Should().Be("original_test.mp3");
        sound.DurationSeconds.Should().Be(3.5);
        sound.FileSizeBytes.Should().Be(12345);
        sound.PlayCount.Should().Be(42);
        sound.UploadedById.Should().Be("987654321098765432");
        sound.UploadedAt.Should().Be("2025-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void ExportedSoundDto_SerializesWithNullUploadedById()
    {
        // Arrange
        var sound = new ExportedSoundDto
        {
            Id = "123e4567-e89b-12d3-a456-426614174000",
            Name = "Test Sound",
            FileName = "test_sound.mp3",
            OriginalFileName = "original_test.mp3",
            DurationSeconds = 3.5,
            FileSizeBytes = 12345,
            PlayCount = 42,
            UploadedById = null,
            UploadedAt = "2025-01-15T10:30:00.0000000Z"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(sound, options);

        // Assert
        json.Should().Contain("\"uploadedById\": null", "null uploadedById should serialize to null");
    }

    [Fact]
    public void ExportedSoundDto_TimestampIsInIso8601Format()
    {
        // Arrange
        var uploadedAt = DateTime.UtcNow;
        var sound = new ExportedSoundDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test",
            FileName = "test.mp3",
            OriginalFileName = "test.mp3",
            DurationSeconds = 1.0,
            FileSizeBytes = 100,
            PlayCount = 1,
            UploadedById = "123",
            UploadedAt = uploadedAt.ToString("O")
        };

        // Act & Assert
        sound.UploadedAt.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$",
            "timestamp should be in ISO8601 format with Z suffix for UTC");
    }

    [Fact]
    public void SoundboardExportManifestDto_SerializesToJson_WithCamelCasePropertyNames()
    {
        // Arrange
        var manifest = new SoundboardExportManifestDto
        {
            ExportedAt = "2025-01-15T10:30:00.0000000Z",
            GuildId = "123456789012345678",
            GuildName = "Test Guild",
            TotalSounds = 5,
            TotalSizeBytes = 67890,
            Sounds = new List<ExportedSoundDto>
            {
                new()
                {
                    Id = "123e4567-e89b-12d3-a456-426614174000",
                    Name = "Sound 1",
                    FileName = "sound1.mp3",
                    OriginalFileName = "sound1_original.mp3",
                    DurationSeconds = 2.5,
                    FileSizeBytes = 5000,
                    PlayCount = 10,
                    UploadedById = "987654321098765432",
                    UploadedAt = "2025-01-10T08:00:00.0000000Z"
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(manifest, options);

        // Assert
        json.Should().Contain("\"exportedAt\":", "exportedAt property should be camelCase");
        json.Should().Contain("\"guildId\":", "guildId property should be camelCase");
        json.Should().Contain("\"guildName\":", "guildName property should be camelCase");
        json.Should().Contain("\"totalSounds\":", "totalSounds property should be camelCase");
        json.Should().Contain("\"totalSizeBytes\":", "totalSizeBytes property should be camelCase");
        json.Should().Contain("\"sounds\":", "sounds property should be camelCase");
    }

    [Fact]
    public void SoundboardExportManifestDto_DeserializesFromJson_WithCamelCasePropertyNames()
    {
        // Arrange
        var json = """
        {
            "exportedAt": "2025-01-15T10:30:00.0000000Z",
            "guildId": "123456789012345678",
            "guildName": "Test Guild",
            "totalSounds": 1,
            "totalSizeBytes": 5000,
            "sounds": [
                {
                    "id": "123e4567-e89b-12d3-a456-426614174000",
                    "name": "Sound 1",
                    "fileName": "sound1.mp3",
                    "originalFileName": "sound1_original.mp3",
                    "durationSeconds": 2.5,
                    "fileSizeBytes": 5000,
                    "playCount": 10,
                    "uploadedById": "987654321098765432",
                    "uploadedAt": "2025-01-10T08:00:00.0000000Z"
                }
            ]
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var manifest = JsonSerializer.Deserialize<SoundboardExportManifestDto>(json, options);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.ExportedAt.Should().Be("2025-01-15T10:30:00.0000000Z");
        manifest.GuildId.Should().Be("123456789012345678");
        manifest.GuildName.Should().Be("Test Guild");
        manifest.TotalSounds.Should().Be(1);
        manifest.TotalSizeBytes.Should().Be(5000);
        manifest.Sounds.Should().HaveCount(1);
        manifest.Sounds[0].Name.Should().Be("Sound 1");
    }

    [Fact]
    public void SoundboardExportManifestDto_RoundTripSerialization_PreservesAllData()
    {
        // Arrange
        var originalManifest = new SoundboardExportManifestDto
        {
            ExportedAt = "2025-01-15T10:30:00.0000000Z",
            GuildId = "123456789012345678",
            GuildName = "Test Guild",
            TotalSounds = 2,
            TotalSizeBytes = 10000,
            Sounds = new List<ExportedSoundDto>
            {
                new()
                {
                    Id = "123e4567-e89b-12d3-a456-426614174000",
                    Name = "Sound 1",
                    FileName = "sound1.mp3",
                    OriginalFileName = "sound1_original.mp3",
                    DurationSeconds = 2.5,
                    FileSizeBytes = 5000,
                    PlayCount = 10,
                    UploadedById = "987654321098765432",
                    UploadedAt = "2025-01-10T08:00:00.0000000Z"
                },
                new()
                {
                    Id = "456e7890-a12b-34c5-d678-901234567890",
                    Name = "Sound 2",
                    FileName = "sound2.mp3",
                    OriginalFileName = "sound2_original.mp3",
                    DurationSeconds = 3.0,
                    FileSizeBytes = 5000,
                    PlayCount = 5,
                    UploadedById = null,
                    UploadedAt = "2025-01-11T12:00:00.0000000Z"
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(originalManifest, options);
        var deserializedManifest = JsonSerializer.Deserialize<SoundboardExportManifestDto>(json, options);

        // Assert
        deserializedManifest.Should().NotBeNull();
        deserializedManifest!.ExportedAt.Should().Be(originalManifest.ExportedAt);
        deserializedManifest.GuildId.Should().Be(originalManifest.GuildId);
        deserializedManifest.GuildName.Should().Be(originalManifest.GuildName);
        deserializedManifest.TotalSounds.Should().Be(originalManifest.TotalSounds);
        deserializedManifest.TotalSizeBytes.Should().Be(originalManifest.TotalSizeBytes);
        deserializedManifest.Sounds.Should().HaveCount(2);

        deserializedManifest.Sounds[0].Id.Should().Be(originalManifest.Sounds[0].Id);
        deserializedManifest.Sounds[0].Name.Should().Be(originalManifest.Sounds[0].Name);
        deserializedManifest.Sounds[0].UploadedById.Should().Be(originalManifest.Sounds[0].UploadedById);

        deserializedManifest.Sounds[1].Id.Should().Be(originalManifest.Sounds[1].Id);
        deserializedManifest.Sounds[1].UploadedById.Should().BeNull("second sound has null uploadedById");
    }

    [Fact]
    public void SoundboardExportManifestDto_TimestampIsInIso8601Format()
    {
        // Arrange
        var exportedAt = DateTime.UtcNow;
        var manifest = new SoundboardExportManifestDto
        {
            ExportedAt = exportedAt.ToString("O"),
            GuildId = "123456789012345678",
            GuildName = "Test Guild",
            TotalSounds = 0,
            TotalSizeBytes = 0,
            Sounds = new List<ExportedSoundDto>()
        };

        // Act & Assert
        manifest.ExportedAt.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$",
            "timestamp should be in ISO8601 format with Z suffix for UTC");
    }

    [Fact]
    public void SoundboardExportManifestDto_WithEmptySoundsList_SerializesCorrectly()
    {
        // Arrange
        var manifest = new SoundboardExportManifestDto
        {
            ExportedAt = "2025-01-15T10:30:00.0000000Z",
            GuildId = "123456789012345678",
            GuildName = "Test Guild",
            TotalSounds = 0,
            TotalSizeBytes = 0,
            Sounds = new List<ExportedSoundDto>()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(manifest, options);

        // Assert
        json.Should().Contain("\"sounds\": []", "empty sounds list should serialize as empty array");
        json.Should().Contain("\"totalSounds\": 0", "totalSounds should be 0");
    }

}
