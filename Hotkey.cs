// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    internal static class Hotkey
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private static readonly Dictionary<string, uint> Keys = BuildKeys();

        private static Dictionary<string, uint> BuildKeys()
        {
            var d = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            for (char c = 'A'; c <= 'Z'; c++) d[c.ToString()] = (uint)c;
            for (int i = 0; i <= 9; i++) d[i.ToString()] = (uint)(0x30 + i);
            for (int i = 1; i <= 24; i++) d["F" + i] = (uint)(0x6F + i);
            for (int i = 0; i <= 9; i++) { d["Num" + i] = (uint)(0x60 + i); d["Numpad" + i] = (uint)(0x60 + i); }
            d["Esc"] = 0x1B; d["Escape"] = 0x1B;
            d["Tab"] = 0x09;
            d["Space"] = 0x20;
            d["Enter"] = 0x0D; d["Return"] = 0x0D;
            d["Backspace"] = 0x08;
            d["Pause"] = 0x13; d["Break"] = 0x13;
            d["ScrollLock"] = 0x91; d["Scroll"] = 0x91;
            d["CapsLock"] = 0x14;
            d["NumLock"] = 0x90;
            d["PrintScreen"] = 0x2C; d["PrtSc"] = 0x2C;
            d["Ins"] = 0x2D; d["Insert"] = 0x2D;
            d["Del"] = 0x2E; d["Delete"] = 0x2E;
            d["Home"] = 0x24; d["End"] = 0x23;
            d["PgUp"] = 0x21; d["PageUp"] = 0x21;
            d["PgDn"] = 0x22; d["PageDown"] = 0x22;
            d["Left"] = 0x25; d["Up"] = 0x26; d["Right"] = 0x27; d["Down"] = 0x28;
            d["NumMul"] = 0x6A; d["NumPlus"] = 0x6B; d["NumMinus"] = 0x6D;
            d["NumDot"] = 0x6E; d["NumDiv"] = 0x6F;
            d["Tilde"] = 0xC0; d["`"] = 0xC0;
            d["Minus"] = 0xBD; d["-"] = 0xBD;
            d["Plus"] = 0xBB; d["="] = 0xBB;
            d["["] = 0xDB; d["]"] = 0xDD;
            d[";"] = 0xBA; d["'"] = 0xDE;
            d[","] = 0xBC; d["."] = 0xBE; d["/"] = 0xBF; d["\\"] = 0xDC;
            return d;
        }

        /// <summary>Превращает нажатие в строку вида "Ctrl+Alt+1". null — такую клавишу назначить нельзя.</summary>
        public static string Describe(System.Windows.Input.ModifierKeys mods, System.Windows.Input.Key key)
        {
            string name = KeyName(key);
            if (name == null) return null;

            var sb = new System.Text.StringBuilder();
            if ((mods & System.Windows.Input.ModifierKeys.Control) != 0) sb.Append("Ctrl+");
            if ((mods & System.Windows.Input.ModifierKeys.Alt) != 0) sb.Append("Alt+");
            if ((mods & System.Windows.Input.ModifierKeys.Shift) != 0) sb.Append("Shift+");
            if ((mods & System.Windows.Input.ModifierKeys.Windows) != 0) sb.Append("Win+");
            sb.Append(name);
            return sb.ToString();
        }

        private static string KeyName(System.Windows.Input.Key key)
        {
            var K = typeof(System.Windows.Input.Key);
            string raw = Enum.GetName(K, key);
            if (raw == null) return null;

            if (raw.Length == 1 && raw[0] >= 'A' && raw[0] <= 'Z') return raw;
            if (raw.Length == 2 && raw[0] == 'D' && raw[1] >= '0' && raw[1] <= '9') return raw.Substring(1);
            if (raw.StartsWith("NumPad")) return "Num" + raw.Substring(6);
            if (raw.Length >= 2 && raw[0] == 'F')
            {
                int n;
                if (int.TryParse(raw.Substring(1), out n) && n >= 1 && n <= 24) return raw;
            }
            switch (raw)
            {
                case "Escape": return "Esc";
                case "Return": case "Enter": return "Enter";
                case "Space": return "Space";
                case "Tab": return "Tab";
                case "Back": return "Backspace";
                case "Insert": return "Ins";
                case "Delete": return "Del";
                case "Home": return "Home";
                case "End": return "End";
                case "PageUp": case "Prior": return "PgUp";
                case "PageDown": case "Next": return "PgDn";
                case "Left": return "Left";
                case "Right": return "Right";
                case "Up": return "Up";
                case "Down": return "Down";
                case "Pause": return "Pause";
                case "Scroll": return "ScrollLock";
                case "PrintScreen": case "Snapshot": return "PrintScreen";
                case "Add": return "NumPlus";
                case "Subtract": return "NumMinus";
                case "Multiply": return "NumMul";
                case "Divide": return "NumDiv";
                case "Decimal": return "NumDot";
                case "OemTilde": return "Tilde";
                case "OemMinus": return "Minus";
                case "OemPlus": return "Plus";
                case "OemOpenBrackets": return "[";
                case "OemCloseBrackets": return "]";
                case "OemSemicolon": return ";";
                case "OemQuotes": return "'";
                case "OemComma": return ",";
                case "OemPeriod": return ".";
                case "OemQuestion": return "/";
                case "OemBackslash": case "OemPipe": return "\\";
                default: return null;
            }
        }

        /// <summary>Разбирает "Ctrl+Alt+1", "Win+F12", "Pause".</summary>
        public static bool TryParse(string text, out uint modifiers, out uint vk, out string error)
        {
            modifiers = 0; vk = 0; error = null;
            if (string.IsNullOrEmpty(text)) { error = "пустое сочетание"; return false; }

            string[] parts = text.Split('+');
            string keyPart = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0)
                {
                    // Случай «Ctrl++» — плюс как клавиша. Проверка по самой строке,
                    // а не по числу частей: «Ctrl+» — незаконченный ввод — тоже даёт
                    // пустой хвост, и прежде он молча превращался в рабочее
                    // сочетание Ctrl+«=». Человек не дописал имя клавиши, а сочетание
                    // ему всё равно назначили.
                    if (i == parts.Length - 1 && keyPart == null &&
                        text.Length - text.TrimEnd('+').Length == 2)
                    {
                        keyPart = "+";
                        continue;
                    }
                    continue;
                }
                if (string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Control", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_CONTROL;
                else if (string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_ALT;
                else if (string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_SHIFT;
                else if (string.Equals(p, "Win", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= MOD_WIN;
                else keyPart = p;
            }

            if (keyPart == null) { error = "не указана клавиша в «" + text + "»"; return false; }
            if (keyPart == "+") keyPart = "Plus";
            if (!Keys.TryGetValue(keyPart, out vk)) { error = "неизвестная клавиша «" + keyPart + "»"; return false; }

            // Клавиша без приставки — это клавиша, отобранная у всей системы.
            // Назначив профилю «A», пользователь перестал бы набирать эту букву
            // где угодно, пока программа работает: RegisterHotKey забирает нажатие
            // до того, как оно дойдёт до окна, в котором печатают. «Shift+A» так же
            // уносит заглавную. Приставкой считаем только Ctrl, Alt и Win; Shift
            // сам по себе ничего не спасает.
            if ((modifiers & (MOD_CONTROL | MOD_ALT | MOD_WIN)) == 0 && !StandaloneSafe(vk))
            {
                error = "добавьте Ctrl, Alt или Win — иначе клавиша перестанет работать в других программах";
                return false;
            }

            modifiers |= MOD_NOREPEAT;
            return true;
        }

        /// <summary>
        /// Клавиши, которые можно занять целиком: ими ничего не набирают и в
        /// обычной работе их не нажимают. F13…F24 на большинстве клавиатур вообще
        /// нет физически — их шлют макросы и дополнительные ряды.
        /// </summary>
        private static bool StandaloneSafe(uint vk)
        {
            if (vk >= 0x7C && vk <= 0x87) return true;   // F13…F24
            return vk == 0x13                            // Pause
                || vk == 0x91;                           // ScrollLock
        }
    }
}
