-- Seed data for UI audit verification (docs/plans/ui-audit-improvements.md section 11).
-- 3 guilds (one with a long name), 40 members, 300 command logs (some failures),
-- 8 reminders, 3 scheduled messages, 12 sounds, 6 notifications.
--
-- Run after the app has started once (so migrations have created the schema) and the
-- default admin exists:
--   PGPASSWORD=postgres psql -h localhost -U postgres -d discordbot_ui -f scripts/dev/seed-ui-audit.sql
-- Idempotent: re-running leaves existing rows alone. Dev Sandbox gets no commands today,
-- so the dashboard shows both an "Active today" and a "Quiet" guild.

BEGIN;

INSERT INTO "Guilds" ("Id", "Name", "JoinedAt", "IsActive", "Prefix") VALUES
  (123456789012345678, 'Pixel Pirates', now() - interval '90 days', true, '!'),
  (223456789012345678, 'The Late Night Study Hall & Coffee Appreciation Society', now() - interval '60 days', true, NULL),
  (323456789012345678, 'Dev Sandbox', now() - interval '30 days', true, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO "GuildAudioSettings" ("GuildId", "AudioEnabled", "CreatedAt", "UpdatedAt", "SilentPlayback", "EnableMemberPortal")
SELECT g, true, now(), now(), false, true
FROM unnest(ARRAY[123456789012345678, 223456789012345678, 323456789012345678]::bigint[]) AS g
ON CONFLICT DO NOTHING;

-- 40 users (ids 500000000000000001..040), spread over the three guilds
INSERT INTO "Users" ("Id", "Username", "Discriminator", "FirstSeenAt", "LastSeenAt", "GlobalDisplayName")
SELECT 500000000000000000 + n, 'audit_user_' || lpad(n::text, 2, '0'), '0',
       now() - (n || ' days')::interval, now() - (n || ' hours')::interval, 'Audit User ' || n
FROM generate_series(1, 40) AS n
ON CONFLICT DO NOTHING;

INSERT INTO "GuildMembers" ("GuildId", "UserId", "JoinedAt", "Nickname", "LastActiveAt", "LastCachedAt", "IsActive")
SELECT CASE WHEN n % 4 = 0 THEN 323456789012345678::bigint
            WHEN n % 2 = 0 THEN 223456789012345678::bigint
            ELSE 123456789012345678::bigint END,
       500000000000000000 + n, now() - (n || ' days')::interval,
       CASE WHEN n % 5 = 0 THEN 'Nick ' || n END,
       now() - (n || ' hours')::interval, now(), true
FROM generate_series(1, 40) AS n
ON CONFLICT DO NOTHING;

-- 300 command logs over the last week; about 1 in 9 fails. Dev Sandbox only has rows older
-- than 2 days; the other two guilds have rows today.
INSERT INTO "CommandLogs" ("Id", "GuildId", "UserId", "CommandName", "ExecutedAt", "ResponseTimeMs", "Success", "ErrorMessage")
SELECT gen_random_uuid(),
       CASE WHEN n % 3 = 0 THEN 323456789012345678::bigint
            WHEN n % 3 = 1 THEN 123456789012345678::bigint
            ELSE 223456789012345678::bigint END,
       500000000000000000 + 1 + (n % 40),
       (ARRAY['ping','play','remind','sound','status','help','tts','vox'])[1 + n % 8],
       CASE WHEN n % 3 = 0 THEN now() - ((2 + n % 5) || ' days')::interval - ((n % 20) || ' hours')::interval
            ELSE now() - ((n % 6) || ' hours')::interval - ((n % 50) || ' minutes')::interval
                 - (CASE WHEN n % 4 = 0 THEN (n % 5) || ' days' ELSE '0 days' END)::interval END,
       20 + (n * 37) % 480,
       n % 9 <> 0,
       CASE WHEN n % 9 = 0 THEN 'Seeded failure for audit' END
FROM generate_series(1, 300) AS n
WHERE NOT EXISTS (SELECT 1 FROM "CommandLogs" WHERE "UserId" > 500000000000000000);

-- 8 reminders: 6 pending, 2 delivered
INSERT INTO "Reminders" ("Id", "GuildId", "ChannelId", "UserId", "Message", "TriggerAt", "CreatedAt", "Status", "DeliveredAt")
SELECT gen_random_uuid(),
       CASE WHEN n % 2 = 0 THEN 123456789012345678::bigint ELSE 223456789012345678::bigint END,
       600000000000000001, 500000000000000000 + n, 'Seeded reminder ' || n,
       CASE WHEN n <= 6 THEN now() + (n || ' hours')::interval ELSE now() - (n || ' hours')::interval END,
       now() - interval '1 day',
       CASE WHEN n <= 6 THEN 0 ELSE 1 END,
       CASE WHEN n > 6 THEN now() - ((n - 6) || ' hours')::interval END
FROM generate_series(1, 8) AS n
WHERE NOT EXISTS (SELECT 1 FROM "Reminders" WHERE "Message" LIKE 'Seeded reminder %');

-- 3 scheduled messages
INSERT INTO "ScheduledMessages" ("Id", "GuildId", "ChannelId", "Title", "Content", "Frequency", "IsEnabled", "NextExecutionAt", "CreatedAt", "CreatedBy", "UpdatedAt")
SELECT gen_random_uuid(), 123456789012345678, 600000000000000001, t, 'Seeded content for ' || t, f, e, now() + interval '1 day', now(), 'seed', now()
FROM (VALUES ('Daily standup', 3, true), ('Weekly recap', 4, true), ('Retired announcement', 1, false)) AS v(t, f, e)
WHERE NOT EXISTS (SELECT 1 FROM "ScheduledMessages" WHERE "CreatedBy" = 'seed');

-- 12 sounds
INSERT INTO "Sounds" ("Id", "GuildId", "Name", "FileName", "FileSizeBytes", "DurationSeconds", "UploadedAt", "PlayCount")
SELECT gen_random_uuid(),
       CASE WHEN n <= 8 THEN 123456789012345678::bigint ELSE 223456789012345678::bigint END,
       'seed-sound-' || lpad(n::text, 2, '0'), 'seed-sound-' || lpad(n::text, 2, '0') || '.mp3',
       50000 + n * 7000, 1.5 + n * 0.7, now() - (n || ' days')::interval, n * 3
FROM generate_series(1, 12) AS n
WHERE NOT EXISTS (SELECT 1 FROM "Sounds" WHERE "Name" LIKE 'seed-sound-%');

-- 6 notifications for the first Identity user (the default admin)
INSERT INTO "UserNotifications" ("Id", "UserId", "Type", "Severity", "Title", "Message", "IsRead", "CreatedAt")
SELECT gen_random_uuid(), (SELECT "Id" FROM "AspNetUsers" ORDER BY "Id" LIMIT 1),
       1 + n % 2, n % 3, 'Seeded notification ' || n, 'Seeded notification body ' || n, n > 4,
       now() - (n || ' hours')::interval
FROM generate_series(1, 6) AS n
WHERE EXISTS (SELECT 1 FROM "AspNetUsers")
  AND NOT EXISTS (SELECT 1 FROM "UserNotifications" WHERE "Title" LIKE 'Seeded notification %');

COMMIT;
