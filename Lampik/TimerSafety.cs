using System;

namespace Lampik;

/// <summary>Reject delayed power actions after sleep, clock adjustments or a stalled UI.</summary>
internal static class TimerSafety
{
    public static readonly TimeSpan WarningLead = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan LateTolerance = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SnoozeDuration = TimeSpan.FromMinutes(10);

    public static bool ShouldWarn(DateTime due, DateTime now) =>
        now < due && due - now <= WarningLead;

    public static bool ShouldExecute(DateTime due, DateTime now) =>
        now >= due && now - due <= LateTolerance;

    public static bool IsExpired(DateTime due, DateTime now) =>
        now - due > LateTolerance;

    public static DateTime Snooze(DateTime now) => now.Add(SnoozeDuration);
}
