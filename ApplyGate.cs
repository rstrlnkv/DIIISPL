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
    /// Идёт ли перестроение прямо сейчас — один признак на всю программу.
    ///
    /// Прежде таких признака было два: свой у окна и свой у трея, и друг про
    /// друга они не знали. Отсюда: кнопка «Применить» оставалась доступной, пока
    /// перестраивала горячая клавиша, — второе применение вставало в затвор и
    /// ждало до тридцати секунд; а «Выход» из трея не видел работы, начатой
    /// кнопкой, и обрывал её посреди вызовов в драйвер.
    ///
    /// Затвор от одновременности защищает и без этого, но защищает он ядро, а не
    /// человека: два перестроения подряд там, где человек просил одно, — всё
    /// равно два.
    /// </summary>
    internal static class ApplyBusy
    {
        private static int _active;

        public static bool Active
        {
            get { return Thread.VolatileRead(ref _active) != 0; }
        }

        /// <summary>Занять. Ложь — уже занято кем-то другим.</summary>
        public static bool Begin()
        {
            return Interlocked.CompareExchange(ref _active, 1, 0) == 0;
        }

        public static void End()
        {
            Interlocked.Exchange(ref _active, 0);
        }
    }

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
        private bool _forceFullPause;

        /// <summary>
        /// Пропустили ли нас. Ложь означает «не пропустили» — трогать дисплеи нельзя.
        ///
        /// Раньше `Enter` возвращал затвор в любом случае, а `_held` не читал никто.
        /// По истечении ожидания защиты не оставалось вовсе — ни очереди, ни паузы:
        /// отметка ставится по окончании работы, значит пока сосед перестраивает
        /// дисплеи, в файле лежит конец прошлого применения, и пауза пропускала
        /// без задержки. Это ровно та нагрузка, ради которой затвор и писался.
        /// </summary>
        public bool Held
        {
            get { return _mutex == null || _held; }
        }

        /// <summary>Встать в очередь. Возвращает затвор, который надо закрыть.</summary>
        public static ApplyGate Enter()
        {
            var gate = new ApplyGate();
            try
            {
                bool created;
                gate._mutex = new Mutex(false, MutexName, out created);
                try { gate._held = gate._mutex.WaitOne(MaxWaitMs); }
                catch (AbandonedMutexException)
                {
                    // Сосед умер, не отпустив: владение перешло к нам, и отпустить
                    // его обязаны мы.
                    gate._held = true;
                    gate._forceFullPause = true;
                }

                // На брошенный мьютекс полагаться нельзя, и это измерено: он
                // приходит только если на момент смерти описатель держал кто-то
                // ещё. Когда умерший был единственным, ядро уничтожает объект
                // целиком, здесь создаётся новый — никем не брошенный, — ожидание
                // проходит мгновенно, а в файле лежит конец **позапрошлого**
                // применения, то есть пауза не выдерживается вовсе.
                //
                // Поэтому смерть посреди перестроения отмечается своим файлом:
                // он создаётся перед первым обращением к дисплеям и убирается по
                // окончании. Мы держим очередь — значит между началом и концом
                // сейчас никого нет; уцелевший файл может означать только одно:
                // прошлый перестраивал экраны и не дожил до конца. Это ровно тот
                // случай, ради которого пауза и написана.
                if (gate._held && File.Exists(BusyPath))
                {
                    gate._forceFullPause = true;
                    try { File.Delete(BusyPath); }
                    catch { }
                }
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
            MarkBusy();

            if (_forceFullPause)
            {
                _forceFullPause = false;
                Thread.Sleep(MinIntervalMs);
                return;
            }

            DateTime last = Read();
            if (last == Unknown)
            {
                // Отметку не удалось прочитать — например, сосед как раз её дописывал.
                // Не знать, когда было прошлое перестроение, значит ждать полностью:
                // прежде здесь получался ноль паузы ровно в тот миг, когда сосед
                // заканчивал работу, то есть когда следующий как раз просыпался.
                Thread.Sleep(MinIntervalMs);
                return;
            }

            double since = (DateTime.UtcNow - last).TotalMilliseconds;
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

            try { if (File.Exists(BusyPath)) File.Delete(BusyPath); }
            catch { }
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

        /// <summary>«Когда было прошлое перестроение — неизвестно». Значит ждать полностью.</summary>
        private static readonly DateTime Unknown = DateTime.MinValue;

        /// <summary>
        /// Раньше 2020 года отметки быть не может, и это не придирка к календарю.
        /// Обрывок недописанного числа — «638000» вместо девятнадцати знаков —
        /// разбирается успешно и даёт дату первого века: пауза выходит
        /// отрицательной длины, то есть затвор открывается мгновенно. Измерено:
        /// «638000» и «6» пропускали без задержки, тогда как «abc» — нет.
        /// </summary>
        private static readonly long MinPlausibleTicks = new DateTime(2020, 1, 1).Ticks;

        private static string StampPath
        {
            get { return Path.Combine(Store.DataDir, "last-apply"); }
        }

        /// <summary>Файл «идёт перестроение». Уцелел — значит прошлый не дожил до конца.</summary>
        private static string BusyPath
        {
            get { return Path.Combine(Store.DataDir, "apply-busy"); }
        }

        /// <summary>Пометить, что дисплеи вот-вот тронут. Снимает пометку Stamp.</summary>
        private static void MarkBusy()
        {
            try { File.WriteAllText(BusyPath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)); }
            catch { }
        }

        private static DateTime Read()
        {
            try
            {
                if (!File.Exists(StampPath)) return _fallback;

                // Читаем, разрешая соседу писать в тот же миг. Прежде файл
                // открывался так, что чужая запись делала чтение невозможным —
                // и происходило это ровно в конце чужого применения, то есть
                // когда следующий как раз просыпался и пауза была нужнее всего.
                string text;
                using (var stream = new FileStream(StampPath, FileMode.Open, FileAccess.Read,
                                                   FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();

                long ticks;
                if (!long.TryParse(text.Trim(), NumberStyles.Integer,
                                   CultureInfo.InvariantCulture, out ticks))
                    return Unknown;      // мусор или обрывок недописанного — не знаем

                if (ticks < MinPlausibleTicks) return Unknown;   // обрывок недописанного числа

                var stamped = new DateTime(ticks, DateTimeKind.Utc);
                // Часы могли перевести назад: отметка из будущего означает,
                // что верить ей нельзя, — тогда просто ждём полную паузу.
                if (stamped > DateTime.UtcNow) return Unknown;
                return stamped > _fallback ? stamped : _fallback;
            }
            catch { return Unknown; }
        }
    }
}
