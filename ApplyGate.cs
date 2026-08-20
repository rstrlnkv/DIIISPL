// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace DIIISPL
{
    /// <summary>
    /// Затвор перед перестроением дисплеев: пропускает по одному и не чаще,
    /// чем раз в 1,2 секунды.
    ///
    /// Раньше и то, и другое держалось в статическом поле — то есть внутри одного
    /// процесса. А команда из консоли работает в своём: `Cli.Run` вызывается в
    /// `App.Main` до мьютекса единственного экземпляра, и цикл вида
    /// `DIIISPL.exe /apply A` не выдерживал ничего. Это ровно та нагрузка, которой
    /// 19.08.2026 машину увело в BSOD `0x13A`: около сорока перестроений подряд.
    ///
    /// Поэтому и очередь, и отметка времени общие для всех процессов сеанса:
    /// именованный мьютекс и файл рядом с профилями. Отметка ставится **по
    /// окончании** работы, а не в начале: тяжёлое применение с запасными попытками
    /// занимает секунд десять, и пауза, отмеренная от начала, к концу уже истекла.
    /// </summary>
    internal sealed class ApplyGate : IDisposable
    {
        private const int MinIntervalMs = 1200;

        /// <summary>Дольше этого не ждём: подвисший сосед не должен морозить программу навсегда.</summary>
        private const int MaxWaitMs = 30000;

        private const string MutexName = "Local\\DIIISPL.Apply";

        private Mutex _mutex;
        private bool _held;

        /// <summary>Встать в очередь. Возвращает затвор, который надо закрыть.</summary>
        public static ApplyGate Enter()
        {
            var gate = new ApplyGate();
            try
            {
                bool created;
                gate._mutex = new Mutex(false, MutexName, out created);
                try { gate._held = gate._mutex.WaitOne(MaxWaitMs); }
                catch (AbandonedMutexException) { gate._held = true; }   // сосед умер, не отпустив — теперь наш
            }
            catch
            {
                // без мьютекса работаем как раньше: хуже, чем с ним, но лучше, чем никак
                gate._mutex = null;
            }
            return gate;
        }

        /// <summary>Выдержать паузу с прошлого перестроения. Звать перед тем, как трогать дисплеи.</summary>
        public void Wait()
        {
            double since = (DateTime.UtcNow - Read()).TotalMilliseconds;
            if (since >= 0 && since < MinIntervalMs)
                Thread.Sleep((int)(MinIntervalMs - since));
        }

        /// <summary>Отметить, что перестроение закончилось. Звать в finally.</summary>
        public void Stamp()
        {
            try
            {
                File.WriteAllText(StampPath,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch { /* некуда записать — останется пауза внутри процесса */ }
            _fallback = DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (_mutex == null) return;
            try { if (_held) _mutex.ReleaseMutex(); }
            catch { }
            _mutex.Close();
            _mutex = null;
        }

        // ------------------------------------------------------------------ отметка
        private static DateTime _fallback = DateTime.MinValue;

        private static string StampPath
        {
            get { return Path.Combine(Store.DataDir, "last-apply"); }
        }

        private static DateTime Read()
        {
            try
            {
                if (File.Exists(StampPath))
                {
                    long ticks;
                    if (long.TryParse(File.ReadAllText(StampPath).Trim(),
                                      NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
                    {
                        var stamped = new DateTime(ticks, DateTimeKind.Utc);
                        // Часы могли перевести назад: отметка из будущего означает,
                        // что верить ей нельзя, — тогда просто ждём полную паузу.
                        if (stamped > DateTime.UtcNow) return DateTime.UtcNow;
                        return stamped > _fallback ? stamped : _fallback;
                    }
                }
            }
            catch { }
            return _fallback;
        }
    }
}
