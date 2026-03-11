# =============================================================================
# Build Stage
# =============================================================================
# Noble (Ubuntu 24.04) is required because libdave.so (Discord DAVE E2EE
# protocol) needs GLIBC 2.38 / GLIBCXX 3.4.32. Debian Bookworm (the default
# 8.0 tag) ships older versions, and Alpine uses musl instead of glibc, so
# neither can load the prebuilt libdave binary.
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build

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
COPY Directory.Build.props ./
COPY src/DiscordBot.Core/DiscordBot.Core.csproj src/DiscordBot.Core/
COPY src/DiscordBot.Infrastructure/DiscordBot.Infrastructure.csproj src/DiscordBot.Infrastructure/
COPY src/DiscordBot.Bot/DiscordBot.Bot.csproj src/DiscordBot.Bot/

# Local NuGet feed for forked Discord.Net packages (DAVE multi-party voice fix)
COPY nuget.config ./
COPY local-packages/ local-packages/

RUN dotnet restore src/DiscordBot.Bot/DiscordBot.Bot.csproj

# Install npm dependencies (cached separately from source changes)
COPY src/DiscordBot.Bot/package.json src/DiscordBot.Bot/package-lock.json src/DiscordBot.Bot/
RUN cd src/DiscordBot.Bot && npm ci

# Download libdave native library for Discord DAVE E2EE protocol (cached separately from source changes)
RUN curl -fsSL -o /tmp/libdave.zip \
        https://github.com/discord/libdave/releases/download/v1.1.1/cpp/libdave-Linux-X64-boringssl.zip \
    && apt-get update && apt-get install -y --no-install-recommends unzip \
    && unzip /tmp/libdave.zip -d /tmp/libdave \
    && rm /tmp/libdave.zip

# Copy remaining source code
COPY src/ src/

# Build Tailwind CSS (npm packages already installed above)
WORKDIR /src/src/DiscordBot.Bot
RUN npm run build:css

# Publish the application
WORKDIR /src
RUN dotnet publish src/DiscordBot.Bot/DiscordBot.Bot.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# =============================================================================
# Runtime Stage
# =============================================================================
# See build stage comment for why Noble is required (libdave glibc dependency).
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS runtime

# Install runtime dependencies (audio libs + curl for health checks)
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl \
        ffmpeg \
        python3 \
        libsodium23 \
        libopus0 \
    && rm -rf /var/lib/apt/lists/* \
    && ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /usr/lib/x86_64-linux-gnu/libopus.so \
    && ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /usr/lib/x86_64-linux-gnu/libsodium.so

# Copy libdave native library for Discord DAVE E2EE protocol
COPY --from=build /tmp/libdave/lib/libdave.so /usr/lib/x86_64-linux-gnu/libdave.so

# Create non-root user
RUN useradd --no-create-home --shell /bin/false appuser

WORKDIR /app

# Copy published output
COPY --from=build /app/publish ./

# Copy AI assistant runtime docs (prompt templates + documentation tool articles)
COPY docs/agents/ ./docs/agents/
COPY docs/articles/ ./docs/articles/

# Create data directory for SQLite database volume mount (not used when running PostgreSQL)
RUN mkdir -p /app/data && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -sf http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "DiscordBot.Bot.dll"]
