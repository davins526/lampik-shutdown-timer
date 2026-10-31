# Lampik v0.3.0-beta.2 — русский и английский / Russian & English

**Pre-release:** компактный Windows-таймер выключения со светящейся лампочкой. This is an early beta and is not intended for critical unattended shutdown tasks.

## Новое / What's new

- **Два языка / Two languages:** переключатель **Русский / English** в настройках; автоподбор языка при первом запуске по языку Windows; сохранение выбранного языка
- Переведены заголовки, настройки, подсказки, сообщения об ошибках, предупреждения и меню системного трея
- **Команды на двух языках / Commands in both languages** работают независимо от языка интерфейса:
  - «выключи через 30 минут» / “shut down in 30 minutes”
  - «сон через час» / “sleep in an hour”
  - «перезагрузи в 23:00» / “restart at 11 pm”
  - «гибернация через полчаса» / “hibernate in half an hour”
  - «отмени» / “cancel”
- Установщик Inno Setup предлагает русский и английский / Installer supports English and Russian
- Добавлены автоматические smoke-тесты парсера и переводов / Added bilingual automated smoke tests

## Осталось из Beta 1 / Carried over

- Компактное окно и мини-режим, поверх окон / Compact always-on-top widget and mini mode
- Светящийся персонаж, тёмная тема / Glowing mascot and dark theme
- Таймер обратного отсчёта, трей / Countdown timer and tray
- Переназначаемые глобальные горячие клавиши / Customizable global hotkeys
- Оповещение за минуту и отмена / One-minute warning and cancellation

## Загрузки / Downloads

- **Lampik-Setup-0.3.0-beta.2-win-x64.exe** — установщик / installer
- **Lampik.exe** — автономная версия / portable self-contained version

## Важно / Known limitations

- **Beta:** хотя сборка и smoke-тесты должны проходить в GitHub Actions, приложение ещё необходимо протестировать вручную на Windows 10/11. Please test carefully on a real PC.
- До тестирования сохраняйте открытые документы: Windows может завершить работу и закрыть приложения. Save unsaved work before testing shutdown.
- Таймер срабатывает, только пока Лампик запущен и Windows не спит / Timers require Lampik to remain running and Windows to be awake
- Глобальные сочетания могут конфликтовать с другими приложениями / Shortcut collisions are possible
- Установщик не имеет цифровой подписи / Installer is unsigned
