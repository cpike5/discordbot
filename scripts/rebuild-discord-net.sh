#!/bin/bash
# Rebuild Discord.Net packages from the local fork.
# Usage: ./scripts/rebuild-discord-net.sh [suffix]
# Default suffix: fork (produces version 3.19.0-fork)
#
# After rebuilding, clear NuGet cache and restore:
#   dotnet nuget locals all --clear
#   dotnet restore src/DiscordBot.Bot/DiscordBot.Bot.csproj

set -euo pipefail

SUFFIX="${1:-fork}"
FORK_DIR="${FORK_DIR:-../Discord.Net}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="$REPO_DIR/local-packages"

if [ ! -d "$FORK_DIR" ]; then
    echo "Error: Discord.Net fork not found at $FORK_DIR"
    echo "Set FORK_DIR environment variable or clone the fork adjacent to this repo."
    exit 1
fi

echo "Building Discord.Net packages from $FORK_DIR (suffix: $SUFFIX)"
echo "Output: $OUTPUT_DIR"
echo ""

# Clean output
rm -f "$OUTPUT_DIR"/Discord.Net.*.nupkg

# Pack all required projects
PROJECTS=(
    "src/Discord.Net.Core/Discord.Net.Core.csproj"
    "src/Discord.Net.Rest/Discord.Net.Rest.csproj"
    "src/Discord.Net.WebSocket/Discord.Net.WebSocket.csproj"
    "src/Discord.Net.Commands/Discord.Net.Commands.csproj"
    "src/Discord.Net.Interactions/Discord.Net.Interactions.csproj"
    "tools/Discord.Net.Dave/Discord.Net.Dave.csproj"
)

for proj in "${PROJECTS[@]}"; do
    name=$(basename "$proj" .csproj)
    echo "=== Packing $name ==="
    dotnet pack "$FORK_DIR/$proj" \
        -c Release \
        -o "$OUTPUT_DIR" \
        /p:VersionSuffix="$SUFFIX" \
        /p:IsTagBuild=true \
        /p:TreatWarningsAsErrors=false \
        /p:TargetFrameworks=net8.0 \
        /p:LangVersion=preview \
        --verbosity quiet
    echo "  -> $name.3.19.0-$SUFFIX.nupkg"
    echo ""
done

echo "Done. Packages built:"
ls -la "$OUTPUT_DIR"/Discord.Net.*.nupkg
echo ""
echo "Next steps:"
echo "  dotnet nuget locals all --clear"
echo "  dotnet restore src/DiscordBot.Bot/DiscordBot.Bot.csproj"
