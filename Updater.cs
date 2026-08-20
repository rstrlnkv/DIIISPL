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
                    return ParseVersion(v);
                }
                catch { return new Version(0, 0, 0, 0); }
            }
        }

        /// <summary>Версия для показа: «1.0.1» читается лучше, чем «1.0.1.0».</summary>
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
                if (releases == null) { error = "GitHub ответил непонятным"; return null; }

                Release best = null;
                foreach (var item in releases)
                {
                    Release r = Read(item);
                    if (r == null) continue;
                    if (Truthy(item, "draft")) continue;
                    // на стабильном канале ранние сборки не предлагаем
                    if (r.Early && channel != Settings.Early) continue;
                    if (best == null || r.Version > best.Version) best = r;
                }

                if (best == null) { error = "выпусков пока нет"; return null; }
                Settings.LastCheck = DateTime.UtcNow;
                return best.Version > Current ? best : null;
            }
            catch (WebException ex)
            {
                // Пока не вышло ни одного выпуска, GitHub отвечает «нет такого» —
                // это не поломка связи, и говорить о ней так не надо.
                var http = ex.Response as HttpWebResponse;
                error = http != null && http.StatusCode == HttpStatusCode.NotFound
                    ? "выпусков пока нет"
                    : "не удалось связаться с GitHub";
                return null;
            }
            catch
            {
                error = "не удалось прочитать список выпусков";
                return null;
            }
        }

        private static Release Read(Dictionary<string, object> item)
        {
            string tag = Text(item, "tag_name");
            Version v = ParseVersion(tag);
            if (v == null) return null;

            var r = new Release();
            r.Tag = tag;
            r.Version = v;
            r.Title = Text(item, "name");
            r.Notes = Text(item, "body");
            r.Page = Text(item, "html_url");
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
                        string name = Text(asset, "name");
                        if (name == null || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;
                        r.FileName = name;
                        r.FileUrl = Text(asset, "browser_download_url");
                        break;
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
                error = "к этому выпуску не приложен установщик";
                return null;
            }

            string path = null;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "DIIISPL-update");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, release.FileName);

                Prepare();
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", Repo);
                    client.DownloadFile(release.FileUrl, path);
                }

                string wrong = SignatureProblem(path);
                if (wrong != null)
                {
                    Forget(path);
                    error = wrong;
                    return null;
                }
                return path;
            }
            catch (WebException)
            {
                Forget(path);
                error = "не удалось скачать установщик";
                return null;
            }
            catch (Exception ex)
            {
                Forget(path);
                error = ex.Message;
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
            X509Certificate2 cert;
            try { cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)); }
            catch { return "установщик не подписан — ставить его так нельзя"; }

            if (!string.Equals(cert.Thumbprint, SignerThumbprint, StringComparison.OrdinalIgnoreCase))
                return "установщик подписан чужим сертификатом — ставить его нельзя";

            return null;
        }

        private static void Forget(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ------------------------------------------------------------------ установка
        /// <summary>
        /// Запустить установщик и выйти. Дальше работает он: закрывает эту копию,
        /// заменяет файлы и запускает программу заново.
        /// </summary>
        public static bool Install(string msiPath, out string error)
        {
            error = null;
            try
            {
                var start = new ProcessStartInfo("msiexec.exe");
                start.Arguments = "/i \"" + msiPath + "\" /qb /norestart";
                start.UseShellExecute = true;
                Process.Start(start);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void OpenPage(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* нечем открыть — молча */ }
        }

        // ------------------------------------------------------------------ мелочи
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
            while (numbers.Count < 4) numbers.Add(0);
            if (numbers.Count == 0) return null;
            return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        }

        private static string Text(Dictionary<string, object> map, string key)
        {
            object v;
            return map.TryGetValue(key, out v) && v != null ? v.ToString() : null;
        }

        private static bool Truthy(Dictionary<string, object> map, string key)
        {
            object v;
            return map.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        /// <summary>«сегодня», «вчера», «3 дня назад» — для строки о последней проверке.</summary>
        public static string Ago(DateTime? moment)
        {
            if (!moment.HasValue) return "ещё не проверялось";
            TimeSpan passed = DateTime.UtcNow - moment.Value;
            if (passed.TotalMinutes < 2) return "проверялось только что";
            if (passed.TotalHours < 1) return "проверялось " + (int)passed.TotalMinutes + " мин назад";
            if (passed.TotalHours < 24) return "проверялось " + Plural((int)passed.TotalHours, "час", "часа", "часов") + " назад";
            return "проверялось " + Plural((int)passed.TotalDays, "день", "дня", "дней") + " назад";
        }

        private static string Plural(int n, string one, string few, string many)
        {
            int t = n % 100;
            if (t >= 11 && t <= 14) return n + " " + many;
            switch (n % 10)
            {
                case 1: return n + " " + one;
                case 2:
                case 3:
                case 4: return n + " " + few;
                default: return n + " " + many;
            }
        }
    }
}
