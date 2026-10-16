using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WorkTimeApp
{
    internal static class AppUpdater
    {
        internal static int CompareVersions(string left, string right)
        {
            string[] a = left.TrimStart('v').Split('-'), b = right.TrimStart('v').Split('-');
            int core = Version.Parse(a[0]).CompareTo(Version.Parse(b[0]));
            if (core != 0) return core;
            if (a.Length == 1 || b.Length == 1) return a.Length == b.Length ? 0 : a.Length == 1 ? 1 : -1;
            string[] x = a[1].Split('.'), y = b[1].Split('.');
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
            {
                int c = int.TryParse(x[i], out int n) && int.TryParse(y[i], out int m) ? n.CompareTo(m) : string.CompareOrdinal(x[i], y[i]);
                if (c != 0) return c;
            }
            return x.Length.CompareTo(y.Length);
        }

        internal static async Task Check(Form owner, Control button, string current)
        {
            const string releasesPage = "https://github.com/ZheLang1223/OffWork/releases";
            button.Enabled = false;
            button.Text = "检查中…";
            string download = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Encoding = System.Text.Encoding.UTF8;
                    client.Headers[HttpRequestHeader.UserAgent] = "OffWork-Updater";
                    client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    byte[] response = await client.DownloadDataTaskAsync("https://api.github.com/repos/ZheLang1223/OffWork/releases?per_page=30");
                    string json = new System.Text.UTF8Encoding(false, true).GetString(response);
                    var releases = new JavaScriptSerializer().Deserialize<object[]>(json);
                    Dictionary<string, object> best = null;
                    string newest = current;
                    foreach (Dictionary<string, object> release in releases)
                    {
                        if ((bool)release["draft"]) continue;
                        string tag = (string)release["tag_name"];
                        try { if (CompareVersions(tag, newest) > 0) { best = release; newest = tag; } }
                        catch (FormatException) { }
                        catch (ArgumentException) { }
                    }
                    if (best == null) { MessageBox.Show(owner, "当前已是最新版本。", "检查更新"); return; }
                    var assets = (object[])best["assets"];
                    var asset = assets.Cast<Dictionary<string, object>>().FirstOrDefault(a => (string)a["name"] == "OffWork-Setup.exe");
                    if (asset == null) throw new InvalidOperationException("新版尚未提供安装包。");
                    string digest = asset.ContainsKey("digest") ? asset["digest"] as string : null;
                    if (digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal)) throw new InvalidOperationException("安装包缺少 SHA-256 校验信息。");
                    string notes = best["body"] as string ?? "";
                    if (notes.Length > 1800) notes = notes.Substring(0, 1800) + "…";
                    if (MessageBox.Show(owner, "发现 " + newest + "\n\n" + notes + "\n\n下载更新？请先保存当前设置。", "应用更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                    string url = (string)asset["browser_download_url"];
                    if (!url.StartsWith("https://github.com/ZheLang1223/OffWork/releases/download/", StringComparison.Ordinal)) throw new InvalidOperationException("安装包地址无效。");
                    string folder = Path.Combine(Path.GetTempPath(), "OffWork-update-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(folder);
                    download = Path.Combine(folder, "OffWork-Setup.exe");
                    client.DownloadProgressChanged += (s, e) => { if (!button.IsDisposed) button.Text = "下载 " + e.ProgressPercentage + "%"; };
                    await client.DownloadFileTaskAsync(url, download);
                    button.Text = "校验中…";
                    string hash;
                    using (var stream = File.OpenRead(download))
                    using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(hash, digest.Substring(7), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("安装包校验失败，请重试。");
                    if (MessageBox.Show(owner, "安装包已下载并通过校验。现在退出程序并安装更新？", "安装更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(download) { UseShellExecute = true });
                        Application.Exit();
                    }
                }
            }
            catch (Exception ex)
            {
                if (download != null && File.Exists(download)) { try { File.Delete(download); } catch (IOException) { } }
                string reason = ex is ArgumentException || ex is System.Text.DecoderFallbackException
                    ? "无法解析更新信息，请稍后重试。" : ex.Message;
                if (reason.Length > 240) reason = reason.Substring(0, 240) + "…";
                if (!owner.IsDisposed && MessageBox.Show(owner, "更新失败：" + reason + "\n\n打开下载页面？", "应用更新", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(releasesPage) { UseShellExecute = true });
            }
            finally { if (!button.IsDisposed) { button.Enabled = true; button.Text = "检查更新"; } }
        }
    }
}
