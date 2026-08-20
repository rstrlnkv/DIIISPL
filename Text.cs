// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System.Globalization;

namespace DIIISPL
{
    /// <summary>
    /// Все тексты, которые видит человек, — на двух языках рядом.
    ///
    /// Не файл ресурсов, а обычный код, и это осознанно. Ресурсы в .resx требуют
    /// сборки через MSBuild, а здесь её нет: программа собирается одним вызовом
    /// csc. Кроме того, пара «русский рядом с английским» в одной строке не даёт
    /// им разъехаться — забытый перевод становится ошибкой компиляции, а не
    /// пустой строкой в чужом окне.
    ///
    /// Строки с подстановкой сделаны методами, а не склейкой на месте: порядок
    /// слов в двух языках разный, и склеивать «профиль» + имя + «уже есть» можно
    /// только там, где обе половины видны сразу.
    /// </summary>
    internal static class Text
    {
        public const string Auto = "auto";
        public const string Russian = "ru";
        public const string English = "en";

        private static string _lang;

        /// <summary>Язык окна: ru или en. Читается один раз за запуск.</summary>
        public static bool Ru
        {
            get
            {
                if (_lang == null) _lang = Resolve(Settings.Language);
                return _lang == Russian;
            }
        }

        /// <summary>
        /// Во что превращается выбор из настроек. «auto» — язык Windows: русский
        /// у русской системы, английский у любой другой. Второй язык программы
        /// один, и притворяться, будто мы знаем ещё какие-то, незачем.
        /// </summary>
        public static string Resolve(string setting)
        {
            if (setting == Russian || setting == English) return setting;
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Russian ? Russian : English;
        }

        private static string S(string ru, string en) { return Ru ? ru : en; }

        // ------------------------------------------------------------------ общее
        public static string AppTagline { get { return S("Раскладки мониторов", "Monitor layouts"); } }
        public static string Cancel { get { return S("Отмена", "Cancel"); } }
        public static string Save { get { return S("Сохранить", "Save"); } }
        public static string Apply { get { return S("Применить", "Apply"); } }
        public static string Delete { get { return S("Удалить", "Delete"); } }
        public static string Rename { get { return S("Переименовать", "Rename"); } }
        public static string About { get { return S("О программе", "About"); } }
        public static string Exit { get { return S("Выход", "Exit"); } }

        // ------------------------------------------------------------------ окно менеджера
        public static string ProfilesHeader { get { return S("ПРОФИЛИ", "PROFILES"); } }
        public static string NewProfileTip
        {
            get { return S("Новый профиль из текущей раскладки мониторов", "New profile from the current layout"); }
        }
        public static string ProfileActionsTip
        {
            get { return S("Переименовать, переставить, удалить", "Rename, reorder, delete"); }
        }
        public static string MoveUp { get { return S("Переместить вверх", "Move up"); } }
        public static string MoveDown { get { return S("Переместить вниз", "Move down"); } }
        public static string HotkeyButton { get { return S("Сочетание клавиш", "Keyboard shortcut"); } }
        public static string PressCombo { get { return S("Нажмите сочетание…", "Press a shortcut…"); } }
        public static string MonitorsCount(int n)
        {
            return S("мониторов: " + n, "monitors: " + n);
        }
        public static string MonitorsCount(int on, int all)
        {
            return S("мониторов: " + on + "/" + all, "monitors: " + on + "/" + all);
        }
        public static string KeyLabel(string combo) { return S("клавиша " + combo, "key " + combo); }

        // ------------------------------------------------------------------ свойства монитора
        public static string PickMonitor { get { return S("Выберите монитор", "Select a monitor"); } }
        public static string Enabled { get { return S("Включён", "On"); } }
        public static string PrimaryScreen { get { return S("Основной", "Primary"); } }
        public static string Resolution { get { return S("Разрешение", "Resolution"); } }
        public static string Refresh { get { return S("Частота", "Refresh rate"); } }
        public static string Rotation { get { return S("Поворот", "Rotation"); } }
        public static string Cloning { get { return S("Дублирование", "Duplicate"); } }
        public static string Hdr { get { return S("HDR", "HDR"); } }
        public static string Hz { get { return S("Гц", "Hz"); } }
        public static string RotNone { get { return S("Без поворота", "None"); } }
        public static string Rot90 { get { return S("90° по часовой", "90° clockwise"); } }
        public static string Rot180 { get { return S("180°", "180°"); } }
        public static string Rot270 { get { return S("90° против часовой", "90° counter-clockwise"); } }
        public static string CloneOff { get { return S("Отдельный экран", "Separate desktop"); } }
        public static string CloneWith(string name) { return S("Дублировать с «" + name + "»", "Duplicate with “" + name + "”"); }
        public static string HdrKeep { get { return S("Не трогать", "Leave as is"); } }
        public static string HdrOn { get { return S("Включить", "Turn on"); } }
        public static string HdrOff { get { return S("Выключить", "Turn off"); } }

        // ------------------------------------------------------------------ холст
        public static string NotInProfile
        {
            get { return S("НЕ В ПРОФИЛЕ · ПЕРЕТАЩИТЕ НАВЕРХ", "NOT IN PROFILE · DRAG UP"); }
        }
        public static string NotConnected { get { return S("не подключён", "not connected"); } }
        public static string BadgePrimary { get { return S("Основной", "Primary"); } }
        public static string BadgeClone { get { return S("Дубль", "Duplicate"); } }
        public static string NoMonitorPicked { get { return S("Не выбран ни один монитор.", "No monitor is selected."); } }
        public static string GapBetween
        {
            get
            {
                return S("Между мониторами есть разрыв — придвиньте их вплотную, иначе Windows сдвинет их сама.",
                         "There is a gap between the monitors — put them edge to edge, or Windows will move them itself.");
            }
        }
        public static string Overlap(string a, string b)
        {
            return S("«" + a + "» и «" + b + "» налезают друг на друга — разведите их: рабочие столы наложенными быть не могут.",
                     "“" + a + "” and “" + b + "” overlap — move them apart: desktops cannot overlap.");
        }

        // ------------------------------------------------------------------ применение
        public static string AlreadyApplying
        {
            get { return S("экраны уже перестраиваются — подождите и повторите", "the screens are already being rearranged — wait and try again"); }
        }
        public static string WindowsRefused(string why)
        {
            return S("Windows отклонила раскладку: " + why, "Windows refused the layout: " + why);
        }
        public static string WindowsPartly(string why)
        {
            return S("Windows приняла раскладку не целиком: " + why +
                     ". На экране может стоять не то, что в профиле, — проверьте и примените заново.",
                     "Windows accepted the layout only in part: " + why +
                     ". What is on the screens may differ from the profile — check it and apply again.");
        }
        public static string NoTaskbarLibrary
        {
            get
            {
                return S("Профиль задаёт вид панели задач, но в программе не хватает файла для этого.",
                         "The profile sets a taskbar look, but the file needed for it is missing.");
            }
        }
        public static string TaskbarPrefix(string what) { return S("Панель задач: " + what, "Taskbar: " + what); }
        public static string SkippedNotConnected(string name)
        {
            return S("«" + name + "» сейчас не подключён — пропущен.",
                     "“" + name + "” is not connected right now — skipped.");
        }
        public static string NoneConnected
        {
            get { return S("Ни один монитор из профиля сейчас не подключён.", "None of the profile’s monitors are connected."); }
        }
        public static string NoPathList
        {
            get { return S("Windows не отдала список подключений мониторов.", "Windows did not return the display connection list."); }
        }
        public static string NoFreeDesktop(string name)
        {
            return S("Не удалось разместить «" + name + "»: видеокарта не даёт столько независимых рабочих столов.",
                     "Could not place “" + name + "”: the graphics card does not offer that many independent desktops.");
        }
        public static string CannotAttach(string name)
        {
            return S("«" + name + "» нельзя подключить к выбранному рабочему столу — пропущен.",
                     "“" + name + "” cannot be attached to the chosen desktop — skipped.");
        }
        public static string NothingUsable
        {
            get
            {
                return S("Не получилось составить раскладку: ни один монитор не удалось задействовать.",
                         "Could not build the layout: no monitor could be used.");
            }
        }
        public static string DriverPickedRate
        {
            get
            {
                return S("Частоту выбрал драйвер — заданную Windows не приняла.",
                         "The driver picked the refresh rate — Windows did not accept the one you set.");
            }
        }
        public static string GotModeInstead(string name, int gotW, int gotH, int wantW, int wantH)
        {
            return S("«" + name + "»: получилось " + gotW + "×" + gotH + " вместо " + wantW + "×" + wantH + ".",
                     "“" + name + "”: got " + gotW + "×" + gotH + " instead of " + wantW + "×" + wantH + ".");
        }
        public static string GotRateInstead(string name, string got, string want)
        {
            return S("«" + name + "»: частота " + got + " Гц вместо " + want + " Гц.",
                     "“" + name + "”: refresh rate " + got + " Hz instead of " + want + " Hz.");
        }
        public static string RotationNotApplied(string name)
        {
            return S("«" + name + "»: поворот экрана Windows не применила.",
                     "“" + name + "”: Windows did not apply the screen rotation.");
        }
        public static string NotCloned(string name)
        {
            return S("«" + name + "» не встал в дублирование — Windows развела экраны.",
                     "“" + name + "” did not join the duplicate — Windows separated the screens.");
        }
        public static string DidNotTurnOn(string names)
        {
            return S("Не включились: " + names + ". Чаще всего монитор обесточен или спит так, что видеокарта его не видит.",
                     "Did not turn on: " + names + ". Usually the monitor has no power, or sleeps so deeply the card cannot see it.");
        }
        public static string StayedOn(string names)
        {
            return S("Остались включёнными вопреки профилю: " + names + ".",
                     "Stayed on against the profile: " + names + ".");
        }
        public static string NotPrimary(string name)
        {
            return S("«" + name + "» не стал основным — Windows выбрала другой экран.",
                     "“" + name + "” did not become primary — Windows chose another screen.");
        }
        public static string WindowsMoved(string names)
        {
            return S("Windows расставила по-своему: " + names +
                     ". На экране теперь не то, что записано в профиле, — поправьте раскладку и примените заново.",
                     "Windows arranged them its own way: " + names +
                     ". The screens no longer match the profile — fix the layout and apply again.");
        }
        public static string HdrNotInClone
        {
            get
            {
                return S("При дублировании HDR не работает. Разведите экраны — тогда его можно включить на каждом.",
                         "HDR does not work while duplicating. Separate the screens and it can be turned on for each.");
            }
        }
        public static string HdrUnavailable(string name)
        {
            return S("«" + name + "»: этот выход сейчас не отдаёт HDR — включить нечего.",
                     "“" + name + "”: this output does not offer HDR right now — there is nothing to turn on.");
        }
        public static string HdrFailed(string name, bool turningOn, string why)
        {
            return S("«" + name + "»: не удалось " + (turningOn ? "включить" : "выключить") + " HDR — " + why,
                     "“" + name + "”: could not turn HDR " + (turningOn ? "on" : "off") + " — " + why);
        }
        public static string HdrComesBack(string name)
        {
            return S("«" + name + "»: не удалось выключить HDR — система включает его обратно.",
                     "“" + name + "”: could not turn HDR off — the system turns it back on.");
        }
        public static string HdrDidNotTurnOn(string name, bool cloned)
        {
            return S("«" + name + "»: HDR не включился." +
                     (cloned
                        ? " Выходам в дублировании Windows расширенный цвет не даёт: чтобы включить HDR, разведите экраны врастяжку."
                        : " Обычно это нехватка пропускной способности кабеля на выбранном режиме."),
                     "“" + name + "”: HDR did not turn on." +
                     (cloned
                        ? " Windows gives no wide colour to duplicated outputs: to use HDR, extend the screens instead."
                        : " Usually the cable cannot carry it at the chosen mode."));
        }

        // ------------------------------------------------------------------ сочетания клавиш
        public static string ComboEmpty { get { return S("пустое сочетание", "empty shortcut"); } }
        public static string ComboNoKey(string text)
        {
            return S("не указана клавиша в «" + text + "»", "no key given in “" + text + "”");
        }
        public static string ComboUnknownKey(string key)
        {
            return S("неизвестная клавиша «" + key + "»", "unknown key “" + key + "”");
        }
        public static string ComboNeedsModifier
        {
            get
            {
                return S("добавьте Ctrl, Alt или Win — иначе клавиша перестанет работать в других программах",
                         "add Ctrl, Alt or Win — otherwise the key stops working in every other program");
            }
        }

        // ------------------------------------------------------------------ мониторы и коды системы
        public static string NotInUse { get { return S("не используется", "not in use"); } }
        public static string DisplayNumbered(uint id) { return S("Дисплей " + id, "Display " + id); }
        public static string ConnBuiltIn { get { return S("встроенный", "built-in"); } }
        public static string ConnDigital { get { return S("цифровой", "digital"); } }

        public static string ErrNoRights { get { return S("система не дала прав на смену раскладки", "the system did not allow changing the layout"); } }
        public static string ErrDriver { get { return S("видеодрайвер не смог её применить", "the display driver could not apply it"); } }
        public static string ErrHardware { get { return S("оборудование такого не умеет", "the hardware cannot do this"); } }
        public static string ErrCombination { get { return S("видеокарта не принимает такое сочетание режимов", "the graphics card rejects this combination of modes"); } }
        public static string ErrImpossible { get { return S("на этом оборудовании такая раскладка невозможна", "this layout is impossible on this hardware"); } }
        public static string ErrUnexplained { get { return S("система не объяснила причину", "the system gave no reason"); } }

        // ------------------------------------------------------------------ трей и запуск
        public static string StartFailed(string app, string why, string folder)
        {
            return S("Не удалось запустить " + app + ":\n\n" + why + "\n\nПодробности: %AppData%\\" + folder + "\\error.log",
                     "Could not start " + app + ":\n\n" + why + "\n\nDetails: %AppData%\\" + folder + "\\error.log");
        }
        public static string TrayHint
        {
            get
            {
                return S("DIIISPL продолжает работать здесь: горячие клавиши переключают раскладки. Совсем выйти — пункт «Выход» в этом меню.",
                         "DIIISPL keeps running here: the shortcuts still switch layouts. To quit for good, use “Exit” in this menu.");
            }
        }
        public static string NewVersionBalloon(string version)
        {
            return S("Вышла версия " + version + ". Открыть «О программе», чтобы обновиться.",
                     "Version " + version + " is out. Open “About” to update.");
        }
        public static string NoProfilesYet { get { return S("Профилей пока нет", "No profiles yet"); } }
        public static string OpenManager { get { return S("Менеджер раскладок…", "Layout manager…"); } }
        public static string RunAtLogon { get { return S("Запускать при входе в Windows", "Start with Windows"); } }
        public static string AboutMenu { get { return S("О программе…", "About…"); } }
        public static string BusyWait
        {
            get
            {
                return S("Идёт перестроение экранов — подождите несколько секунд.",
                         "The screens are being rearranged — wait a few seconds.");
            }
        }
        public static string HotkeyProblem(string combo, string why) { return combo + " — " + why; }
        public static string HotkeyShared(string combo, string a, string b)
        {
            return S(combo + " — одно сочетание у «" + a + "» и «" + b + "»",
                     combo + " — the same shortcut on “" + a + "” and “" + b + "”");
        }
        public static string HotkeyTakenElsewhere(string combo)
        {
            return S(combo + " — занято другой программой", combo + " — taken by another program");
        }
        public static string HotkeysNotSet(string list)
        {
            return S("Не удалось назначить: " + list, "Could not assign: " + list);
        }
        public static string ApplyFailedShort(string profile, string why)
        {
            return S("«" + profile + "» не удалось: " + why, "“" + profile + "” failed: " + why);
        }
        public static string ApplyDoneShort(string profile, int warnings)
        {
            return S("Раскладка «" + profile + "» применена. Ещё замечаний: " + warnings + " — откройте окно.",
                     "Layout “" + profile + "” applied. " + warnings + " more note(s) — open the window.");
        }
        public static string ApplyDone(string profile)
        {
            return S("Раскладка «" + profile + "» применена.", "Layout “" + profile + "” applied.");
        }

        // ------------------------------------------------------------------ обновления
        public static string GitHubGibberish { get { return S("GitHub ответил непонятным", "GitHub answered with something unreadable"); } }
        public static string OnlyPrereleases
        {
            get
            {
                return S("готовых выпусков пока нет — то, что вышло, помечено предварительным. Такие приходят на канале Dev",
                         "no finished releases yet — what is out is marked pre-release. Those arrive on the Dev channel");
            }
        }
        public static string NoReleasesYet { get { return S("выпусков пока нет", "no releases yet"); } }
        public static string GitHubUnreachable { get { return S("не удалось связаться с GitHub", "could not reach GitHub"); } }
        public static string ReleaseListUnreadable { get { return S("не удалось прочитать список выпусков", "could not read the release list"); } }
        public static string NoInstallerAttached { get { return S("к этому выпуску не приложен установщик", "this release has no installer attached"); } }
        public static string BadDownloadLink { get { return S("ссылка на установщик ведёт не туда, куда должна", "the installer link does not point where it should"); } }
        public static string DownloadFailed { get { return S("не удалось скачать установщик", "could not download the installer"); } }
        public static string SaveFailed { get { return S("не удалось сохранить установщик — проверьте место на диске", "could not save the installer — check the free disk space"); } }
        public static string TamperedFile { get { return S("файл изменён после подписания — скачайте выпуск заново", "the file was changed after signing — download the release again"); } }
        public static string SignatureUnconfirmed
        {
            get
            {
                return S("подпись установщика не подтверждается: он повреждён или не подписан. Скачайте выпуск заново",
                         "the installer’s signature cannot be confirmed: it is damaged or unsigned. Download the release again");
            }
        }
        public static string SignatureRejected { get { return S("подпись установщика не прошла проверку — скачайте выпуск заново", "the installer’s signature failed the check — download the release again"); } }
        public static string ForeignKey
        {
            get
            {
                return S("установщик подписан чужим ключом. Берите пакет только со страницы выпусков DIIISPL",
                         "the installer is signed with a foreign key. Take the package only from the DIIISPL releases page");
            }
        }
        public static string InstallerStartFailed
        {
            get
            {
                return S("не удалось запустить установщик — откройте скачанный пакет вручную",
                         "could not start the installer — open the downloaded package by hand");
            }
        }
        public static string NeverChecked { get { return S("ещё не проверялось", "not checked yet"); } }
        public static string CheckedJustNow { get { return S("проверялось только что", "checked just now"); } }
        public static string CheckedMinutes(int n) { return S("проверялось " + n + " мин назад", "checked " + n + " min ago"); }
        public static string CheckedHours(int n)
        {
            return S("проверялось " + Plural(n, "час", "часа", "часов") + " назад",
                    "checked " + n + (n == 1 ? " hour" : " hours") + " ago");
        }
        public static string CheckedDays(int n)
        {
            return S("проверялось " + Plural(n, "день", "дня", "дней") + " назад",
                    "checked " + n + (n == 1 ? " day" : " days") + " ago");
        }

        /// <summary>
        /// Русское склонение после числа. Живёт здесь, рядом со строками, которым
        /// оно нужно: в английской ветке множественное число проще, и тащить
        /// русские формы через границу языка незачем.
        /// </summary>
        private static string Plural(int n, string one, string few, string many)
        {
            int t = n % 100;
            if (t >= 11 && t <= 14) return n + " " + many;
            switch (n % 10)
            {
                case 1: return n + " " + one;
                case 2:
                case 3:
                case 4: return n + " " + few;
                default: return n + " " + many;
            }
        }
    }
}
