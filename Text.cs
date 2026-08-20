// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

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

        private static void Load()
        {
            _code = Resolve(Settings.Language);
            _table = Lang.Table(_code);
            _fallback = _code == Lang.Codes[0] ? _table : Lang.Table(Lang.Codes[0]);
        }

        /// <summary>
        /// Во что превращается выбор из настроек. «auto» — язык Windows, если он
        /// у нас есть; иначе запасной. Явный выбор человека сильнее системы:
        /// английская Windows не повод отбирать русский у того, кто его выбрал.
        /// </summary>
        public static string Resolve(string setting)
        {
            foreach (string c in Lang.Codes) if (c == setting) return c;

            string system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (string c in Lang.Codes) if (c == system) return c;
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
            var one = Lang.Table(code);
            foreach (var kv in full) if (!one.ContainsKey(kv.Key)) result.Add(kv.Key);
            result.Sort();
            return result;
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
            return string.Format(CultureInfo.InvariantCulture, G(key), args);
        }

        /// <summary>
        /// Форма после числа. У английского их две, у русского три, у арабского
        /// шесть — поэтому выбор формы принадлежит языку, а не месту вызова.
        /// Ключи ищутся как «имя.one», «имя.few», «имя.many».
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
                return string.Format(CultureInfo.InvariantCulture, v, n);
            return key;
        }

        // ------------------------------------------------------------------ общее
        public static string AppTagline { get { return G("app.tagline"); } }
        public static string Cancel { get { return G("common.cancel"); } }
        public static string Save { get { return G("common.save"); } }
        public static string Apply { get { return G("common.apply"); } }
        public static string Delete { get { return G("common.delete"); } }
        public static string Rename { get { return G("common.rename"); } }
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
        public static string MonitorsCount(int on, int all) { return G("win.monitorsOf", on, all); }
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
        public static string RotNone { get { return G("rot.none"); } }
        public static string Rot90 { get { return G("rot.90"); } }
        public static string Rot180 { get { return G("rot.180"); } }
        public static string Rot270 { get { return G("rot.270"); } }
        public static string CloneOff { get { return G("clone.off"); } }
        public static string CloneWith(string name) { return G("clone.with", name); }
        public static string HdrKeep { get { return G("hdr.keep"); } }
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
        public static string NotInUse { get { return G("mon.notInUse"); } }
        public static string DisplayNumbered(uint id) { return G("mon.display", id); }
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
        public static string AboutMenu { get { return G("tray.about"); } }
        public static string BusyWait { get { return G("tray.busy"); } }
        public static string HotkeyProblem(string combo, string why) { return combo + " — " + why; }
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
    }
}
