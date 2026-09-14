using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <summary>
    /// Re-seeds the two default themes ("discord-dark", "purple-dusk") that
    /// <c>Migrations/20260115204355_AddThemeSupport.cs</c> originally inserted - that migration
    /// carries the pre-split <c>[DbContext(typeof(BotDbContext))]</c> attribute, so it is not part
    /// of <see cref="Data.SqliteBotDbContext"/>'s own migration chain (see CLAUDE.md "Database and
    /// migrations" on the Sqlite/Postgres split). <c>20260219205009_AddIsEnabledToGuildModerationConfig</c>
    /// - the first <c>SqliteBotDbContext</c>-attributed migration, effectively a from-scratch
    /// baseline for that context - re-creates the <c>Themes</c> table (matching the model shape)
    /// but never carried over the <c>InsertData</c> calls, since those aren't part of the model
    /// snapshot a migration diff picks up. A brand-new Sqlite database migrated only through
    /// <c>SqliteBotDbContext</c> (the only context Program.cs's startup <c>MigrateAsync()</c> now
    /// resolves - see the same "Database and migrations" note) therefore got a <c>Themes</c> table
    /// with zero rows, and <c>ThemeService.GetDefaultThemeAsync</c> throws
    /// <c>InvalidOperationException("No active themes available in the system")</c> the moment
    /// anything renders the shared layout. The Postgres baseline
    /// (<c>Migrations/Postgresql/20260219132220_InitialPostgresql.cs</c>) already seeds both rows
    /// correctly - a genuinely from-scratch "Initial" migration written after the split, not a
    /// diff against the old chain - so this fix is Sqlite-only.
    /// </summary>
    /// <remarks>
    /// Uses <c>INSERT OR IGNORE</c> keyed on <c>ThemeKey</c>'s unique index rather than
    /// <c>migrationBuilder.InsertData</c>, so this is a no-op (not a unique-constraint failure) on
    /// a database whose history predates the Sqlite/Postgres split and therefore already has these
    /// two rows from the original <c>AddThemeSupport</c> migration.
    /// </remarks>
    public partial class SeedDefaultThemesSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Themes (ThemeKey, DisplayName, Description, ColorDefinition, IsActive, CreatedAt)
                VALUES (
                    'discord-dark',
                    'Discord Dark',
                    'Dark theme inspired by Discord''s interface with orange and blue accents.',
                    '{"bgPrimary":"#1d2022","bgSecondary":"#262a2d","bgTertiary":"#2f3336","bgHover":"#3a3f42","textPrimary":"#d7d3d0","textSecondary":"#a8a5a3","textTertiary":"#7a7876","textInverse":"#1d2022","accentOrange":"#cb4e1b","accentOrangeHover":"#e55d22","accentOrangeActive":"#b3440f","accentOrangeMuted":"#cb4e1b33","accentBlue":"#098ecf","accentBlueHover":"#0aa5ed","accentBlueActive":"#0778ab","accentBlueMuted":"#098ecf33","borderPrimary":"#3a3f42","borderSecondary":"#2f3336","borderFocus":"#cb4e1b"}',
                    1,
                    CURRENT_TIMESTAMP
                );
                """);

            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Themes (ThemeKey, DisplayName, Description, ColorDefinition, IsActive, CreatedAt)
                VALUES (
                    'purple-dusk',
                    'Purple Dusk',
                    'Light theme with warm beige backgrounds and purple accents.',
                    '{"bgPrimary":"#E8E3DF","bgSecondary":"#DAD4D0","bgTertiary":"#CCC5C0","bgHover":"#C0B8B2","textPrimary":"#4F214A","textSecondary":"#614978","textTertiary":"#887A99","textInverse":"#E8E3DF","accentPurple":"#614978","accentPurpleHover":"#7A5C8F","accentPurpleActive":"#4F214A","accentPurpleMuted":"#61497833","accentPink":"#D5345B","accentPinkHover":"#E5476D","accentPinkActive":"#B82A4D","accentPinkMuted":"#D5345B33","borderPrimary":"#C0B8B2","borderSecondary":"#DAD4D0","borderFocus":"#614978"}',
                    1,
                    CURRENT_TIMESTAMP
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM Themes WHERE ThemeKey IN ('discord-dark', 'purple-dusk');");
        }
    }
}
