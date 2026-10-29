#!/usr/bin/env bash
# Rewrites repo.json so Dalamud lists every plugin from the latest release, then pushes it.
set -euo pipefail
git fetch origin "$BRANCH"
git checkout -B "$BRANCH" "origin/$BRANCH"
NOW=$(date +%s)
for f in "$RUNNER_TEMP"/out/*.json; do
  name=$(basename "$f" .json)
  if [ "$name" = "TailsAdvisor" ]; then icon="icon.png"; else icon="$(echo "$name" | tr 'A-Z' 'a-z')-icon.png"; fi
  DL="https://github.com/$GITHUB_REPOSITORY/releases/download/$TAG/$name.zip"
  ICON="https://raw.githubusercontent.com/$GITHUB_REPOSITORY/$BRANCH/images/$icon"
  jq --arg dl "$DL" --arg icon "$ICON" --argjson now "$NOW" \
    '. + {DownloadLinkInstall: $dl, DownloadLinkUpdate: $dl, DownloadLinkTesting: $dl, IconUrl: $icon, IsHide: false, IsTestingExclusive: false, LastUpdate: $now}' \
    "$f" > "$RUNNER_TEMP/entry-$name.json"
done
jq -s '.' "$RUNNER_TEMP"/entry-*.json > repo.json
cat repo.json
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git add repo.json
git commit -m "Update repo.json for $TAG" || echo "repo.json unchanged"
git push origin "$BRANCH"
