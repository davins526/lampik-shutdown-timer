using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lampik;

internal static class I18n
{
    private static readonly Dictionary<string, (string Ru, string En)> Strings = new(StringComparer.Ordinal)
    {
        ["title"] = ("Лампик — таймер выключения", "Lampik — shutdown timer"),
        ["appName"] = ("Лампик", "Lampik"),
        ["question"] = ("Когда выключаем компьютер?", "When should I turn off your PC?"),
        ["idle"] = ("Лампик на связи", "Lampik is ready"),
        ["noTimer"] = ("Таймер не запущен", "No timer scheduled"),
        ["open"] = ("Открыть", "Open"),
        ["settings"] = ("Настройки", "Settings"),
        ["hide"] = ("Свернуть в питомца", "Switch to Pet Mode"),
        ["mini"] = ("Режим питомца", "Pet Mode"),
        ["miniHint"] = ("Карточка ↔ питомец · Ctrl+Alt+L", "Card ↔ Pet · Ctrl+Alt+L"),
        ["inputPlaceholder"] = ("Напиши команду...", "Type a command..."),
        ["localChip"] = ("● ЛОКАЛЬНО", "● OFFLINE"),
        ["shortcutColumn"] = ("Сочетание", "Shortcut"),
        ["settingsHint"] = ("Изменения сохраняются локально", "Saved on this device"),
        ["settingsCancel"] = ("Отмена", "Cancel"),
        ["send"] = ("Отправить команду", "Send command"),
        ["example"] = ("Например: выключи через 30 минут", "E.g. shut down in 30 minutes"),
        ["cancelTimer"] = ("Отменить таймер", "Cancel timer"),
        ["exit"] = ("Выход", "Exit"),
        ["appearance"] = ("Внешний вид", "Appearance"),
        ["language"] = ("Язык / Language", "Language / Язык"),
        ["dark"] = ("Тёмная тема (светящаяся лампочка)", "Dark theme (glowing bulb)"),
        ["autostart"] = ("Запускать вместе с Windows", "Launch with Windows"),
        ["restorePrompt"] = ("Найден сохранённый таймер. Напиши «восстанови», чтобы продолжить, или «отмени».", "Saved timer found. Type “restore” to resume, or “cancel”."),
        ["restoreExpired"] = ("Старый таймер просрочен и не запущен.", "Expired timer discarded; no power action started."),
        ["persistFailed"] = ("Не удалось сохранить таймер на диск.", "Could not save timer to disk."),
        ["hotkeyConflict"] = ("Сочетания конфликтуют. Измени клавиши.", "Keyboard shortcuts conflict. Choose different keys."),
        ["onTop"] = ("Поверх всех окон", "Always on top"),
        ["hotkeys"] = ("Глобальные горячие клавиши", "Global keyboard shortcuts"),
        ["addHotkey"] = ("+ Сочетание", "+ Shortcut"),
        ["hotkeyHint"] = ("Ctrl+1 или Ctrl+6, Ctrl+0 (два нажатия подряд)", "Ctrl+1 or Ctrl+6, Ctrl+0 (two strokes)"),
        ["minutes"] = ("Минуты", "Minutes"),
        ["save"] = ("Сохранить", "Save"),
        ["invalidHotkey"] = ("Проверь сочетание и число минут (1–43200).", "Check the shortcut and minutes (1–43200)."),
        ["saveError"] = ("Не удалось сохранить настройки", "Could not save settings"),
        ["powerError"] = ("Ошибка Windows: {0}", "Windows error: {0}"),
        ["winError"] = ("Ошибка Windows", "Windows error"),
        ["commandSent"] = ("Команда передана Windows.", "Command sent to Windows."),
        ["shutdown"] = ("Выключение", "Shutdown"),
        ["restart"] = ("Перезагрузка", "Restart"),
        ["sleep"] = ("Сон", "Sleep"),
        ["hibernate"] = ("Гибернация", "Hibernate"),
        ["scheduled"] = ("{0} в {1}. Ctrl+Alt+X — отмена.", "{0} at {1}. Ctrl+Alt+X to cancel."),
        ["statusScheduled"] = ("Запланировано на {0}", "Scheduled for {0}"),
        ["cancelled"] = ("Таймер отменён.", "Timer cancelled."),
        ["cancelNoTimer"] = ("Таймер сейчас не запущен.", "There is no active timer."),
        ["help"] = ("Напиши: «выключи через час», «сон через 20 минут» или «отмени».",
                    "Try: “shut down in an hour”, “sleep in 20 minutes” or “cancel”."),
        ["late"] = ("Таймер просрочен после сна Windows и отменён.", "Timer expired during Windows sleep and was cancelled."),
        ["warningHeading"] = ("Скоро выключение", "Power action in progress"),
        ["warningAction"] = ("Действие: {0}", "Action: {0}"),
        ["snoozeTen"] = ("+10 минут", "+10 minutes"),
        ["snoozed"] = ("Таймер продлён на 10 минут.", "Timer extended by 10 minutes."),
        ["soonTitle"] = ("Лампик — скоро действие", "Lampik — action approaching"),
        ["soonBody"] = ("{0} менее чем через минуту. Ctrl+Alt+X — отменить.",
                        "{0} in under a minute. Ctrl+Alt+X to cancel."),
        ["trayActiveTitle"] = ("Лампик продолжает работать", "Lampik is still running"),
        ["trayActiveBody"] = ("Таймер активен в трее.", "Your timer is active in the system tray."),
        ["quitConfirm"] = ("Таймер будет отменён. Завершить Лампика?",
                           "The timer will be cancelled. Exit Lampik?"),
        ["chordBusy"] = ("Вторая комбинация занята другой программой.",
                         "The second shortcut is reserved by another app."),
        ["windowsRejected"] = ("Windows не приняла команду.", "Windows did not accept the command."),
        ["powerUnavailable"] = ("Этот режим питания недоступен.", "This power mode is not available."),
        ["timePast"] = ("Это время сегодня уже прошло.", "That time has already passed today."),
        ["prompt"] = ("Напиши, например: «выключи через 40 минут» или «в 23:30».",
                      "Try: “shut down in 40 minutes” or “at 11:30 pm”."),
        ["range"] = ("Доступен интервал от 10 секунд до 30 дней.", "Choose a time between 10 seconds and 30 days."),
    };

    public static string Language(string? selected) => selected == "en" ? "en" : "ru";

    public static string T(string language, string key, params object[] values)
    {
        if (!Strings.TryGetValue(key, out var pair)) return key;
        var template = Language(language) == "en" ? pair.En : pair.Ru;
        return values.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, values);
    }
}
