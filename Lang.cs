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
    /// пустое место и не имя ключа. Сколько строк ещё английские, скажет `/lang`.
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
            d["common.autoPick"] = "Pick automatically";
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
            d["win.key"] = "shortcut {0}";

            // свойства монитора
            d["prop.pick"] = "Select a monitor";
            d["prop.enabled"] = "On";
            d["prop.primary"] = "Primary";
            d["prop.resolution"] = "Resolution";
            d["prop.refresh"] = "Refresh rate";
            d["prop.rotation"] = "Rotation";
            d["prop.clone"] = "Duplicate";
            d["prop.hdr"] = "HDR";
            d["rot.90"] = "90° right";
            d["rot.180"] = "180°";
            d["rot.270"] = "90° left";
            d["hdr.on"] = "Turn on";
            d["hdr.off"] = "Turn off";

            // холст
            d["canvas.notInProfile"] = "NOT IN PROFILE · DRAG UP";
            d["canvas.notConnected"] = "not connected";
            d["canvas.badgePrimary"] = "Primary";
            d["canvas.badgeClone"] = "Duplicate";
            d["canvas.noneSelected"] = "No monitor is selected.";
            d["canvas.gap"] = "There is a gap between the monitors — put them edge to edge, or Windows will move them for you.";
            d["canvas.overlap"] = "“{0}” and “{1}” run into each other — move them apart: desktops cannot overlap.";

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
            d["apply.didNotTurnOn"] = "Did not turn on: {0}. Usually the monitor has no power, or sleeps so deeply the graphics card cannot see it.";
            d["apply.stayedOn"] = "Still on, though the profile turns them off: {0}.";
            d["apply.notPrimary"] = "“{0}” did not become primary — Windows chose another screen.";
            d["apply.moved"] = "Windows rearranged them on its own: {0}. The screens no longer match the profile — fix the layout and apply again.";
            d["hdr.notInClone"] = "HDR does not work while duplicating. Separate the screens and it can be turned on for each.";
            d["hdr.unavailable"] = "“{0}”: this output does not offer HDR right now — there is nothing to turn on.";
            d["hdr.failedOn"] = "“{0}”: could not turn HDR on — {1}";
            d["hdr.failedOff"] = "“{0}”: could not turn HDR off — {1}";
            d["hdr.comesBack"] = "“{0}”: could not turn HDR off — the system turns it back on.";
            d["hdr.noOnCloned"] = "“{0}”: HDR did not turn on. Windows gives no advanced colour to duplicated outputs: to use HDR, extend the screens instead.";
            d["hdr.noOnPlain"] = "“{0}”: HDR did not turn on. Usually the cable cannot carry it at the chosen mode.";

            // сочетания клавиш
            d["err.hotkeyProblem"] = "{0} — {1}";
            d["combo.empty"] = "empty shortcut";
            d["combo.noKey"] = "no key given in “{0}”";
            d["combo.unknownKey"] = "unknown key “{0}”";
            d["combo.needsModifier"] = "add Ctrl, Alt or Win — otherwise the key stops working in every other program";

            // мониторы и коды системы
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
            d["tray.autoSwitch"] = "Follow the monitors";
            d["tray.tbReset"] = "Reset the taskbar look";
            d["tray.autoSwitchTip"] = "Apply the profile that matches the monitors you plugged in";
            d["auto.switched"] = "Profile “{0}” applied: the set of monitors changed.";
            d["tray.about"] = "About…";
            d["tray.busy"] = "The screens are being rearranged — wait a few seconds.";
            d["tray.hotkeyShared"] = "{0} — the same shortcut on “{1}” and “{2}”";
            d["tray.hotkeyTaken"] = "{0} — taken by another program";
            d["tray.hotkeysNotSet"] = "Could not assign: {0}";
            d["tray.applyFailed"] = "“{0}” failed: {1}";
            d["tray.applyDoneMore"] = "Layout “{0}” applied. There is more to read: {1} — open the window.";
            d["tray.applyDone"] = "Layout “{0}” applied.";

            // обновления
            d["upd.gibberish"] = "GitHub answered with something unreadable";
            d["upd.onlyPre"] = "no finished releases yet — what is out is marked pre-release. Those arrive on the Dev channel";
            d["upd.noReleases"] = "no releases yet";
            // Заголовок раздела в описании выпуска — по нему программа находит
            // свой язык. Должен совпадать с тем, что пишет сборщик в тело выпуска.
            d["upd.notesHeading"] = "What's new in";
            d["upd.moreNotes"] = "and {0} more — open the page";
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
            d["win.moreNotes"] = " and {0} more — open the window.";
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
            d["win.nowUnplugged"] = "not connected now";
            d["win.noHdrInClone"] = "HDR is unavailable while duplicating";
            d["rot.plain"] = "Normal";
            d["clone.separate"] = "Separate";
            d["clone.dup"] = "Together with {0}";
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

            // командная строка
            d["cli.unknownCmd"] = "I do not know the command “{0}”. Here is what there is:";
            d["cli.error"] = "Error: {0}";
            d["cli.title"] = "DIIISPL — monitor layouts";
            d["cli.help.window"] = "  DIIISPL.exe                 open the manager window";
            d["cli.help.apply"] = "  DIIISPL.exe /apply \"Name\"   apply a profile";
            d["cli.help.save"] = "  DIIISPL.exe /save \"Name\"    write the current layout into a profile";
            d["cli.help.list"] = "  DIIISPL.exe /list           list the profiles";
            d["cli.help.delete"] = "  DIIISPL.exe /delete \"Name\"  delete a profile";
            d["cli.help.monitors"] = "  DIIISPL.exe /monitors       what is connected and how it is identified";
            d["cli.help.hdr"] = "  DIIISPL.exe /hdr            HDR state on every output";
            d["cli.help.state"] = "  DIIISPL.exe /state          the same for scripts: one line per monitor, fields split by tabs";
            d["cli.help.auto"] = "  DIIISPL.exe /auto           which profile matches the monitors now, and what the program would do";
            d["auto.connected"] = "Monitors connected: {0}";
            d["auto.on"] = "Following the monitors: on.";
            d["auto.off"] = "Following the monitors: off — the program will not switch anything by itself.";
            d["auto.noFit"] = "No profile fits these monitors, so there is nothing to switch to.";
            d["auto.fits"] = "Matching profile: “{0}”.";
            d["auto.already"] = "It is already applied — nothing to change.";
            d["auto.would"] = "Had the set just changed, the program would apply it.";
            d["auto.excluded"] = "Profiles excluded from automatic picking: {0}.";
            d["cli.help.taskbar"] = "  DIIISPL.exe /taskbar look [AARRGGBB] [line]   taskbar look right now";
            d["cli.help.taskbarState"] = "  DIIISPL.exe /taskbar-state [\"Name\"]           what is on the screens and which look follows";
            d["cli.noMonitors"] = "  no monitors found";
            d["cli.key"] = "    key:       {0}";
            d["cli.state"] = "    state:     {0}";
            d["cli.on"] = "on, {0}x{1} @ {2},{3}, {4} Hz, source {5}";
            d["cli.primarySuffix"] = ", primary";
            d["cli.off"] = "off";
            d["cli.hdrLine"] = "    HDR:       {0}";
            d["cli.hdrOnSupported"] = "supported, on";
            d["cli.hdrOffSupported"] = "supported, off";
            d["cli.hdrNone"] = "unavailable";
            d["cli.preferred"] = "    preferred: {0}x{1} {2} Hz";
            d["cli.modes"] = "    modes:     {0}{1}";
            d["cli.modesApprox"] = " (the list is approximate)";
            d["cli.outputs"] = "    outputs:   source {0}";
            d["cli.profileNotFound"] = "  profile not found: {0}";
            d["cli.profileIs"] = "  profile: {0}";
            d["cli.colorLine"] = "{0}, colour {1}, line {2}";
            d["cli.yes"] = "yes";
            d["cli.no"] = "no";
            d["cli.stateOff"] = "off";
            d["cli.noTaskbarFile"] = "  The file that draws the taskbar look is missing.";
            d["cli.pickLook"] = "  Pick a look: {0}";
            d["cli.barsFound"] = "  taskbars found: {0}";
            d["cli.lineOnly"] = "  the last word can only be “line”";
            d["cli.colorFormat"] = "  the colour is written as AARRGGBB, or shorter — RRGGBB";
            d["cli.applied"] = "  applied: {0}";
            d["cli.failed"] = "  failed: {0}";
            d["cli.modeLine"] = "    mode: {0}";
            d["cli.hdrShort"] = "    HDR:  {0}";
            d["cli.hdrOnCaps"] = "ON";
            d["cli.hdrNoInClone"] = "unavailable: the output is duplicated";
            d["cli.hdrNoAtMode"] = "unavailable at this mode";
            d["cli.cloneLine"] = "  Duplicate: {0}  →  {1}";
            d["cli.cloneNote"] = "  (inside a duplicate group HDR must be the same on every output)";
            d["cli.noProfiles"] = "  no profiles";
            d["cli.profileRow"] = "  {0}   monitors: {1}/{2}{3}";
            d["cli.needName"] = "Give a profile name.";
            d["cli.sameName"] = "“{0}” and “{1}” are the same name to the program.";
            d["cli.sameNameHint"] = "Pick another name, or re-take the existing one: /save \"{0}\"";
            d["cli.retaken"] = "Re-taken: {0}";
            d["cli.saved"] = "Saved: {0}";
            d["cli.notFound"] = "Profile not found: {0}";
            d["cli.appliedName"] = "Applied: {0}";
            d["cli.deleted"] = "Deleted: {0}";
            d["cli.help.lang"] = "  DIIISPL.exe /lang cs        write a translation template for the language \"cs\"";
            d["cli.lang.needCode"] = "  Give a language code, for example: /lang cs";
            d["cli.lang.badCode"] = "  A language code is a short word without slashes or colons: cs, de, pl.";
            d["cli.lang.written"] = "  Template written: {0}";
            d["cli.lang.left"] = "  Not translated yet: {0}";
            d["cli.lang.howTo"] = "  Translate the values after \"=\", save as UTF-8, and pick the language in About.";

            // «О программе»
            d["about.tagline"] = "Monitor layouts for Windows 11";
            d["about.version"] = "VERSION";
            d["about.build"] = "BUILD";
            d["about.profiles"] = "PROFILES";
            d["about.author"] = "Author";
            d["about.authorName"] = "Rostislav Strelnikov";
            d["about.channel"] = "Update channel";
            d["about.language"] = "Language";
            d["about.whatsNew"] = "What’s new";
            d["changelog.yours"] = "you have this one";
            d["changelog.empty"] = "This build carries no changelog inside.";
            d["about.footer"] = "© 2026 {0} · GPL-3.0";
            d["about.devHint"] = "Dev builds arrive before the general release and can do more — set the taskbar look, for one. In exchange, they break more often.";
            d["about.stableHint"] = "Only finished releases arrive. What is still being worked on — the taskbar look — is in Dev builds only.";
            d["about.check"] = "Check";
            d["about.checking"] = "checking…";
            d["about.again"] = "Again";
            d["about.upToDate"] = "you have the latest version";
            d["about.newVersion"] = "version {0} is out";
            d["about.update"] = "Update";
            d["about.downgradeTitle"] = "Move to the general release?";
            d["about.downgradeBody"] = "This is a Dev build. Version {0} has no taskbar settings — the section will disappear from the program. The look you already set stays written in the profiles and comes back if you install a Dev build again.";
            d["about.downgradeOk"] = "Install anyway";
            d["about.downloading"] = "downloading the installer…";
            d["about.installerStarted"] = "the installer has started";
            d["about.langRestart"] = "The program will restart to switch the language.";
            d["about.langRestartTitle"] = "Switch the language to {0}?";
            d["about.langRestartOk"] = "Switch and restart";
            d["about.langRestartFailed"] = "Could not restart to switch the language — the program stays as it was.";

            // панель задач — только в сборках Dev
            d["tb.desktop"] = "Desktop is visible";
            d["tb.visibleWindow"] = "A window is open";
            d["tb.maximized"] = "A window is full screen";
            d["tb.start"] = "Start is open";
            d["tb.search"] = "Search is open";
            d["tb.taskView"] = "Task view";
            d["tb.battery"] = "Battery saver";
            d["tb.noTaskbar"] = "no taskbar found";
            d["tb.noExplorer"] = "could not connect to Explorer";
            d["tb.lostExplorer"] = "the link to Explorer is lost";
            d["tb.look.default"] = "As in the system";
            d["tb.look.opaque"] = "Opaque";
            d["tb.look.clear"] = "Transparent";
            d["tb.look.blur"] = "Blurred";
            d["tb.look.acrylic"] = "Acrylic";
            d["tb.look.keep"] = "Leave alone";
            d["tb.signals"] = "  signals: battery saver={0}, Start={1}, search={2}, task view={3}";
            d["tb.barsFound"] = "  taskbars: {0}";
            d["tb.barLine"] = "  bar 0x{0}: full-screen window={1}, open window={2}  →  {3} ({4})";
            d["tb.stateOff"] = "state is off";
            d["tb.win.title"] = "Taskbar";
            d["tb.win.subtitle"] = "Profile “{0}”";
            d["tb.win.done"] = "Done";
            d["tb.win.reset"] = "Reset the look";
            d["tb.win.resetTip"] = "Forget what has already been applied and paint the taskbars anew — for when the look got stuck from a previous layout";
            d["tb.win.card"] = "Taskbar look";
            d["tb.win.master"] = "Change the taskbar look in this profile";
            d["tb.win.masterOff"] = "While this is off, the taskbar looks the way Windows draws it.";
            d["tb.win.usual"] = "Usual look";
            d["tb.win.usualHint"] = "This is how the taskbar looks when nothing special is happening.";
            d["tb.win.line"] = "A thin line at the edge of the taskbar";
            d["tb.win.lineHint"] = "The same for every case.";
            d["tb.win.casesCard"] = "When the look is different";
            d["tb.win.casesHint"] = "The cases are checked top to bottom — the first match wins.";
            d["tb.win.colorOpaque"] = "The first two digits do nothing — the taskbar is opaque.";
            d["tb.win.colorAlpha"] = "The first two digits are how solid the colour is.";
            d["tb.win.color"] = "Colour";
            d["tb.win.perScreen"] = "Counted for each screen separately.";
            d["tb.win.asUsual"] = "As usual";
            d["tb.win.colorTip"] = "AARRGGBB or RRGGBB — the first two digits set how solid the colour is";
            d["tb.win.pickProfile"] = "— pick a profile —";
            d["tb.win.noOthers"] = "no other profiles";
            d["tb.win.copied"] = "Settings copied from “{0}”.";
            d["tb.win.applyAll"] = "Apply to all";
            d["tb.win.transferCard"] = "Moving between profiles";
            d["tb.win.transferHint"] = "Having set the look once, there is no need to repeat it in every profile.";
            d["tb.win.applyAllTitle"] = "Apply to every profile?";
            d["tb.win.applyAllBody"] = "The taskbar look from “{0}” will replace the settings in the other profiles ({1}). The monitor layouts do not change.";
            d["tb.win.writtenAll"] = "Settings written into every profile: {0}.";
            d["tb.win.noLibrary"] = "The file that draws the taskbar look is missing — there is nothing to set the look with.";
            d["tb.win.liveHint"] = "Changes show at once and are saved together with the profile.";
            d["tb.win.offHint"] = "The taskbar stays the way Windows draws it.";
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
            d["common.autoPick"] = "Выбирать автоматически";
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
            d["win.key"] = "сочетание {0}";

            d["prop.pick"] = "Выберите монитор";
            d["prop.enabled"] = "Включён";
            d["prop.primary"] = "Основной";
            d["prop.resolution"] = "Разрешение";
            d["prop.refresh"] = "Частота";
            d["prop.rotation"] = "Поворот";
            d["prop.clone"] = "Дублирование";
            d["prop.hdr"] = "HDR";
            d["rot.90"] = "90° вправо";
            d["rot.180"] = "180°";
            d["rot.270"] = "90° влево";
            d["hdr.on"] = "Включить";
            d["hdr.off"] = "Выключить";

            d["canvas.notInProfile"] = "НЕ В ПРОФИЛЕ · ПЕРЕТАЩИТЕ НАВЕРХ";
            d["canvas.notConnected"] = "не подключён";
            d["canvas.badgePrimary"] = "Основной";
            d["canvas.badgeClone"] = "Дубль";
            d["canvas.noneSelected"] = "Не выбран ни один монитор.";
            d["canvas.gap"] = "Между мониторами есть разрыв — придвиньте их вплотную, иначе Windows сдвинет их сама.";
            d["canvas.overlap"] = "«{0}» и «{1}» налезают друг на друга — разведите их: рабочие столы перекрываться не могут.";

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

            d["err.hotkeyProblem"] = "{0} — {1}";
            d["combo.empty"] = "пустое сочетание";
            d["combo.noKey"] = "не указана клавиша в «{0}»";
            d["combo.unknownKey"] = "неизвестная клавиша «{0}»";
            d["combo.needsModifier"] = "добавьте Ctrl, Alt или Win — иначе клавиша перестанет работать в других программах";

            d["mon.builtIn"] = "встроенный";
            d["mon.digital"] = "цифровой";
            d["err.noRights"] = "система не дала прав на смену раскладки";
            d["err.driver"] = "видеодрайвер не смог её применить";
            d["err.hardware"] = "оборудование такого не умеет";
            d["err.combination"] = "видеокарта не принимает такое сочетание режимов";
            d["err.impossible"] = "на этом оборудовании такая раскладка невозможна";
            d["err.unexplained"] = "система не объяснила причину";

            d["tray.startFailed"] = "Не удалось запустить {0}:\n\n{1}\n\nПодробности: %AppData%\\{2}\\error.log";
            d["tray.hint"] = "DIIISPL продолжает работать здесь: сочетания клавиш переключают раскладки. Совсем выйти — пункт «Выход» в этом меню.";
            d["tray.newVersion"] = "Вышла версия {0}. Откройте «О программе», чтобы обновиться.";
            d["tray.noProfiles"] = "Профилей пока нет";
            d["tray.manager"] = "Менеджер раскладок…";
            d["tray.runAtLogon"] = "Запускать при входе в Windows";
            d["tray.autoSwitch"] = "Следовать за мониторами";
            d["tray.tbReset"] = "Сбросить вид панели задач";
            d["tray.autoSwitchTip"] = "Применять профиль, подходящий подключённым мониторам";
            d["auto.switched"] = "Профиль «{0}» применён: изменился состав мониторов.";
            d["tray.about"] = "О программе…";
            d["tray.busy"] = "Идёт перестроение экранов — подождите несколько секунд.";
            d["tray.hotkeyShared"] = "{0} — одно сочетание у «{1}» и «{2}»";
            d["tray.hotkeyTaken"] = "{0} — занято другой программой";
            d["tray.hotkeysNotSet"] = "Не удалось назначить: {0}";
            d["tray.applyFailed"] = "«{0}» не удалось: {1}";
            d["tray.applyDoneMore"] = "Раскладка «{0}» применена. Ещё замечаний: {1} — откройте окно.";
            d["tray.applyDone"] = "Раскладка «{0}» применена.";

            d["upd.gibberish"] = "GitHub ответил чем-то непонятным";
            d["upd.onlyPre"] = "готовых выпусков пока нет — то, что вышло, помечено предварительным. Такие приходят на канале Dev";
            d["upd.noReleases"] = "выпусков пока нет";
            d["upd.notesHeading"] = "Что нового в";
            d["upd.moreNotes"] = "ещё {0} — открыть страницу";
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
            d["clone.dup"] = "Вместе с {0}";
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

            d["cli.unknownCmd"] = "Не знаю команды «{0}». Вот что есть:";
            d["cli.error"] = "Ошибка: {0}";
            d["cli.title"] = "DIIISPL — раскладки мониторов";
            d["cli.help.window"] = "  DIIISPL.exe                 открыть окно менеджера";
            d["cli.help.apply"] = "  DIIISPL.exe /apply \"Имя\"    применить профиль";
            d["cli.help.save"] = "  DIIISPL.exe /save \"Имя\"     записать текущую раскладку в профиль";
            d["cli.help.list"] = "  DIIISPL.exe /list           список профилей";
            d["cli.help.delete"] = "  DIIISPL.exe /delete \"Имя\"   удалить профиль";
            d["cli.help.monitors"] = "  DIIISPL.exe /monitors       что подключено и как опознано";
            d["cli.help.hdr"] = "  DIIISPL.exe /hdr            состояние HDR на каждом выходе";
            d["cli.help.state"] = "  DIIISPL.exe /state          то же для скриптов: строка на монитор, поля через табуляцию";
            d["cli.help.auto"] = "  DIIISPL.exe /auto           какой профиль подходит мониторам сейчас и что программа сделала бы";
            d["auto.connected"] = "Подключено мониторов: {0}";
            d["auto.on"] = "Следование за мониторами: включено.";
            d["auto.off"] = "Следование за мониторами: выключено — сама программа ничего переключать не станет.";
            d["auto.noFit"] = "Ни один профиль этим мониторам не подходит — переключаться не на что.";
            d["auto.fits"] = "Подходит профиль «{0}».";
            d["auto.already"] = "Он и так применён — менять нечего.";
            d["auto.would"] = "Если бы состав только что изменился, программа применила бы его.";
            d["auto.excluded"] = "Исключено из автоматического выбора профилей: {0}.";
            d["cli.help.taskbar"] = "  DIIISPL.exe /taskbar вид [AARRGGBB] [line]   вид панели задач сейчас";
            d["cli.help.taskbarState"] = "  DIIISPL.exe /taskbar-state [\"Имя\"]           что видно на экранах и какой вид из этого следует";
            d["cli.noMonitors"] = "  мониторы не найдены";
            d["cli.key"] = "    ключ:      {0}";
            d["cli.state"] = "    состояние: {0}";
            d["cli.on"] = "включён, {0}x{1} @ {2},{3}, {4} Гц, источник {5}";
            d["cli.primarySuffix"] = ", основной";
            d["cli.off"] = "выключен";
            d["cli.hdrLine"] = "    HDR:       {0}";
            d["cli.hdrOnSupported"] = "поддерживается, включён";
            d["cli.hdrOffSupported"] = "поддерживается, выключен";
            d["cli.hdrNone"] = "недоступен";
            d["cli.preferred"] = "    предпочт.: {0}x{1} {2} Гц";
            d["cli.modes"] = "    режимов:   {0}{1}";
            d["cli.modesApprox"] = " (список приблизительный)";
            d["cli.outputs"] = "    выходы:    источник {0}";
            d["cli.profileNotFound"] = "  профиль не найден: {0}";
            d["cli.profileIs"] = "  профиль: {0}";
            d["cli.colorLine"] = "{0}, цвет {1}, линия {2}";
            d["cli.yes"] = "да";
            d["cli.no"] = "нет";
            d["cli.stateOff"] = "выключено";
            d["cli.noTaskbarFile"] = "  В программе не хватает файла для оформления панели задач.";
            d["cli.pickLook"] = "  Укажите вид: {0}";
            d["cli.barsFound"] = "  панелей задач: {0}";
            d["cli.lineOnly"] = "  последним словом может быть только «line»";
            d["cli.colorFormat"] = "  цвет пишется как AARRGGBB или короче — RRGGBB";
            d["cli.applied"] = "  применено: {0}";
            d["cli.failed"] = "  не удалось: {0}";
            d["cli.modeLine"] = "    режим: {0}";
            d["cli.hdrShort"] = "    HDR:   {0}";
            d["cli.hdrOnCaps"] = "ВКЛЮЧЁН";
            d["cli.hdrNoInClone"] = "недоступен: выход в дублировании";
            d["cli.hdrNoAtMode"] = "недоступен на этом режиме";
            d["cli.cloneLine"] = "  Дублирование: {0}  →  {1}";
            d["cli.cloneNote"] = "  (в группе дублирования HDR должен быть одинаковым на всех выходах)";
            d["cli.noProfiles"] = "  профилей нет";
            d["cli.profileRow"] = "  {0}   мониторов: {1}/{2}{3}";
            d["cli.needName"] = "Укажите имя профиля.";
            d["cli.sameName"] = "«{0}» и «{1}» — для программы одно и то же имя.";
            d["cli.sameNameHint"] = "Возьмите другое имя или переснимите существующий: /save \"{0}\"";
            d["cli.retaken"] = "Переснято: {0}";
            d["cli.saved"] = "Сохранено: {0}";
            d["cli.notFound"] = "Профиль не найден: {0}";
            d["cli.appliedName"] = "Применено: {0}";
            d["cli.deleted"] = "Удалено: {0}";
            d["cli.help.lang"] = "  DIIISPL.exe /lang cs        записать образец перевода для языка «cs»";
            d["cli.lang.needCode"] = "  Укажите код языка, например: /lang cs";
            d["cli.lang.badCode"] = "  Код языка — короткое слово без косых черт и двоеточий: cs, de, pl.";
            d["cli.lang.written"] = "  Образец записан: {0}";
            d["cli.lang.left"] = "  Ещё не переведено строк: {0}";
            d["cli.lang.howTo"] = "  Переведите значения после «=», сохраните в UTF-8 и выберите язык в «О программе».";

            d["about.tagline"] = "Раскладки мониторов для Windows 11";
            d["about.version"] = "ВЕРСИЯ";
            d["about.build"] = "СБОРКА";
            d["about.profiles"] = "ПРОФИЛИ";
            d["about.author"] = "Автор";
            d["about.authorName"] = "Ростислав Стрельников";
            d["about.channel"] = "Канал обновлений";
            d["about.language"] = "Язык";
            d["about.whatsNew"] = "Что нового";
            d["changelog.yours"] = "сейчас у вас";
            d["changelog.empty"] = "В эту сборку чейнджлог не попал.";
            d["about.footer"] = "© 2026 {0} · GPL-3.0";
            d["about.devHint"] = "Сборки Dev приходят раньше общего выпуска и умеют больше — например, задавать вид панели задач. Взамен в них чаще что-то не работает.";
            d["about.stableHint"] = "Приходят только готовые выпуски. То, что ещё дорабатывается — вид панели задач, — есть только в сборках Dev.";
            d["about.check"] = "Проверить";
            d["about.checking"] = "проверяю…";
            d["about.again"] = "Ещё раз";
            d["about.upToDate"] = "установлена последняя версия";
            d["about.newVersion"] = "вышла версия {0}";
            d["about.update"] = "Обновить";
            d["about.downgradeTitle"] = "Перейти на общий выпуск?";
            d["about.downgradeBody"] = "Это сборка Dev. В версии {0} нет настроек вида панели задач — раздел из программы исчезнет. Уже заданный вид останется записанным в профилях и вернётся, если снова поставить сборку Dev.";
            d["about.downgradeOk"] = "Всё равно поставить";
            d["about.downloading"] = "скачиваю установщик…";
            d["about.installerStarted"] = "установщик запущен";
            d["about.langRestart"] = "Программа перезапустится, чтобы сменить язык.";
            d["about.langRestartTitle"] = "Сменить язык на «{0}»?";
            d["about.langRestartOk"] = "Сменить и перезапустить";
            d["about.langRestartFailed"] = "Не удалось перезапуститься для смены языка — программа осталась прежней.";

            d["tb.desktop"] = "Виден рабочий стол";
            d["tb.visibleWindow"] = "Открыто окно";
            d["tb.maximized"] = "Окно во весь экран";
            d["tb.start"] = "Открыт «Пуск»";
            d["tb.search"] = "Открыт поиск";
            d["tb.taskView"] = "Представление задач";
            d["tb.battery"] = "Экономия заряда";
            d["tb.noTaskbar"] = "не найдена панель задач";
            d["tb.noExplorer"] = "не удалось подключиться к проводнику";
            d["tb.lostExplorer"] = "связь с проводником потеряна";
            d["tb.look.default"] = "Как в системе";
            d["tb.look.opaque"] = "Непрозрачная";
            d["tb.look.clear"] = "Прозрачная";
            d["tb.look.blur"] = "Размытая";
            d["tb.look.acrylic"] = "Акриловая";
            d["tb.look.keep"] = "Не менять";
            d["tb.signals"] = "  сигналы: экономия заряда={0}, «Пуск»={1}, поиск={2}, представление задач={3}";
            d["tb.barsFound"] = "  панелей задач: {0}";
            d["tb.barLine"] = "  панель 0x{0}: развёрнутое окно={1}, открытое окно={2}  →  {3} ({4})";
            d["tb.stateOff"] = "состояние выключено";
            d["tb.win.title"] = "Панель задач";
            d["tb.win.subtitle"] = "Профиль «{0}»";
            d["tb.win.done"] = "Готово";
            d["tb.win.reset"] = "Сбросить вид";
            d["tb.win.resetTip"] = "Забыть, что уже применено, и покрасить панели заново — на случай, когда вид застрял от прежней раскладки";
            d["tb.win.card"] = "Вид панели задач";
            d["tb.win.master"] = "Менять вид панели в этом профиле";
            d["tb.win.masterOff"] = "Пока выключено, панель выглядит так, как её рисует Windows.";
            d["tb.win.usual"] = "Обычный вид";
            d["tb.win.usualHint"] = "Так панель выглядит, когда ничего особенного не происходит.";
            d["tb.win.line"] = "Тонкая линия у края панели";
            d["tb.win.lineHint"] = "Одинаково для всех случаев.";
            d["tb.win.casesCard"] = "Когда вид другой";
            d["tb.win.casesHint"] = "Случаи проверяются сверху вниз — побеждает первый подходящий.";
            d["tb.win.colorOpaque"] = "Первые две цифры не действуют — панель непрозрачная.";
            d["tb.win.colorAlpha"] = "Первые две цифры — насколько цвет плотный.";
            d["tb.win.color"] = "Цвет";
            d["tb.win.perScreen"] = "Считается для каждого экрана отдельно.";
            d["tb.win.asUsual"] = "Как обычно";
            d["tb.win.colorTip"] = "AARRGGBB или RRGGBB — первые две цифры задают плотность цвета";
            d["tb.win.pickProfile"] = "— выберите профиль —";
            d["tb.win.noOthers"] = "других профилей нет";
            d["tb.win.copied"] = "Настройки скопированы из «{0}».";
            d["tb.win.applyAll"] = "Применить ко всем";
            d["tb.win.transferCard"] = "Перенос между профилями";
            d["tb.win.transferHint"] = "Настроив вид один раз, незачем повторять его в каждом профиле.";
            d["tb.win.applyAllTitle"] = "Применить ко всем профилям?";
            d["tb.win.applyAllBody"] = "Вид панели задач из «{0}» заменит настройки в остальных профилях ({1} шт.). Раскладки мониторов не изменятся.";
            d["tb.win.writtenAll"] = "Настройки записаны во все профили: {0}.";
            d["tb.win.noLibrary"] = "В программе не хватает файла для оформления панели — задать вид нечем.";
            d["tb.win.liveHint"] = "Изменения видны сразу и сохраняются вместе с профилем.";
            d["tb.win.offHint"] = "Панель останется такой, какой её рисует Windows.";
            return d;
        }
    }
}
