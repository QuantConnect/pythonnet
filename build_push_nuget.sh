#!/usr/bin/env bash
# Builds Python.Runtime, packs the QuantConnect.pythonnet NuGet and symbol packages into ./artifacts
# with the <Version> in Python.Runtime.csproj and pushes them to nuget.org when NUGET_API_KEY is set
#
# Requires git and the .NET 10 SDK.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/runtime/Python.Runtime.csproj"
OUTPUT="$ROOT/artifacts"

# Everything under pythonnet/ is packed as content, so drop stale build outputs and __pycache__ first
git -C "$ROOT" clean -fdXq -- pythonnet

echo "Packing NuGet package"
rm -rf "$OUTPUT"
dotnet pack "$PROJECT" --configuration Release --output "$OUTPUT"

ls "$OUTPUT"

if [[ -n "${NUGET_API_KEY:-}" ]]; then
    echo "Pushing to nuget.org"
    # Also pushes the .snupkg symbol package next to it
    dotnet nuget push "$OUTPUT"/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
fi
