// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DIIISPL
{
    /// <summary>Состояния, при которых панель задач может выглядеть по-разному.</summary>
    internal enum TaskbarState
    {
        Desktop,          // виден рабочий стол
        VisibleWindow,    // на этом экране есть открытое окно
        MaximizedWindow,  // на этом экране окно развёрнуто во весь экран
        StartOpened,      // открыто меню «Пуск»
        SearchOpened,     // открыт поиск
        TaskViewOpened,   // открыто представление задач
        BatterySaver      // включена экономия заряда
    }

    /// <summary>Как выглядит панель в одном состоянии.</summary>
    internal class TaskbarAppearance
    {
        public bool Enabled;
        public string Accent = "default";     // default, opaque, clear, blur, acrylic
        public uint Color = 0x00000000;       // AARRGGBB
        public bool ShowLine = true;          // тонкая линия у края панели
        public float BlurRadius = 9.0f;

        public TaskbarAppearance Copy() { return (TaskbarAppearance)MemberwiseClone(); }

        public string Serialize()
        {
            if (!Enabled) return "-";
            return Accent + "," + Color.ToString("X8", CultureInfo.InvariantCulture) + "," + (ShowLine ? "1" : "0");
        }

        public static TaskbarAppearance Parse(string text)
        {
            var a = new TaskbarAppearance();
            text = (text ?? "").Trim();
            if (text.Length == 0 || text == "-") return a;

            string[] parts = text.Split(',');
            a.Enabled = true;
            a.Accent = parts[0].Trim().ToLowerInvariant();
            if (parts.Length > 1)
            {
                uint c;
                if (uint.TryParse(parts[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out c)) a.Color = c;
            }
            if (parts.Length > 2) a.ShowLine = parts[2].Trim() != "0";
            return a;
        }
    }

    /// <summary>
    /// Набор видов панели задач для всех состояний. Порядок важен: состояния проверяются
    /// сверху вниз, и первое включённое и подходящее задаёт вид. «Рабочий стол» —
    /// базовое состояние, оно применяется, когда не подошло ничто другое.
    /// </summary>
    internal class TaskbarSettings
    {
        public readonly Dictionary<TaskbarState, TaskbarAppearance> States =
            new Dictionary<TaskbarState, TaskbarAppearance>();

        /// <summary>Приоритет проверки: от самого частного к общему.</summary>
        public static readonly TaskbarState[] Order =
        {
            TaskbarState.BatterySaver,
            TaskbarState.TaskViewOpened,
            TaskbarState.SearchOpened,
            TaskbarState.StartOpened,
            TaskbarState.MaximizedWindow,
            TaskbarState.VisibleWindow,
            TaskbarState.Desktop
        };

        public static readonly TaskbarState[] All = Order;

        public TaskbarSettings()
        {
            foreach (TaskbarState s in Order) States[s] = new TaskbarAppearance();
        }

        public TaskbarAppearance this[TaskbarState state]
        {
            get { return States[state]; }
        }

        public bool AnyEnabled
        {
            get
            {
                foreach (var kv in States) if (kv.Value.Enabled) return true;
                return false;
            }
        }

        public TaskbarSettings Copy()
        {
            var copy = new TaskbarSettings();
            foreach (var kv in States) copy.States[kv.Key] = kv.Value.Copy();
            return copy;
        }

        public static string Describe(TaskbarState state)
        {
            switch (state)
            {
                case TaskbarState.Desktop: return "Виден рабочий стол";
                case TaskbarState.VisibleWindow: return "Открыто окно";
                case TaskbarState.MaximizedWindow: return "Окно во весь экран";
                case TaskbarState.StartOpened: return "Открыт «Пуск»";
                case TaskbarState.SearchOpened: return "Открыт поиск";
                case TaskbarState.TaskViewOpened: return "Представление задач";
                case TaskbarState.BatterySaver: return "Экономия заряда";
                default: return state.ToString();
            }
        }

        public static string Key(TaskbarState state)
        {
            switch (state)
            {
                case TaskbarState.Desktop: return "desktop";
                case TaskbarState.VisibleWindow: return "visible";
                case TaskbarState.MaximizedWindow: return "maximized";
                case TaskbarState.StartOpened: return "start";
                case TaskbarState.SearchOpened: return "search";
                case TaskbarState.TaskViewOpened: return "taskview";
                case TaskbarState.BatterySaver: return "battery";
                default: return state.ToString().ToLowerInvariant();
            }
        }

        public static bool TryParseKey(string key, out TaskbarState state)
        {
            foreach (TaskbarState s in Order)
            {
                if (Key(s) == key) { state = s; return true; }
            }
            state = TaskbarState.Desktop;
            return false;
        }

        /// <summary>Строки для файла профиля — по одной на состояние.</summary>
        public List<string> Serialize()
        {
            var lines = new List<string>();
            foreach (TaskbarState s in Order)
                lines.Add(Key(s) + " = " + States[s].Serialize());
            return lines;
        }

        /// <summary>Старый формат: единственное поле taskbar = clear.</summary>
        public void AdoptLegacy(string accent)
        {
            if (string.IsNullOrEmpty(accent)) return;
            var a = States[TaskbarState.Desktop];
            a.Enabled = true;
            a.Accent = accent == "normal" ? "default" : accent;
        }
    }
}
