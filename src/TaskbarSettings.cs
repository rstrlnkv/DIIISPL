// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace DIIISPL
{
    /// <summary>States in which the taskbar can look different.</summary>
    internal enum TaskbarState
    {
        Desktop,          // the desktop is visible
        VisibleWindow,    // there is an open window on this screen
        MaximizedWindow,  // a window is maximized on this screen
        StartOpened,      // the Start menu is open
        SearchOpened,     // search is open
        TaskViewOpened,   // Task View is open
        BatterySaver      // Battery Saver is on
    }

    /// <summary>What the taskbar looks like in one state.</summary>
    internal class TaskbarAppearance
    {
        public bool Enabled;
        public string Accent = "default";     // default, opaque, clear, blur, acrylic
        public uint Color = 0x00000000;       // AARRGGBB
        public bool ShowLine = true;          // a thin line at the edge of the taskbar
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
    /// The set of taskbar looks for all states. Order matters: states are checked
    /// top to bottom, and the first one that is enabled and matches sets the look. "Desktop" is
    /// the base state, applied when nothing else matched.
    /// </summary>
    internal class TaskbarSettings
    {
        /// <summary>Format number: shows whether the file was written by the current settings screen or the previous one.</summary>
        public const int Format = 2;

        public readonly Dictionary<TaskbarState, TaskbarAppearance> States =
            new Dictionary<TaskbarState, TaskbarAppearance>();

        /// <summary>The file was written in the current format — no special cases to clean up.</summary>
        public bool Versioned;

        /// <summary>Check priority: from the most specific to the most general.</summary>
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
            copy.Versioned = Versioned;
            foreach (var kv in States) copy.States[kv.Key] = kv.Value.Copy();
            return copy;
        }

        /// <summary>
        /// The previous settings screen showed seven equal rows, and each had "As in the system"
        /// by default. A row nobody got around to turned into an order:
        /// "in this case give back the Windows taskbar". So for a profile with a transparent taskbar it
        /// became ordinary once a window was maximized or "Start" opened — a look
        /// nobody chose. The current screen holds "Usual look" for that, so all
        /// untouched cases are moved to it — once, for files without a format number.
        /// A deliberate "As in the system" choice is lost this way too, but setting it again is
        /// one row in the settings, whereas working out why the taskbar lives its own life takes longer.
        /// </summary>
        public void DropUntouchedSpecialCases()
        {
            foreach (TaskbarState s in Order)
            {
                if (s == TaskbarState.Desktop) continue;   // the usual look, which is also the fallback
                var a = States[s];
                if (a.Enabled && a.Accent == "default" && a.Color == 0) a.Enabled = false;
            }
        }

        public static string Describe(TaskbarState state)
        {
            switch (state)
            {
                case TaskbarState.Desktop: return Text.TbState("desktop");
                case TaskbarState.VisibleWindow: return Text.TbState("visibleWindow");
                case TaskbarState.MaximizedWindow: return Text.TbState("maximized");
                case TaskbarState.StartOpened: return Text.TbState("start");
                case TaskbarState.SearchOpened: return Text.TbState("search");
                case TaskbarState.TaskViewOpened: return Text.TbState("taskView");
                case TaskbarState.BatterySaver: return Text.TbState("battery");
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

        /// <summary>Lines for the profile file — one per state.</summary>
        public List<string> Serialize()
        {
            var lines = new List<string>();
            lines.Add("version = " + Format);
            foreach (TaskbarState s in Order)
                lines.Add(Key(s) + " = " + States[s].Serialize());
            return lines;
        }

        /// <summary>Old format: the single field taskbar = clear.</summary>
        public void AdoptLegacy(string accent)
        {
            if (string.IsNullOrEmpty(accent)) return;
            var a = States[TaskbarState.Desktop];
            a.Enabled = true;
            a.Accent = accent == "normal" ? "default" : accent;
        }
    }
}
