// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;

namespace DIIISPL
{
    internal class ApplyResult
    {
        public bool Success;
        public string Error;
        public List<string> Warnings = new List<string>();
        /// <summary>Monitors that actually made it into the request (were connected at build time).</summary>
        public List<string> Requested = new List<string>();
    }

    /// <summary>
    /// Turns a profile into a display configuration: builds the CCD paths and modes itself,
    /// without relying on what Windows remembered about these monitors.
    /// </summary>
    internal static class Applier
    {
        private class Group
        {
            public string Key;
            public LayoutDisplay Rep;                       // group representative (sets position and mode)
            public List<LayoutDisplay> Members = new List<LayoutDisplay>();
            public List<MonitorInfo> Monitors = new List<MonitorInfo>();
            public uint SourceId;
            public int ModeIndex;
        }

        /// <summary>Pause between the rungs of the attempt ladder within one apply.</summary>
        private const int RungPauseMs = 600;

        public static ApplyResult Apply(Layout layout)
        {
            // The queue and the pause are shared by all processes of the session, see ApplyGate.
            // The gate is taken before the first look at the displays: otherwise two that started
            // simultaneously would both see "layout already like this" and both go on.
            using (ApplyGate gate = ApplyGate.Enter())
            {
                // Not let through — so a neighbouring copy is still rebuilding the displays.
                // We must not go on: two simultaneous SetDisplayConfig calls are exactly the
                // load the gate exists for. Previously, when the wait ran out, we
                // went on as if nothing had happened.
                if (!gate.Held)
                {
                    var busy = new ApplyResult();
                    busy.Error = Text.AlreadyApplying;
                    return busy;
                }

                // Windows will not accept an overlapping layout: it will arrange the screens
                // its own way, and the profile will be left with impossible coordinates.
                // The check is here, and not only in the window: the hotkey, the tray icon menu
                // and a hand-edited file bypass the window.
                string overlap = layout.OverlapProblem();
                if (overlap != null)
                {
                    var bad = new ApplyResult();
                    bad.Error = overlap;
                    return bad;
                }

                try { return Apply(layout, gate); }
                finally { gate.Stamp(); }
            }
        }

        private static ApplyResult Apply(Layout layout, ApplyGate gate)
        {
            var res = new ApplyResult();

            // The layout is already exactly like this — we do not touch the displays.
            //
            // Every SetDisplayConfig call renegotiates the output: the screen blinks,
            // and on an HDR monitor there is also a noticeable brightness jump. Previously, re-applying
            // the same profile went the full way to the end — the strict request was
            // rejected, the fallback with relaxations kicked in — and every
            // press of an already pressed hotkey blinked the screen for no reason.
            //
            // We still set the taskbar look and HDR: they may have drifted apart
            // on their own, and they are cheap — HDR is touched only where it differs.
            // Here we ask exactly "will anything change", not "is this what is
            // on the screens": the questions differ. A profile asking for a TV that is off now
            // is not counted as active — its dot in the list does not
            // light — but there is nothing to apply for it anyway: the request skips
            // that monitor, and the rest is already as it should be.
            if (layout.ChangesNothing(Monitors.Enumerate()))
            {
                res.Success = true;
                ApplyHdr(layout, res, gate);
                ApplyTaskbar(layout, res);
                return res;
            }

            // Rebuilding the display configuration is a heavy operation in the graphics stack kernel.
            // Frequent back-to-back changes (a held hotkey, a script in a loop) can
            // hang or crash the video driver, so we keep a pause between applies.
            gate.Wait();

            // An output entering duplication gets a different target id from the driver: for a TV
            // it is 16781568 instead of 4352. A pair of "ordinary" ids does not exist, so a hand-built
            // configuration loses the second screen. If the duplicate is not built yet, let
            // Windows build it, and then we overlay the exact layout using the ids valid in the duplicate.
            if (HasClone(layout) && !ClonesFormed(layout))
            {
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | Native.SDC_TOPOLOGY_CLONE);
                System.Threading.Thread.Sleep(1300);
            }

            int err = Submit(layout, res);
            if (res.Error != null) return res;

            if (err != 0)
            {
                // In some states the system rejects any request entirely.
                // We reset the topology to a known-good one and try again.
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | Native.SDC_TOPOLOGY_EXTEND);
                System.Threading.Thread.Sleep(1400);
                res.Warnings.Clear();
                err = Submit(layout, res);
                if (res.Error != null) return res;
            }
            if (err != 0)
            {
                res.Error = Text.WindowsRefused(Native.ErrorText(err));
                return res;
            }
            res.Success = true;

            // The first attempt does not always bring up a blanked output: Windows applies the rest
            // and reports success. So we check the result and, if needed, insist.
            var missing = Missing(layout, res, 1300);
            if (missing.Count > 0)
            {
                res.Warnings.Clear();
                NoteLate(Submit(layout, res), res);
                missing = Missing(layout, res, 900);
            }

            // A separate trouble is duplication: some monitors (TVs especially) get a
            // different target id when they enter a duplicate, and a pair of "ordinary" ids simply does not exist.
            // So we ask Windows to build the duplication normally, and then overlay
            // the exact layout — already by the fresh output identifiers.
            if (missing.Count > 0 || !ClonesFormed(layout))
            {
                uint topology = HasClone(layout) ? Native.SDC_TOPOLOGY_CLONE : Native.SDC_TOPOLOGY_EXTEND;
                Native.SetDisplayTopology(0, IntPtr.Zero, 0, IntPtr.Zero, Native.SDC_APPLY | topology);
                System.Threading.Thread.Sleep(1500);
                res.Warnings.Clear();
                NoteLate(Submit(layout, res), res);
                System.Threading.Thread.Sleep(600);
            }

            Verify(layout, res);
            ApplyHdr(layout, res, gate);
            ApplyTaskbar(layout, res);
            return res;
        }

        /// <summary>
        /// Follow-up requests go after Success is set, and nobody used to read
        /// their return code: the layout could be left arranged by
        /// Windows itself, while `DIIISPL.exe apply X` in a script still printed
        /// "Applied" and returned zero. Success cannot be dropped here — part of the
        /// layout is applied — but staying silent is not allowed either.
        /// </summary>
        private static void NoteLate(int err, ApplyResult res)
        {
            if (res.Error != null) { res.Warnings.Add(res.Error); res.Error = null; }
            if (err == 0) return;
            res.Warnings.Add(Text.WindowsPartly(Native.ErrorText(err)));
        }

        /// <summary>
        /// Pass the taskbar settings from the profile to the watcher. The cache is reset necessarily:
        /// a layout change recreates the taskbars, and everything we remembered about their windows
        /// and the link with the service inside Explorer is invalid after that.
        ///
        /// The stable build has no taskbar at all — no settings screen, no
        /// watcher, no library inside Explorer — so there is nothing to apply.
        /// </summary>
        private static void ApplyTaskbar(Layout layout, ApplyResult res)
        {
#if DEV
            TaskbarWatcher.ResetCache();

            if (layout.Taskbar == null || !layout.Taskbar.AnyEnabled) { TaskbarWatcher.Use(null); TaskbarWatcher.ResetSoon(); return; }
            if (!ExplorerTap.Available)
            {
                res.Warnings.Add(Text.NoTaskbarLibrary);
                return;
            }

            TaskbarWatcher.Use(layout.Taskbar);

            // And once more when the taskbars settle: Windows recreates them not at once,
            // and everything the watcher managed to remember about them in the meantime is untrue.
            TaskbarWatcher.ResetSoon();
            if (ExplorerTap.LastError != null)
                res.Warnings.Add(Text.TaskbarPrefix(ExplorerTap.LastError));
#endif
        }

        private static bool HasClone(Layout layout)
        {
            foreach (var d in layout.Displays)
                if (d.Enabled && d.CloneGroup >= 0 && IsCloned(layout, d.Identity)) return true;
            return false;
        }

        /// <summary>Whether duplication groups came together: all members must sit on one source.</summary>
        private static bool ClonesFormed(Layout layout)
        {
            if (!HasClone(layout)) return true;
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var groupSource = new Dictionary<int, uint>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                MonitorInfo mon;

                // Two different cases, and they must not be confused.
                //
                // The monitor is not in the enumeration — "nothing to put together", we skip it.
                // Previously a failure stood here, and it was permanent: the monitor is not
                // connected, so the condition would never be met, and every
                // apply drove the system through two extra topology rebuilds.
                if (!byId.TryGetValue(d.Identity, out mon)) continue;

                // But a connected, blanked member is exactly the
                // state for which duplication is built by Windows itself:
                // an output in a duplicate gets a different target id, a hand-built
                // configuration does not bring it up, and the screen stays dark. It must not be counted
                // as "duplicate came together".
                if (!mon.Active) return false;
                uint src;
                if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                else if (src != mon.SourceId) return false;
            }
            return true;
        }

        private static int Submit(Layout layout, ApplyResult res)
        {
            var monitors = Monitors.Enumerate();
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in monitors) byId[m.Identity] = m;

            // --- members ---
            var groups = new List<Group>();
            var groupByKey = new Dictionary<string, Group>();

            foreach (var d in layout.Displays)
            {
                if (!d.Enabled) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon))
                {
                    res.Warnings.Add(Text.SkippedNotConnected(d.Name ?? d.Identity));
                    continue;
                }
                string key = d.CloneGroup >= 0 ? "g" + d.CloneGroup : "s" + d.Identity;
                Group g;
                if (!groupByKey.TryGetValue(key, out g))
                {
                    g = new Group();
                    g.Key = key;
                    g.Rep = d;
                    groupByKey[key] = g;
                    groups.Add(g);
                }
                g.Members.Add(d);
                g.Monitors.Add(mon);
                if (!res.Requested.Contains(d.Identity)) res.Requested.Add(d.Identity);
            }

            if (groups.Count == 0)
            {
                res.Error = Text.NoneConnected;
                return -1;
            }

            Native.DISPLAYCONFIG_PATH_INFO[] allPaths;
            Native.DISPLAYCONFIG_MODE_INFO[] allModes;
            if (!Monitors.QueryAllPaths(out allPaths, out allModes))
            {
                res.Error = Text.NoPathList;
                return -1;
            }

            // --- handing out source ids: one group = one desktop ---
            var usedSources = new Dictionary<string, bool>();
            foreach (var g in groups)
            {
                var candidates = new List<uint>(g.Monitors[0].ValidSourceIds);
                for (int i = 1; i < g.Monitors.Count; i++)
                {
                    var keep = new List<uint>();
                    foreach (uint s in candidates) if (g.Monitors[i].ValidSourceIds.Contains(s)) keep.Add(s);
                    candidates = keep;
                }
                candidates.Sort();

                // First we try the source on which these outputs work right now:
                // the video card does not support every "source + pair of outputs" combination,
                // and the lowest free number taken at random may turn out unworkable.
                uint current = 0;
                bool hasCurrent = g.Monitors[0].Active;
                if (hasCurrent)
                {
                    current = g.Monitors[0].SourceId;
                    for (int i = 1; i < g.Monitors.Count && hasCurrent; i++)
                        if (!g.Monitors[i].Active || g.Monitors[i].SourceId != current) hasCurrent = false;
                }
                if (hasCurrent && candidates.Contains(current))
                {
                    candidates.Remove(current);
                    candidates.Insert(0, current);
                }

                bool assigned = false;
                foreach (uint s in candidates)
                {
                    string ukey = g.Monitors[0].Adapter.Key + "/" + s;
                    if (usedSources.ContainsKey(ukey)) continue;
                    g.SourceId = s;
                    usedSources[ukey] = true;
                    assigned = true;
                    break;
                }
                if (!assigned)
                {
                    res.Error = Text.NoFreeDesktop(g.Rep.Name);
                    return -1;
                }
            }

            // --- source modes (one per group) ---
            var modes = new List<Native.DISPLAYCONFIG_MODE_INFO>();
            foreach (var g in groups)
            {
                var mode = new Native.DISPLAYCONFIG_MODE_INFO();
                mode.infoType = Native.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE;
                mode.id = g.SourceId;
                mode.adapterId = g.Monitors[0].Adapter;
                // the source mode is set in the panel's "native" orientation: Windows applies
                // rotation on top of it itself, and the rotated sides are not needed here
                mode.modeInfo.sourceMode.width = (uint)Math.Max(640, g.Rep.Width);
                mode.modeInfo.sourceMode.height = (uint)Math.Max(480, g.Rep.Height);
                mode.modeInfo.sourceMode.pixelFormat = Native.DISPLAYCONFIG_PIXELFORMAT_32BPP;
                mode.modeInfo.sourceMode.position.x = g.Rep.X;
                mode.modeInfo.sourceMode.position.y = g.Rep.Y;
                g.ModeIndex = modes.Count;
                modes.Add(mode);
            }

            // --- the primary monitor must stand at (0,0) ---
            // `break` from the inner loop exits only it: the group scan
            // continued, and the last group with a primary
            // monitor was found, not the first. Today there is exactly one primary, so there was no harm
            // done — but what was written was not what was meant.
            Group primary = null;
            foreach (var g in groups)
            {
                foreach (var d in g.Members)
                    if (d.Primary) { primary = g; break; }
                if (primary != null) break;
            }
            if (primary == null) primary = groups[0];

            int dx = -modes[primary.ModeIndex].modeInfo.sourceMode.position.x;
            int dy = -modes[primary.ModeIndex].modeInfo.sourceMode.position.y;
            if (dx != 0 || dy != 0)
            {
                for (int i = 0; i < modes.Count; i++)
                {
                    var m = modes[i];
                    m.modeInfo.sourceMode.position.x += dx;
                    m.modeInfo.sourceMode.position.y += dy;
                    modes[i] = m;
                }
            }

            // --- paths ---
            var paths = new List<Native.DISPLAYCONFIG_PATH_INFO>();
            foreach (var g in groups)
            {
                for (int i = 0; i < g.Members.Count; i++)
                {
                    LayoutDisplay d = g.Members[i];
                    MonitorInfo mon = g.Monitors[i];

                    Native.DISPLAYCONFIG_PATH_INFO path;
                    if (!FindTemplate(allPaths, mon, g.SourceId, out path))
                    {
                        res.Warnings.Add(Text.CannotAttach(d.Name));
                        continue;
                    }

                    path.sourceInfo.adapterId = mon.Adapter;
                    path.sourceInfo.id = g.SourceId;
                    path.sourceInfo.modeInfoIdx = (uint)g.ModeIndex;
                    path.sourceInfo.statusFlags = 0;

                    path.targetInfo.adapterId = mon.Adapter;
                    path.targetInfo.id = mon.TargetId;
                    path.targetInfo.modeInfoIdx = Native.DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
                    path.targetInfo.rotation = d.Rotation == 0 ? Native.DISPLAYCONFIG_ROTATION_IDENTITY : d.Rotation;
                    path.targetInfo.scaling = Native.DISPLAYCONFIG_SCALING_PREFERRED;
                    path.targetInfo.scanLineOrdering = Native.DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE;
                    path.targetInfo.refreshRate = ToRational(d.Hz);
                    path.targetInfo.targetAvailable = 1;
                    path.targetInfo.statusFlags = 0;

                    path.flags = Native.DISPLAYCONFIG_PATH_ACTIVE;
                    paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                res.Error = Text.NothingUsable;
                return -1;
            }

            // --- applying, with gradual relaxation of requirements ---
            var pathArr = paths.ToArray();
            var modeArr = modes.ToArray();

            uint baseFlags = Native.SDC_APPLY | Native.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Native.SDC_SAVE_TO_DATABASE;

            // Strict: without SDC_ALLOW_CHANGES Windows either does exactly what is asked or refuses.
            // With this flag it may "correct" the layout — for example, silently drop the paths
            // to blanked outputs and return success, which is exactly what we do not want.
            // SDC_FORCE_MODE_ENUMERATION makes the driver enumerate all modes again —
            // a heavy operation, so it is not in the first attempt but in the fallback.
            // Between rungs — a pause. A refusal comes quickly, so without it four
            // requests in a row go to the driver almost back to back: the 1.2-second pause
            // stands before the whole apply and does not act inside the ladder. And the heaviest
            // are exactly the rungs with SDC_FORCE_MODE_ENUMERATION — the driver enumerates modes
            // again, and it was such a stream of requests that crashed the machine.
            int err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr, baseFlags);
            if (err != 0)
            {
                System.Threading.Thread.Sleep(RungPauseMs);
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_FORCE_MODE_ENUMERATION);
            }

            if (err != 0)
            {
                // we allow the system to pick the modes itself
                // discrepancies, if any, will be shown by the check below — internals are not needed by the user
                System.Threading.Thread.Sleep(RungPauseMs);
                err = Native.SetDisplayConfig((uint)pathArr.Length, pathArr, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
            }
            if (err != 0)
            {
                // last attempt: without an explicit refresh rate
                System.Threading.Thread.Sleep(RungPauseMs);
                var relaxed = (Native.DISPLAYCONFIG_PATH_INFO[])pathArr.Clone();
                for (int i = 0; i < relaxed.Length; i++)
                {
                    relaxed[i].targetInfo.refreshRate.Numerator = 0;
                    relaxed[i].targetInfo.refreshRate.Denominator = 0;
                }
                err = Native.SetDisplayConfig((uint)relaxed.Length, relaxed, (uint)modeArr.Length, modeArr,
                        baseFlags | Native.SDC_ALLOW_CHANGES | Native.SDC_FORCE_MODE_ENUMERATION);
                if (err == 0) res.Warnings.Add(Text.DriverPickedRate);
            }
            return err;
        }

        /// <summary>
        /// Which of the ordered monitors never came on. We compare what we got with
        /// what was ordered: SetDisplayConfig can return success having applied something other than what was
        /// asked — especially if the monitor is physically blanked.
        ///
        /// A monitor that vanished from the enumeration
        /// is also counted as missing: after a failed attempt Windows blanks the output, and it
        /// temporarily disappears from the list — this used to be taken for "all is well".
        /// </summary>
        private static List<string> Missing(Layout layout, ApplyResult res, int settleMs)
        {
            System.Threading.Thread.Sleep(settleMs);
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var missing = new List<string>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || !res.Requested.Contains(d.Identity)) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) missing.Add(d.Name);
            }
            return missing;
        }

        private static void Verify(Layout layout, ApplyResult res)
        {
            System.Threading.Thread.Sleep(300);

            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var notTurnedOn = new List<string>();
            var notTurnedOff = new List<string>();

            foreach (var d in layout.Displays)
            {
                MonitorInfo mon;
                bool known = byId.TryGetValue(d.Identity, out mon);

                if (!d.Enabled)
                {
                    if (known && mon.Active) notTurnedOff.Add(d.Name);
                    continue;
                }
                if (!known) continue;                       // already reported about the disconnected ones above
                if (!mon.Active) { notTurnedOn.Add(d.Name); continue; }

                if (mon.Width != d.Width || mon.Height != d.Height)
                    res.Warnings.Add(Text.GotModeInstead(d.Name, mon.Width, mon.Height, d.Width, d.Height));
                else if (d.Hz > 0 && Math.Abs(mon.Hz - d.Hz) > 1.5)
                    res.Warnings.Add(Text.GotRateInstead(d.Name, mon.Hz.ToString("0.##"), d.Hz.ToString("0.##")));

                // We check rotation on a par with the mode. Previously it was the only
                // quantity that the program orders and does not check — and silence
                // here was costly: the "applied now" check takes rotation into account, so
                // the profile's dot did not light, the quick exit never fired,
                // and every hotkey press went for a full rebuild. The person
                // saw "Layout applied" without a single remark and pressed again,
                // because the dot did not light. This is the repeatable
                // load that sent the machine into a blue screen on 19.08.2026.
                if (mon.Rotation != d.Rotation)
                    res.Warnings.Add(Text.RotationNotApplied(d.Name));
            }

            // we check duplication by source id: a group must sit on one source
            var groupSource = new Dictionary<int, uint>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) continue;
                uint src;
                if (!groupSource.TryGetValue(d.CloneGroup, out src)) groupSource[d.CloneGroup] = mon.SourceId;
                else if (src != mon.SourceId)
                    res.Warnings.Add(Text.NotCloned(d.Name));
            }

            // Positions and the primary screen are the only things that differ when
            // the request was rejected and Windows arranged the layout itself. Previously nobody
            // checked them: the window said "Layout applied", yet the desk had
            // something else, and the profile was then never marked with the "applied
            // now" dot — because something else had been applied.
            // We report the non-enabled and non-disabled ones here, before choosing the origin
            // of coordinates. Previously those two lines stood below, and between them and the set
            // was the exit "nothing to check" — and a profile in which no monitor
            // came on left with no warnings at all: "Layout applied" and that's it.
            if (notTurnedOn.Count > 0)
                res.Warnings.Add(Text.DidNotTurnOn(string.Join(", ", notTurnedOn.ToArray())));
            if (notTurnedOff.Count > 0)
                res.Warnings.Add(Text.StayedOn(string.Join(", ", notTurnedOff.ToArray())));

            // We choose the origin by the same rule Submit uses:
            // the primary if it is connected and lit, otherwise the first lit one. Otherwise
            // we get two different origins for one layout, and the check complains
            // about a correctly applied profile — measured on a profile where a disconnected
            // TV is marked primary: "Windows arranged it its own way".
            LayoutDisplay origin = null;
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || !d.Primary) continue;
                MonitorInfo p;
                if (byId.TryGetValue(d.Identity, out p) && p.Active) { origin = d; break; }
            }
            if (origin == null)
                foreach (var d in layout.Displays)
                {
                    if (!d.Enabled) continue;
                    MonitorInfo p;
                    if (byId.TryGetValue(d.Identity, out p) && p.Active) { origin = d; break; }
                }
            if (origin == null) return;                  // nothing to check: nothing is lit

            MonitorInfo originMon = byId[origin.Identity];
            int shiftX = originMon.X - origin.X;
            int shiftY = originMon.Y - origin.Y;

            // We report the primary only when the ordered primary is in place
            // and lit, and Windows still put another at the origin of coordinates.
            if (origin.Primary && (originMon.X != 0 || originMon.Y != 0))
                res.Warnings.Add(Text.NotPrimary(origin.Name));

            var movedNames = new List<string>();
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled) continue;
                MonitorInfo mon;
                if (!byId.TryGetValue(d.Identity, out mon) || !mon.Active) continue;
                if (mon.X - d.X != shiftX || mon.Y - d.Y != shiftY) movedNames.Add(d.Name);
            }
            if (movedNames.Count > 0)
                res.Warnings.Add(Text.WindowsMoved(string.Join(", ", movedNames.ToArray())));

        }

        /// <summary>
        /// HDR is set separately and after the topology is applied: on a layout change
        /// (especially with duplication) Windows rebuilds the output chain and drops advanced colour.
        /// In a duplication group the HDR state must be the same on all outputs, otherwise
        /// Windows forcibly puts the whole group into SDR — so we set the group as a whole.
        /// </summary>
        public static void ApplyHdr(Layout layout, ApplyResult res, ApplyGate gate)
        {
            var wanted = new Dictionary<string, bool>();     // identity -> desired state
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.Hdr < 0) continue;
                wanted[d.Identity] = d.Hdr == 1;
            }
            if (wanted.Count == 0) return;

            // we align duplication groups: if HDR is requested for at least one output — we enable it for the whole group
            foreach (var d in layout.Displays)
            {
                if (!d.Enabled || d.CloneGroup < 0) continue;
                bool groupWantsHdr = false;
                foreach (var o in layout.Displays)
                {
                    bool v;
                    if (o.Enabled && o.CloneGroup == d.CloneGroup && wanted.TryGetValue(o.Identity, out v) && v)
                        groupWantsHdr = true;
                }
                if (groupWantsHdr) wanted[d.Identity] = true;
            }

            // we re-read the monitors: after a layout change the output identifiers may have changed
            var byId = new Dictionary<string, MonitorInfo>();
            foreach (var m in Monitors.Enumerate()) byId[m.Identity] = m;

            var touched = new List<MonitorInfo>();
            var names = new Dictionary<string, string>();
            foreach (var d in layout.Displays) names[d.Identity] = d.Name;

            var cloneWarned = new Dictionary<int, bool>();
            foreach (var kv in wanted)
            {
                MonitorInfo mon;
                if (!byId.TryGetValue(kv.Key, out mon) || !mon.Active) continue;

                if (kv.Value && !mon.HdrSupported)
                {
                    var self = layout.Find(kv.Key);
                    if (self != null && IsCloned(layout, kv.Key))
                    {
                        // verified: Windows does not give advanced colour to outputs in duplication
                        if (!cloneWarned.ContainsKey(self.CloneGroup))
                        {
                            cloneWarned[self.CloneGroup] = true;
                            res.Warnings.Add(Text.HdrNotInClone);
                        }
                    }
                    else res.Warnings.Add(Text.HdrUnavailable(names[kv.Key]));
                    continue;
                }
                // Already as needed — nothing to touch. Previously such a monitor still
                // landed in touched, because of which an extra 700 ms pause
                // always piled up later, and the list name stopped being true.
                if (mon.HdrEnabled == kv.Value) continue;

                // Switching advanced colour renegotiates the link — an operation
                // of the same class as a layout change. The quick path "layout already
                // like this" gets here bypassing the pause, so we keep it here and
                // only before the real first change.
                if (gate != null) { gate.Wait(); gate = null; }

                int err = Monitors.SetHdr(mon.Adapter, mon.TargetId, kv.Value);
                if (err != 0)
                {
                    res.Warnings.Add(Text.HdrFailed(names[kv.Key], kv.Value, Native.ErrorText(err)));
                    continue;
                }
                touched.Add(mon);
            }

            if (touched.Count == 0) return;

            // Windows renegotiates the link not instantly — we check the result with a pause.
            // We also repeat: the system likes to restore the remembered HDR state after a mode change.
            System.Threading.Thread.Sleep(700);
            foreach (var mon in touched)
            {
                bool want;
                if (!wanted.TryGetValue(mon.Identity, out want)) continue;

                var check = new MonitorInfo();
                check.Adapter = mon.Adapter;
                check.TargetId = mon.TargetId;
                Monitors.ReadHdr(check);

                if (check.HdrEnabled != want && check.HdrSupported)
                {
                    Monitors.SetHdr(mon.Adapter, mon.TargetId, want);
                    System.Threading.Thread.Sleep(600);
                    Monitors.ReadHdr(check);
                }

                if (!want && check.HdrEnabled)
                    res.Warnings.Add(Text.HdrComesBack(names[mon.Identity]));

                if (want && !check.HdrEnabled)
                {
                    // The advice "set the group to 4K60" is gone here. It promised what
                    // cannot happen: Windows does not give advanced colour to outputs
                    // joined in duplication at all — verified at both 60 Hz and
                    // 120. The window says the same, and so does the autotest; this one line
                    // sent a person off to pick a mode that would not help.
                    res.Warnings.Add(Text.HdrDidNotTurnOn(names[mon.Identity], IsCloned(layout, mon.Identity)));
                }
            }
        }

        private static bool IsCloned(Layout layout, string identity)
        {
            var self = layout.Find(identity);
            if (self == null || self.CloneGroup < 0) return false;
            foreach (var o in layout.Displays)
                if (!ReferenceEquals(o, self) && o.Enabled && o.CloneGroup == self.CloneGroup) return true;
            return false;
        }

        private static bool FindTemplate(Native.DISPLAYCONFIG_PATH_INFO[] all, MonitorInfo mon, uint sourceId,
            out Native.DISPLAYCONFIG_PATH_INFO path)
        {
            // exact match of source+target
            foreach (var p in all)
            {
                if (p.targetInfo.adapterId.Key == mon.Adapter.Key && p.targetInfo.id == mon.TargetId &&
                    p.sourceInfo.id == sourceId)
                { path = p; return true; }
            }
            // any path to this monitor — we substitute the source ourselves
            foreach (var p in all)
            {
                if (p.targetInfo.adapterId.Key == mon.Adapter.Key && p.targetInfo.id == mon.TargetId)
                { path = p; return true; }
            }
            path = new Native.DISPLAYCONFIG_PATH_INFO();
            return false;
        }

        private static Native.DISPLAYCONFIG_RATIONAL ToRational(double hz)
        {
            var r = new Native.DISPLAYCONFIG_RATIONAL();
            if (hz <= 0) { r.Numerator = 0; r.Denominator = 0; return r; }
            if (Math.Abs(hz - Math.Round(hz)) < 0.01)
            {
                r.Numerator = (uint)Math.Round(hz);
                r.Denominator = 1;
            }
            else
            {
                r.Numerator = (uint)Math.Round(hz * 1000.0);
                r.Denominator = 1000;
            }
            return r;
        }
    }
}
