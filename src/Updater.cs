// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;

namespace DIIISPL
{
    /// <summary>
    /// Updating through GitHub releases: ask what is out, fetch the installer,
    /// verify the signature and launch it.
    ///
    /// The installer is launched rather than unpacked by hand: it knows how to close
    /// the running copy, replace the files and update the entry in the programs list, while
    /// a homemade on-the-fly file replacement runs into the file being in use.
    /// </summary>
    internal static class Updater
    {
        public const string Owner = "rstrlnkv";
        public const string Repo = "DIIISPL";

        /// <summary>
        /// Thumbprint of the certificate the builds are signed with. The downloaded installer must
        /// be signed with exactly it — otherwise it is not launched, even if it came from GitHub.
        /// The certificate is self-made, and a chain of trust would prove nothing here: on someone else's
        /// machine its root is not installed. Matching a known thumbprint is stricter — it
        /// checks not "someone signed" but "that very one signed".
        /// </summary>
        private const string SignerThumbprint = "103850469089DEECC19D0DD6CE1D4DD3307601FA";

        public static string ReleasesPage
        {
            get { return "https://github.com/" + Owner + "/" + Repo + "/releases"; }
        }

        public static string ProjectPage
        {
            get { return "https://github.com/" + Owner + "/" + Repo; }
        }

        /// <summary>What was found on GitHub.</summary>
        internal class Release
        {
            /// <summary>The tag without the "v", e.g. "0.2.4-dev.2": shown to the person.</summary>
            public string Number;
            public string Tag;
            public string Title;
            public string Notes;
            public string Page;
            public string FileUrl;
            public string FileName;
            public bool Early;
        }

        /// <summary>Version of the running program.</summary>
        public static Version Current
        {
            get
            {
                try
                {
                    string v = FileVersionInfo.GetVersionInfo(
                        Assembly.GetExecutingAssembly().Location).FileVersion;
                    Version parsed = ParseVersion(v);
                    return parsed != null ? parsed : new Version(0, 0, 0, 0);
                }
                catch { return new Version(0, 0, 0, 0); }
            }
        }

        /// <summary>Version for display: "0.1.0" reads better than "0.1.0.0".</summary>
        public static string CurrentText
        {
            get
            {
                Version v = Current;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        /// <summary>
        /// Full version for display, with the -dev.N suffix: the ProductVersion of the exe,
        /// which csc fills from AssemblyInformationalVersion (version-check forbids a missing or
        /// empty attribute). Falls back to CurrentText if the version cannot be read.
        /// </summary>
        public static string DisplayText
        {
            get
            {
                try
                {
                    string v = FileVersionInfo.GetVersionInfo(
                        Assembly.GetExecutingAssembly().Location).ProductVersion;
                    v = VersionText.Display(v);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                catch { }
                return CurrentText;
            }
        }

        // ------------------------------------------------------------------ checking
        /// <summary>
        /// Ask GitHub what is out. Returns the newer release or null if
        /// there is nothing newer. The error is returned as text — it is shown on the
        /// About page rather than written to a log.
        /// </summary>
        public static Release Check(string channel, out string error)
        {
            error = null;
            try
            {
                string json = Fetch("https://api.github.com/repos/" + Owner + "/" + Repo +
                                    "/releases?per_page=20");
                var releases = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json);
                if (releases == null) { error = Text.GitHubGibberish; return null; }

                Release best = null;
                bool skippedEarly = false;
                foreach (var item in releases)
                {
                    Release r = Read(item, channel);
                    if (r == null) continue;
                    if (Truthy(item, "draft")) continue;
                    // the Stable channel is not offered Dev builds
                    if (r.Early && channel != Settings.Dev) { skippedEarly = true; continue; }
                    if (best == null || VersionText.IsNewer(r.Tag, best.Tag)) best = r;
                }

                // The mark is set even when no suitable release was found:
                // there was a connection, a reply came — the check took place. It used to
                // be set only on a find, and on an empty channel the "when it was
                // checked" line never changed.
                Settings.LastCheck = DateTime.UtcNow;
                if (best == null)
                {
                    // "No releases" and "there are some, but only pre-releases" are different
                    // things, and otherwise the second one suggests pressing "Again" on something that
                    // will not change.
                    error = skippedEarly
                        ? Text.OnlyPrereleases
                        : Text.NoReleasesYet;
                    return null;
                }
                // DisplayText, not Current: Current is the FileVersion, which has no -dev.N.
                return VersionText.IsNewer(best.Tag, DisplayText) ? best : null;
            }
            catch (WebException ex)
            {
                // While no release has been published, GitHub answers "not found" —
                // that is not a broken connection, and it should not be described as one.
                var http = ex.Response as HttpWebResponse;
                error = http != null && http.StatusCode == HttpStatusCode.NotFound
                    ? Text.NoReleasesYet
                    : Text.GitHubUnreachable;
                return null;
            }
            catch
            {
                error = Text.ReleaseListUnreadable;
                return null;
            }
        }

        private static Release Read(Dictionary<string, object> item, string channel)
        {
            string tag = Field(item, "tag_name");
            if (ParseVersion(tag) == null) return null;

            var r = new Release();
            r.Tag = tag;
            r.Number = VersionText.Display(tag.Trim().TrimStart('v', 'V'));
            r.Title = Field(item, "name");
            r.Notes = Field(item, "body");
            r.Page = Field(item, "html_url");
            r.Early = Truthy(item, "prerelease");

            object assets;
            if (item.TryGetValue("assets", out assets))
            {
                var list = assets as System.Collections.ArrayList;
                if (list != null)
                {
                    foreach (object a in list)
                    {
                        var asset = a as Dictionary<string, object>;
                        if (asset == null) continue;
                        string name = Field(asset, "name");
                        if (name == null || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;

                        // Own channel first. Right now a release has a single package, so
                        // there is nothing to choose from; but if both ever land in one release,
                        // a stable copy must not silently install
                        // Dev just because it happened to be first.
                        bool mine = name.IndexOf("-" + channel + "-", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (r.FileName != null && !mine) continue;

                        r.FileName = name;
                        r.FileUrl = Field(asset, "browser_download_url");
                        if (mine) break;
                    }
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ download
        /// <summary>
        /// Fetch the installer into a temp folder and make sure it is signed with our
        /// certificate. Returns the path to the file or null with an explanation.
        /// </summary>
        public static string Download(Release release, out string error)
        {
            error = null;
            if (release == null || string.IsNullOrEmpty(release.FileUrl))
            {
                error = Text.NoInstallerAttached;
                return null;
            }

            string path = null;
            try
            {
                // We keep the previous package open so it cannot be swapped; until
                // we release it, it cannot be deleted or written over.
                Unhold();

                string dir = Path.Combine(Path.GetTempPath(), "DIIISPL-update");
                Directory.CreateDirectory(dir);

                // Past packages are removed here, not after installation: there the file
                // is still being read by msiexec. Otherwise they piled up at one and a half megabytes
                // per update and stayed even after the program
                // was uninstalled — we create this folder, and nobody else would clean it.
                try
                {
                    foreach (string old in Directory.GetFiles(dir, "*.msi"))
                        try { File.Delete(old); } catch { }
                }
                catch { }
                // The file name only and nothing else. The name comes from the server, and Path.Combine
                // with a rooted second argument throws away the first entirely: "C:\…\what.msi"
                // or "..\..\what.msi" would take the write out of the temp folder anywhere —
                // and the write happens before the signature check.
                path = Path.Combine(dir, Path.GetFileName(release.FileName));

                // The address comes from the GitHub reply, and nobody promised the scheme in it.
                // The signature would not let foreign content through anyway, but downloading via file:// from a network
                // share or in clear text over http is not what we asked for.
                Uri url;
                if (!Uri.TryCreate(release.FileUrl, UriKind.Absolute, out url) ||
                    url.Scheme != Uri.UriSchemeHttps)
                {
                    error = Text.BadDownloadLink;
                    return null;
                }

                Prepare();
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", Repo);
                    client.DownloadFile(url, path);
                }

                // We keep the file open from the signature check until the installer launches.
                //
                // Between those moments the package sits in a temp folder, where
                // any program of this user can write. The signature was checked,
                // the person read what's new, thought for a minute, pressed "Update" —
                // and what launched was no longer the file that was checked. Opened for
                // reading with writing denied, it cannot be swapped: msiexec will
                // read it, and nobody can rewrite it.
                Hold(path);

                string wrong = SignatureProblem(path);
                if (wrong != null)
                {
                    Unhold();
                    Forget(path);
                    error = wrong;
                    return null;
                }
                return path;
            }
            catch (WebException)
            {
                Forget(path);
                error = Text.DownloadFailed;
                return null;
            }
            catch (Exception)
            {
                // The system message does not help here: it is read in the
                // About window, and there is nothing to do with it. We speak of the matter —
                // what failed and where to look.
                Forget(path);
                error = Text.SaveFailed;
                return null;
            }
        }

        /// <summary>
        /// What is wrong with the file's signature. null — all is well. Two different troubles are named
        /// differently: "not signed" happens with a build made without a certificate, while
        /// "signed by the wrong one" is a reason to be seriously wary.
        /// </summary>
        private static string SignatureProblem(string path)
        {
            // Integrity first: whether the signature is intact for this content.
            //
            // A thumbprint alone is not enough, and this is no small matter. The certificate is read straight from
            // the file, so for a file with foreign data appended it comes out just the same:
            // the check would answer "signed by that very one" about a package built by anyone.
            int verdict = Verify(path);

            // The self-signed root is not installed on someone else's machine, and WinVerifyTrust
            // says so honestly. For us this is not a refusal: we take trust not from
            // the chain but from the thumbprint below. We reject everything else — and first of
            // all a mismatch between the content and the signature.
            //
            // An expired certificate is not a refusal either. The signature is made with a timestamp,
            // so an old release stays valid after
            // expiry; but if the timestamp is ever not applied, all past
            // builds would stop installing on one day — and the calendar is no evidence.
            // Content tampering is caught not by the expiry but by the separate verdict TRUST_E_BAD_DIGEST.
            if (verdict != 0 &&
                verdict != Native.CERT_E_UNTRUSTEDROOT &&
                verdict != Native.CERT_E_CHAINING &&
                verdict != Native.CERT_E_EXPIRED)
            {
                if (verdict == Native.TRUST_E_BAD_DIGEST)
                    return Text.TamperedFile;

                // Measured on corrupted files: with appended bytes, with a
                // flipped byte in the middle, and with a wiped payload
                // the system answers the same — "no signature", with the same code as
                // for an honestly unsigned build. The TRUST_E_BAD_DIGEST branch above never
                // fires, although Get-AuthenticodeSignature on the same
                // files reports the hash mismatch. This call cannot tell "not signed" from
                // "corrupted" — so the text must not
                // pick one of the two cases for the person.
                if (verdict == Native.TRUST_E_NOSIGNATURE)
                    return Text.SignatureUnconfirmed;

                return Text.SignatureRejected;
            }

            // Then identity: whether this is the right key.
            X509Certificate2 cert;
            try { cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)); }
            catch { return Text.SignatureUnconfirmed; }

            if (!string.Equals(cert.Thumbprint, SignerThumbprint, StringComparison.OrdinalIgnoreCase))
                return Text.ForeignKey;

            return null;
        }

        /// <summary>What the system says about the file's signature. 0 — all is well.</summary>
        private static int Verify(string path)
        {
            var file = new Native.WINTRUST_FILE_INFO();
            file.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_FILE_INFO));
            file.pcwszFilePath = path;

            IntPtr filePtr = Marshal.AllocHGlobal((int)file.cbStruct);
            IntPtr dataPtr = IntPtr.Zero;
            bool fileWritten = false, dataWritten = false;
            try
            {
                Marshal.StructureToPtr(file, filePtr, false);
                fileWritten = true;

                var data = new Native.WINTRUST_DATA();
                data.cbStruct = (uint)Marshal.SizeOf(typeof(Native.WINTRUST_DATA));
                data.dwUIChoice = Native.WTD_UI_NONE;
                data.fdwRevocationChecks = Native.WTD_REVOKE_NONE;   // the network may already have dropped
                data.dwUnionChoice = Native.WTD_CHOICE_FILE;
                data.pFile = filePtr;
                data.dwStateAction = Native.WTD_STATEACTION_VERIFY;
                data.dwProvFlags = Native.WTD_SAFER_FLAG;

                dataPtr = Marshal.AllocHGlobal((int)data.cbStruct);
                Marshal.StructureToPtr(data, dataPtr, false);
                dataWritten = true;

                Guid action = Native.WINTRUST_ACTION_GENERIC_VERIFY_V2;
                int result = Native.WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                // The state must be closed, otherwise the provider's memory leaks.
                data = (Native.WINTRUST_DATA)Marshal.PtrToStructure(dataPtr, typeof(Native.WINTRUST_DATA));
                data.dwStateAction = Native.WTD_STATEACTION_CLOSE;
                Marshal.StructureToPtr(data, dataPtr, true);
                Native.WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                return result;
            }
            catch { return Native.TRUST_E_NOSIGNATURE; }   // could not check — treat as failed
            finally
            {
                // Freeing the memory under the structure is not enough. The file path in it is a
                // string, and StructureToPtr allocates a separate chunk for it;
                // FreeHGlobal does not know about it and leaves it hanging. The leak
                // is small, but it happens on every update check.
                if (dataPtr != IntPtr.Zero)
                {
                    if (dataWritten) Marshal.DestroyStructure(dataPtr, typeof(Native.WINTRUST_DATA));
                    Marshal.FreeHGlobal(dataPtr);
                }
                if (fileWritten) Marshal.DestroyStructure(filePtr, typeof(Native.WINTRUST_FILE_INFO));
                Marshal.FreeHGlobal(filePtr);
            }
        }

        private static void Forget(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        /// <summary>
        /// A verified package that we keep open so it cannot be swapped.
        /// Lives until the installer launches — it closes the program itself.
        /// </summary>
        private static FileStream _held;

        private static void Hold(string path)
        {
            Unhold();
            try { _held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch { _held = null; }     // could not hold it — the signature is checked anyway
        }

        private static void Unhold()
        {
            if (_held == null) return;
            try { _held.Dispose(); } catch { }
            _held = null;
        }

        // ------------------------------------------------------------------ installation
        /// <summary>
        /// Launch the installer and exit. From there it takes over: closes this copy,
        /// replaces the files and starts the program again.
        /// </summary>
        public static bool Install(string msiPath, out string error)
        {
            error = null;

            // We check the signature once more, right before launching. Normally the file has been
            // open by us all this time and cannot be swapped, but opening it does not always
            // succeed — and the installer gets the person's full trust, and one
            // extra second here is cheaper than a foreign package with system rights.
            string wrong = SignatureProblem(msiPath);
            if (wrong != null)
            {
                Unhold();
                Forget(msiPath);
                error = wrong;
                return false;
            }

            try
            {
                // The full path is mandatory. With a bare name ShellExecute also searches the current
                // directory, and for the program it is sometimes foreign — from "Downloads", from the
                // console, from autostart. A msiexec.exe planted there would receive
                // the already downloaded and verified package in its arguments and the person's
                // full trust in what is happening.
                var start = new ProcessStartInfo(
                    Path.Combine(Environment.SystemDirectory, "msiexec.exe"));
                // DIIISPLRELAUNCH=1 asks the installer to start the program again.
                // Without it an in-app update ended with the tray icon vanishing:
                // /qb is an installation without the final window, and the launch hung
                // only on its "Finish" button.
                start.Arguments = "/i \"" + msiPath + "\" DIIISPLRELAUNCH=1 /qb /norestart";
                start.UseShellExecute = true;
                Process.Start(start);
                return true;
            }
            catch (Exception)
            {
                error = Text.InstallerStartFailed;
                return false;
            }
        }

        public static void OpenPage(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* nothing to open it with — silently */ }
        }

        // ------------------------------------------------------------------ odds and ends
        /// <summary>
        /// The "what's new" lines from the release description — in the program's language.
        ///
        /// The description is downloaded along with the release details anyway, and until now
        /// it was parsed and thrown away: nobody read the Notes field, and the person
        /// went to the browser for the same text.
        ///
        /// The release body is assembled by release.yml from .github/release-template.ru.md and
        /// .github/release-template.en.md, filled with the CHANGELOG.ru.md and CHANGELOG.md sections,
        /// so it has a predictable shape: the Russian page first, with "## Что нового" holding the
        /// changelog lines, then "## Установка и обновление" and "## Что дальше"; then the English page
        /// inside a "&lt;details&gt;" block, with "## What's new" holding the English lines. The language
        /// names its own heading, with a string from the dictionary; a language that arrived as
        /// a file has no section of its own in the description — and gets English,
        /// because a string that is not defined falls back to the fallback language.
        ///
        /// The release description is foreign text from a foreign server. Markup must not be
        /// shown from here, so only list lines are taken, and only
        /// the first few. A line without its own badge takes the badge of its "###" group.
        /// </summary>
        public static List<string> Highlights(string notes, int max, out int more)
        {
            return Changelog.Highlights(notes, Text.NotesHeading, max, out more);
        }


        private static string Fetch(string url)
        {
            Prepare();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = Repo;                    // GitHub answers 403 without it
            request.Accept = "application/vnd.github+json";
            request.Timeout = 15000;
            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                return reader.ReadToEnd();
        }

        /// <summary>
        /// By default .NET Framework offers protocols that GitHub dropped
        /// back in 2018. Without this line the connection breaks before the first byte.
        /// </summary>
        private static void Prepare()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }   // TLS 1.2
            catch { }
        }

        /// <summary>"v1.0.1", "1.0.1", "1.0.1-beta.2" — see VersionText.Parse.</summary>
        public static Version ParseVersion(string text)
        {
            return VersionText.Parse(text);
        }

        /// <summary>A string field from the GitHub reply. Named Field, not Text: the class
        /// with the program's texts is called Text, and a method of the same name shadowed it.</summary>
        private static string Field(Dictionary<string, object> map, string key)
        {
            object v;
            return map.TryGetValue(key, out v) && v != null ? v.ToString() : null;
        }

        private static bool Truthy(Dictionary<string, object> map, string key)
        {
            object v;
            return map.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        /// <summary>"just now", "5 min ago", "3 days ago" — for the last-check line.</summary>
        public static string Ago(DateTime? moment)
        {
            if (!moment.HasValue) return Text.NeverChecked;
            TimeSpan passed = DateTime.UtcNow - moment.Value;
            if (passed.TotalMinutes < 2) return Text.CheckedJustNow;
            if (passed.TotalHours < 1) return Text.CheckedMinutes((int)passed.TotalMinutes);
            if (passed.TotalHours < 24) return Text.CheckedHours((int)passed.TotalHours);
            return Text.CheckedDays((int)passed.TotalDays);
        }

    }
}
