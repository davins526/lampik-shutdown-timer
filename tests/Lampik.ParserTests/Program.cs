using Lampik;

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
Console.WriteLine($"All {count} checks passed.");
