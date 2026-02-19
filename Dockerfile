# =============================================================================
# Build Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Install Node.js 20.x (required for Tailwind CSS compilation)
RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy solution and project files for layer-cached restore
COPY DiscordBot.sln ./
COPY src/DiscordBot.Core/DiscordBot.Core.csproj src/DiscordBot.Core/
COPY src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj src/DiscordBot.Infrastructure/
COPY src/DiscordBot.Bot/DiscordBot.Bot.csproj src/DiscordBot.Bot/

RUN dotnet restore src/DiscordBot.Bot/DiscordBot.Bot.csproj

# Copy remaining source code
COPY src/ src/

# Build Tailwind CSS
WORKDIR /src/src/DiscordBot.Bot
RUN npm ci && npm run build:css

# Publish the application
WORKDIR /src
RUN dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# =============================================================================
# Runtime Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install runtime dependencies (audio libs + curl for health checks)
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl \
        ffmpeg \
        libsodium23 \
        libopus0 \
    && rm -rf /var/lib/apt/lists/* \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/x86_64-linux-gnu/libopus.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/x86_64-linux-gnu/libsodium.so

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser

WORKDIR /app

# Copy published output
COPY --from=build /app/publish ./

# Create data directory for SQLite database volume mount (not used when running PostgreSQL)
RUN mkdir -p /app/data && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -sf http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "DiscordBot.Bot.dll"]
