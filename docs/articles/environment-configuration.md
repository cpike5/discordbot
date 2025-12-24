# Environment-Specific Configuration

This document describes the environment-specific configuration files and their intended use for Development, Staging, and Production environments.

## Overview

The Discord Bot uses ASP.NET Core's configuration system which automatically loads environment-specific settings based on the `ASPNETCORE_ENVIRONMENT` environment variable. Configuration files are loaded in the following order (later files override earlier ones):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific overrides)
3. User secrets (development only)
4. Environment variables

## Environment Files

| File | Environment | Purpose |
|------|-------------|---------|
| `appsettings.json` | All | Base configuration with sensible defaults |
| `appsettings.Development.json` | Development | Debug-level logging, development-friendly settings |
| `appsettings.Staging.json` | Staging | Pre-production testing with moderate logging |
| `appsettings.Production.json` | Production | Optimized for performance and reduced log volume |

## Log Level Configuration

### Development

- **Default Level:** Debug
- **Purpose:** Maximum visibility for debugging
- **Use Case:** Local development and troubleshooting

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Discord": "Debug"
      }
    }
  }
}
```

### Staging

- **Default Level:** Information
- **DiscordBot Namespace:** Debug (for pre-production debugging)
- **File Retention:** 14 days
- **Purpose:** Pre-production validation with enhanced application logging

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Information",
        "DiscordBot": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "retainedFileCountLimit": 14
        }
      }
    ]
  }
}
```

### Production

- **Default Level:** Warning
- **DiscordBot Namespace:** Information (important business events only)
- **File Retention:** 30 days
- **Buffered Writing:** Enabled for performance
- **Purpose:** Minimal logging overhead, focus on warnings and errors

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Warning",
        "Discord": "Warning",
        "DiscordBot": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "retainedFileCountLimit": 30,
          "buffered": true,
          "flushToDiskInterval": "00:00:01"
        }
      }
    ]
  }
}
```

## Database Configuration

Database query logging thresholds vary by environment:

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `SlowQueryThresholdMs` | 100ms | 200ms | 500ms |
| `LogQueryParameters` | true | false | false |

- **Development:** Low threshold to catch potential performance issues early; parameters logged for debugging
- **Staging:** Moderate threshold; parameters hidden for security
- **Production:** Higher threshold to reduce noise; parameters never logged

## Setting the Environment

### Local Development

The environment defaults to `Development` when running locally with `dotnet run`.

### Command Line

```bash
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT="Staging"
dotnet run --project src/DiscordBot.Bot

# Windows CMD
set ASPNETCORE_ENVIRONMENT=Staging
dotnet run --project src/DiscordBot.Bot

# Linux/macOS
ASPNETCORE_ENVIRONMENT=Staging dotnet run --project src/DiscordBot.Bot
```

### Docker

```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
```

### Azure App Service

Set the `ASPNETCORE_ENVIRONMENT` application setting in the Azure Portal or via ARM template.

### IIS

Set the environment variable in the application pool's environment variables or in `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\DiscordBot.Bot.dll">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```

## Startup Logging

On application startup, the current environment is logged for verification:

```
[12:00:00 INF] Starting Discord bot application
[12:00:00 INF] Environment: Production
[12:00:00 INF] ContentRootPath: /app
```

This helps confirm that the expected configuration is being loaded.

## Log Retention Summary

| Environment | Retention | Buffered | Purpose |
|-------------|-----------|----------|---------|
| Development | 7 days | No | Quick iteration, immediate visibility |
| Staging | 14 days | No | Pre-production debugging |
| Production | 30 days | Yes | Compliance, performance |

## Best Practices

1. **Never commit secrets** - Use user secrets for development and environment variables for production
2. **Verify environment on deploy** - Check startup logs to confirm the correct environment is loaded
3. **Adjust thresholds as needed** - The provided thresholds are starting points; tune based on your traffic patterns
4. **Monitor log volume** - Production logging should be minimal; if logs are too verbose, adjust overrides
5. **Use structured logging** - All logging uses Serilog structured logging for queryability

## Related Documentation

- [Identity Configuration](identity-configuration.md) - Authentication setup per environment
- [APM Tracing Plan](apm-tracing-plan.md) - Application performance monitoring configuration
