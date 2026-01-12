using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEmptyTtsVoiceValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Update existing records with empty DefaultVoice
            migrationBuilder.Sql(
                "UPDATE GuildTtsSettings SET DefaultVoice = 'en-US-JennyNeural' WHERE DefaultVoice = '' OR DefaultVoice IS NULL");

            // Step 2: Change column default value
            migrationBuilder.AlterColumn<string>(
                name: "DefaultVoice",
                table: "GuildTtsSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "en-US-JennyNeural",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: "");

            // Step 3: Add check constraint (SQLite triggers)
            migrationBuilder.Sql(
                @"CREATE TRIGGER trg_GuildTtsSettings_DefaultVoice_NotEmpty
                  BEFORE INSERT ON GuildTtsSettings
                  WHEN NEW.DefaultVoice = ''
                  BEGIN
                      SELECT RAISE(ABORT, 'DefaultVoice cannot be empty');
                  END;");

            migrationBuilder.Sql(
                @"CREATE TRIGGER trg_GuildTtsSettings_DefaultVoice_NotEmpty_Update
                  BEFORE UPDATE ON GuildTtsSettings
                  WHEN NEW.DefaultVoice = ''
                  BEGIN
                      SELECT RAISE(ABORT, 'DefaultVoice cannot be empty');
                  END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_GuildTtsSettings_DefaultVoice_NotEmpty_Update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_GuildTtsSettings_DefaultVoice_NotEmpty");

            // Revert column default
            migrationBuilder.AlterColumn<string>(
                name: "DefaultVoice",
                table: "GuildTtsSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: "en-US-JennyNeural");
        }
    }
}
