// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DIIISPL
{
    /// <summary>
    /// Все тексты, которые видит человек, — через именованные члены этого класса.
    ///
    /// Устроено в два слоя намеренно. Здесь — имена, которыми пользуется код:
    /// `Text.Apply`, `Text.Overlap(a, b)`. Имя проверяет компилятор, опечатка в
    /// нём — ошибка сборки, а не пустая строка в чужом окне. Сами переводы лежат
    /// в <see cref="Lang"/>, по словарю на язык.
    ///
    /// Почему не пара «русский рядом с английским», как было сначала: пара — это
    /// пара. Третий язык в неё не помещается, а восемь позиционных аргументов —
    /// способ перепутать чешский с польским и не заметить. Теперь добавить язык
    /// значит добавить один словарь; ни один из четырёхсот вызовов в программе
    /// при этом не меняется.
    ///
    /// Почему не .resx: он требует сборки через MSBuild, а её здесь нет —
    /// программа собирается одним вызовом csc.
    /// </summary>
    internal static class Text
    {
        public const string Auto = "auto";

        private static Dictionary<string, string> _table;
        private static Dictionary<string, string> _fallback;
        private static string _code;

        /// <summary>Код текущего языка: «ru», «en», …</summary>
        public static string Code
        {
            get
            {
                if (_code == null) Load();
                return _code;
            }
        }

        private static readonly object _lock = new object();

        /// <summary>
        /// Словари собираются целиком и только потом попадают в поля. Иначе
        /// сосед, зашедший за строкой в этот момент, видел бы наполовину готовое:
        /// таблицу уже есть, запасной ещё нет — и промах по ключу заканчивался
        /// бы падением. Растущий словарь читать тем более нельзя.
        /// </summary>
        private static void Load()
        {
            lock (_lock)
            {
                if (_table != null) return;

                string code = Resolve(Settings.Language);
                var table = Lang.Table(code);
                var fallback = code == Lang.Codes[0] ? table : Lang.Table(Lang.Codes[0]);

                // Файл поверх встроенного словаря, а не вместо него: перевод, в котором
                // чего-то не хватает, дополняется тем, что программа знает сама, и окно
                // не остаётся с пустыми местами.
                LangFile.Overlay(code, table);
                if (!ReferenceEquals(fallback, table)) LangFile.Overlay(Lang.Codes[0], fallback);

                _fallback = fallback;
                _code = code;
                _table = table;      // последним: по нему судят, загружено ли
            }
        }

        /// <summary>
        /// Во что превращается выбор из настроек. «auto» — язык Windows, если он
        /// у нас есть; иначе запасной. Явный выбор человека сильнее системы:
        /// английская Windows не повод отбирать русский у того, кто его выбрал.
        /// </summary>
        public static string Resolve(string setting)
        {
            var all = AvailableCodes();
            foreach (string c in all) if (c == setting) return c;

            // Спрашиваем оба языка системы. Первый — язык, которым человек
            // пользуется; второй — язык, на котором Windows поставлена. Обычно
            // они совпадают, но не всегда: у процесса язык бывает английским на
            // насквозь русской Windows, и тогда программа заговорила бы
            // по-английски там, где всё вокруг по-русски.
            foreach (string system in new string[] {
                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName })
                foreach (string c in all)
                    if (c == system) return c;

            return Lang.Codes[0];
        }

        /// <summary>
        /// Чего не хватает в переводе. Пусто — язык полон. Нужна не программе, а
        /// тому, кто добавляет язык: без такой проверки неполнота обнаруживается
        /// в чужом окне и не сразу.
        /// </summary>
        public static List<string> Missing(string code)
        {
            var result = new List<string>();
            var full = Lang.Table(Lang.Codes[0]);
            // Язык из файла не наследует английский: непереведённой считается
            // каждая строка, которой в файле нет, — иначе счёт всегда нулевой.
            bool builtIn = false;
            foreach (string c in Lang.Codes) if (c == code) builtIn = true;
            var one = builtIn ? Lang.Table(code) : new Dictionary<string, string>(StringComparer.Ordinal);
            LangFile.Overlay(code, one);
            foreach (var kv in full) if (!one.ContainsKey(kv.Key)) result.Add(kv.Key);
            result.Sort();
            return result;
        }

        /// <summary>
        /// Все языки, которые программа может показать: встроенные плюс лежащие
        /// файлами. Файл с кодом встроенного языка новым языком не считается — он
        /// просто правит его строки.
        /// </summary>
        public static List<string> AvailableCodes()
        {
            var all = new List<string>(Lang.Codes);
            foreach (string c in LangFile.Codes()) if (!all.Contains(c)) all.Add(c);
            return all;
        }

        /// <summary>Как язык подписывает себя сам.</summary>
        public static string NameOf(string code)
        {
            foreach (string c in Lang.Codes) if (c == code) return Lang.NativeName(code);
            return LangFile.NameOf(code);
        }

        // ------------------------------------------------------------------ доступ к словарю
        private static string G(string key)
        {
            if (_table == null) Load();
            string v;
            if (_table.TryGetValue(key, out v)) return v;
            if (_fallback.TryGetValue(key, out v)) return v;
            return key;      // ключ виден в окне — значит его забыли завести
        }

        private static string G(string key, params object[] args)
        {
            return Fill(G(key), key, args);
        }

        /// <summary>
        /// Подставить значения в строку — и пережить перевод, который обещает
        /// больше, чем ему дают.
        ///
        /// Переводы приходят файлами от людей, и «{2}» там, где программа
        /// передаёт два значения, — обычная опечатка. Без этой оговорки она
        /// роняла окно: строка складывается прямо во время отрисовки, и
        /// FormatException выходил наружу. Ошибка в чужом файле не должна
        /// стоить работающей программы — берём английскую строку, а если и она
        /// не сходится, показываем шаблон как есть.
        /// </summary>
        private static string Fill(string format, string key, object[] args)
        {
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch (FormatException) { }

            string english;
            if (_fallback != null && _fallback.TryGetValue(key, out english) && english != format)
            {
                try { return string.Format(CultureInfo.InvariantCulture, english, args); }
                catch (FormatException) { }
            }
            return format;
        }

        /// <summary>
        /// Форма после числа. У английского их две, у русского три — поэтому
        /// выбор формы делается здесь, а не в месте вызова: там о нём забыли бы.
        /// Ключи ищутся как «имя.one», «имя.few», «имя.many».
        ///
        /// Правило пока знает два языка: русское — для русского, английское —
        /// для остальных. Язык, приехавший файлом, третью форму задать может
        /// (ключ «имя.few» из файла попадёт в словарь), а вот выбрать её
        /// программа сумеет только для русского. Для польского или чешского это
        /// заметно; чинится, когда такой перевод появится, — гадать о правилах
        /// языка, которого ещё нет, значит написать их неправильно.
        /// </summary>
        private static string Plural(string key, int n)
        {
            string form;
            if (Code == "ru")
            {
                int t = n % 100;
                if (t >= 11 && t <= 14) form = "many";
                else if (n % 10 == 1) form = "one";
                else if (n % 10 >= 2 && n % 10 <= 4) form = "few";
                else form = "many";
            }
            else form = n == 1 ? "one" : "many";

            if (_table == null) Load();
            string v;
            if (_table.TryGetValue(key + "." + form, out v) ||
                _table.TryGetValue(key + ".many", out v) ||
                _fallback.TryGetValue(key + "." + form, out v) ||
                _fallback.TryGetValue(key + ".many", out v))
                return Fill(v, key + "." + form, new object[] { n });
            return key;
        }

        // ------------------------------------------------------------------ общее
        public static string AppTagline { get { return G("app.tagline"); } }
        public static string Cancel { get { return G("common.cancel"); } }
        public static string Save { get { return G("common.save"); } }
        public static string Apply { get { return G("common.apply"); } }
        public static string Delete { get { return G("common.delete"); } }
        public static string Rename { get { return G("common.rename"); } }
        public static string AutoPick { get { return G("common.autoPick"); } }
        public static string About { get { return G("common.about"); } }
        public static string Exit { get { return G("common.exit"); } }
        public static string Hz { get { return G("common.hz"); } }

        // ------------------------------------------------------------------ окно менеджера
        public static string ProfilesHeader { get { return G("win.profiles"); } }
        public static string NewProfileTip { get { return G("win.newProfileTip"); } }
        public static string ProfileActionsTip { get { return G("win.profileActionsTip"); } }
        public static string MoveUp { get { return G("win.moveUp"); } }
        public static string MoveDown { get { return G("win.moveDown"); } }
        public static string HotkeyButton { get { return G("win.hotkeyButton"); } }
        public static string PressCombo { get { return G("win.pressCombo"); } }
        public static string MonitorsCount(int n) { return G("win.monitors", n); }
        public static string KeyLabel(string combo) { return G("win.key", combo); }

        // ------------------------------------------------------------------ свойства монитора
        public static string PickMonitor { get { return G("prop.pick"); } }
        public static string Enabled { get { return G("prop.enabled"); } }
        public static string PrimaryScreen { get { return G("prop.primary"); } }
        public static string Resolution { get { return G("prop.resolution"); } }
        public static string Refresh { get { return G("prop.refresh"); } }
        public static string Rotation { get { return G("prop.rotation"); } }
        public static string Cloning { get { return G("prop.clone"); } }
        public static string Hdr { get { return G("prop.hdr"); } }
        public static string Rot90 { get { return G("rot.90"); } }
        public static string Rot180 { get { return G("rot.180"); } }
        public static string Rot270 { get { return G("rot.270"); } }
        public static string HdrOn { get { return G("hdr.on"); } }
        public static string HdrOff { get { return G("hdr.off"); } }

        // ------------------------------------------------------------------ холст
        public static string NotInProfile { get { return G("canvas.notInProfile"); } }
        public static string NotConnected { get { return G("canvas.notConnected"); } }
        public static string BadgePrimary { get { return G("canvas.badgePrimary"); } }
        public static string BadgeClone { get { return G("canvas.badgeClone"); } }
        public static string NoMonitorPicked { get { return G("canvas.noneSelected"); } }
        public static string GapBetween { get { return G("canvas.gap"); } }
        public static string Overlap(string a, string b) { return G("canvas.overlap", a, b); }

        // ------------------------------------------------------------------ применение
        public static string AlreadyApplying { get { return G("apply.busy"); } }
        public static string WindowsRefused(string why) { return G("apply.refused", why); }
        public static string WindowsPartly(string why) { return G("apply.partly", why); }
        public static string NoTaskbarLibrary { get { return G("apply.noTaskbarLib"); } }
        public static string TaskbarPrefix(string what) { return G("apply.taskbarPrefix", what); }
        public static string SkippedNotConnected(string name) { return G("apply.skipped", name); }
        public static string NoneConnected { get { return G("apply.noneConnected"); } }
        public static string NoPathList { get { return G("apply.noPathList"); } }
        public static string NoFreeDesktop(string name) { return G("apply.noFreeDesktop", name); }
        public static string CannotAttach(string name) { return G("apply.cannotAttach", name); }
        public static string NothingUsable { get { return G("apply.nothingUsable"); } }
        public static string DriverPickedRate { get { return G("apply.driverRate"); } }
        public static string GotModeInstead(string name, int gotW, int gotH, int wantW, int wantH)
        {
            return G("apply.gotMode", name, gotW, gotH, wantW, wantH);
        }
        public static string GotRateInstead(string name, string got, string want)
        {
            return G("apply.gotRate", name, got, want);
        }
        public static string RotationNotApplied(string name) { return G("apply.noRotation", name); }
        public static string NotCloned(string name) { return G("apply.notCloned", name); }
        public static string DidNotTurnOn(string names) { return G("apply.didNotTurnOn", names); }
        public static string StayedOn(string names) { return G("apply.stayedOn", names); }
        public static string NotPrimary(string name) { return G("apply.notPrimary", name); }
        public static string WindowsMoved(string names) { return G("apply.moved", names); }
        public static string HdrNotInClone { get { return G("hdr.notInClone"); } }
        public static string HdrUnavailable(string name) { return G("hdr.unavailable", name); }
        public static string HdrFailed(string name, bool turningOn, string why)
        {
            return G(turningOn ? "hdr.failedOn" : "hdr.failedOff", name, why);
        }
        public static string HdrComesBack(string name) { return G("hdr.comesBack", name); }
        public static string HdrDidNotTurnOn(string name, bool cloned)
        {
            return G(cloned ? "hdr.noOnCloned" : "hdr.noOnPlain", name);
        }

        // ------------------------------------------------------------------ сочетания клавиш
        public static string ComboEmpty { get { return G("combo.empty"); } }
        public static string ComboNoKey(string text) { return G("combo.noKey", text); }
        public static string ComboUnknownKey(string key) { return G("combo.unknownKey", key); }
        public static string ComboNeedsModifier { get { return G("combo.needsModifier"); } }

        // ------------------------------------------------------------------ мониторы и коды системы
        public static string ConnBuiltIn { get { return G("mon.builtIn"); } }
        public static string ConnDigital { get { return G("mon.digital"); } }
        public static string ErrNoRights { get { return G("err.noRights"); } }
        public static string ErrDriver { get { return G("err.driver"); } }
        public static string ErrHardware { get { return G("err.hardware"); } }
        public static string ErrCombination { get { return G("err.combination"); } }
        public static string ErrImpossible { get { return G("err.impossible"); } }
        public static string ErrUnexplained { get { return G("err.unexplained"); } }

        // ------------------------------------------------------------------ трей и запуск
        public static string StartFailed(string app, string why, string folder)
        {
            return G("tray.startFailed", app, why, folder);
        }
        public static string TrayHint { get { return G("tray.hint"); } }
        public static string NewVersionBalloon(string version) { return G("tray.newVersion", version); }
        public static string NoProfilesYet { get { return G("tray.noProfiles"); } }
        public static string OpenManager { get { return G("tray.manager"); } }
        public static string RunAtLogon { get { return G("tray.runAtLogon"); } }
        public static string AutoSwitchMenu { get { return G("tray.autoSwitch"); } }
        public static string TrayTaskbarReset { get { return G("tray.tbReset"); } }
        public static string AutoSwitchTip { get { return G("tray.autoSwitchTip"); } }
        public static string AutoSwitched(string name) { return G("auto.switched", name); }
        public static string AboutMenu { get { return G("tray.about"); } }
        public static string BusyWait { get { return G("tray.busy"); } }
        public static string HotkeyProblem(string combo, string why) { return G("err.hotkeyProblem", combo, why); }
        public static string HotkeyShared(string combo, string a, string b) { return G("tray.hotkeyShared", combo, a, b); }
        public static string HotkeyTakenElsewhere(string combo) { return G("tray.hotkeyTaken", combo); }
        public static string HotkeysNotSet(string list) { return G("tray.hotkeysNotSet", list); }
        public static string ApplyFailedShort(string profile, string why) { return G("tray.applyFailed", profile, why); }
        public static string ApplyDoneShort(string profile, int warnings) { return G("tray.applyDoneMore", profile, warnings); }
        public static string ApplyDone(string profile) { return G("tray.applyDone", profile); }

        // ------------------------------------------------------------------ обновления
        public static string GitHubGibberish { get { return G("upd.gibberish"); } }
        public static string OnlyPrereleases { get { return G("upd.onlyPre"); } }
        public static string NoReleasesYet { get { return G("upd.noReleases"); } }
        public static string GitHubUnreachable { get { return G("upd.unreachable"); } }
        public static string ReleaseListUnreadable { get { return G("upd.listUnreadable"); } }
        public static string NoInstallerAttached { get { return G("upd.noInstaller"); } }
        public static string BadDownloadLink { get { return G("upd.badLink"); } }
        public static string DownloadFailed { get { return G("upd.downloadFailed"); } }
        public static string SaveFailed { get { return G("upd.saveFailed"); } }
        public static string TamperedFile { get { return G("upd.tampered"); } }
        public static string SignatureUnconfirmed { get { return G("upd.unconfirmed"); } }
        public static string SignatureRejected { get { return G("upd.rejected"); } }
        public static string ForeignKey { get { return G("upd.foreignKey"); } }
        public static string InstallerStartFailed { get { return G("upd.startFailed"); } }
        public static string NeverChecked { get { return G("upd.never"); } }
        public static string CheckedJustNow { get { return G("upd.justNow"); } }
        public static string CheckedMinutes(int n) { return G("upd.minutes", n); }
        public static string CheckedHours(int n) { return Plural("upd.hours", n); }
        public static string CheckedDays(int n) { return Plural("upd.days", n); }

        // ------------------------------------------------------------------ окно: подписи и ответы
        public static string RenameTip { get { return G("win.renameTip"); } }
        public static string PrimaryTip { get { return G("win.primaryTip"); } }
        public static string TurnOnNote { get { return G("win.turnOnNote"); } }
        public static string NoProfilesHint { get { return G("win.noProfilesHint"); } }
        public static string ActiveDotTip { get { return G("win.activeDot"); } }
        public static string NewProfileName { get { return G("win.newProfileName"); } }
        public static string DefaultProfileName { get { return G("win.defaultProfileName"); } }
        public static string ProfileCreated { get { return G("win.created"); } }
        public static string DeleteTitle { get { return G("win.deleteTitle"); } }
        public static string DeleteBody(string name) { return G("win.deleteBody", name); }
        public static string SaveTitle(string name) { return G("win.saveTitle", name); }
        public static string SaveBody { get { return G("win.saveBody"); } }
        public static string DontSave { get { return G("win.dontSave"); } }
        public static string Saved { get { return G("win.saved"); } }
        public static string Rebuilding { get { return G("win.rebuilding"); } }
        public static string MoreNotes(int n) { return G("win.moreNotes", n); }
        public static string NameUnchanged(string name) { return G("win.nameUnchanged", name); }
        public static string NameTaken(string name) { return G("win.nameTaken", name); }
        public static string TaskbarButton { get { return G("win.taskbarButton"); } }
        public static string TaskbarButtonN(int n) { return G("win.taskbarButtonN", n); }
        public static string TaskbarTip { get { return G("win.taskbarTip"); } }
        public static string TaskbarMissing { get { return G("win.taskbarMissing"); } }
        public static string OneMonitor { get { return G("win.oneMonitor"); } }
        public static string OwnTaskbarLook { get { return G("win.ownTaskbar"); } }
        public static string NowOn { get { return G("win.nowOn"); } }
        public static string NowOff { get { return G("win.nowOff"); } }
        public static string NowUnplugged { get { return G("win.nowUnplugged"); } }
        public static string NoHdrInClone { get { return G("win.noHdrInClone"); } }
        public static string RotPlain { get { return G("rot.plain"); } }
        public static string CloneSeparate { get { return G("clone.separate"); } }
        public static string CloneDup(string name) { return G("clone.dup", name); }
        public static string HdrLeave { get { return G("hdr.leave"); } }
        public static string PrimaryOnlyOne { get { return G("win.primaryOnlyOne"); } }
        public static string CloneModesDiffer { get { return G("win.cloneModes"); } }
        public static string PressComboHint { get { return G("win.pressComboHint"); } }
        public static string ComboRemoved { get { return G("win.comboRemoved"); } }
        public static string ComboImpossible { get { return G("win.comboImpossible"); } }
        public static string ComboRejected(string combo, string why) { return G("win.comboRejected", combo, why); }
        public static string ComboBusy(string combo, string other) { return G("win.comboBusy", combo, other); }
        public static string ComboSet(string combo, string profile) { return G("win.comboSet", combo, profile); }
        public static string ComboNeedsAutostart { get { return G("win.comboNeedsAutostart"); } }

        // ------------------------------------------------------------------ командная строка
        public static string CliUnknownCmd(string cmd) { return G("cli.unknownCmd", cmd); }
        public static string CliError(string why) { return G("cli.error", why); }
        public static string CliTitle { get { return G("cli.title"); } }
        public static string CliHelpWindow { get { return G("cli.help.window"); } }
        public static string CliHelpApply { get { return G("cli.help.apply"); } }
        public static string CliHelpSave { get { return G("cli.help.save"); } }
        public static string CliHelpList { get { return G("cli.help.list"); } }
        public static string CliHelpDelete { get { return G("cli.help.delete"); } }
        public static string CliHelpMonitors { get { return G("cli.help.monitors"); } }
        public static string CliHelpHdr { get { return G("cli.help.hdr"); } }
        public static string CliHelpState { get { return G("cli.help.state"); } }
        public static string CliHelpAuto { get { return G("cli.help.auto"); } }
        public static string AutoConnected(int n) { return G("auto.connected", n); }
        public static string AutoOn { get { return G("auto.on"); } }
        public static string AutoOff { get { return G("auto.off"); } }
        public static string AutoNoFit { get { return G("auto.noFit"); } }
        public static string AutoFits(string name) { return G("auto.fits", name); }
        public static string AutoAlready { get { return G("auto.already"); } }
        public static string AutoWould { get { return G("auto.would"); } }
        public static string AutoExcluded(int n) { return G("auto.excluded", n); }
        public static string CliHelpTaskbar { get { return G("cli.help.taskbar"); } }
        public static string CliHelpTaskbarState { get { return G("cli.help.taskbarState"); } }
        public static string CliNoMonitors { get { return G("cli.noMonitors"); } }
        public static string CliKey(string id) { return G("cli.key", id); }
        public static string CliState(string what) { return G("cli.state", what); }
        public static string CliOn(int w, int h, int x, int y, string hz, uint source, bool primary)
        {
            return G("cli.on", w, h, x, y, hz, source) + (primary ? G("cli.primarySuffix") : "");
        }
        public static string CliOff { get { return G("cli.off"); } }
        public static string CliHdrLine(string what) { return G("cli.hdrLine", what); }
        public static string CliHdrState(bool supported, bool on)
        {
            return supported ? G(on ? "cli.hdrOnSupported" : "cli.hdrOffSupported") : G("cli.hdrNone");
        }
        public static string CliPreferred(int w, int h, string hz) { return G("cli.preferred", w, h, hz); }
        public static string CliModes(int n, bool approx) { return G("cli.modes", n, approx ? G("cli.modesApprox") : ""); }
        public static string CliOutputs(string list) { return G("cli.outputs", list); }
        public static string CliProfileNotFound(string name) { return G("cli.profileNotFound", name); }
        public static string CliProfileIs(string name) { return G("cli.profileIs", name); }
        public static string CliColorLine(string look, string color, bool line)
        {
            return G("cli.colorLine", look, color, G(line ? "cli.yes" : "cli.no"));
        }
        public static string CliStateOff { get { return G("cli.stateOff"); } }
        public static string CliNoTaskbarFile { get { return G("cli.noTaskbarFile"); } }
        public static string CliPickLook(string names) { return G("cli.pickLook", names); }
        public static string CliBarsFound(int n) { return G("cli.barsFound", n); }
        public static string CliLineOnly { get { return G("cli.lineOnly"); } }
        public static string CliColorFormat { get { return G("cli.colorFormat"); } }
        public static string CliApplied(string what) { return G("cli.applied", what); }
        public static string CliFailed(string why) { return G("cli.failed", why); }
        public static string CliModeLine(string mode) { return G("cli.modeLine", mode); }
        public static string CliHdrShort(string what) { return G("cli.hdrShort", what); }
        public static string CliHdrDetail(bool active, bool supported, bool on, bool inClone)
        {
            if (!active) return "—";
            if (supported) return on ? G("cli.hdrOnCaps") : G("cli.hdrOffSupported");
            return G(inClone ? "cli.hdrNoInClone" : "cli.hdrNoAtMode");
        }
        public static string CliCloneLine(string names, string states) { return G("cli.cloneLine", names, states); }
        public static string CliCloneNote { get { return G("cli.cloneNote"); } }
        public static string CliNoProfiles { get { return G("cli.noProfiles"); } }
        public static string CliProfileRow(string name, int on, int all, string hotkey)
        {
            return G("cli.profileRow", name, on, all, string.IsNullOrEmpty(hotkey) ? "" : "   [" + hotkey + "]");
        }
        public static string CliNeedName { get { return G("cli.needName"); } }
        public static string CliSameName(string a, string b) { return G("cli.sameName", a, b); }
        public static string CliSameNameHint(string clash) { return G("cli.sameNameHint", clash); }
        public static string CliRetaken(string name) { return G("cli.retaken", name); }
        public static string CliSaved(string name) { return G("cli.saved", name); }
        public static string CliNotFound(string name) { return G("cli.notFound", name); }
        public static string CliAppliedName(string name) { return G("cli.appliedName", name); }
        public static string CliDeleted(string name) { return G("cli.deleted", name); }
        public static string CliHelpLang { get { return G("cli.help.lang"); } }
        public static string CliLangNeedCode { get { return G("cli.lang.needCode"); } }
        public static string CliLangBadCode { get { return G("cli.lang.badCode"); } }
        public static string CliLangWritten(string path) { return G("cli.lang.written", path); }
        public static string CliLangLeft(int n) { return G("cli.lang.left", n); }
        public static string CliLangHowTo { get { return G("cli.lang.howTo"); } }

        // ------------------------------------------------------------------ «О программе»
        public static string AboutTagline { get { return G("about.tagline"); } }
        public static string AboutVersion { get { return G("about.version"); } }
        public static string AboutBuild { get { return G("about.build"); } }
        public static string AboutProfiles { get { return G("about.profiles"); } }
        public static string AboutAuthor { get { return G("about.author"); } }
        public static string AboutAuthorName { get { return G("about.authorName"); } }
        public static string AboutChannel { get { return G("about.channel"); } }
        public static string AboutLanguage { get { return G("about.language"); } }
        public static string WhatsNew { get { return G("about.whatsNew"); } }
        public static string ChangelogYours { get { return G("changelog.yours"); } }
        public static string ChangelogEmpty { get { return G("changelog.empty"); } }
        public static string NotesHeading { get { return G("upd.notesHeading"); } }
        public static string MoreInNotes(int n) { return G("upd.moreNotes", n); }
        public static string AboutFooter(string app) { return G("about.footer", app); }
        public static string ChannelDevHint { get { return G("about.devHint"); } }
        public static string ChannelStableHint { get { return G("about.stableHint"); } }
        public static string CheckButton { get { return G("about.check"); } }
        public static string Checking { get { return G("about.checking"); } }
        public static string AgainButton { get { return G("about.again"); } }
        public static string UpToDate { get { return G("about.upToDate"); } }
        public static string NewVersionFound(string version) { return G("about.newVersion", version); }
        public static string UpdateButton { get { return G("about.update"); } }
        public static string DowngradeTitle { get { return G("about.downgradeTitle"); } }
        public static string DowngradeBody(string version) { return G("about.downgradeBody", version); }
        public static string DowngradeOk { get { return G("about.downgradeOk"); } }
        public static string Downloading { get { return G("about.downloading"); } }
        public static string InstallerStarted { get { return G("about.installerStarted"); } }
        public static string LangRestart { get { return G("about.langRestart"); } }
        public static string LangRestartTitle(string language) { return G("about.langRestartTitle", language); }
        public static string LangRestartOk { get { return G("about.langRestartOk"); } }
        public static string LangRestartFailed { get { return G("about.langRestartFailed"); } }

        // ------------------------------------------------------------------ панель задач (Dev)
        public static string TbState(string key) { return G("tb." + key); }
        public static string TbNoTaskbar { get { return G("tb.noTaskbar"); } }
        public static string TbNoExplorer { get { return G("tb.noExplorer"); } }
        public static string TbLostExplorer { get { return G("tb.lostExplorer"); } }
        public static string TbLook(string name) { return G("tb.look." + name); }
        public static string TbSignals(bool battery, bool start, bool search, bool taskView)
        {
            return G("tb.signals", YesNo(battery), YesNo(start), YesNo(search), YesNo(taskView));
        }
        public static string TbBarsFound(int n) { return G("tb.barsFound", n); }
        public static string TbBarLine(string handle, bool maximized, bool window, string state, string look)
        {
            return G("tb.barLine", handle, YesNo(maximized), YesNo(window), state, look);
        }
        public static string TbStateOff { get { return G("tb.stateOff"); } }
        public static string YesNo(bool yes) { return G(yes ? "cli.yes" : "cli.no"); }

        public static string TbWinTitle { get { return G("tb.win.title"); } }
        public static string TbWinSubtitle(string profile) { return G("tb.win.subtitle", profile); }
        public static string TbWinDone { get { return G("tb.win.done"); } }
        public static string TbWinReset { get { return G("tb.win.reset"); } }
        public static string TbWinResetTip { get { return G("tb.win.resetTip"); } }
        public static string TbWinCard { get { return G("tb.win.card"); } }
        public static string TbWinMaster { get { return G("tb.win.master"); } }
        public static string TbWinMasterOff { get { return G("tb.win.masterOff"); } }
        public static string TbWinUsual { get { return G("tb.win.usual"); } }
        public static string TbWinUsualHint { get { return G("tb.win.usualHint"); } }
        public static string TbWinLine { get { return G("tb.win.line"); } }
        public static string TbWinLineHint { get { return G("tb.win.lineHint"); } }
        public static string TbWinCasesCard { get { return G("tb.win.casesCard"); } }
        public static string TbWinCasesHint { get { return G("tb.win.casesHint"); } }
        public static string TbWinColorHint(bool opaque) { return G(opaque ? "tb.win.colorOpaque" : "tb.win.colorAlpha"); }
        public static string TbWinColor { get { return G("tb.win.color"); } }
        public static string TbWinPerScreen { get { return G("tb.win.perScreen"); } }
        public static string TbWinAsUsual { get { return G("tb.win.asUsual"); } }
        public static string TbWinColorTip { get { return G("tb.win.colorTip"); } }
        public static string TbWinPickProfile { get { return G("tb.win.pickProfile"); } }
        public static string TbWinNoOthers { get { return G("tb.win.noOthers"); } }
        public static string TbWinCopied(string from) { return G("tb.win.copied", from); }
        public static string TbWinApplyAll { get { return G("tb.win.applyAll"); } }
        public static string TbWinTransferCard { get { return G("tb.win.transferCard"); } }
        public static string TbWinTransferHint { get { return G("tb.win.transferHint"); } }
        public static string TbWinApplyAllTitle { get { return G("tb.win.applyAllTitle"); } }
        public static string TbWinApplyAllBody(string profile, int others) { return G("tb.win.applyAllBody", profile, others); }
        public static string TbWinWrittenAll(int n) { return G("tb.win.writtenAll", n); }
        public static string TbWinNoLibrary { get { return G("tb.win.noLibrary"); } }
        public static string TbWinLiveHint { get { return G("tb.win.liveHint"); } }
        public static string TbWinOffHint { get { return G("tb.win.offHint"); } }
    }
}
