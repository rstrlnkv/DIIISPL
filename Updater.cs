// DIIISPL — менеджер раскладок мониторов для Windows.
// Copyright (C) 2026 r.strlnkv
// Свободная программа под GNU General Public License v3 или новее; см. LICENSE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;

namespace DIIISPL
{
    /// <summary>
    /// Обновление через релизы на GitHub: спросить, что вышло, забрать установщик,
    /// проверить подпись и запустить.
    ///
    /// Установщик запускается, а не распаковывается вручную: он умеет закрыть
    /// работающую копию, заменить файлы и обновить запись в списке программ, а
    /// самодельная замена файла на ходу упирается в то, что файл занят.
    /// </summary>
    internal static class Updater
    {
        public const string Owner = "rstrlnkv";
        public const string Repo = "DIIISPL";

        /// <summary>
        /// Отпечаток сертификата, которым подписаны сборки. Скачанный установщик обязан
        /// быть подписан именно им — иначе он не запускается, даже если пришёл с GitHub.
        /// Сертификат самодельный, и цепочка доверия здесь ничего не доказала бы: на чужой
        /// машине его корень не установлен. Сверка с известным отпечатком строже — она
        /// проверяет не «кто-то подписал», а «подписал тот самый».
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

        /// <summary>Что нашлось на GitHub.</summary>
        internal class Release
        {
            public Version Version;
            public string Tag;
            public string Title;
            public string Notes;
            public string Page;
            public string FileUrl;
            public string FileName;
            public bool Early;
        }

        /// <summary>Версия работающей программы.</summary>
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

        /// <summary>Версия для показа: «0.1.0» читается лучше, чем «0.1.0.0».</summary>
        public static string CurrentText
        {
            get
            {
                Version v = Current;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        // ------------------------------------------------------------------ проверка
        /// <summary>
        /// Спросить GitHub, что вышло. Возвращает более новый выпуск или null, если
        /// новее ничего нет. Ошибку возвращает текстом — её показывают на странице
        /// «О программе», а не пишут в журнал.
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
                    // на канале Stable сборки Dev не предлагаем
                    if (r.Early && channel != Settings.Dev) { skippedEarly = true; continue; }
                    if (best == null || r.Version > best.Version) best = r;
                }

                // Отметку ставим и тогда, когда подходящего выпуска не нашлось:
                // связь была, ответ получен — проверка состоялась. Прежде она
                // ставилась только при находке, и на пустом канале строка «когда
                // проверяли» не менялась никогда.
                Settings.LastCheck = DateTime.UtcNow;
                if (best == null)
                {
                    // «Выпусков нет» и «есть, но только предварительные» — разные
                    // вещи, и вторая иначе предлагает жать «Ещё раз» на том, что
                    // не изменится.
                    error = skippedEarly
                        ? Text.OnlyPrereleases
                        : Text.NoReleasesYet;
                    return null;
                }
                return best.Version > Current ? best : null;
            }
            catch (WebException ex)
            {
                // Пока не вышло ни одного выпуска, GitHub отвечает «нет такого» —
                // это не поломка связи, и говорить о ней так не надо.
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
            Version v = ParseVersion(tag);
            if (v == null) return null;

            var r = new Release();
            r.Tag = tag;
            r.Version = v;
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

                        // Свой канал — вперёд. Сейчас в выпуске один пакет, и
                        // выбирать не из чего; но если в один выпуск когда-нибудь
                        // лягут оба, стабильная копия не должна молча поставить
                        // себе Dev просто потому, что тот оказался первым.
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

        // ------------------------------------------------------------------ загрузка
        /// <summary>
        /// Забрать установщик во временную папку и убедиться, что он подписан нашим
        /// сертификатом. Возвращает путь к файлу или null с объяснением.
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
                // Прежний пакет мы держим открытым, чтобы его не подменили; пока
                // не отпустим, его не удалить и поверх него не записать.
                Unhold();

                string dir = Path.Combine(Path.GetTempPath(), "DIIISPL-update");
                Directory.CreateDirectory(dir);

                // Прошлые пакеты убираем здесь, а не после установки: там файл
                // ещё читает msiexec. Иначе они копились по полтора мегабайта
                // за обновление и оставались лежать даже после удаления
                // программы — папку эту создаём мы, и убрать её некому.
                try
                {
                    foreach (string old in Directory.GetFiles(dir, "*.msi"))
                        try { File.Delete(old); } catch { }
                }
                catch { }
                // Только имя файла и ничего больше. Имя приходит с сервера, а Path.Combine
                // при корневом втором аргументе выбрасывает первый целиком: «C:\…\что.msi»
                // или «..\..\что.msi» увели бы запись из временной папки куда угодно —
                // и запись идёт до проверки подписи.
                path = Path.Combine(dir, Path.GetFileName(release.FileName));

                // Адрес приходит из ответа GitHub, и схему в нём никто не обещал.
                // Подпись всё равно не пропустит чужое, но качать по file:// с сетевой
                // шары или открытым текстом по http — не то, чего мы просили.
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

                // Держим файл открытым от проверки подписи до запуска установщика.
                //
                // Между этими мгновениями пакет лежит во временной папке, куда
                // пишет любая программа этого пользователя. Проверили подпись,
                // человек прочитал, что нового, подумал минуту, нажал «Обновить» —
                // и запустился уже не тот файл, который проверяли. Открытый на
                // чтение с запретом записи, он подменён быть не может: msiexec его
                // прочитает, а переписать не сможет никто.
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
                // Системное сообщение здесь не помогает: его читают в окне
                // «О программе», и сделать с ним нечего. Говорим о деле —
                // что не получилось и куда смотреть.
                Forget(path);
                error = Text.SaveFailed;
                return null;
            }
        }

        /// <summary>
        /// Что не так с подписью файла. null — всё в порядке. Две разные беды названы
        /// по-разному: «не подписан» бывает у сборки, собранной без сертификата, а
        /// «подписан не тем» — повод насторожиться всерьёз.
        /// </summary>
        private static string SignatureProblem(string path)
        {
            // Сначала целостность: цела ли подпись для этого содержимого.
            //
            // Одного отпечатка мало, и это не мелочь. Сертификат читается прямо из
            // файла, поэтому в файл, к которому дописали чужое, он придёт всё тот же:
            // проверка отвечала бы «подписан тем самым» про пакет, собранный кем угодно.
            int verdict = Verify(path);

            // Самоподписанный корень на чужой машине не установлен, и WinVerifyTrust
            // честно об этом говорит. Для нас это не отказ: доверие мы берём не из
            // цепочки, а из отпечатка ниже. Отвергаем всё остальное — и в первую
            // очередь несовпадение содержимого с подписью.
            //
            // Истёкший срок сертификата тоже не отказ. Подпись ставится с меткой
            // времени, поэтому старый выпуск остаётся действительным и после
            // истечения; но если метку когда-нибудь не поставят, все прошлые
            // сборки в один день перестанут ставиться — а календарь не улика.
            // Подмену содержимого ловит не срок, а отдельный вердикт TRUST_E_BAD_DIGEST.
            if (verdict != 0 &&
                verdict != Native.CERT_E_UNTRUSTEDROOT &&
                verdict != Native.CERT_E_CHAINING &&
                verdict != Native.CERT_E_EXPIRED)
            {
                if (verdict == Native.TRUST_E_BAD_DIGEST)
                    return Text.TamperedFile;

                // Замерено на испорченных файлах: и на дописанных байтах, и на
                // перевёрнутом байте в середине, и на затёртой полезной нагрузке
                // система отвечает одинаково — «подписи нет», тем же кодом, что и
                // у честно неподписанной сборки. Ветка TRUST_E_BAD_DIGEST выше не
                // срабатывает ни разу, хотя Get-AuthenticodeSignature на тех же
                // файлах про несовпадение хеша говорит. Различить «не подписан» и
                // «испорчен» этим вызовом нельзя — значит и текст не должен
                // выбирать за человека один из двух случаев.
                if (verdict == Native.TRUST_E_NOSIGNATURE)
                    return Text.SignatureUnconfirmed;

                return Text.SignatureRejected;
            }

            // Потом личность: тот ли это ключ.
            X509Certificate2 cert;
            try { cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)); }
            catch { return Text.SignatureUnconfirmed; }

            if (!string.Equals(cert.Thumbprint, SignerThumbprint, StringComparison.OrdinalIgnoreCase))
                return Text.ForeignKey;

            return null;
        }

        /// <summary>Что скажет система о подписи файла. 0 — всё в порядке.</summary>
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
                data.fdwRevocationChecks = Native.WTD_REVOKE_NONE;   // сеть уже могла отвалиться
                data.dwUnionChoice = Native.WTD_CHOICE_FILE;
                data.pFile = filePtr;
                data.dwStateAction = Native.WTD_STATEACTION_VERIFY;
                data.dwProvFlags = Native.WTD_SAFER_FLAG;

                dataPtr = Marshal.AllocHGlobal((int)data.cbStruct);
                Marshal.StructureToPtr(data, dataPtr, false);
                dataWritten = true;

                Guid action = Native.WINTRUST_ACTION_GENERIC_VERIFY_V2;
                int result = Native.WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                // Закрыть состояние обязательно, иначе течёт память провайдера.
                data = (Native.WINTRUST_DATA)Marshal.PtrToStructure(dataPtr, typeof(Native.WINTRUST_DATA));
                data.dwStateAction = Native.WTD_STATEACTION_CLOSE;
                Marshal.StructureToPtr(data, dataPtr, true);
                Native.WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

                return result;
            }
            catch { return Native.TRUST_E_NOSIGNATURE; }   // не смогли проверить — считаем, что не прошло
            finally
            {
                // Освободить память под структурой мало. Путь к файлу в ней —
                // строка, и StructureToPtr отводит под неё отдельный кусок;
                // FreeHGlobal о нём не знает и оставляет его висеть. Утечка
                // маленькая, но при каждой проверке обновлений.
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
        /// Проверенный пакет, который держим открытым, чтобы его не подменили.
        /// Живёт до запуска установщика — тот закрывает программу сам.
        /// </summary>
        private static FileStream _held;

        private static void Hold(string path)
        {
            Unhold();
            try { _held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch { _held = null; }     // не удержали — подпись всё равно проверим
        }

        private static void Unhold()
        {
            if (_held == null) return;
            try { _held.Dispose(); } catch { }
            _held = null;
        }

        // ------------------------------------------------------------------ установка
        /// <summary>
        /// Запустить установщик и выйти. Дальше работает он: закрывает эту копию,
        /// заменяет файлы и запускает программу заново.
        /// </summary>
        public static bool Install(string msiPath, out string error)
        {
            error = null;

            // Подпись сверяем ещё раз, прямо перед запуском. Обычно файл всё это
            // время открыт нами и подменить его нельзя, но открыть удаётся не
            // всегда — а установщик получает полное доверие человека, и одна
            // лишняя секунда здесь дешевле чужого пакета с правами системы.
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
                // Полный путь обязателен. По голому имени ShellExecute ищет и в текущем
                // каталоге тоже, а он у программы бывает чужим — из «Загрузок», из
                // консоли, из автозапуска. Подставленный туда msiexec.exe получил бы
                // уже скачанный и проверенный пакет в аргументах и полное доверие
                // человека к происходящему.
                var start = new ProcessStartInfo(
                    Path.Combine(Environment.SystemDirectory, "msiexec.exe"));
                // DIIISPLRELAUNCH=1 просит установщик поднять программу заново.
                // Без него обновление изнутри кончалось исчезновением значка из
                // трея: /qb — это установка без последнего окна, а запуск висел
                // только на его кнопке «Готово».
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
            catch { /* нечем открыть — молча */ }
        }

        // ------------------------------------------------------------------ мелочи
        /// <summary>
        /// Строки «что нового» из описания выпуска — на языке программы.
        ///
        /// Описание и так скачивается вместе со сведениями о выпуске, и до сих
        /// пор его разбирали и выбрасывали: поле Notes не читал никто, а человек
        /// шёл за тем же текстом в браузер.
        ///
        /// Тело выпуска сборщик складывает из CHANGELOG.md и CHANGELOG.en.md,
        /// поэтому оно устроено предсказуемо: «## Что нового в 0.2.0», следом
        /// «## Whats new in 0.2.0», потом черта и рассказ о программе. Свой
        /// заголовок язык называет сам, строкой из словаря; язык, приехавший
        /// файлом, своего раздела в описании не имеет — и получит английский,
        /// потому что незаведённая строка падает на запасной язык.
        ///
        /// Описание выпуска — чужой текст с чужого сервера. Разметку отсюда
        /// показывать нельзя, поэтому берутся только строки списка, и только
        /// первые несколько.
        /// </summary>
        public static List<string> Highlights(string notes, int max, out int more)
        {
            more = 0;
            var found = new List<string>();
            if (string.IsNullOrEmpty(notes)) return found;

            string heading = Text.NotesHeading;
            bool inside = false;
            // Разбираем по переводам строки; возврат каретки снимет Trim ниже.
            foreach (string raw in notes.Split((char)10))
            {
                string line = raw.Trim();
                if (line.StartsWith("## "))
                {
                    if (inside) break;                 // свой раздел кончился
                    inside = line.Substring(3).TrimStart()
                                 .StartsWith(heading, StringComparison.CurrentCultureIgnoreCase);
                    continue;
                }
                if (!inside) continue;
                if (line == "---") break;
                if (!line.StartsWith("- ")) continue;
                found.Add(Changelog.Plain(line.Substring(2)));
            }

            if (max > 0 && found.Count > max)
            {
                more = found.Count - max;
                found.RemoveRange(max, more);
            }
            return found;
        }


        private static string Fetch(string url)
        {
            Prepare();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = Repo;                    // GitHub отвечает 403 без него
            request.Accept = "application/vnd.github+json";
            request.Timeout = 15000;
            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                return reader.ReadToEnd();
        }

        /// <summary>
        /// .NET Framework по умолчанию предлагает протоколы, от которых GitHub отказался
        /// ещё в 2018-м. Без этой строки соединение рвётся до первого байта.
        /// </summary>
        private static void Prepare()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }   // TLS 1.2
            catch { }
        }

        /// <summary>«v1.0.1», «1.0.1», «1.0.1-beta.2» — берём числа, остальное отбрасываем.</summary>
        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string s = text.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int cut = s.IndexOfAny(new char[] { '-', '+', ' ' });
            if (cut > 0) s = s.Substring(0, cut);

            string[] parts = s.Split('.');
            var numbers = new List<int>();
            foreach (string p in parts)
            {
                int n;
                if (!int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) break;
                numbers.Add(n);
            }
            // Спрашивать, есть ли числа, надо до заполнения нулями, а не после:
            // прежде строка сначала добавляла четыре нуля, и ответ был всегда
            // «есть». Метка вроде «latest» становилась версией 0.0.0.0 — то есть
            // выпуском, который дальше сравнивают с нашим, а не мусором.
            if (numbers.Count == 0) return null;
            while (numbers.Count < 4) numbers.Add(0);
            return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        }

        /// <summary>Строковое поле из ответа GitHub. Названо Field, а не Text: класс
        /// с текстами программы зовётся Text, и одноимённый метод его перекрывал.</summary>
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

        /// <summary>«только что», «5 мин назад», «3 дня назад» — для строки о последней проверке.</summary>
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
