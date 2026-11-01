using System;
using System.IO;
using Microsoft.Win32;

namespace Lampik;

// HKCU Run does not request elevation and is reversible in Settings.
internal static class WindowsAutoStart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Lampik";

    public static string CommandFor(string executable) =>
        "\"" + executable.Replace("\"", "") + "\" --tray";

    // Keep unrelated settings independent of registry permissions.
    internal static bool TryApplyChange(bool previous, bool requested, Action<bool> apply, out bool effective)
    {
        effective = previous;
        if (previous == requested) return true;
        try { apply(requested); effective = requested; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return false; }
    }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new IOException("Cannot open current-user startup registry key.");
        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
                throw new IOException("Application executable location is unavailable.");
            key.SetValue(ValueName, CommandFor(exe), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
