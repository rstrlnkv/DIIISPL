// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace DIIISPL
{
    /// <summary>
    /// Whether a rebuild is running right now — one flag for the whole program.
    ///
    /// There used to be two such flags: one in the window and one in the tray, and they knew nothing
    /// about each other. Hence: the Apply button stayed available while a
    /// hotkey was rebuilding — the second apply queued at the gate and
    /// waited up to thirty seconds; and "Exit" from the tray did not see work started by the
    /// button and cut it off in the middle of driver calls.
    ///
    /// The gate protects against simultaneity even without this, but it protects the kernel, not
    /// the person: two rebuilds in a row where the person asked for one are
    /// still two.
    /// </summary>
    internal static class ApplyBusy
    {
        private static int _active;

        public static bool Active
        {
            get { return Thread.VolatileRead(ref _active) != 0; }
        }

        /// <summary>Take it. False — already taken by someone else.</summary>
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
    /// The gate before a display rebuild: lets one through at a time and no more often
    /// than once every 1.2 seconds.
    ///
    /// Previously both were held in a static field — that is, within one
    /// process. But a console command runs in its own: `Cli.Run` is called in
    /// `App.Main` before the single-instance mutex, and a loop like
    /// `DIIISPL.exe /apply A` withstood nothing. This is exactly the load that on
    /// 19.08.2026 sent the machine into BSOD `0x13A`: about forty rebuilds in a row.
    ///
    /// So both the queue and the timestamp are shared by all processes of the session:
    /// a named mutex and a file next to the profiles. The timestamp is set **at the
    /// end** of the work, not at the start: a heavy apply with fallback attempts
    /// takes about ten seconds, and a pause measured from the start has expired by the end.
    /// </summary>
    internal sealed class ApplyGate : IDisposable
    {
        private const int MinIntervalMs = 1200;

        /// <summary>We do not wait longer than this: a hung neighbour must not freeze the program forever.</summary>
        private const int MaxWaitMs = 30000;

        private const string MutexName = "Local\\DIIISPL.Apply";

        private Mutex _mutex;
        private bool _held;
        private bool _forceFullPause;

        /// <summary>
        /// Whether we were let through. False means "not let through" — the displays must not be touched.
        ///
        /// Previously `Enter` returned the gate in any case, and nobody read `_held`.
        /// When the wait ran out there was no protection at all — no queue, no pause:
        /// the timestamp is set at the end of the work, so while a neighbour is rebuilding
        /// the displays, the file holds the end of the previous apply, and the pause let us through
        /// without delay. This is exactly the load the gate was written for.
        /// </summary>
        public bool Held
        {
            get { return _mutex == null || _held; }
        }

        /// <summary>Join the queue. Returns the gate, which must be closed.</summary>
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
                    // The neighbour died without releasing: ownership passed to us, and we
                    // are obliged to release it.
                    gate._held = true;
                    gate._forceFullPause = true;
                }

                // An abandoned mutex cannot be relied on, and this was measured: it
                // arrives only if at the moment of death someone else held the handle.
                // When the dead process was the only one, the kernel destroys the object
                // entirely, a new one is created here — abandoned by no one — the wait
                // passes instantly, and the file holds the end of the **one before the last**
                // apply, so the pause is not observed at all.
                //
                // So death in the middle of a rebuild is marked by a file of its own:
                // it is created before the first access to the displays and removed at
                // the end. We hold the queue — so between start and end
                // there is nobody now; a surviving file can mean only one thing:
                // the previous one was rebuilding the screens and did not live to the end. This is exactly the
                // case the pause was written for.
                if (gate._held && File.Exists(BusyPath))
                {
                    gate._forceFullPause = true;
                    try { File.Delete(BusyPath); }
                    catch { }
                }
            }
            catch
            {
                // without the mutex we work as before: worse than with it, but better than nothing
                gate._mutex = null;
            }
            return gate;
        }

        /// <summary>Observe the pause since the previous rebuild. Call before touching the displays.</summary>
        public void Wait()
        {
            // If we could not write the marker, we will not be able to write the
            // timestamp either, and without both the gate protects against nothing: a neighbour's death
            // in the middle of a rebuild becomes invisible, and the next apply goes
            // into the driver right after the broken one. Measured: 6 ms instead of 1200.
            //
            // Then we always wait. The data folder may be on a network drive or
            // write-protected; an extra second there is the price of the protection
            // remaining rather than silently vanishing.
            if (!MarkBusy())
            {
                _forceFullPause = false;
                Thread.Sleep(MinIntervalMs);
                return;
            }

            if (_forceFullPause)
            {
                _forceFullPause = false;
                Thread.Sleep(MinIntervalMs);
                return;
            }

            DateTime last = Read();
            if (last == Unknown)
            {
                // The timestamp could not be read — e.g. the neighbour was just writing it.
                // Not knowing when the previous rebuild was means waiting in full:
                // previously this gave zero pause exactly at the moment the neighbour
                // finished its work, that is, when the next one was just waking up.
                Thread.Sleep(MinIntervalMs);
                return;
            }

            double since = (DateTime.UtcNow - last).TotalMilliseconds;
            if (since >= 0 && since < MinIntervalMs)
                Thread.Sleep((int)(MinIntervalMs - since));
        }

        /// <summary>Mark that the rebuild has finished. Call in finally.</summary>
        public void Stamp()
        {
            try
            {
                File.WriteAllText(StampPath,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch { /* nowhere to write — the in-process pause remains */ }
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

        // ------------------------------------------------------------------ timestamp
        private static DateTime _fallback = DateTime.MinValue;

        /// <summary>"When the previous rebuild was is unknown". So wait in full.</summary>
        private static readonly DateTime Unknown = DateTime.MinValue;

        /// <summary>
        /// There can be no timestamp before 2020, and this is not a calendar quibble.
        /// A fragment of an unfinished number — "638000" instead of nineteen digits —
        /// parses successfully and gives a date in the first century: the pause comes out
        /// of negative length, that is, the gate opens instantly. Measured:
        /// "638000" and "6" passed without delay, while "abc" did not.
        /// </summary>
        private static readonly long MinPlausibleTicks = new DateTime(2020, 1, 1).Ticks;

        private static string StampPath
        {
            get { return Path.Combine(Store.DataDir, "last-apply"); }
        }

        /// <summary>The "rebuild in progress" file. If it survived, the previous one did not live to the end.</summary>
        private static string BusyPath
        {
            get { return Path.Combine(Store.DataDir, "apply-busy"); }
        }

        /// <summary>
        /// Mark that the displays are about to be touched. Stamp removes the marker.
        /// False — could not write.
        /// </summary>
        private static bool MarkBusy()
        {
            try
            {
                File.WriteAllText(BusyPath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch { return false; }
        }

        private static DateTime Read()
        {
            try
            {
                if (!File.Exists(StampPath)) return _fallback;

                // We read, allowing the neighbour to write at the same moment. Previously the file
                // was opened so that a foreign write made reading impossible —
                // and this happened exactly at the end of another apply, that is,
                // when the next one was just waking up and the pause was needed most.
                string text;
                using (var stream = new FileStream(StampPath, FileMode.Open, FileAccess.Read,
                                                   FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();

                long ticks;
                if (!long.TryParse(text.Trim(), NumberStyles.Integer,
                                   CultureInfo.InvariantCulture, out ticks))
                    return Unknown;      // garbage or a fragment of an unfinished one — we do not know

                if (ticks < MinPlausibleTicks) return Unknown;   // fragment of an unfinished number

                var stamped = new DateTime(ticks, DateTimeKind.Utc);
                // The clock may have been set back: a timestamp from the future means
                // it cannot be trusted — then we simply wait the full pause.
                if (stamped > DateTime.UtcNow) return Unknown;
                return stamped > _fallback ? stamped : _fallback;
            }
            catch { return Unknown; }
        }
    }
}
