# Wondrous Tails Advisor

A Dalamud plugin that tells you when to **Shuffle** your Wondrous Tails journal and when to **Hold**.

The game drops each sticker on a random open square, so you can't choose where it lands. What you can choose is when to spend Second Chance points on a Shuffle. This plugin solves that decision exactly for the points you have.

## What it shows

1. **Verdict:** SHUFFLE NOW or HOLD, with your goal odds for each choice.
2. **Odds table:** chance of 1+, 2+, and 3 lines if you stop shuffling, if you hold, and if you shuffle now.
3. **Live grid:** your journal, with each open square colored by how lucky it would be if the next sticker landed there. Hover a square for the full odds.
4. **Planner mode:** click squares to test any board and any number of points.
5. **Settings:** pick your goal, keep points in reserve for Retry, and auto open with the journal.

Open it with `/wta`, or let it pop up when you open the journal.

### Goals

| Goal | Best for |
|---|---|
| At least one line | Tomestones, MGP card, Bronze cert |
| Two or more lines | Silver cert, uncapped tomes |
| Three lines (jackpot) | 1M MGP, Gold cert. Rare: about 0.9% even with 9 points |
| Most lines on average | A balanced middle ground |

### How the math works

Every board is one of 65,536 sticker patterns. For each goal the plugin works backward from all finished boards and, for every board and every point budget from 0 to 9, compares holding (average over the next random sticker) with shuffling (average over every board with the same sticker count, at 2 points less). The result is the best possible plan, including shuffling again later. It solves in about 50 ms.

Quick facts it reproduces: with no shuffles, a blank journal has a 58.5% chance of at least one line and a 0.21% chance of three. For every goal and point total, the optimal plan only ever shuffles at exactly 7 stickers; at 3 to 6 it always says hold. At 7 it shuffles on roughly 60% to 93% of boards depending on your goal, which is why the per board call matters.

The plan assumes you earn no new Second Chance points during the week, so it is slightly cautious.

## Publish it as your own custom repository

You only do this once. After that, new versions take one click.

1. **Create a repo.** On GitHub, create a new **public** repository, for example `WondrousTailsAdvisor`. It must be public so Dalamud can download from it.
2. **Upload the files.** Upload everything in this folder, keeping the structure. Make sure the hidden `.github` folder comes along (GitHub's web uploader accepts it if you drag the whole folder in; `git push` also works).
3. **Run the release.** Go to the **Actions** tab, choose **Release**, click **Run workflow**, enter `1.0.0`, and run it. In about 2 minutes it builds the plugin, attaches it to a GitHub Release, and writes `repo.json` to your main branch.
4. **Add the repo in game.** Type `/xlsettings`, open **Experimental**, and under **Custom Plugin Repositories** paste:

   ```
   https://raw.githubusercontent.com/Jaqolantern-tech/WondrousTailsAdvisor/main/repo.json
   ```

   Tick the box next to it and click **Save**.
5. **Install.** Type `/xlplugins`, search **Wondrous Tails Advisor**, and install.

### Shipping an update

Change the code, push it, then run the **Release** workflow again with a higher version (1.0.1, 1.1.0, and so on). Dalamud picks up the new version on its next update check.

Every push also runs the **Build** workflow, which compiles the plugin and uploads a test zip as an artifact, so you see a green check before releasing.

## First run check

Open your in game journal next to the advisor and confirm the gold squares match your stickers. If the grid looks shifted or mirrored, use Planner mode in the meantime and report it; the fix is a one line index change in `Game/JournalReader.cs`.

## Troubleshooting

| Problem | Fix |
|---|---|
| Release workflow fails at "Update repo.json" with a permission error | Repo **Settings**, **Actions**, **General**, set **Workflow permissions** to **Read and write**, then rerun |
| Plugin doesn't show in `/xlplugins` | Check that `repo.json` exists on your main branch and the URL in Dalamud settings matches your username and repo name exactly |
| Shows as outdated after a big game patch | Dalamud bumped its API level. Update `Dalamud.NET.Sdk/15.0.0` in `TailsAdvisor/TailsAdvisor.csproj` to the new major version and release again |

## Project layout

```
TailsAdvisor/
  Plugin.cs              entry point, /wta command, journal open and close hooks
  Configuration.cs       saved settings
  Game/JournalReader.cs  reads stickers and points from game memory (read only)
  Solver/TailsMath.cs    board geometry and no shuffle odds
  Solver/ShufflePolicy.cs  optimal shuffle plan per goal and point budget
  Solver/Advisor.cs      turns a board into the verdict, odds, and square colors
  Windows/MainWindow.cs  the UI
.github/workflows/       build check and release automation
images/icon.png          plugin icon
```

## Note

Dalamud plugins are third party tools that Square Enix does not support. This one only reads your journal and draws a window; it never sends anything to the game servers or automates actions.
