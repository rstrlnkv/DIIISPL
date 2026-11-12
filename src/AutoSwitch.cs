// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    /// <summary>
    /// Выбор профиля по составу экранов — «включил телевизор, раскладка встала сама».
    ///
    /// Решение живёт здесь, отдельно от того, кто его исполняет, по одной причине:
    /// его должны одинаково видеть и программа, и команда `DIIISPL.exe /auto`.
    /// Команда, которая пересказывает решение своими словами, рано или поздно
    /// расскажет не то, что программа сделает на самом деле.
    ///
    /// Поводом служит **состав**, а не событие. Windows шлёт `WM_DISPLAYCHANGE`
    /// на любое шевеление, в том числе на наше собственное применение раскладки, —
    /// и «применили, получили событие, применили снова» это ровно та плотная
    /// очередь перестроений, которой 19.08.2026 машину увело в BSOD. Применение
    /// раскладки состав не меняет: те же мониторы остаются подключёнными. Значит
    /// петли не будет, даже если все прочие предосторожности снять.
    /// </summary>
    internal static class AutoSwitch
    {
        /// <summary>
        /// Кто сейчас подключён — ключи всех мониторов, которые видит система,
        /// упорядоченные, чтобы два состава можно было сравнить построчно.
        ///
        /// Именно подключён, а не включён в раскладке: выключенный по HDMI
        /// телевизор исчезает из системы совсем, и это тот самый сигнал, ради
        /// которого всё затевается. Погашенный, но подключённый монитор из
        /// состава не выпадает — его профиль обязан зажечь.
        /// </summary>
        public static List<string> Composition(List<MonitorInfo> live)
        {
            var keys = new List<string>();
            foreach (var m in live)
                if (!string.IsNullOrEmpty(m.Identity) && !keys.Contains(m.Identity))
                    keys.Add(m.Identity);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary>Тот же ли это состав. Пустой список и «не знаем» — разные вещи, поэтому null не равен ничему.</summary>
        public static bool Same(List<string> a, List<string> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>
        /// Профиль под этот состав — или null, если такого нет.
        ///
        /// Применим тот, все включённые мониторы которого подключены. Требовать
        /// **точного равенства** нельзя, и это измерено на живых профилях
        /// 26.08.2026: профиль «Телевизор» нарочно оставляет два экрана тёмными,
        /// хотя провода из них никто не вынимал, — при точном равенстве он не
        /// сработал бы никогда. Профиль перечисляет все известные мониторы и
        /// говорит, какие зажечь; подключённый, но погашенный им — это его
        /// замысел, а не несовпадение.
        ///
        /// Из применимых берём тот, что задействует больше экранов: иначе,
        /// подключив всё, человек получал бы профиль на один монитор — он
        /// применим всегда. При равенстве побеждает первый по списку: порядок
        /// задаёт человек, двигает его `Alt+↑` и `Alt+↓` и видит глазами.
        /// Правило, которое видно в окне, объяснять не нужно.
        /// </summary>
        public static Layout Pick(List<string> composition, List<Layout> profiles)
        {
            if (composition == null || composition.Count == 0 || profiles == null) return null;

            Layout best = null;
            int bestUsed = 0;
            foreach (Layout l in profiles)
            {
                if (!l.Auto) continue;                // исключён человеком — клавишей применяется, сам не встаёт

                List<string> wanted = Wanted(l);
                if (wanted.Count == 0) continue;      // профиль, гасящий всё, не выбираем никогда

                bool all = true;
                foreach (string id in wanted)
                    if (!composition.Contains(id)) { all = false; break; }
                if (!all) continue;

                // Строго больше — значит при равенстве остаётся найденный раньше,
                // то есть первый по списку.
                if (wanted.Count > bestUsed) { best = l; bestUsed = wanted.Count; }
            }
            return best;
        }

        /// <summary>Какие мониторы профиль хочет видеть включёнными.</summary>
        public static List<string> Wanted(Layout layout)
        {
            var keys = new List<string>();
            foreach (LayoutDisplay d in layout.Displays)
                if (d.Enabled && !string.IsNullOrEmpty(d.Identity) && !keys.Contains(d.Identity))
                    keys.Add(d.Identity);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }
}
