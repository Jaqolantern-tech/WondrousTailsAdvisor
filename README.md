# Wondrous Tails Advisor

A Dalamud plugin that tells you when to **Shuffle** your Wondrous Tails journal and when to **Hold**.

The game drops each sticker on a random open square, so you can't choose where it lands. What you can choose is when to spend Second Chance points on a Shuffle. This plugin solves that decision exactly for the points you have.

## What it shows

A small window styled like Khloe's journal that gives you one clear call:

| Verdict | When |
|---|---|
| **Keep stamping!** | A shuffle wouldn't help right now. Before 7 stickers, with a lucky board, or after 8. |
| **Shake it up!** | You're at 7 stickers and a shuffle improves your odds. |
| **Grab more points!** | A shuffle would help, but you're short on Second Chance points. Tells you how to earn them. |
| **Off to Khloe!** | Your journal is full. |

Each verdict comes with one sentence explaining why, using your real odds for the goal you picked: **Any line**, **2 lines**, or **Jackpot**. A tiny mirror of your journal shows the stickers the advisor sees, and **Show the math** opens the full odds.

Open it with `/wta`, or let it pop up when you open the journal.

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

Open your in game journal next to the advisor and confirm the orange stickers in the mini grid match your journal. If the grid looks shifted or mirrored, report it; the fix is a one line index change in `Game/JournalReader.cs`.

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
