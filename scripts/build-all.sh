#!/usr/bin/env bash
# Builds every plugin folder (any folder holding a .csproj) and collects the packages.
set -euo pipefail
OUT="$RUNNER_TEMP/out"
mkdir -p "$OUT"
for proj in */*.csproj; do
  name=$(basename "$proj" .csproj)
  echo "Building $name"
  dotnet build "$proj" -c Release -p:RepoUrl="https://github.com/$GITHUB_REPOSITORY" -p:EnableWindowsTargeting=true
  zip=$(find "$(dirname "$proj")/bin" -path '*Release*' -name latest.zip | head -n 1)
  if [ -z "$zip" ]; then echo "No package for $name"; exit 1; fi
  cp "$zip" "$OUT/$name.zip"
  cp "$(dirname "$zip")/$name.json" "$OUT/$name.json"
done
ls -la "$OUT"
