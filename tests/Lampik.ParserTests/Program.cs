using Lampik;

// Optional local visual review harness; no power commands or persisted timers.
if (args.Contains("--pet-preview"))
{
    var thread = new System.Threading.Thread(() =>
    {
        var app = new System.Windows.Application();
        var pet = new PetWindow(new Preferences(), () => app.Shutdown(), () => { },
            () => { }, () => app.Shutdown(), (_, _) => { }) { ShowInTaskbar = true };
        app.Run(pet);
    });
    thread.SetApartmentState(System.Threading.ApartmentState.STA);
    thread.Start(); thread.Join();
    return;
}

var now = new DateTime(2026, 11, 1, 20, 0, 0);
var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Failed: " + name);
    Console.WriteLine("PASS " + name);
    count++;
}
bool Parse(string phrase, string language, out PowerCommand? command, out string response)
    => Commands.Parse(phrase, now, language, out command, out response);

Check(Parse("выключи через 30 минут", "ru", out var cmd, out _) &&
    cmd?.Action == PowerAction.Shutdown && cmd?.Target == now.AddMinutes(30), "Russian shutdown");
Check(Parse("shut down in 30 minutes", "en", out cmd, out _) &&
    cmd?.Target == now.AddMinutes(30), "English shutdown");
Check(Parse("restart at 11 pm", "en", out cmd, out _) &&
    cmd?.Action == PowerAction.Restart && cmd.Target == new DateTime(2026,11,1,23,0,0), "English 12-hour time");
Check(Parse("перезагрузи в 23:15", "en", out cmd, out _) &&
    cmd?.Action == PowerAction.Restart && cmd.Target == new DateTime(2026,11,1,23,15,0), "Russian command in English UI");
Check(Parse("sleep in an hour", "en", out cmd, out _) &&
    cmd?.Action == PowerAction.Sleep && cmd.Target == now.AddHours(1), "English one-hour words");
Check(Parse("сон через полтора часа", "ru", out cmd, out _) &&
    cmd?.Action == PowerAction.Sleep && cmd.Target == now.AddMinutes(90), "Russian one and a half hours");
Check(Parse("hibernate in half an hour", "en", out cmd, out _) &&
    cmd?.Action == PowerAction.Hibernate && cmd.Target == now.AddMinutes(30), "English half-hour");
Check(Parse("restart tomorrow at 3:00", "en", out cmd, out _) &&
    cmd?.Target == new DateTime(2026,11,2,3,0,0), "Tomorrow clock time");
Check(Parse("отмени", "en", out cmd, out var response) && response == "cancel", "Russian cancel in English UI");
Check(Parse("cancel", "ru", out cmd, out response) && response == "cancel", "English cancel in Russian UI");
Check(Parse("status", "en", out cmd, out response) && response == "status", "English status");
Check(Parse("помощь", "ru", out cmd, out response) && response == "help", "Russian help");
Check(!Parse("shut down sometime", "en", out cmd, out response) &&
    response.Contains("Try:"), "English input hint");
Check(!Parse("выключи сегодня в 19:00", "ru", out cmd, out response) &&
    response.Contains("прошло"), "Past-time validation");
Check(I18n.T("en","settings") == "Settings" && I18n.T("ru","settings") == "Настройки", "Translated settings");
Check(I18n.T("en","scheduled","Shutdown","23:00").Contains("23:00"), "Translated parameter");
Check(KeySyntax.Parse("Ctrl+6, Ctrl+0", out var strokes) && strokes.Length == 2, "Hotkey chord remains available");
Check(MascotState.Resolve(now, null, false, now, DateTime.MinValue) == MascotMood.Idle, "Mascot is calm when idle");
Check(MascotState.Resolve(now, null, true, now, DateTime.MinValue) == MascotMood.Listening, "Mascot listens while typing");
Check(MascotState.Resolve(now, now.AddMinutes(30), false, now, DateTime.MinValue) == MascotMood.Scheduled, "Mascot reacts to active timer");
Check(MascotState.Resolve(now, now.AddSeconds(45), true, now, now.AddSeconds(2)) == MascotMood.Warning, "Warning mood takes priority");
Check(MascotState.Resolve(now, now.AddMinutes(10), false, now, now.AddSeconds(2)) == MascotMood.Happy, "Mascot celebrates successful command");
Check(MascotState.Resolve(now, null, false, now.AddMinutes(-2), DateTime.MinValue) == MascotMood.Drowsy, "Mascot drowses after inactivity");
Check(I18n.T("ru", "inputPlaceholder") != I18n.T("en", "inputPlaceholder"), "Mini popup placeholder translated");
Check(I18n.T("ru", "miniHint").Contains("Ctrl+Alt+L"), "Quick popup shortcut hint available");
var icoPath = System.IO.Path.Combine("Lampik", "Assets", "lampik.ico");
Check(System.IO.File.Exists(icoPath), "Brand icon is checked into the repository");
var icoBytes = System.IO.File.ReadAllBytes(icoPath);
Check(icoBytes.Length > 2048 && icoBytes[0] == 0 && icoBytes[1] == 0 &&
    icoBytes[2] == 1 && icoBytes[3] == 0 && icoBytes[4] >= 2, "Windows icon container has multiple sizes");
Check(I18n.T("ru", "localChip") != I18n.T("en", "localChip"),
    "Online-free status badge has both localizations");
Check(I18n.T("en", "settingsCancel") == "Cancel", "Custom settings footer localized");

// Construct the settings view on an STA thread to catch startup/templating regressions.
// Do not Show() it: GitHub hosted runners have no interactive desktop.
Exception? uiError = null;
var uiThread = new System.Threading.Thread(() =>
{
    try
    {
        var dialog = new LampikSettingsWindow(new Preferences());
        Check(dialog.Content is not null, "Glass settings content constructed");
        dialog.Close();
    }
    catch (Exception ex) { uiError = ex; }
});
uiThread.SetApartmentState(System.Threading.ApartmentState.STA);
uiThread.Start();
uiThread.Join();
if (uiError is not null) throw new Exception("Glass settings failed to construct", uiError);
Check(uiError is null, "Glass settings window smoke test");



Check(Commands.Parse("восстанови", now, "ru", out cmd, out response) && response == "restore", "Russian recovery confirmation");
Check(Commands.Parse("restore", now, "en", out cmd, out response) && response == "restore", "English recovery confirmation");
var future = new TimerSnapshot { Active = true, Action = PowerAction.Shutdown, DueUtc = now.ToUniversalTime().AddMinutes(5) };
var recovered = TimerPersistence.Evaluate(future, now.ToUniversalTime());
Check(recovered.Pending?.Action == PowerAction.Shutdown && !recovered.Expired, "Future timer is pending only");
Check(TimerPersistence.Evaluate(future, now.ToUniversalTime().AddMinutes(6)).Expired, "Expired timer cannot be armed");
Check(TimerPersistence.Evaluate(new TimerSnapshot { Active = false }, now.ToUniversalTime()).Pending is null, "Cancellation tombstone prevents recovery");
Check(TimerPersistence.Evaluate(new TimerSnapshot { Active = true, Action = PowerAction.Shutdown, DueUtc = now.ToUniversalTime().AddDays(40) }, now.ToUniversalTime()).Expired, "Invalid recovery horizon blocked");
Check(WindowsAutoStart.CommandFor(@"C:\Program Files\Lampik\Lampik.exe").Contains(" --tray"), "Autostart uses tray mode");
Check(I18n.T("ru", "autostart") != I18n.T("en", "autostart"), "Autostart setting is localized");

Check(TimerSafety.ShouldWarn(now.AddSeconds(59), now), "Warning appears at T-59 seconds");
Check(!TimerSafety.ShouldWarn(now.AddMinutes(2), now), "No warning before final minute");
Check(TimerSafety.ShouldExecute(now, now.AddSeconds(1)), "On-time action allowed");
Check(!TimerSafety.ShouldExecute(now, now.AddSeconds(8)), "Late wake-up never powers off");
Check(TimerSafety.IsExpired(now, now.AddSeconds(8)), "Late wake-up detected");
Check(TimerSafety.Snooze(now) == now.AddMinutes(10), "Snooze extends by exactly ten minutes");
Check(I18n.T("ru", "cancelTimer") != I18n.T("en", "cancelTimer"), "Large cancel button bilingual");
Check(I18n.T("ru", "snoozeTen") != I18n.T("en", "snoozeTen"), "Snooze button bilingual");

// Build the new dialog without displaying it; tests run on a headless Windows runner.
Exception? warningError = null;
var warningThread = new System.Threading.Thread(() =>
{
    try
    {
        var warning = new PowerWarningWindow("ru", PowerAction.Shutdown, () => { }, () => { });
        Check(warning.Content is not null, "Power warning glass dialog created");
        warning.UpdateRemaining(TimeSpan.FromSeconds(29));
        warning.Dismiss();
    }
    catch (Exception exception) { warningError = exception; }
});
warningThread.SetApartmentState(System.Threading.ApartmentState.STA);
warningThread.Start();
warningThread.Join();
if (warningError is not null) throw new Exception("Power warning smoke test failed", warningError);

// Verify the real native input region, not just WPF hit testing.
Exception? petError = null;
var petThread = new System.Threading.Thread(() =>
{
    try
    {
        int opened = 0, cancelled = 0;
        var pet = new PetWindow(new Preferences { PetX = double.NaN, PetY = double.PositiveInfinity },
            () => opened++, () => cancelled++, () => { }, () => { }, (_, _) => { });
        var handle = new System.Windows.Interop.WindowInteropHelper(pet).EnsureHandle();
        Check(pet.AllowsTransparency && pet.Topmost && !pet.ShowInTaskbar &&
            pet.WindowStyle == System.Windows.WindowStyle.None, "Pet has transparent borderless topmost surface");
        Check(double.IsFinite(pet.Left) && double.IsFinite(pet.Top), "Invalid saved pet position recovers safely");
        var region = NativeRegion.CreateRectRgn(0, 0, 0, 0);
        try
        {
            Check(NativeRegion.GetWindowRgn(handle, region) != 0, "Pet installs native input region");
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(pet);
            Check(!NativeRegion.PtInRegion(region, 0, 0), "Transparent corner passes clicks to desktop");
            Check(NativeRegion.PtInRegion(region, (int)(110 * dpi.DpiScaleX), (int)(70 * dpi.DpiScaleY)),
                "Bulb body remains clickable");
        }
        finally { NativeRegion.DeleteObject(region); }
        pet.UpdateLanguage("ru");
        var open = (System.Windows.Controls.MenuItem)pet.ContextMenu.Items[0];
        Check((string)open.Header == I18n.T("ru", "open"), "Pet menu Russian");
        open.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        ((System.Windows.Controls.MenuItem)pet.ContextMenu.Items[1]).RaiseEvent(
            new System.Windows.RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        Check(opened == 1 && cancelled == 1, "Pet menu routes open and cancellation");
        pet.UpdateLanguage("en");
        Check((string)open.Header == I18n.T("en", "open"), "Pet menu English");
        pet.Close();
    }
    catch (Exception ex) { petError = ex; }
});
petThread.SetApartmentState(System.Threading.ApartmentState.STA);
petThread.Start(); petThread.Join();
if (petError is not null) throw new Exception("Pet smoke test failed", petError);
Console.WriteLine($"All {count} checks passed.");

internal static class NativeRegion
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] internal static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [System.Runtime.InteropServices.DllImport("user32.dll")] internal static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] internal static extern bool PtInRegion(IntPtr region, int x, int y);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
}
