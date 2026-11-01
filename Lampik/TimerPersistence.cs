using System;
using System.IO;
using System.Text.Json;

namespace Lampik;

// Restored timers are NEVER armed silently. They remain a pending suggestion until
// the user explicitly types "restore" / "восстанови". A stale cancelled snapshot
// cannot trigger a power command on application startup.
internal sealed class TimerSnapshot
{
    public bool Active { get; set; }
    public PowerAction Action { get; set; }
    public DateTime DueUtc { get; set; }
}

internal readonly record struct TimerRecovery(PowerCommand? Pending, bool Expired);

internal static class TimerPersistence
{
    private static readonly string StateFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lampik", "timer.json");

    public static TimerRecovery Evaluate(TimerSnapshot? snapshot, DateTime nowUtc)
    {
        if (snapshot is null || !snapshot.Active)
            return new TimerRecovery(null, false);

        var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var due = DateTime.SpecifyKind(snapshot.DueUtc, DateTimeKind.Utc);
        var remaining = due - utc;
        if (!Enum.IsDefined(snapshot.Action) || remaining <= TimeSpan.Zero ||
            remaining > TimeSpan.FromDays(30))
            return new TimerRecovery(null, true);

        return new TimerRecovery(new PowerCommand(snapshot.Action, due.ToLocalTime()), false);
    }

    public static TimerRecovery Load(DateTime nowUtc)
    {
        if (!File.Exists(StateFile)) return new TimerRecovery(null, false);
        try
        {
            var snapshot = JsonSerializer.Deserialize<TimerSnapshot>(File.ReadAllText(StateFile));
            var result = Evaluate(snapshot, nowUtc);
            if (result.Expired) Cancel();
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Invalid state must never result in a power action.
            return new TimerRecovery(null, true);
        }
    }

    public static void Save(PowerAction action, DateTime dueLocal)
    {
        Write(new TimerSnapshot
        {
            Active = true,
            Action = action,
            DueUtc = dueLocal.ToUniversalTime()
        });
    }

    // Write a tombstone rather than merely deleting the file: if cancellation
    // occurs before a restart, no old schedule should be restored.
    public static void Cancel() => Write(new TimerSnapshot { Active = false });

    private static void Write(TimerSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
        string temp = StateFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
        File.Move(temp, StateFile, overwrite: true);
    }
}
