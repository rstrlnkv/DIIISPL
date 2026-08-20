// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    /// <summary>
    /// Словари переводов: по одному на язык.
    ///
    /// Добавить язык — значит добавить сюда один метод и одну строку в список
    /// ниже. Ни один из четырёхсот вызовов в программе при этом не меняется:
    /// они обращаются к именованным членам <see cref="Text"/>, а те берут строку
    /// из словаря по ключу.
    ///
    /// Английский — запасной. Ключ, которого в языке нет, берётся из него, и это
    /// осознанный размен: неполный перевод показывает английскую строку, а не
    /// пустое место и не имя ключа. Полноту можно спросить у <see cref="Text.Missing"/>.
    ///
    /// Подстановки — через {0}, {1}: порядок слов в языках разный, и только так
    /// переводчик может его поменять, не трогая код.
    /// </summary>
    internal static class Lang
    {
        /// <summary>
        /// Языки в том порядке, в каком они появятся в окне. Первый — запасной.
        /// </summary>
        public static readonly string[] Codes = { "en", "ru" };

        /// <summary>Название языка на нём самом — так их и подписывают в окнах выбора.</summary>
        public static string NativeName(string code)
        {
            switch (code)
            {
                case "ru": return "Русский";
                default: return "English";
            }
        }

        public static Dictionary<string, string> Table(string code)
        {
            switch (code)
            {
                case "ru": return Ru();
                default: return En();
            }
        }

        // ------------------------------------------------------------------ English
        public static Dictionary<string, string> En()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);

            // общее
            d["app.tagline"] = "Monitor layouts";
            d["common.cancel"] = "Cancel";
            d["common.save"] = "Save";
            d["common.apply"] = "Apply";
            d["common.delete"] = "Delete";
            d["common.rename"] = "Rename";
            d["common.about"] = "About";
            d["common.exit"] = "Exit";
            d["common.hz"] = "Hz";

            // окно менеджера
            d["win.profiles"] = "PROFILES";
            d["win.newProfileTip"] = "New profile from the current layout";
            d["win.profileActionsTip"] = "Rename, reorder, delete";
            d["win.moveUp"] = "Move up";
            d["win.moveDown"] = "Move down";
            d["win.hotkeyButton"] = "Keyboard shortcut";
            d["win.pressCombo"] = "Press a shortcut…";
            d["win.monitors"] = "monitors: {0}";
            d["win.monitorsOf"] = "monitors: {0}/{1}";
            d["win.key"] = "key {0}";

            // свойства монитора
            d["prop.pick"] = "Select a monitor";
            d["prop.enabled"] = "On";
            d["prop.primary"] = "Primary";
            d["prop.resolution"] = "Resolution";
            d["prop.refresh"] = "Refresh rate";
            d["prop.rotation"] = "Rotation";
            d["prop.clone"] = "Duplicate";
            d["prop.hdr"] = "HDR";
            d["rot.none"] = "None";
            d["rot.90"] = "90° clockwise";
            d["rot.180"] = "180°";
            d["rot.270"] = "90° counter-clockwise";
            d["clone.off"] = "Separate desktop";
            d["clone.with"] = "Duplicate with “{0}”";
            d["hdr.keep"] = "Leave as is";
            d["hdr.on"] = "Turn on";
            d["hdr.off"] = "Turn off";

            // холст
            d["canvas.notInProfile"] = "NOT IN PROFILE · DRAG UP";
            d["canvas.notConnected"] = "not connected";
            d["canvas.badgePrimary"] = "Primary";
            d["canvas.badgeClone"] = "Duplicate";
            d["canvas.noneSelected"] = "No monitor is selected.";
            d["canvas.gap"] = "There is a gap between the monitors — put them edge to edge, or Windows will move them itself.";
            d["canvas.overlap"] = "“{0}” and “{1}” overlap — move them apart: desktops cannot overlap.";

            // применение
            d["apply.busy"] = "the screens are already being rearranged — wait and try again";
            d["apply.refused"] = "Windows refused the layout: {0}";
            d["apply.partly"] = "Windows accepted the layout only in part: {0}. What is on the screens may differ from the profile — check it and apply again.";
            d["apply.noTaskbarLib"] = "The profile sets a taskbar look, but the file needed for it is missing.";
            d["apply.taskbarPrefix"] = "Taskbar: {0}";
            d["apply.skipped"] = "“{0}” is not connected right now — skipped.";
            d["apply.noneConnected"] = "None of the profile’s monitors are connected.";
            d["apply.noPathList"] = "Windows did not return the display connection list.";
            d["apply.noFreeDesktop"] = "Could not place “{0}”: the graphics card does not offer that many independent desktops.";
            d["apply.cannotAttach"] = "“{0}” cannot be attached to the chosen desktop — skipped.";
            d["apply.nothingUsable"] = "Could not build the layout: no monitor could be used.";
            d["apply.driverRate"] = "The driver picked the refresh rate — Windows did not accept the one you set.";
            d["apply.gotMode"] = "“{0}”: got {1}×{2} instead of {3}×{4}.";
            d["apply.gotRate"] = "“{0}”: refresh rate {1} Hz instead of {2} Hz.";
            d["apply.noRotation"] = "“{0}”: Windows did not apply the screen rotation.";
            d["apply.notCloned"] = "“{0}” did not join the duplicate — Windows separated the screens.";
            d["apply.didNotTurnOn"] = "Did not turn on: {0}. Usually the monitor has no power, or sleeps so deeply the card cannot see it.";
            d["apply.stayedOn"] = "Stayed on against the profile: {0}.";
            d["apply.notPrimary"] = "“{0}” did not become primary — Windows chose another screen.";
            d["apply.moved"] = "Windows arranged them its own way: {0}. The screens no longer match the profile — fix the layout and apply again.";
            d["hdr.notInClone"] = "HDR does not work while duplicating. Separate the screens and it can be turned on for each.";
            d["hdr.unavailable"] = "“{0}”: this output does not offer HDR right now — there is nothing to turn on.";
            d["hdr.failedOn"] = "“{0}”: could not turn HDR on — {1}";
            d["hdr.failedOff"] = "“{0}”: could not turn HDR off — {1}";
            d["hdr.comesBack"] = "“{0}”: could not turn HDR off — the system turns it back on.";
            d["hdr.noOnCloned"] = "“{0}”: HDR did not turn on. Windows gives no wide colour to duplicated outputs: to use HDR, extend the screens instead.";
            d["hdr.noOnPlain"] = "“{0}”: HDR did not turn on. Usually the cable cannot carry it at the chosen mode.";

            // сочетания клавиш
            d["combo.empty"] = "empty shortcut";
            d["combo.noKey"] = "no key given in “{0}”";
            d["combo.unknownKey"] = "unknown key “{0}”";
            d["combo.needsModifier"] = "add Ctrl, Alt or Win — otherwise the key stops working in every other program";

            // мониторы и коды системы
            d["mon.notInUse"] = "not in use";
            d["mon.display"] = "Display {0}";
            d["mon.builtIn"] = "built-in";
            d["mon.digital"] = "digital";
            d["err.noRights"] = "the system did not allow changing the layout";
            d["err.driver"] = "the display driver could not apply it";
            d["err.hardware"] = "the hardware cannot do this";
            d["err.combination"] = "the graphics card rejects this combination of modes";
            d["err.impossible"] = "this layout is impossible on this hardware";
            d["err.unexplained"] = "the system gave no reason";

            // трей и запуск
            d["tray.startFailed"] = "Could not start {0}:\n\n{1}\n\nDetails: %AppData%\\{2}\\error.log";
            d["tray.hint"] = "DIIISPL keeps running here: the shortcuts still switch layouts. To quit for good, use “Exit” in this menu.";
            d["tray.newVersion"] = "Version {0} is out. Open “About” to update.";
            d["tray.noProfiles"] = "No profiles yet";
            d["tray.manager"] = "Layout manager…";
            d["tray.runAtLogon"] = "Start with Windows";
            d["tray.about"] = "About…";
            d["tray.busy"] = "The screens are being rearranged — wait a few seconds.";
            d["tray.hotkeyShared"] = "{0} — the same shortcut on “{1}” and “{2}”";
            d["tray.hotkeyTaken"] = "{0} — taken by another program";
            d["tray.hotkeysNotSet"] = "Could not assign: {0}";
            d["tray.applyFailed"] = "“{0}” failed: {1}";
            d["tray.applyDoneMore"] = "Layout “{0}” applied. {1} more note(s) — open the window.";
            d["tray.applyDone"] = "Layout “{0}” applied.";

            // обновления
            d["upd.gibberish"] = "GitHub answered with something unreadable";
            d["upd.onlyPre"] = "no finished releases yet — what is out is marked pre-release. Those arrive on the Dev channel";
            d["upd.noReleases"] = "no releases yet";
            d["upd.unreachable"] = "could not reach GitHub";
            d["upd.listUnreadable"] = "could not read the release list";
            d["upd.noInstaller"] = "this release has no installer attached";
            d["upd.badLink"] = "the installer link does not point where it should";
            d["upd.downloadFailed"] = "could not download the installer";
            d["upd.saveFailed"] = "could not save the installer — check the free disk space";
            d["upd.tampered"] = "the file was changed after signing — download the release again";
            d["upd.unconfirmed"] = "the installer’s signature cannot be confirmed: it is damaged or unsigned. Download the release again";
            d["upd.rejected"] = "the installer’s signature failed the check — download the release again";
            d["upd.foreignKey"] = "the installer is signed with a foreign key. Take the package only from the DIIISPL releases page";
            d["upd.startFailed"] = "could not start the installer — open the downloaded package by hand";
            d["upd.never"] = "not checked yet";
            d["upd.justNow"] = "checked just now";
            d["upd.minutes"] = "checked {0} min ago";
            d["upd.hours.one"] = "checked {0} hour ago";
            d["upd.hours.many"] = "checked {0} hours ago";
            d["upd.days.one"] = "checked {0} day ago";
            d["upd.days.many"] = "checked {0} days ago";

            // окно менеджера — подписи и подсказки
            d["win.renameTip"] = "Double-click or F2 to rename";
            d["win.primaryTip"] = "The taskbar and new windows live on the primary monitor";
            d["win.turnOnNote"] = "Turn the monitor on to set it up in this profile.";
            d["win.noProfilesHint"] = "No profiles yet. The “+” button above the list makes one from the way the monitors stand now.";
            d["win.activeDot"] = "This layout is applied right now";
            d["win.newProfileName"] = "New layout";
            d["win.defaultProfileName"] = "Layout";
            d["win.created"] = "Profile created from the current layout. Give it a name and press “Save”.";
            d["win.deleteTitle"] = "Delete the profile?";
            d["win.deleteBody"] = "“{0}” will be gone for good.";
            d["win.saveTitle"] = "Save the changes to “{0}”?";
            d["win.saveBody"] = "Otherwise they are lost. Esc — go back and change nothing.";
            d["win.dontSave"] = "Don’t save";
            d["win.saved"] = "Saved.";
            d["win.rebuilding"] = "Rearranging the screens…";
            d["win.moreNotes"] = " {0} more note(s) — open the window.";
            d["win.nameUnchanged"] = "The name is unchanged: profile “{0}” already exists.";
            d["win.nameTaken"] = "Profile “{0}” already exists — pick another name. Esc — keep the old one.";
            d["win.taskbarButton"] = "Taskbar";
            d["win.taskbarButtonN"] = "Taskbar · {0}";
            d["win.taskbarTip"] = "How the taskbar looks in this profile";
            d["win.taskbarMissing"] = "The file that draws the taskbar look is missing";
            d["win.oneMonitor"] = "one monitor";
            d["win.ownTaskbar"] = "its own taskbar look";
            d["win.nowOn"] = "on now";
            d["win.nowOff"] = "off now";
            d["win.nowUnplugged"] = "not connected right now";
            d["win.noHdrInClone"] = "HDR is unavailable while duplicating";
            d["rot.plain"] = "Normal";
            d["clone.separate"] = "Separate";
            d["clone.dup"] = "Duplicate: {0}";
            d["hdr.leave"] = "Leave alone";
            d["win.primaryOnlyOne"] = "There is always exactly one primary screen. To change it, mark “Primary” on another monitor.";
            d["win.cloneModes"] = "The resolutions differ — while duplicating, Windows fits the picture to a common mode.";
            d["win.pressComboHint"] = "Press a keyboard shortcut. Esc — cancel, Delete — remove the shortcut.";
            d["win.comboRemoved"] = "Shortcut removed.";
            d["win.comboImpossible"] = "That key cannot be assigned — pick another.";
            d["win.comboRejected"] = "{0} — {1}. Press another shortcut or Esc.";
            d["win.comboBusy"] = "{0} is already on profile “{1}”. Remove it there first: open “{1}”, press “Keyboard shortcut” and Delete.";
            d["win.comboSet"] = "Shortcut {0} assigned to profile “{1}”.";
            d["win.comboNeedsAutostart"] = " It works while the program is running — turn on “Start with Windows” in the tray menu.";
            return d;
        }

        // ------------------------------------------------------------------ русский
        public static Dictionary<string, string> Ru()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);

            d["app.tagline"] = "Раскладки мониторов";
            d["common.cancel"] = "Отмена";
            d["common.save"] = "Сохранить";
            d["common.apply"] = "Применить";
            d["common.delete"] = "Удалить";
            d["common.rename"] = "Переименовать";
            d["common.about"] = "О программе";
            d["common.exit"] = "Выход";
            d["common.hz"] = "Гц";

            d["win.profiles"] = "ПРОФИЛИ";
            d["win.newProfileTip"] = "Новый профиль из текущей раскладки мониторов";
            d["win.profileActionsTip"] = "Переименовать, переставить, удалить";
            d["win.moveUp"] = "Переместить вверх";
            d["win.moveDown"] = "Переместить вниз";
            d["win.hotkeyButton"] = "Сочетание клавиш";
            d["win.pressCombo"] = "Нажмите сочетание…";
            d["win.monitors"] = "мониторов: {0}";
            d["win.monitorsOf"] = "мониторов: {0}/{1}";
            d["win.key"] = "клавиша {0}";

            d["prop.pick"] = "Выберите монитор";
            d["prop.enabled"] = "Включён";
            d["prop.primary"] = "Основной";
            d["prop.resolution"] = "Разрешение";
            d["prop.refresh"] = "Частота";
            d["prop.rotation"] = "Поворот";
            d["prop.clone"] = "Дублирование";
            d["prop.hdr"] = "HDR";
            d["rot.none"] = "Без поворота";
            d["rot.90"] = "90° по часовой";
            d["rot.180"] = "180°";
            d["rot.270"] = "90° против часовой";
            d["clone.off"] = "Отдельный экран";
            d["clone.with"] = "Дублировать с «{0}»";
            d["hdr.keep"] = "Не трогать";
            d["hdr.on"] = "Включить";
            d["hdr.off"] = "Выключить";

            d["canvas.notInProfile"] = "НЕ В ПРОФИЛЕ · ПЕРЕТАЩИТЕ НАВЕРХ";
            d["canvas.notConnected"] = "не подключён";
            d["canvas.badgePrimary"] = "Основной";
            d["canvas.badgeClone"] = "Дубль";
            d["canvas.noneSelected"] = "Не выбран ни один монитор.";
            d["canvas.gap"] = "Между мониторами есть разрыв — придвиньте их вплотную, иначе Windows сдвинет их сама.";
            d["canvas.overlap"] = "«{0}» и «{1}» налезают друг на друга — разведите их: рабочие столы наложенными быть не могут.";

            d["apply.busy"] = "экраны уже перестраиваются — подождите и повторите";
            d["apply.refused"] = "Windows отклонила раскладку: {0}";
            d["apply.partly"] = "Windows приняла раскладку не целиком: {0}. На экране может стоять не то, что в профиле, — проверьте и примените заново.";
            d["apply.noTaskbarLib"] = "Профиль задаёт вид панели задач, но в программе не хватает файла для этого.";
            d["apply.taskbarPrefix"] = "Панель задач: {0}";
            d["apply.skipped"] = "«{0}» сейчас не подключён — пропущен.";
            d["apply.noneConnected"] = "Ни один монитор из профиля сейчас не подключён.";
            d["apply.noPathList"] = "Windows не отдала список подключений мониторов.";
            d["apply.noFreeDesktop"] = "Не удалось разместить «{0}»: видеокарта не даёт столько независимых рабочих столов.";
            d["apply.cannotAttach"] = "«{0}» нельзя подключить к выбранному рабочему столу — пропущен.";
            d["apply.nothingUsable"] = "Не получилось составить раскладку: ни один монитор не удалось задействовать.";
            d["apply.driverRate"] = "Частоту выбрал драйвер — заданную Windows не приняла.";
            d["apply.gotMode"] = "«{0}»: получилось {1}×{2} вместо {3}×{4}.";
            d["apply.gotRate"] = "«{0}»: частота {1} Гц вместо {2} Гц.";
            d["apply.noRotation"] = "«{0}»: поворот экрана Windows не применила.";
            d["apply.notCloned"] = "«{0}» не встал в дублирование — Windows развела экраны.";
            d["apply.didNotTurnOn"] = "Не включились: {0}. Чаще всего монитор обесточен или спит так, что видеокарта его не видит.";
            d["apply.stayedOn"] = "Остались включёнными вопреки профилю: {0}.";
            d["apply.notPrimary"] = "«{0}» не стал основным — Windows выбрала другой экран.";
            d["apply.moved"] = "Windows расставила по-своему: {0}. На экране теперь не то, что записано в профиле, — поправьте раскладку и примените заново.";
            d["hdr.notInClone"] = "При дублировании HDR не работает. Разведите экраны — тогда его можно включить на каждом.";
            d["hdr.unavailable"] = "«{0}»: этот выход сейчас не отдаёт HDR — включить нечего.";
            d["hdr.failedOn"] = "«{0}»: не удалось включить HDR — {1}";
            d["hdr.failedOff"] = "«{0}»: не удалось выключить HDR — {1}";
            d["hdr.comesBack"] = "«{0}»: не удалось выключить HDR — система включает его обратно.";
            d["hdr.noOnCloned"] = "«{0}»: HDR не включился. Выходам в дублировании Windows расширенный цвет не даёт: чтобы включить HDR, разведите экраны врастяжку.";
            d["hdr.noOnPlain"] = "«{0}»: HDR не включился. Обычно это нехватка пропускной способности кабеля на выбранном режиме.";

            d["combo.empty"] = "пустое сочетание";
            d["combo.noKey"] = "не указана клавиша в «{0}»";
            d["combo.unknownKey"] = "неизвестная клавиша «{0}»";
            d["combo.needsModifier"] = "добавьте Ctrl, Alt или Win — иначе клавиша перестанет работать в других программах";

            d["mon.notInUse"] = "не используется";
            d["mon.display"] = "Дисплей {0}";
            d["mon.builtIn"] = "встроенный";
            d["mon.digital"] = "цифровой";
            d["err.noRights"] = "система не дала прав на смену раскладки";
            d["err.driver"] = "видеодрайвер не смог её применить";
            d["err.hardware"] = "оборудование такого не умеет";
            d["err.combination"] = "видеокарта не принимает такое сочетание режимов";
            d["err.impossible"] = "на этом оборудовании такая раскладка невозможна";
            d["err.unexplained"] = "система не объяснила причину";

            d["tray.startFailed"] = "Не удалось запустить {0}:\n\n{1}\n\nПодробности: %AppData%\\{2}\\error.log";
            d["tray.hint"] = "DIIISPL продолжает работать здесь: горячие клавиши переключают раскладки. Совсем выйти — пункт «Выход» в этом меню.";
            d["tray.newVersion"] = "Вышла версия {0}. Открыть «О программе», чтобы обновиться.";
            d["tray.noProfiles"] = "Профилей пока нет";
            d["tray.manager"] = "Менеджер раскладок…";
            d["tray.runAtLogon"] = "Запускать при входе в Windows";
            d["tray.about"] = "О программе…";
            d["tray.busy"] = "Идёт перестроение экранов — подождите несколько секунд.";
            d["tray.hotkeyShared"] = "{0} — одно сочетание у «{1}» и «{2}»";
            d["tray.hotkeyTaken"] = "{0} — занято другой программой";
            d["tray.hotkeysNotSet"] = "Не удалось назначить: {0}";
            d["tray.applyFailed"] = "«{0}» не удалось: {1}";
            d["tray.applyDoneMore"] = "Раскладка «{0}» применена. Ещё замечаний: {1} — откройте окно.";
            d["tray.applyDone"] = "Раскладка «{0}» применена.";

            d["upd.gibberish"] = "GitHub ответил непонятным";
            d["upd.onlyPre"] = "готовых выпусков пока нет — то, что вышло, помечено предварительным. Такие приходят на канале Dev";
            d["upd.noReleases"] = "выпусков пока нет";
            d["upd.unreachable"] = "не удалось связаться с GitHub";
            d["upd.listUnreadable"] = "не удалось прочитать список выпусков";
            d["upd.noInstaller"] = "к этому выпуску не приложен установщик";
            d["upd.badLink"] = "ссылка на установщик ведёт не туда, куда должна";
            d["upd.downloadFailed"] = "не удалось скачать установщик";
            d["upd.saveFailed"] = "не удалось сохранить установщик — проверьте место на диске";
            d["upd.tampered"] = "файл изменён после подписания — скачайте выпуск заново";
            d["upd.unconfirmed"] = "подпись установщика не подтверждается: он повреждён или не подписан. Скачайте выпуск заново";
            d["upd.rejected"] = "подпись установщика не прошла проверку — скачайте выпуск заново";
            d["upd.foreignKey"] = "установщик подписан чужим ключом. Берите пакет только со страницы выпусков DIIISPL";
            d["upd.startFailed"] = "не удалось запустить установщик — откройте скачанный пакет вручную";
            d["upd.never"] = "ещё не проверялось";
            d["upd.justNow"] = "проверялось только что";
            d["upd.minutes"] = "проверялось {0} мин назад";

            // Русское склонение после числа. Три формы вместо английских двух —
            // поэтому ключи с суффиксом, а не «одно или много»: язык, у которого
            // форм окажется больше, добавит свои и опишет их в своём Plural.
            d["upd.hours.one"] = "проверялось {0} час назад";
            d["upd.hours.few"] = "проверялось {0} часа назад";
            d["upd.hours.many"] = "проверялось {0} часов назад";
            d["upd.days.one"] = "проверялось {0} день назад";
            d["upd.days.few"] = "проверялось {0} дня назад";
            d["upd.days.many"] = "проверялось {0} дней назад";

            d["win.renameTip"] = "Двойной щелчок или F2 — переименовать";
            d["win.primaryTip"] = "На основном мониторе живут панель задач и новые окна";
            d["win.turnOnNote"] = "Включите монитор, чтобы настроить его в этом профиле.";
            d["win.noProfilesHint"] = "Профилей пока нет. Кнопка «+» над списком создаст профиль из того, как мониторы стоят сейчас.";
            d["win.activeDot"] = "Эта раскладка применена сейчас";
            d["win.newProfileName"] = "Новая раскладка";
            d["win.defaultProfileName"] = "Раскладка";
            d["win.created"] = "Создан профиль из текущей раскладки. Назовите его и нажмите «Сохранить».";
            d["win.deleteTitle"] = "Удалить профиль?";
            d["win.deleteBody"] = "«{0}» будет удалён безвозвратно.";
            d["win.saveTitle"] = "Сохранить правки в «{0}»?";
            d["win.saveBody"] = "Иначе они пропадут. Esc — вернуться и ничего не менять.";
            d["win.dontSave"] = "Не сохранять";
            d["win.saved"] = "Сохранено.";
            d["win.rebuilding"] = "Перестраиваю экраны…";
            d["win.moreNotes"] = " Ещё замечаний: {0} — откройте окно.";
            d["win.nameUnchanged"] = "Имя не изменилось: профиль «{0}» уже есть.";
            d["win.nameTaken"] = "Профиль «{0}» уже есть — придумайте другое имя. Esc — оставить прежнее.";
            d["win.taskbarButton"] = "Панель задач";
            d["win.taskbarButtonN"] = "Панель задач · {0}";
            d["win.taskbarTip"] = "Как выглядит панель задач в этом профиле";
            d["win.taskbarMissing"] = "В программе не хватает файла, отвечающего за вид панели задач";
            d["win.oneMonitor"] = "один монитор";
            d["win.ownTaskbar"] = "свой вид панели задач";
            d["win.nowOn"] = "сейчас включён";
            d["win.nowOff"] = "сейчас выключен";
            d["win.nowUnplugged"] = "сейчас не подключён";
            d["win.noHdrInClone"] = "при дублировании HDR недоступен";
            d["rot.plain"] = "Обычный";
            d["clone.separate"] = "Отдельно";
            d["clone.dup"] = "Дубль: {0}";
            d["hdr.leave"] = "Не менять";
            d["win.primaryOnlyOne"] = "Основной экран всегда один. Чтобы сменить его, отметьте «Основной» у другого монитора.";
            d["win.cloneModes"] = "Разрешения различаются — при дублировании Windows подгонит картинку под общий режим.";
            d["win.pressComboHint"] = "Нажмите сочетание клавиш. Esc — отмена, Delete — убрать сочетание.";
            d["win.comboRemoved"] = "Сочетание убрано.";
            d["win.comboImpossible"] = "Такую клавишу назначить нельзя — выберите другую.";
            d["win.comboRejected"] = "{0} — {1}. Нажмите другое сочетание или Esc.";
            d["win.comboBusy"] = "{0} уже назначено профилю «{1}». Сначала снимите его там: откройте «{1}», нажмите «Сочетание клавиш» и Delete.";
            d["win.comboSet"] = "Сочетание {0} назначено профилю «{1}».";
            d["win.comboNeedsAutostart"] = " Оно работает, пока запущена программа — включите «Запускать при входе в Windows» в меню значка.";
            return d;
        }
    }
}
