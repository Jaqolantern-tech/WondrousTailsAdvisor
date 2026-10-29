using System;
using GamePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace TailsAdvisor.Game;

/// <summary>A read only copy of your Wondrous Tails journal.</summary>
public readonly record struct JournalSnapshot(
    bool HasJournal,
    bool Expired,
    int Mask,
    int SecondChancePoints,
    int StickersWaiting,
    DateTime? ExpiresUtc);

/// <summary>
/// Reads the journal straight from the game's player state. This only reads memory; it never
/// sends anything to the server or changes your journal.
/// </summary>
public static unsafe class JournalReader
{
    public static JournalSnapshot? Read()
    {
        var ps = GamePlayerState.Instance();
        if (ps == null) return null;

        if (!ps->HasWeeklyBingoJournal)
            return new JournalSnapshot(false, false, 0, (int)ps->WeeklyBingoNumSecondChancePoints, 0, null);

        var mask = 0;
        var waiting = 0;
        for (var i = 0; i < 16; i++)
        {
            if (ps->IsWeeklyBingoStickerPlaced(i)) mask |= 1 << i;
            if (ps->GetWeeklyBingoTaskStatus(i) == GamePlayerState.WeeklyBingoTaskStatus.Claimable) waiting++;
        }

        DateTime? expires = null;
        try
        {
            expires = ps->WeeklyBingoExpireDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Bad timestamp while loading; ignore.
        }

        return new JournalSnapshot(
            true,
            ps->IsWeeklyBingoExpired(),
            mask,
            (int)ps->WeeklyBingoNumSecondChancePoints,
            waiting,
            expires);
    }
}
