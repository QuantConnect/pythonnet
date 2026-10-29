#!/usr/bin/env bash
# Builds Python.Runtime, packs the QuantConnect.pythonnet NuGet and symbol packages into ./artifacts
# and pushes them to nuget.org when NUGET_API_KEY is set
#
# Usage: ./build_push_nuget.sh [version]
#   version: package version, e.g. 2.0.68 or v2.0.68. Defaults to the <Version> in Python.Runtime.csproj.
#
# Requires git and the .NET 10 SDK.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/runtime/Python.Runtime.csproj"
OUTPUT="$ROOT/artifacts"

VERSION="${1:-}"
VERSION="${VERSION#v}"
if [[ -n "$VERSION" && ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "Invalid version '$1', expected e.g. 2.0.68" >&2
    exit 1
fi

# Everything under pythonnet/ is packed as content, so drop stale build outputs and __pycache__ first
git -C "$ROOT" clean -fdXq -- pythonnet

echo "Packing NuGet package ${VERSION:-with the csproj version}"
rm -rf "$OUTPUT"
dotnet pack "$PROJECT" --configuration Release --output "$OUTPUT" ${VERSION:+-p:Version=$VERSION}

ls "$OUTPUT"

if [[ -n "${NUGET_API_KEY:-}" ]]; then
    echo "Pushing to nuget.org"
    # Also pushes the .snupkg symbol package next to it
    dotnet nuget push "$OUTPUT"/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
fi
