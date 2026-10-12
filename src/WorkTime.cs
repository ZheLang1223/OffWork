using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WorkTimeApp
{
    internal static class JsonCompat
    {
        public static Dictionary<string, object> DeserializeDictionary(string json)
        {
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }

        public static string Serialize(object value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }
    }

    public class Entry
    {
        public TimeSpan? Start { get; set; }
        public TimeSpan? Finish { get; set; }
        public int BreakMin { get; set; } = 0; // 无午休，恒为0
        public string Kind { get; set; }
        public string Note { get; set; }

        public Entry(TimeSpan? start = null, TimeSpan? finish = null, int breakMin = 0, string kind = "自动", string note = "")
        {
            Start = start;
            Finish = finish;
            BreakMin = 0;
            Kind = string.IsNullOrEmpty(kind) ? "自动" : kind;
            Note = note ?? "";
        }

        public int? WorkedMinutes
        {
            get
            {
                if (!Start.HasValue || !Finish.HasValue) return null;
                DateTime s = new DateTime(2000, 1, 1).Add(Start.Value);
                DateTime f = new DateTime(2000, 1, 1).Add(Finish.Value);
                if (f < s) f = f.AddDays(1);
                int total = (int)Math.Floor((f - s).TotalMinutes);
                return Math.Max(0, total);
            }
        }

        public string StartClock => Start.HasValue ? string.Format("{0:D2}:{1:D2}", Start.Value.Hours, Start.Value.Minutes) : "";
        public string FinishClock => Finish.HasValue ? string.Format("{0:D2}:{1:D2}", Finish.Value.Hours, Finish.Value.Minutes) : "";

        public Dictionary<string, object> ToDict()
        {
            var dict = new Dictionary<string, object>();
            dict["start"] = StartClock;
            dict["finish"] = FinishClock;
            dict["break_min"] = 0;
            dict["kind"] = Kind;
            dict["note"] = Note;
            return dict;
        }

        public static Entry FromDict(Dictionary<string, object> dict)
        {
            var entry = new Entry();
            if (dict == null) return entry;
            if (dict.ContainsKey("start")) entry.Start = ParseTime(dict["start"] != null ? dict["start"].ToString() : null);
            string fKey = dict.ContainsKey("finish") ? "finish" : (dict.ContainsKey("end") ? "end" : null);
            if (fKey != null) entry.Finish = ParseTime(dict[fKey] != null ? dict[fKey].ToString() : null);
            entry.BreakMin = 0;
            if (dict.ContainsKey("kind") && dict["kind"] != null) entry.Kind = dict["kind"].ToString();
            if (dict.ContainsKey("note") && dict["note"] != null) entry.Note = dict["note"].ToString();
            return entry;
        }

        public static TimeSpan? ParseTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim().Replace("：", ":");
            var match = Regex.Match(s, @"^(\d{1,2})[:](\d{1,2})$");
            if (match.Success)
            {
                int h = int.Parse(match.Groups[1].Value);
                int m = int.Parse(match.Groups[2].Value);
                if (h >= 0 && h < 24 && m >= 0 && m < 60) return new TimeSpan(h, m, 0);
            }
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                if (d >= 0.0 && d < 1.0)
                {
                    int totalSec = (int)Math.Round(d * 86400.0);
                    int h = (totalSec / 3600) % 24;
                    int m = (totalSec % 3600) / 60;
                    return new TimeSpan(h, m, 0);
                }
            }
            return null;
        }
    }

    public static class HolidayService
    {
        public class DayMeta
        {
            public bool IsHoliday { get; set; }
            public bool IsWorkday { get; set; }
            public string Name { get; set; }
        }

        private static readonly Dictionary<string, DayMeta> Rules = new Dictionary<string, DayMeta>(StringComparer.OrdinalIgnoreCase);

        static HolidayService()
        {
            // 2025
            AddHoliday("2025-01-01", "元旦");
            AddHolidayRange("2025-01-28", "2025-02-04", "春节");
            AddWorkday("2025-01-26", "补班");
            AddWorkday("2025-02-08", "补班");
            AddHolidayRange("2025-04-04", "2025-04-06", "清明");
            AddHolidayRange("2025-05-01", "2025-05-05", "劳动节");
            AddWorkday("2025-04-27", "补班");
            AddHolidayRange("2025-05-31", "2025-06-02", "端午");
            AddHolidayRange("2025-10-01", "2025-10-08", "国庆");
            AddWorkday("2025-09-28", "补班");
            AddWorkday("2025-10-11", "补班");

            // 2026
            AddHolidayRange("2026-01-01", "2026-01-03", "元旦");
            AddWorkday("2026-01-04", "补班");
            AddHolidayRange("2026-02-15", "2026-02-23", "春节");
            AddWorkday("2026-02-14", "补班");
            AddWorkday("2026-02-28", "补班");
            AddHolidayRange("2026-04-04", "2026-04-06", "清明");
            AddHolidayRange("2026-05-01", "2026-05-05", "劳动节");
            AddWorkday("2026-04-26", "补班");
            AddWorkday("2026-05-09", "补班");
            AddHolidayRange("2026-06-19", "2026-06-21", "端午");
            AddHolidayRange("2026-09-25", "2026-09-27", "中秋");
            AddHolidayRange("2026-10-01", "2026-10-07", "国庆");
            AddWorkday("2026-09-27", "补班");
            AddWorkday("2026-10-10", "补班");

            // 2027
            AddHolidayRange("2027-01-01", "2027-01-03", "元旦");
            AddHolidayRange("2027-02-06", "2027-02-14", "春节");
            AddWorkday("2027-01-31", "补班");
            AddWorkday("2027-02-20", "补班");
            AddHolidayRange("2027-04-03", "2027-04-05", "清明");
            AddHolidayRange("2027-05-01", "2027-05-05", "劳动节");
            AddWorkday("2027-04-25", "补班");
            AddWorkday("2027-05-08", "补班");
            AddHolidayRange("2027-06-09", "2027-06-11", "端午");
            AddWorkday("2027-06-13", "补班");
            AddHoliday("2027-09-15", "中秋");
            AddHolidayRange("2027-10-01", "2027-10-07", "国庆");
            AddWorkday("2027-09-26", "补班");
            AddWorkday("2027-10-09", "补班");
        }

        private static void AddHoliday(string dateStr, string name)
        {
            Rules[dateStr] = new DayMeta { IsHoliday = true, IsWorkday = false, Name = name };
        }

        private static void AddHolidayRange(string startStr, string endStr, string name)
        {
            DateTime s = DateTime.ParseExact(startStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateTime e = DateTime.ParseExact(endStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            for (DateTime d = s; d <= e; d = d.AddDays(1))
            {
                Rules[d.ToString("yyyy-MM-dd")] = new DayMeta { IsHoliday = true, IsWorkday = false, Name = name };
            }
        }

        private static void AddWorkday(string dateStr, string name)
        {
            Rules[dateStr] = new DayMeta { IsHoliday = false, IsWorkday = true, Name = name };
        }

        public static void LoadCustomHolidays(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;
            try
            {
                string json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var dict = JsonCompat.DeserializeDictionary(json);
                if (dict == null) return;
                if (dict.ContainsKey("holidays") && dict["holidays"] is Dictionary<string, object> h)
                {
                    foreach (var kv in h) AddHoliday(kv.Key, kv.Value?.ToString() ?? "休");
                }
                if (dict.ContainsKey("workdays") && dict["workdays"] is Dictionary<string, object> w)
                {
                    foreach (var kv in w) AddWorkday(kv.Key, kv.Value?.ToString() ?? "班");
                }
            }
            catch { }
        }

        public static bool IsWorkday(DateTime date)
        {
            string key = date.ToString("yyyy-MM-dd");
            if (Rules.TryGetValue(key, out var meta))
            {
                if (meta.IsHoliday) return false;
                if (meta.IsWorkday) return true;
            }
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
        }

        public static string GetDayTag(DateTime date)
        {
            string key = date.ToString("yyyy-MM-dd");
            if (Rules.TryGetValue(key, out var meta))
            {
                if (meta.IsHoliday) return "休";
                if (meta.IsWorkday) return "班";
            }
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) return "休";
            return null;
        }

        public static int CountWorkdays(int year, int month)
        {
            int total = 0;
            int days = DateTime.DaysInMonth(year, month);
            for (int i = 1; i <= days; i++)
            {
                if (IsWorkday(new DateTime(year, month, i))) total++;
            }
            return total;
        }

        public static int CountWorkdaysUpTo(int year, int month, DateTime upTo)
        {
            int total = 0;
            int limit = Math.Min(DateTime.DaysInMonth(year, month), upTo.Day);
            for (int i = 1; i <= limit; i++)
            {
                if (IsWorkday(new DateTime(year, month, i))) total++;
            }
            return total;
        }
    }

    public class SummaryResult
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalWorkdays { get; set; }
        public int TargetMinutes { get; set; }
        public int ActualMinutes { get; set; }
        public int ExpectedMinutes { get; set; }
        public int DeltaMinutes => ActualMinutes - ExpectedMinutes;
        public int RemainingMinutes => Math.Max(0, TargetMinutes - ActualMinutes);
        public int RemainingWorkdays { get; set; }
        public double DailyAverageRequiredHours => RemainingWorkdays > 0 ? Math.Round((RemainingMinutes / 60.0) / RemainingWorkdays, 1) : 0.0;
    }

    public class ExcelStorage
    {
        public const string APP_TITLE = "下班了";
        public const string WORKBOOK_NAME = "工时记录.xlsx";
        public const string JOURNAL_NAME = "待同步.json";
        public const string HOLIDAY_FILE = "holidays.json";

        public static readonly string[] DAY_KINDS = new string[]
        {
            "自动", "工作日", "出勤", "加班", "法定休假", "调休", "年假", "事假", "病假", "出差"
        };

        private const string TemplateBase64 = "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";

        public string FolderPath { get; private set; }
        public string BookPath => Path.Combine(FolderPath, WORKBOOK_NAME);
        public string JournalPath => Path.Combine(FolderPath, JOURNAL_NAME);
        public string HolidayPath => Path.Combine(FolderPath, HOLIDAY_FILE);

        public Dictionary<DateTime, Entry> Records { get; private set; } = new Dictionary<DateTime, Entry>();
        public double DailyTarget { get; set; } = 8.0;
        public string StandardStart { get; set; } = "09:00";
        public string StandardFinish { get; set; } = "18:00";

        public Dictionary<string, object> PendingEntries { get; private set; } = new Dictionary<string, object>();
        public Dictionary<string, object> PendingConfig { get; private set; } = new Dictionary<string, object>();

        public bool Dirty => PendingEntries.Count > 0 || PendingConfig.Count > 0;

        public ExcelStorage(string folder = null)
        {
            FolderPath = folder ?? GetStorageDir();
            Directory.CreateDirectory(FolderPath);
            HolidayService.LoadCustomHolidays(HolidayPath);
            if (!File.Exists(BookPath))
            {
                ExtractTemplate(BookPath);
            }
            Reload();
            ReadJournal();
            MergePending();
            if (Dirty)
            {
                try { Sync(DateTime.Today); } catch { }
            }
        }

        public static string GetStorageDir()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                // If running portably in a dedicated custom folder (NOT Desktop and NOT Downloads) that already has existing data
                if (!string.IsNullOrEmpty(exeDir) &&
                    !string.Equals(Path.GetFullPath(exeDir).TrimEnd('\\'), Path.GetFullPath(desktopDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(Path.GetFullPath(exeDir).TrimEnd('\\'), Path.GetFullPath(downloadsDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(Path.Combine(exeDir, WORKBOOK_NAME)))
                    {
                        return exeDir;
                    }
                }
            }
            catch { }

            // Default: Always store in user's Documents\下班了 (ensures Desktop and other launch sites stay clean)
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (key != null)
                    {
                        var personal = key.GetValue("Personal");
                        if (personal != null)
                        {
                            string exp = Environment.ExpandEnvironmentVariables(personal.ToString());
                            if (Directory.Exists(exp)) return Path.Combine(exp, APP_TITLE);
                        }
                    }
                }
            }
            catch { }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), APP_TITLE);
        }

        public static void ExtractTemplate(string destinationPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Template.xlsx", StringComparison.OrdinalIgnoreCase));
            if (resName != null)
            {
                using (var stream = asm.GetManifestResourceStream(resName))
                using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                    return;
                }
            }
            if (File.Exists("template.xlsx"))
            {
                File.Copy("template.xlsx", destinationPath, true);
                return;
            }
            if (!string.IsNullOrEmpty(TemplateBase64))
            {
                File.WriteAllBytes(destinationPath, Convert.FromBase64String(TemplateBase64));
                return;
            }
        }

        public void CreateBackup()
        {
            try
            {
                if (!File.Exists(BookPath)) return;
                string backupDir = Path.Combine(FolderPath, "backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, string.Format("工时记录_backup_{0}.xlsx", timestamp));
                File.Copy(BookPath, backupPath, true);

                var dirInfo = new DirectoryInfo(backupDir);
                var files = dirInfo.GetFiles("工时记录_backup_*.xlsx")
                                   .OrderByDescending(f => f.CreationTime)
                                   .ToList();
                if (files.Count > 20)
                {
                    for (int i = 20; i < files.Count; i++)
                    {
                        try { files[i].Delete(); } catch { }
                    }
                }
            }
            catch { }
        }

        public void Reload()
        {
            if (!File.Exists(BookPath)) return;
            var parsed = new Dictionary<DateTime, Entry>();
            try
            {
                using (var archive = ZipFile.OpenRead(BookPath))
                {
                    var sharedStrings = LoadSharedStrings(archive);
                    var sheet1Entry = archive.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheet1Entry != null)
                    {
                        using (var stream = sheet1Entry.Open())
                        {
                            var xdoc = XDocument.Load(stream);
                            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                            foreach (var row in xdoc.Descendants(ns + "row"))
                            {
                                int rIndex = (int?)row.Attribute("r") ?? 0;
                                if (rIndex < 2) continue;
                                var cells = new Dictionary<string, string>();
                                foreach (var c in row.Elements(ns + "c"))
                                {
                                    string r = (string)c.Attribute("r");
                                    if (string.IsNullOrEmpty(r)) continue;
                                    string col = Regex.Replace(r, @"\d", "");
                                    string val = GetCellValue(c, sharedStrings, ns);
                                    cells[col] = val;
                                }

                                if (!cells.ContainsKey("A")) continue;
                                DateTime? d = ParseDateCell(cells["A"]);
                                if (!d.HasValue) continue;

                                var entry = new Entry();
                                if (cells.ContainsKey("C")) entry.Start = Entry.ParseTime(cells["C"]);
                                if (cells.ContainsKey("D")) entry.Finish = Entry.ParseTime(cells["D"]);
                                entry.BreakMin = 0;
                                if (cells.ContainsKey("H")) entry.Kind = cells["H"];
                                if (cells.ContainsKey("I")) entry.Note = cells["I"];
                                parsed[d.Value.Date] = entry;
                            }
                        }
                    }

                    // Read Sheet2 config
                    var sheet2Entry = archive.GetEntry("xl/worksheets/sheet2.xml");
                    if (sheet2Entry != null)
                    {
                        using (var stream = sheet2Entry.Open())
                        {
                            var xdoc = XDocument.Load(stream);
                            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                            foreach (var c in xdoc.Descendants(ns + "c"))
                            {
                                string r = (string)c.Attribute("r");
                                if (r == "B1")
                                {
                                    string val = GetCellValue(c, sharedStrings, ns);
                                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double dt)) DailyTarget = dt;
                                }
                                else if (r == "C1" || r == "D1")
                                {
                                    string val = GetCellValue(c, sharedStrings, ns);
                                    TimeSpan? time = Entry.ParseTime(val);
                                    if (time.HasValue)
                                    {
                                        if (r == "C1") StandardStart = time.Value.ToString(@"hh\:mm");
                                        else StandardFinish = time.Value.ToString(@"hh\:mm");
                                    }
                                }
                            }
                        }
                    }
                }
                Records = parsed;
            }
            catch { }
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            var list = new List<string>();
            var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sstEntry == null) return list;
            using (var stream = sstEntry.Open())
            {
                var xdoc = XDocument.Load(stream);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var si in xdoc.Descendants(ns + "si"))
                {
                    var texts = si.Descendants(ns + "t").Select(t => t.Value);
                    list.Add(string.Concat(texts));
                }
            }
            return list;
        }

        private static string GetCellValue(XElement c, List<string> sharedStrings, XNamespace ns)
        {
            string type = (string)c.Attribute("t");
            if (type == "s")
            {
                var v = c.Element(ns + "v");
                if (v != null && int.TryParse(v.Value, out int idx) && idx >= 0 && idx < sharedStrings.Count) return sharedStrings[idx];
            }
            if (type == "inlineStr")
            {
                var isEl = c.Element(ns + "is");
                if (isEl != null)
                {
                    return string.Concat(isEl.Descendants(ns + "t").Select(t => t.Value));
                }
            }
            var valEl = c.Element(ns + "v");
            return valEl != null ? valEl.Value : "";
        }

        private static DateTime? ParseDateCell(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (DateTime.TryParse(raw, out DateTime dt)) return dt.Date;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
            {
                if (serial > 10000 && serial < 80000)
                {
                    DateTime epoch = new DateTime(1899, 12, 30);
                    return epoch.AddDays(serial).Date;
                }
            }
            return null;
        }

        public void SaveDay(DateTime day, TimeSpan? start, TimeSpan? finish, string kind, string note)
        {
            var entry = new Entry(start, finish, 0, kind, note);
            Records[day.Date] = entry;
            PendingEntries[day.ToString("yyyy-MM-dd")] = entry.ToDict();
            WriteJournal();
            Sync(day);
        }

        public void DeleteDay(DateTime day)
        {
            if (Records.ContainsKey(day.Date)) Records.Remove(day.Date);
            PendingEntries[day.ToString("yyyy-MM-dd")] = null;
            WriteJournal();
            Sync(day);
        }

        public void SaveConfig(double target, string stdStart = "09:00", string stdFinish = "18:00")
        {
            DailyTarget = target;
            StandardStart = stdStart;
            StandardFinish = stdFinish;
            PendingConfig["daily_target_hours"] = target;
            PendingConfig["standard_start"] = stdStart;
            PendingConfig["standard_finish"] = stdFinish;
            WriteJournal();
            Sync(DateTime.Today);
        }

        public void ReadJournal()
        {
            if (!File.Exists(JournalPath)) return;
            try
            {
                string json = File.ReadAllText(JournalPath, Encoding.UTF8);
                var dict = JsonCompat.DeserializeDictionary(json);
                if (dict == null) return;
                if (dict.ContainsKey("entries") && dict["entries"] is Dictionary<string, object> ents)
                {
                    PendingEntries = ents;
                }
                if (dict.ContainsKey("config") && dict["config"] is Dictionary<string, object> cfg)
                {
                    PendingConfig = cfg;
                }
            }
            catch { }
        }

        public void WriteJournal()
        {
            try
            {
                var payload = new Dictionary<string, object>();
                payload["entries"] = PendingEntries;
                payload["config"] = PendingConfig;
                string json = JsonCompat.Serialize(payload);
                File.WriteAllText(JournalPath, json, Encoding.UTF8);
            }
            catch { }
        }

        public void MergePending()
        {
            foreach (var kv in PendingEntries)
            {
                if (DateTime.TryParse(kv.Key, out DateTime dt))
                {
                    if (kv.Value is Dictionary<string, object> d)
                    {
                        Records[dt.Date] = Entry.FromDict(d);
                    }
                    else if (kv.Value == null)
                    {
                        Records.Remove(dt.Date);
                    }
                }
            }
            if (PendingConfig.ContainsKey("daily_target_hours"))
            {
                if (double.TryParse(PendingConfig["daily_target_hours"].ToString(), out double dt)) DailyTarget = dt;
            }
            if (PendingConfig.ContainsKey("standard_start"))
            {
                StandardStart = PendingConfig["standard_start"]?.ToString() ?? "09:00";
            }
            if (PendingConfig.ContainsKey("standard_finish"))
            {
                StandardFinish = PendingConfig["standard_finish"]?.ToString() ?? "18:00";
            }
        }

        public void Sync(DateTime chosen)
        {
            if (!Dirty && File.Exists(BookPath)) return;
            MergePending();

            string tmp = Path.Combine(FolderPath, string.Format("~tmp_{0}.xlsx", Guid.NewGuid().ToString("N")));
            try
            {
                if (File.Exists(BookPath))
                {
                    CreateBackup();
                    File.Copy(BookPath, tmp, true);
                }
                else
                {
                    ExtractTemplate(tmp);
                }

                UpdateSheetXml(tmp, chosen);

                if (File.Exists(BookPath)) File.Delete(BookPath);
                File.Move(tmp, BookPath);

                PendingEntries.Clear();
                PendingConfig.Clear();
                if (File.Exists(JournalPath)) File.Delete(JournalPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(tmp)) { try { File.Delete(tmp); } catch { } }
                throw ex;
            }
        }

        private void UpdateSheetXml(string xlsxPath, DateTime chosen)
        {
            using (var archive = ZipFile.Open(xlsxPath, ZipArchiveMode.Update))
            {
                var sheet1Entry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheet1Entry != null)
                {
                    XDocument xdoc;
                    using (var s = sheet1Entry.Open()) xdoc = XDocument.Load(s);

                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var sheetData = xdoc.Descendants(ns + "sheetData").FirstOrDefault();
                    if (sheetData != null)
                    {
                        var sortedDays = Records.Keys.OrderBy(d => d).ToList();
                        sheetData.Elements(ns + "row").Where(r => ((int?)r.Attribute("r") ?? 0) >= 2).Remove();

                        int rIdx = 2;
                        DateTime epoch = new DateTime(1899, 12, 30);
                        foreach (var day in sortedDays)
                        {
                            var entry = Records[day];
                            var row = new XElement(ns + "row", new XAttribute("r", rIdx));

                            // A: Date
                            int dSerial = (int)(day - epoch).TotalDays;
                            row.Add(CreateCell(ns, "A" + rIdx, "1", dSerial.ToString()));
                            // B: Weekday
                            row.Add(CreateFormulaCell(ns, "B" + rIdx, "2", string.Format("TEXT(A{0},\"aaaa\")", rIdx), GetChineseWeekday(day)));
                            // C: Start
                            if (entry.Start.HasValue)
                            {
                                double tVal = entry.Start.Value.TotalDays;
                                row.Add(CreateCell(ns, "C" + rIdx, "3", tVal.ToString("0.################", CultureInfo.InvariantCulture)));
                            }
                            else row.Add(CreateCell(ns, "C" + rIdx, "3", ""));
                            // D: Finish
                            if (entry.Finish.HasValue)
                            {
                                double tVal = entry.Finish.Value.TotalDays;
                                row.Add(CreateCell(ns, "D" + rIdx, "3", tVal.ToString("0.################", CultureInfo.InvariantCulture)));
                            }
                            else row.Add(CreateCell(ns, "D" + rIdx, "3", ""));
                            // E: Break (0)
                            row.Add(CreateCell(ns, "E" + rIdx, "4", "0"));
                            // F: Worked hours formula
                            row.Add(CreateFormulaCell(ns, "F" + rIdx, "5", string.Format("IF(OR(C{0}=\"\",D{0}=\"\"),\"\",MAX(0,ROUND(((D{0}+(D{0}<C{0})-C{0})*24),2)))", rIdx),
                                entry.WorkedMinutes.HasValue ? (entry.WorkedMinutes.Value / 60.0).ToString("F2", CultureInfo.InvariantCulture) : ""));
                            // G: Worked minutes formula
                            row.Add(CreateFormulaCell(ns, "G" + rIdx, "4", string.Format("IF(F{0}=\"\",\"\",ROUND(F{0}*60,0))", rIdx),
                                entry.WorkedMinutes.HasValue ? entry.WorkedMinutes.Value.ToString() : ""));
                            // H: Kind
                            row.Add(CreateInlineStrCell(ns, "H" + rIdx, "0", entry.Kind));
                            // I: Note
                            row.Add(CreateInlineStrCell(ns, "I" + rIdx, "0", entry.Note));

                            sheetData.Add(row);
                            rIdx++;
                        }
                    }

                    sheet1Entry.Delete();
                    var newEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
                    using (var stream = newEntry.Open()) xdoc.Save(stream);
                }

                // Update Sheet2 (Monthly summary & config)
                var sheet2Entry = archive.GetEntry("xl/worksheets/sheet2.xml");
                if (sheet2Entry != null)
                {
                    XDocument xdoc;
                    using (var s = sheet2Entry.Open()) xdoc = XDocument.Load(s);

                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var sheetData = xdoc.Descendants(ns + "sheetData").FirstOrDefault();
                    if (sheetData != null)
                    {
                        var row1 = sheetData.Elements(ns + "row").FirstOrDefault(r => (int?)r.Attribute("r") == 1);
                        if (row1 == null)
                        {
                            row1 = new XElement(ns + "row", new XAttribute("r", 1));
                            sheetData.AddFirst(row1);
                        }
                        // Persist configuration even when the embedded template has no B1 cell.
                        row1.Elements(ns + "c").Where(c =>
                            (string)c.Attribute("r") == "B1" ||
                            (string)c.Attribute("r") == "C1" ||
                            (string)c.Attribute("r") == "D1").Remove();
                        row1.Add(CreateCell(ns, "B1", "0", DailyTarget.ToString(CultureInfo.InvariantCulture)));
                        row1.Add(CreateInlineStrCell(ns, "C1", "0", StandardStart));
                        row1.Add(CreateInlineStrCell(ns, "D1", "0", StandardFinish));

                        sheetData.Elements(ns + "row").Where(r => ((int?)r.Attribute("r") ?? 0) >= 5).Remove();
                        var m = Summary(chosen.Year, chosen.Month);
                        int mSerial = (int)(new DateTime(chosen.Year, chosen.Month, 1) - new DateTime(1899, 12, 30)).TotalDays;
                        double targetH = Math.Round(m.TargetMinutes / 60.0, 1);
                        double actualH = Math.Round(m.ActualMinutes / 60.0, 1);
                        double deltaH = Math.Round(m.DeltaMinutes / 60.0, 1);

                        var r5 = new XElement(ns + "row", new XAttribute("r", 5));
                        r5.Add(CreateCell(ns, "A5", "1", mSerial.ToString()));
                        r5.Add(CreateCell(ns, "B5", "5", targetH.ToString(CultureInfo.InvariantCulture)));
                        r5.Add(CreateFormulaCell(ns, "C5", "5", "ROUND(SUMIFS(明细!G:G,明细!A:A,\">=\"&A5,明细!A:A,\"<=\"&EOMONTH(A5,0))/60,2)", actualH.ToString(CultureInfo.InvariantCulture)));
                        r5.Add(CreateFormulaCell(ns, "D5", "5", "ROUND(C5-B5,2)", deltaH.ToString(CultureInfo.InvariantCulture)));
                        sheetData.Add(r5);
                    }

                    sheet2Entry.Delete();
                    var newEntry = archive.CreateEntry("xl/worksheets/sheet2.xml");
                    using (var stream = newEntry.Open()) xdoc.Save(stream);
                }
            }
        }

        private static XElement CreateCell(XNamespace ns, string r, string s, string val)
        {
            var el = new XElement(ns + "c", new XAttribute("r", r));
            if (!string.IsNullOrEmpty(s)) el.Add(new XAttribute("s", s));
            if (!string.IsNullOrEmpty(val)) el.Add(new XElement(ns + "v", val));
            return el;
        }

        private static XElement CreateFormulaCell(XNamespace ns, string r, string s, string formula, string val)
        {
            var el = new XElement(ns + "c", new XAttribute("r", r));
            if (!string.IsNullOrEmpty(s)) el.Add(new XAttribute("s", s));
            el.Add(new XElement(ns + "f", formula));
            if (!string.IsNullOrEmpty(val)) el.Add(new XElement(ns + "v", val));
            return el;
        }

        private static XElement CreateInlineStrCell(XNamespace ns, string r, string s, string text)
        {
            var el = new XElement(ns + "c", new XAttribute("r", r), new XAttribute("t", "inlineStr"));
            if (!string.IsNullOrEmpty(s)) el.Add(new XAttribute("s", s));
            el.Add(new XElement(ns + "is", new XElement(ns + "t", text ?? "")));
            return el;
        }

        public static string GetChineseWeekday(DateTime d)
        {
            switch (d.DayOfWeek)
            {
                case DayOfWeek.Monday: return "星期一";
                case DayOfWeek.Tuesday: return "星期二";
                case DayOfWeek.Wednesday: return "星期三";
                case DayOfWeek.Thursday: return "星期四";
                case DayOfWeek.Friday: return "星期五";
                case DayOfWeek.Saturday: return "星期六";
                case DayOfWeek.Sunday: return "星期日";
                default: return "";
            }
        }

        public static string GetChineseWeekdayShort(DateTime d)
        {
            switch (d.DayOfWeek)
            {
                case DayOfWeek.Monday: return "周一";
                case DayOfWeek.Tuesday: return "周二";
                case DayOfWeek.Wednesday: return "周三";
                case DayOfWeek.Thursday: return "周四";
                case DayOfWeek.Friday: return "周五";
                case DayOfWeek.Saturday: return "周六";
                case DayOfWeek.Sunday: return "周日";
                default: return "";
            }
        }

        public SummaryResult Summary(int year, int month, DateTime? today = null)
        {
            DateTime current = (today ?? DateTime.Today).Date;
            int totalWorkdays = HolidayService.CountWorkdays(year, month);
            int targetMinutes = (int)Math.Round(totalWorkdays * DailyTarget * 60);

            int actualMinutes = 0;
            foreach (var kv in Records)
            {
                if (kv.Key.Year == year && kv.Key.Month == month)
                {
                    actualMinutes += kv.Value.WorkedMinutes ?? 0;
                }
            }

            int expectedWorkdays = 0;
            int remainingWorkdays = 0;
            if (current.Year == year && current.Month == month)
            {
                expectedWorkdays = HolidayService.CountWorkdaysUpTo(year, month, current);
                int daysInMonth = DateTime.DaysInMonth(year, month);
                for (int d = current.Day + 1; d <= daysInMonth; d++)
                {
                    if (HolidayService.IsWorkday(new DateTime(year, month, d))) remainingWorkdays++;
                }
            }
            else if (new DateTime(year, month, 1) < new DateTime(current.Year, current.Month, 1))
            {
                expectedWorkdays = totalWorkdays;
                remainingWorkdays = 0;
            }
            else
            {
                expectedWorkdays = 0;
                remainingWorkdays = totalWorkdays;
            }

            int expectedMinutes = (int)Math.Round(expectedWorkdays * DailyTarget * 60);

            return new SummaryResult
            {
                Year = year,
                Month = month,
                TotalWorkdays = totalWorkdays,
                TargetMinutes = targetMinutes,
                ActualMinutes = actualMinutes,
                ExpectedMinutes = expectedMinutes,
                RemainingWorkdays = remainingWorkdays
            };
        }

        public static string FormatDuration(int minutes)
        {
            int sign = minutes < 0 ? -1 : 1;
            int abs = Math.Abs(minutes);
            int h = abs / 60;
            int m = abs % 60;
            string prefix = sign < 0 ? "-" : "";
            if (h == 0) return string.Format("{0}{1}分", prefix, m);
            return string.Format("{0}{1}小时{2:D2}分", prefix, h, m);
        }
    }

    // Modern Rounded Card Panel
// Modern Rounded Card Panel
// Modern Flat Helper Utilities
// Modern Flat Helper Utilities
// Modern Flat Helper Utilities
// Modern Flat Helper Utilities
// Dynamic DPI Scaling Helper (Guarantees identical appearance on all screens)
    // Each window/control reads its own monitor DPI; no shared mutable scale.
    public sealed class DpiMetrics
    {
        private readonly Control owner;
        public DpiMetrics(Control owner) { this.owner = owner; }
        public float Scale => Math.Max(1.0f, UiHelper.LayoutDpi(owner) / 96.0f);
        public int S(int val) => (int)Math.Round(val * Scale);
        public Size S(int w, int h) => new Size(S(w), S(h));
        public Point P(int x, int y) => new Point(S(x), S(y));
    }

    public class DpiAwareForm : Form
    {
        private Size naturalClientSize;
        private readonly List<Font> dpiFonts = new List<Font>();
        private readonly Dictionary<Control, LayoutBaseline> baselines = new Dictionary<Control, LayoutBaseline>();
        private SizeF logicalClientSize;
        public int LayoutDpi { get; private set; }

        public DpiAwareForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            AutoScroll = true;
            LayoutDpi = DeviceDpi;
        }

        protected override void OnLoad(EventArgs e)
        {
            CaptureBaselines(this);
            logicalClientSize = new SizeF(ClientSize.Width * 96f / LayoutDpi, ClientSize.Height * 96f / LayoutDpi);
            naturalClientSize = ClientSize;
            if (!(this is MainForm)) AutoScrollMinSize = naturalClientSize;
            FitToWorkingArea(Screen.FromControl(Owner ?? this).WorkingArea);
            base.OnLoad(e);
        }

        internal void FitToWorkingArea(Rectangle area)
        {
            int maxWidth = Math.Max(100, area.Width - 16);
            int maxHeight = Math.Max(100, area.Height - 16);
            MinimumSize = new Size(Math.Min(MinimumSize.Width, maxWidth), Math.Min(MinimumSize.Height, maxHeight));
            Size = new Size(Math.Min(Width, maxWidth), Math.Min(Height, maxHeight));
            if (WindowState == FormWindowState.Normal)
                Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                    Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
        }

        protected void SetContentMinimum(Size size)
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            Size outer = SizeFromClientSize(size);
            MinimumSize = new Size(Math.Min(outer.Width, Math.Max(100, area.Width - 16)),
                Math.Min(outer.Height, Math.Max(100, area.Height - 16)));
        }

        private sealed class LayoutBaseline
        {
            public RectangleF Bounds;
            public string FontFamily;
            public float FontSize;
            public FontStyle Style;
            public GraphicsUnit Unit;
        }

        private void CaptureBaselines(Control control)
        {
            if (!baselines.ContainsKey(control))
            {
                float factor = 96f / Math.Max(96, LayoutDpi);
                baselines[control] = new LayoutBaseline
                {
                    Bounds = new RectangleF(control.Left * factor, control.Top * factor,
                        control.Width * factor, control.Height * factor),
                    FontFamily = control.Font.FontFamily.Name,
                    FontSize = control.Font.Size * factor,
                    Style = control.Font.Style,
                    Unit = control.Font.Unit
                };
            }
            foreach (Control child in control.Controls) CaptureBaselines(child);
        }

        internal void ScaleForDpi(float factor)
        {
            ApplyLayoutDpi((int)Math.Round(LayoutDpi * factor));
        }

        internal void ApplyLayoutDpi(int dpi)
        {
            CaptureBaselines(this);
            LayoutDpi = Math.Max(96, dpi);
            float factor = LayoutDpi / 96f;
            var replacements = new List<Font>();
            bool scrolling = AutoScroll;
            SuspendLayout();
            AutoScroll = false;
            try
            {
                foreach (var item in baselines)
                {
                    if (item.Key.IsDisposed) continue;
                    var font = new Font(item.Value.FontFamily, item.Value.FontSize * factor,
                        item.Value.Style, item.Value.Unit);
                    item.Key.Font = font;
                    replacements.Add(font);
                    if (item.Key != this && item.Key.Dock == DockStyle.None)
                    {
                        RectangleF rect = item.Value.Bounds;
                        item.Key.SetBounds((int)Math.Round(rect.X * factor), (int)Math.Round(rect.Y * factor),
                            (int)Math.Round(rect.Width * factor), (int)Math.Round(rect.Height * factor));
                    }
                }
                foreach (var font in dpiFonts) font.Dispose();
                dpiFonts.Clear();
                dpiFonts.AddRange(replacements);
            }
            finally
            {
                AutoScroll = scrolling;
                AutoScrollPosition = Point.Empty;
                ResumeLayout(false);
            }
            Invalidate(true);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (var font in dpiFonts) font.Dispose();
                dpiFonts.Clear();
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            // Own the resize once; do not combine cumulative Scale() calls with WM_DPICHANGED resizing.
            e.Cancel = true;
            Rectangle workingArea = Screen.FromRectangle(e.SuggestedRectangle).WorkingArea;
            SuspendLayout();
            try
            {
                if (logicalClientSize.IsEmpty)
                    logicalClientSize = new SizeF(ClientSize.Width * 96f / LayoutDpi, ClientSize.Height * 96f / LayoutDpi);
                ApplyLayoutDpi(e.DeviceDpiNew);
                naturalClientSize = new Size((int)Math.Round(logicalClientSize.Width * LayoutDpi / 96f),
                    (int)Math.Round(logicalClientSize.Height * LayoutDpi / 96f));
                MinimumSize = Size.Empty;
                ClientSize = naturalClientSize;
                if (!(this is MainForm)) AutoScrollMinSize = naturalClientSize;
                Location = e.SuggestedRectangle.Location;
                FitToWorkingArea(workingArea);
                base.OnDpiChanged(e);
            }
            finally { ResumeLayout(true); }
            Invalidate(true);
        }
    }    // Modern Flat Helper Utilities
    public static class UiHelper
    {
        public const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        public static int LayoutDpi(Control owner)
        {
            var form = owner.FindForm() as DpiAwareForm;
            return form == null ? owner.DeviceDpi : form.LayoutDpi;
        }

        public static Font DrawingFont(Control owner, float points, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", points * 96f / 72f * LayoutDpi(owner) / 96f, style, GraphicsUnit.Pixel);
        }

        public static Font FitDrawingFont(Control owner, string text, float points, FontStyle style, Size area)
        {
            Font font = DrawingFont(owner, points, style);
            while (font.Size > 1f)
            {
                Size measured = TextRenderer.MeasureText(text, font, Size.Empty, TextFlags);
                if (measured.Width <= area.Width && measured.Height <= area.Height) break;
                float smaller = Math.Max(1f, font.Size - 0.5f);
                font.Dispose();
                font = new Font("Microsoft YaHei UI", smaller, style, GraphicsUnit.Pixel);
            }
            return font;
        }

        public static int FontAscent(Font font)
        {
            return (int)Math.Ceiling(font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style));
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle area)
        {
            TextRenderer.DrawText(g, text, font, area, color, TextFlags | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;
            radius = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Button CreateButton(Control owner, string text, Point loc, Size sz, bool isPrimary = false)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = sz,
                Font = DrawingFont(owner, 9.5f, isPrimary ? FontStyle.Bold : FontStyle.Regular),
                BackColor = isPrimary ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#F1F5F9"),
                ForeColor = isPrimary ? Color.White : ColorTranslator.FromHtml("#334155"),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }

    // Vector Gear Icon Button
    public class GearButton : Button
    {
        private readonly DpiMetrics Dpi;
        private bool isHovered = false;

        public GearButton()
        {
            Dpi = new DpiMetrics(this);
            SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Hand;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.MouseEnter += (s, e) => { isHovered = true; Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = this.Width / 2;
            int cy = this.Height / 2;
            Color c = isHovered ? ColorTranslator.FromHtml("#0F172A") : ColorTranslator.FromHtml("#64748B");

            float rOut = Math.Max(7f, this.Height * 0.28f);
            float rIn = rOut * 0.55f;

            using (var pen = new Pen(c, Math.Max(1.8f, Dpi.Scale * 1.8f)))
            {
                g.DrawEllipse(pen, cx - rIn, cy - rIn, rIn * 2, rIn * 2);
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3.0;
                    float x1 = (float)(cx + (rIn + 1) * Math.Cos(angle));
                    float y1 = (float)(cy + (rIn + 1) * Math.Sin(angle));
                    float x2 = (float)(cx + rOut * Math.Cos(angle));
                    float y2 = (float)(cy + rOut * Math.Sin(angle));
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }
    }

    // Modern Borderless Progress Bar
    public class ModernProgressBar : Control
    {
        private readonly DpiMetrics Dpi;
        private double percentage = 0.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Percentage
        {
            get => percentage;
            set { percentage = double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : Math.Max(0.0, value); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TrackColor { get; set; } = ColorTranslator.FromHtml("#E2E8F0");
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; } = ColorTranslator.FromHtml("#2563EB");
        private readonly Color completedColor = ColorTranslator.FromHtml("#16A34A");
        private readonly Color excessColor = ColorTranslator.FromHtml("#F59E0B");

        public ModernProgressBar()
        {
            Dpi = new DpiMetrics(this);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, this.Width, this.Height);
            int r = Math.Min(rect.Height / 2, 4);

            using (var path = UiHelper.GetRoundedPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), r))
            using (var brush = new SolidBrush(TrackColor))
            {
                g.FillPath(brush, path);
            }

            int fillW = (int)Math.Round((this.Width - 1) * (Math.Min(100.0, Percentage) / 100.0));
            if (fillW > 4)
            {
                var fillRect = new Rectangle(0, 0, fillW, this.Height - 1);
                using (var fillPath = UiHelper.GetRoundedPath(fillRect, r))
                using (var brush = new SolidBrush(Percentage >= 100.0 ? completedColor : FillColor))
                {
                    g.FillPath(brush, fillPath);
                }
            }
            int excessW = (int)Math.Round((Width - 1) * Math.Min(100.0, Math.Max(0.0, Percentage - 100.0)) / 100.0);
            if (excessW > 0)
            {
                using (var path = UiHelper.GetRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), r))
                using (var brush = new SolidBrush(excessColor))
                {
                    var previousClip = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    g.FillRectangle(brush, 0, 0, excessW, Height - 1);
                    g.Restore(previousClip);
                }
            }
        }
    }

        // Modern Today Punch Double Card Control with Vector Self-Painted Buttons
    public class TodayPunchControl : Control
    {
        private readonly DpiMetrics Dpi;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string StartTime { get; set; } = "--:--";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string FinishTime { get; set; } = "--:--";
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasStart { get; set; } = false;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HasFinish { get; set; } = false;

        private bool hoverStart = false;
        private bool hoverFinish = false;
        private bool hoverStartTime;
        private bool hoverFinishTime;

        public event EventHandler StartClicked;
        public event EventHandler FinishClicked;
        public event Action<bool, TimeSpan?> TimeEdited;
        private readonly TextBox inlineTimeEditor;
        private bool editingStart;
        private readonly Timer confirmationTimer;
        private readonly ToolTip punchToolTip;
        private int pendingConfirmation = -1;
        private long confirmationExpiresAt;
        private DateTime confirmationDay;
        private bool activationKeyHeld;

        public TodayPunchControl()
        {
            Dpi = new DpiMetrics(this);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            this.BackColor = Color.White;
            this.TabStop = true;
            this.AccessibleName = "今日上下班打卡";
            this.AccessibleRole = AccessibleRole.Grouping;
            this.AccessibleDescription = "左右方向键选择上班或下班，回车或空格打卡；更新需在10秒内再次确认，F2编辑时间，Esc取消确认。";
            confirmationTimer = new Timer { Interval = 250 };
            confirmationTimer.Tick += (s, e) => ExpireConfirmation();
            punchToolTip = new ToolTip();
            punchToolTip.SetToolTip(this, "点击整张卡片打卡；已有时间需在10秒内再次点击确认；右键或 F2 编辑时间。");
            inlineTimeEditor = new TextBox
            {
                Visible = false, AutoSize = false, BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 5, TextAlign = HorizontalAlignment.Left
            };
            Controls.Add(inlineTimeEditor);
            inlineTimeEditor.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { SaveInlineTime(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { CancelInlineTime(); e.SuppressKeyPress = true; }
            };
            inlineTimeEditor.LostFocus += (s, e) => CancelInlineTime();
            inlineTimeEditor.TextChanged += (s, e) => inlineTimeEditor.BackColor = Color.White;
        }

        private bool focusFinishButton;

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Enter || key == Keys.Space || key == Keys.F2 || key == Keys.Escape || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                focusFinishButton = e.KeyCode == Keys.Right;
                Invalidate();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (!activationKeyHeld) RequestPunch(!focusFinishButton);
                activationKeyHeld = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape) { CancelConfirmation(); e.Handled = true; }
            else if (e.KeyCode == Keys.F2) { EditTime(!focusFinishButton); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) activationKeyHeld = false;
            base.OnKeyUp(e);
        }

        private void CancelConfirmation()
        {
            pendingConfirmation = -1;
            confirmationTimer.Stop();
            Invalidate();
        }

        private void ExpireConfirmation()
        {
            if (pendingConfirmation >= 0 && (Stopwatch.GetTimestamp() >= confirmationExpiresAt || confirmationDay != DateTime.Today))
                CancelConfirmation();
        }

        private void RequestPunch(bool start)
        {
            ExpireConfirmation();
            int action = start ? 0 : 1;
            bool exists = start ? HasStart : HasFinish;
            if (exists && pendingConfirmation != action)
            {
                pendingConfirmation = action;
                confirmationDay = DateTime.Today;
                confirmationExpiresAt = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
                confirmationTimer.Start();
                Invalidate();
                return;
            }
            CancelConfirmation();
            if (start) StartClicked?.Invoke(this, EventArgs.Empty);
            else FinishClicked?.Invoke(this, EventArgs.Empty);
        }

        private void EditTime(bool start)
        {
            CancelConfirmation();
            editingStart = start;
            inlineTimeEditor.Bounds = start ? GetStartTimeRect() : GetFinishTimeRect();
            inlineTimeEditor.Font = UiHelper.DrawingFont(this, 16f, FontStyle.Bold);
            inlineTimeEditor.Text = (start ? HasStart : HasFinish) ? (start ? StartTime : FinishTime) : "";
            inlineTimeEditor.BackColor = Color.White;
            punchToolTip.SetToolTip(inlineTimeEditor, "输入 HH:mm，回车保存，Esc取消；留空可清除该时间。");
            inlineTimeEditor.Visible = true;
            inlineTimeEditor.BringToFront();
            inlineTimeEditor.Focus();
            inlineTimeEditor.SelectAll();
        }

        private void CancelInlineTime()
        {
            inlineTimeEditor.Visible = false;
            Invalidate();
        }

        internal void RepositionEditor()
        {
            if (!inlineTimeEditor.Visible) return;
            inlineTimeEditor.Bounds = editingStart ? GetStartTimeRect() : GetFinishTimeRect();
            inlineTimeEditor.Font = UiHelper.DrawingFont(this, 16f, FontStyle.Bold);
        }

        private void SaveInlineTime()
        {
            string text = inlineTimeEditor.Text.Trim().Replace('：', ':');
            TimeSpan parsed;
            TimeSpan? time = null;
            if (text.Length > 0)
            {
                if (!TimeSpan.TryParseExact(text, new[] { @"hh\:mm", @"h\:mm" }, CultureInfo.InvariantCulture, out parsed) || parsed.TotalHours >= 24)
                {
                    inlineTimeEditor.BackColor = ColorTranslator.FromHtml("#FEE2E2");
                    punchToolTip.Show("请输入有效时间，例如 09:00。", inlineTimeEditor, 0, inlineTimeEditor.Height, 2500);
                    return;
                }
                time = parsed;
            }
            bool start = editingStart;
            CancelInlineTime();
            TimeEdited?.Invoke(start, time);
            Focus();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            activationKeyHeld = false;
            CancelConfirmation();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Focus();
        }

        internal void ResetDayInteraction()
        {
            CancelConfirmation();
            CancelInlineTime();
            activationKeyHeld = false;
        }

        public void UpdatePunch(string startTime, bool hasStart, string finishTime, bool hasFinish)
        {
            if (StartTime != startTime || FinishTime != finishTime || HasStart != hasStart || HasFinish != hasFinish)
                CancelConfirmation();
            StartTime = string.IsNullOrEmpty(startTime) ? "--:--" : startTime;
            HasStart = hasStart;
            FinishTime = string.IsNullOrEmpty(finishTime) ? "--:--" : finishTime;
            HasFinish = hasFinish;
            Invalidate();
        }

        private Rectangle GetStartBtnRect()
        {
            int cardW = (Width - Dpi.S(12)) / 2;
            return new Rectangle(0, 0, cardW, Height);
        }

        private Rectangle GetFinishBtnRect()
        {
            int cardW = (Width - Dpi.S(12)) / 2;
            return new Rectangle(cardW + Dpi.S(12), 0, cardW, Height);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool inStart = GetStartBtnRect().Contains(e.Location);
            bool inFinish = GetFinishBtnRect().Contains(e.Location);
            bool inStartTime = GetStartTimeRect().Contains(e.Location);
            bool inFinishTime = GetFinishTimeRect().Contains(e.Location);

            if (inStart != hoverStart || inFinish != hoverFinish || inStartTime != hoverStartTime || inFinishTime != hoverFinishTime)
            {
                hoverStart = inStart;
                hoverFinish = inFinish;
                hoverStartTime = inStartTime;
                hoverFinishTime = inFinishTime;
                Invalidate();
            }
            this.Cursor = (inStart || inFinish || GetStartTimeRect().Contains(e.Location) || GetFinishTimeRect().Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverStart || hoverFinish || hoverStartTime || hoverFinishTime)
            {
                hoverStart = false;
                hoverFinish = false;
                hoverStartTime = false;
                hoverFinishTime = false;
                this.Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right)
            {
                if (GetStartBtnRect().Contains(e.Location)) { focusFinishButton = false; EditTime(true); }
                else if (GetFinishBtnRect().Contains(e.Location)) { focusFinishButton = true; EditTime(false); }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (GetStartBtnRect().Contains(e.Location))
            {
                focusFinishButton = false;
                RequestPunch(true);
            }
            else if (GetFinishBtnRect().Contains(e.Location))
            {
                focusFinishButton = true;
                RequestPunch(false);
            }

        }

        private Rectangle GetStartTimeRect()
        {
            return GetTimeRect(new Rectangle(0, 0, (Width - Dpi.S(12)) / 2, Height), GetStartBtnRect());
        }

        private Rectangle GetFinishTimeRect()
        {
            int width = (Width - Dpi.S(12)) / 2;
            return GetTimeRect(new Rectangle(width + Dpi.S(12), 0, width, Height), GetFinishBtnRect());
        }

        private Rectangle GetTimeRect(Rectangle card, Rectangle button)
        {
            // Reserve only the measured label width, the right inset and a small gap.
            using (var labelFont = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular))
            {
                int labelWidth = TextRenderer.MeasureText("更新", labelFont, Size.Empty, UiHelper.TextFlags).Width;
                return new Rectangle(card.X + Dpi.S(4), card.Y + Dpi.S(2),
                    Math.Max(1, card.Width - labelWidth - Dpi.S(12)), Math.Max(1, card.Height - Dpi.S(4)));
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int gap = Dpi.S(12);
            int cardW = (Width - gap) / 2;
            var first = new Rectangle(0, 0, cardW, Height);
            var second = new Rectangle(cardW + gap, 0, cardW, Height);
            var startButton = GetStartBtnRect();
            var finishButton = GetFinishBtnRect();
            DrawPunchCard(g, first, startButton, "上班", StartTime, HasStart, hoverStart);
            DrawPunchCard(g, second, finishButton, "下班", FinishTime, HasFinish, hoverFinish);
            if (Focused)
            {
                Rectangle focusRect = focusFinishButton ? finishButton : startButton;
                focusRect.Inflate(-Dpi.S(2), -Dpi.S(2));
                ControlPaint.DrawFocusRectangle(g, focusRect, ColorTranslator.FromHtml("#1D4ED8"), Color.White);
            }
        }

        private void DrawPunchCard(Graphics g, Rectangle card, Rectangle button,
            string label, string time, bool punched, bool hovered)
        {
            bool confirming = pendingConfirmation == (label == "上班" ? 0 : 1);
            if (confirming || hovered)
            {
                using (var path = UiHelper.GetRoundedPath(card, Dpi.S(8)))
                using (var brush = new SolidBrush(ColorTranslator.FromHtml(confirming ? "#FEF2F2" : "#F1F5F9")))
                    g.FillPath(brush, path);
            }
            using (var font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular))
            {
                int textX = card.Right - Dpi.S(38);
                int textY = card.Y + Math.Max(0, (card.Height - Dpi.S(44)) / 2);
                TextRenderer.DrawText(g, label, font, new Rectangle(textX, textY + Dpi.S(24), Dpi.S(32), Dpi.S(20)), ColorTranslator.FromHtml("#64748B"), UiHelper.TextFlags | TextFormatFlags.Right);
                string hint = confirming ? "确认" : hovered ? (punched ? "更新" : "打卡") : "";
                TextRenderer.DrawText(g, hint, font, new Rectangle(textX, textY, Dpi.S(32), Dpi.S(20)), ColorTranslator.FromHtml(confirming ? "#DC2626" : hovered ? "#2563EB" : "#64748B"), UiHelper.TextFlags | TextFormatFlags.Right);
            }
            Rectangle timeRect = GetTimeRect(card, button);
            using (var family = new FontFamily("Microsoft YaHei UI"))
            using (var digits = new GraphicsPath())
            using (var format = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (var brush = new SolidBrush(ColorTranslator.FromHtml(confirming ? "#B91C1C" : punched ? "#334155" : "#94A3B8")))
            {
                digits.AddString(time, family, (int)FontStyle.Bold, 100f, PointF.Empty, format);
                RectangleF ink = digits.GetBounds();
                if (ink.Width > 0 && ink.Height > 0)
                {
                    float scale = Math.Min(timeRect.Width / ink.Width, timeRect.Height / ink.Height);
                    using (var transform = new Matrix(scale, 0, 0, scale,
                        timeRect.Left - ink.Left * scale,
                        timeRect.Top + (timeRect.Height - ink.Height * scale) / 2 - ink.Top * scale))
                        digits.Transform(transform);
                    g.FillPath(brush, digits);
                }
            }
        }
        private void DrawButton(Graphics g, Rectangle rect, string text, bool isPunched, bool isHover, bool confirming)
        {
            int r = Dpi.S(4);
            using (var path = UiHelper.GetRoundedPath(rect, r))
            {
                if (!isPunched || confirming)
                {
                    // Primary Blue
                    Color bg = confirming ? ColorTranslator.FromHtml(isHover ? "#B91C1C" : "#DC2626") : ColorTranslator.FromHtml(isHover ? "#1D4ED8" : "#2563EB");
                    using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
                    using (var font = UiHelper.DrawingFont(this, 9f, FontStyle.Bold))
                    using (var brushText = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(text, font, brushText, rect, sf);
                    }
                }
                else
                {
                    // Clean White Card Button with Border
                    Color bg = ColorTranslator.FromHtml(isHover ? "#EFF6FF" : "#F1F5F9");
                    using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
                    using (var font = UiHelper.DrawingFont(this, 9f, FontStyle.Regular))
                    using (var brushText = new SolidBrush(ColorTranslator.FromHtml(isHover ? "#2563EB" : "#64748B")))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(text, font, brushText, rect, sf);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { confirmationTimer.Dispose(); punchToolTip.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Modern 2x2 KPI Grid Dashboard Control
    public class MonthKpiControl : Control
    {
        private int hoveredCard = -1;
        private int CardAt(Point point)
        {
            int gap = Dpi.S(12);
            int width = (Width - gap) / 2;
            int height = (Height - gap) / 2;
            for (int index = 0; index < 4; index++)
                if (new Rectangle((index % 2) * (width + gap), (index / 2) * (height + gap), width, height).Contains(point)) return index;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int card = CardAt(e.Location);
            if (hoveredCard == card) return;
            hoveredCard = card;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoveredCard = -1;
            Invalidate();
        }

        private readonly DpiMetrics Dpi;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ActualHours { get; set; } = 0.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ActualDays { get; set; } = 0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double TargetHours { get; set; } = 0.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double RemainHours { get; set; } = 0.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RemainDays { get; set; } = 0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double DailyReqHours { get; set; } = 0.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StandardDailyHours { get; set; } = 8.0;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int DeltaMinutes { get; set; } = 0;

        public MonthKpiControl()
        {
            Dpi = new DpiMetrics(this);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.White;
        }

        public void UpdateStats(double actualHours, int actualDays, double targetHours, double remainHours, int remainDays, double dailyReqHours, double standardDailyHours, int deltaMinutes)
        {
            ActualHours = actualHours;
            ActualDays = actualDays;
            TargetHours = targetHours;
            RemainHours = remainHours;
            RemainDays = remainDays;
            DailyReqHours = dailyReqHours;
            StandardDailyHours = standardDailyHours;
            DeltaMinutes = deltaMinutes;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int gap = Dpi.S(12);
            int cardW = (this.Width - gap) / 2;
            int cardH = (this.Height - gap) / 2;
            int r = Dpi.S(6);

            // Card 1: 累计已出勤 (0, 0)
            DrawCard(g, new Rectangle(0, 0, cardW, cardH), r,
                "已出勤",
                string.Format("{0:F1}", ActualHours),
                "h",
                string.Format("已出勤 {0} 天", ActualDays),
                ColorTranslator.FromHtml("#EFF6FF"),
                ColorTranslator.FromHtml("#2563EB"),
                ColorTranslator.FromHtml("#1D4ED8"),
                ColorTranslator.FromHtml("#3B82F6"));

            // Card 2: 还需完成 (1, 0)
            DrawCard(g, new Rectangle(cardW + gap, 0, cardW, cardH), r,
                "还需完成",
                string.Format("{0:F1}", RemainHours),
                "h",
                string.Format("剩余 {0} 工作日", RemainDays),
                ColorTranslator.FromHtml("#FFF7ED"),
                ColorTranslator.FromHtml("#C2410C"),
                ColorTranslator.FromHtml("#9A3412"),
                ColorTranslator.FromHtml("#C2410C"));

            // Card 3: 每日需达 (0, 1)
            DrawCard(g, new Rectangle(0, cardH + gap, cardW, cardH), r,
                "每日需达",
                string.Format("{0:F1}", DailyReqHours),
                "h/天",
                string.Format("标准 {0:F1}h/天", StandardDailyHours),
                ColorTranslator.FromHtml("#F5F3FF"),
                ColorTranslator.FromHtml("#7C3AED"),
                ColorTranslator.FromHtml("#6D28D9"),
                ColorTranslator.FromHtml("#7C3AED"));

            // Card 4: 进度状态 (1, 1)
            string deltaVal;
            string deltaSub;
            Color card4Bg;
            Color card4Title;
            Color card4Val;
            Color card4Sub;

            if (DeltaMinutes > 0)
            {
                deltaVal = string.Format("+{0:F1}", DeltaMinutes / 60.0);
                deltaSub = "领先正常节奏";
                card4Bg = ColorTranslator.FromHtml("#F0FDF4");
                card4Title = ColorTranslator.FromHtml("#16A34A");
                card4Val = ColorTranslator.FromHtml("#15803D");
                card4Sub = ColorTranslator.FromHtml("#16A34A");
            }
            else if (DeltaMinutes < 0)
            {
                deltaVal = string.Format("-{0:F1}", Math.Abs(DeltaMinutes) / 60.0);
                deltaSub = "落后正常节奏";
                card4Bg = ColorTranslator.FromHtml("#FEF2F2");
                card4Title = ColorTranslator.FromHtml("#DC2626");
                card4Val = ColorTranslator.FromHtml("#DC2626");
                card4Sub = ColorTranslator.FromHtml("#DC2626");
            }
            else
            {
                deltaVal = "持平";
                deltaSub = "与计划一致";
                card4Bg = ColorTranslator.FromHtml("#F8FAFC");
                card4Title = ColorTranslator.FromHtml("#64748B");
                card4Val = ColorTranslator.FromHtml("#0F172A");
                card4Sub = ColorTranslator.FromHtml("#94A3B8");
            }

            DrawCard(g, new Rectangle(cardW + gap, cardH + gap, cardW, cardH), r,
                "进度状态",
                deltaVal,
                DeltaMinutes == 0 ? "" : "h",
                deltaSub,
                card4Bg,
                card4Title,
                card4Val,
                card4Sub);
        }

        private void DrawCard(Graphics g, Rectangle rect, int radius,
            string title, string value, string unit, string subtitle,
            Color bg, Color titleColor, Color valueColor, Color subColor)
        {
            bool hovered = hoveredCard >= 0 && CardAt(new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2)) == hoveredCard;
            if (hovered)
                bg = Color.FromArgb((int)(bg.R * 0.88 + titleColor.R * 0.12),
                    (int)(bg.G * 0.88 + titleColor.G * 0.12), (int)(bg.B * 0.88 + titleColor.B * 0.12));
            using (var path = UiHelper.GetRoundedPath(rect, radius))
            using (var brush = new SolidBrush(bg)) g.FillPath(brush, path);
            if (hovered)
            {
                var outline = rect;
                outline.Inflate(-1, -1);
                using (var path = UiHelper.GetRoundedPath(outline, radius))
                using (var pen = new Pen(Color.FromArgb(130, titleColor), Math.Max(1, Dpi.S(1))))
                    g.DrawPath(pen, path);
            }
            int pad = Dpi.S(10);
            int width = Math.Max(1, rect.Width - pad * 2);
            using (var titleFont = UiHelper.DrawingFont(this, 9f, FontStyle.Regular))
            using (var subFont = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular))
            {
                int titleHeight = TextRenderer.MeasureText(title, titleFont, Size.Empty, UiHelper.TextFlags).Height;
                int subHeight = TextRenderer.MeasureText(subtitle, subFont, Size.Empty, UiHelper.TextFlags).Height;
                int titleTop = rect.Top + pad;
                int subTop = rect.Bottom - pad - subHeight;
                int valueTop = titleTop + titleHeight + Dpi.S(4);
                int valueHeight = Math.Max(1, subTop - Dpi.S(4) - valueTop);
                UiHelper.DrawText(g, title, titleFont, titleColor, new Rectangle(rect.X + pad, titleTop, width, titleHeight));
                UiHelper.DrawText(g, subtitle, subFont, subColor, new Rectangle(rect.X + pad, subTop, width, subHeight));
                string suffix = unit == "h/天" ? "/天" : "";
                string mainValue = value + (string.IsNullOrEmpty(unit) ? "" : "h");
                int suffixWidth = suffix.Length == 0 ? 0 : TextRenderer.MeasureText(suffix, subFont, Size.Empty, UiHelper.TextFlags).Width;
                int suffixGap = suffixWidth == 0 ? 0 : Dpi.S(3);
                var valueArea = new Rectangle(rect.X + pad, valueTop, Math.Max(1, width - suffixWidth - suffixGap), valueHeight);
                using (var valueFont = UiHelper.FitDrawingFont(this, mainValue, 16.5f, FontStyle.Bold, valueArea.Size))
                {
                    Size valueSize = TextRenderer.MeasureText(mainValue, valueFont, Size.Empty, UiHelper.TextFlags);
                    int baseline = valueTop + UiHelper.FontAscent(valueFont);
                    UiHelper.DrawText(g, mainValue, valueFont, valueColor, new Rectangle(valueArea.X, valueTop, valueArea.Width, valueSize.Height));
                    if (suffixWidth > 0)
                        UiHelper.DrawText(g, suffix, subFont, ColorTranslator.FromHtml("#64748B"),
                            new Rectangle(valueArea.X + valueSize.Width + suffixGap, baseline - UiHelper.FontAscent(subFont), suffixWidth, subHeight));
                }
            }
        }
    }    // Modern Borderless Attendance Calendar Control
    public class AttendanceCalendar : Panel
    {
        private readonly DpiMetrics Dpi;
        public DateTime DisplayMonth { get; private set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime SelectedDate { get; private set; } = DateTime.Today;

        private ExcelStorage store;
        private int hoveredIndex = -1;
        private ToolTip toolTip;

        public event Action<DateTime> DateSelected;
        public event Action<DateTime> MonthChanged;

        private static readonly string[] Weekdays = new string[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        public AttendanceCalendar()
        {
            Dpi = new DpiMetrics(this);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            this.BackColor = Color.White;
            this.TabStop = true;
            this.AccessibleName = "考勤日历";
            this.AccessibleRole = AccessibleRole.Table;
            this.AccessibleDescription = "方向键移动日期，回车编辑所选日期，Page Up 和 Page Down 切换月份。";
            this.toolTip = new ToolTip
            {
                InitialDelay = 150,
                AutoPopDelay = 6000,
                ReshowDelay = 100,
                ShowAlways = true
            };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Focus();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
                   key == Keys.PageUp || key == Keys.PageDown || key == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            DateTime next = SelectedDate;
            switch (e.KeyCode)
            {
                case Keys.Left: next = next.AddDays(-1); break;
                case Keys.Right: next = next.AddDays(1); break;
                case Keys.Up: next = next.AddDays(-7); break;
                case Keys.Down: next = next.AddDays(7); break;
                case Keys.PageUp: next = next.AddMonths(-1); break;
                case Keys.PageDown: next = next.AddMonths(1); break;
                case Keys.Enter:
                    DateSelected?.Invoke(SelectedDate);
                    e.Handled = true;
                    base.OnKeyDown(e);
                    return;
                default:
                    base.OnKeyDown(e);
                    return;
            }

            SelectDate(next, false);
            e.Handled = true;
            base.OnKeyDown(e);
        }

        public void BindStorage(ExcelStorage storage)
        {
            this.store = storage;
            Invalidate();
        }

        public void SetMonth(int year, int month)
        {
            DisplayMonth = new DateTime(year, month, 1);
            Invalidate();
            MonthChanged?.Invoke(DisplayMonth);
        }

        public void NextMonth()
        {
            DisplayMonth = DisplayMonth.AddMonths(1);
            Invalidate();
            MonthChanged?.Invoke(DisplayMonth);
        }

        public void PrevMonth()
        {
            DisplayMonth = DisplayMonth.AddMonths(-1);
            Invalidate();
            MonthChanged?.Invoke(DisplayMonth);
        }

        public void SelectDate(DateTime dt, bool triggerEvent = false)
        {
            SelectedDate = dt.Date;
            if (SelectedDate.Year != DisplayMonth.Year || SelectedDate.Month != DisplayMonth.Month)
            {
                DisplayMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
                MonthChanged?.Invoke(DisplayMonth);
            }
            Invalidate();
            if (triggerEvent)
            {
                DateSelected?.Invoke(SelectedDate);
            }
        }

        public int VisibleWeekCount
        {
            get
            {
                int offset = ((int)DisplayMonth.DayOfWeek + 6) % 7;
                return (offset + DateTime.DaysInMonth(DisplayMonth.Year, DisplayMonth.Month) + 6) / 7;
            }
        }

        private List<DateTime> GetCalendarCells()
        {
            var list = new List<DateTime>();
            DateTime firstDay = DisplayMonth;
            int offset = ((int)firstDay.DayOfWeek + 6) % 7;
            DateTime start = firstDay.AddDays(-offset);
            for (int i = 0; i < VisibleWeekCount * 7; i++) list.Add(start.AddDays(i));
            return list;
        }

        private Rectangle GetCellRect(int index)
        {
            int col = index % 7;
            int row = index / 7;
            int headerH = Dpi.S(26);
            int gap = Dpi.S(4);
            int x = col * Width / 7;
            int right = (col + 1) * Width / 7 - (col == 6 ? 0 : gap);
            int y = headerH + row * (Height - headerH) / VisibleWeekCount;
            int bottom = headerH + (row + 1) * (Height - headerH) / VisibleWeekCount - (row == VisibleWeekCount - 1 ? 0 : gap);
            return Rectangle.FromLTRB(x, y, right, bottom);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newIdx = -1;
            for (int i = 0; i < VisibleWeekCount * 7; i++)
            {
                if (GetCellRect(i).Contains(e.Location)) { newIdx = i; break; }
            }
            if (newIdx != hoveredIndex)
            {
                hoveredIndex = newIdx;
                this.Cursor = hoveredIndex >= 0 ? Cursors.Hand : Cursors.Default;
                if (hoveredIndex >= 0 && store != null)
                {
                    var cells = GetCalendarCells();
                    DateTime dt = cells[hoveredIndex];
                    string tag = HolidayService.GetDayTag(dt);
                    bool isWork = HolidayService.IsWorkday(dt);
                    string typeName = isWork ? (tag == "班" ? "调休工作日" : "正常工作日") : (string.IsNullOrEmpty(tag) ? "周末休息" : "法定节假日 (" + tag + ")");

                    var sb = new StringBuilder();
                    sb.AppendFormat("{0:yyyy-MM-dd} ({1}) [{2}]\n", dt, ExcelStorage.GetChineseWeekdayShort(dt), typeName);

                    if (store.Records.TryGetValue(dt.Date, out var ent) && ent != null && (ent.Start.HasValue || ent.Finish.HasValue))
                    {
                        sb.AppendFormat("上班: {0}   下班: {1}\n", string.IsNullOrEmpty(ent.StartClock) ? "未打" : ent.StartClock, string.IsNullOrEmpty(ent.FinishClock) ? "未打" : ent.FinishClock);
                        sb.AppendFormat("工时: {0}\n", ent.WorkedMinutes.HasValue ? string.Format("{0:F1}小时", ent.WorkedMinutes.Value / 60.0) : "未下班");
                        if (!string.IsNullOrEmpty(ent.Kind)) sb.AppendFormat("类型: {0}\n", ent.Kind);
                        if (!string.IsNullOrEmpty(ent.Note)) sb.AppendFormat("备注: {0}\n", ent.Note);
                        sb.Append("点击编辑此日记录");
                    }
                    else if (dt.Date < DateTime.Today && isWork)
                    {
                        sb.Append("未打卡 (缺卡)\n点击补卡");
                    }
                    else
                    {
                        sb.Append("点击补卡");
                    }
                    toolTip.SetToolTip(this, sb.ToString().TrimEnd());
                }
                else
                {
                    toolTip.SetToolTip(this, null);
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoveredIndex = -1;
            toolTip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var cells = GetCalendarCells();
            for (int i = 0; i < VisibleWeekCount * 7; i++)
            {
                if (GetCellRect(i).Contains(e.Location))
                {
                    SelectDate(cells[i], true);
                    break;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Weekday Headers (Clean flat text)
            int cellW = this.Width / 7;
            using (var fontW = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular))
            using (var brushW = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
            using (var brushSat = new SolidBrush(ColorTranslator.FromHtml("#0284C7")))
            using (var brushSun = new SolidBrush(ColorTranslator.FromHtml("#E11D48")))
            {
                for (int i = 0; i < 7; i++)
                {
                    var rect = new Rectangle(i * cellW, Dpi.S(2), cellW - Dpi.S(4), Dpi.S(20));
                    Brush b = brushW;
                    if (i == 5) b = brushSat;
                    else if (i == 6) b = brushSun;
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(Weekdays[i], fontW, b, rect, sf);
                }
            }

            // Only the weeks needed by this month are drawn and hit-tested.
            var cells = GetCalendarCells();
            for (int i = 0; i < VisibleWeekCount * 7; i++)
            {
                DateTime dt = cells[i];
                var rect = GetCellRect(i);
                bool inCurrentMonth = dt.Month == DisplayMonth.Month;
                bool isSelected = dt.Date == SelectedDate.Date;
                bool isToday = dt.Date == DateTime.Today;
                bool isHovered = (i == hoveredIndex);

                Color bg = inCurrentMonth ? ColorTranslator.FromHtml("#FAFBFC") : Color.White;
                if (isToday) bg = ColorTranslator.FromHtml("#EFF6FF");
                else if (isHovered) bg = ColorTranslator.FromHtml("#F1F5F9");

                using (var path = UiHelper.GetRoundedPath(rect, Dpi.S(5)))
                {
                    using (var b = new SolidBrush(bg)) g.FillPath(b, path);

                    if (isToday)
                    {
                        using (var p = new Pen(ColorTranslator.FromHtml("#93C5FD"), Math.Max(1.2f, Dpi.Scale * 1.2f))) g.DrawPath(p, path);
                    }
                }

                // Day Number
                Color dayColor = inCurrentMonth ? ColorTranslator.FromHtml("#1E293B") : ColorTranslator.FromHtml("#CBD5E1");
                if (isToday) dayColor = ColorTranslator.FromHtml("#2563EB");
                using (var fontDay = UiHelper.DrawingFont(this, 10.5f, isToday ? FontStyle.Bold : FontStyle.Regular))
                using (var brushDay = new SolidBrush(dayColor))
                {
                    g.DrawString(dt.Day.ToString(), fontDay, brushDay, new PointF(rect.X + Dpi.S(6), rect.Y + Dpi.S(4)));
                }

                // Tag Badge (休 / 班)
                string tag = HolidayService.GetDayTag(dt);
                if (!string.IsNullOrEmpty(tag) && inCurrentMonth)
                {
                    bool isHolidayTag = tag == "休";
                    Color tBg = isHolidayTag ? ColorTranslator.FromHtml("#FEE2E2") : ColorTranslator.FromHtml("#DBEAFE");
                    Color tFg = isHolidayTag ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#1D4ED8");
                    int tagW = Dpi.S(20);
                    int tagH = Dpi.S(16);
                    var tagRect = new Rectangle(rect.Right - tagW - Dpi.S(6), rect.Y + Dpi.S(4), tagW, tagH);
                    using (var tPath = UiHelper.GetRoundedPath(tagRect, 3))
                    using (var tb = new SolidBrush(tBg))
                    using (var tf = new SolidBrush(tFg))
                    using (var tFont = UiHelper.DrawingFont(this, 8f, FontStyle.Bold))
                    {
                        g.FillPath(tb, tPath);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(tag, tFont, tf, tagRect, sf);
                    }
                }

                // Attendance Status Pill
                if (store != null && inCurrentMonth)
                {
                    bool hasRecord = store.Records.TryGetValue(dt.Date, out var entry);
                    if (hasRecord && entry != null && (entry.Start.HasValue || entry.Finish.HasValue))
                    {
                        string pillText;
                        Color pBg = ColorTranslator.FromHtml("#DCFCE7");
                        Color pFg = ColorTranslator.FromHtml("#15803D");
                        if (entry.WorkedMinutes.HasValue)
                        {
                            pillText = string.Format("{0:F1}h", entry.WorkedMinutes.Value / 60.0);
                        }
                        else if (entry.Start.HasValue && !entry.Finish.HasValue)
                        {
                            pillText = "进行中";
                            pBg = ColorTranslator.FromHtml("#FEF3C7");
                            pFg = ColorTranslator.FromHtml("#B45309");
                        }
                        else
                        {
                            pillText = "已打卡";
                        }

                        var pRect = new Rectangle(rect.X + Dpi.S(4), rect.Bottom - Dpi.S(23), rect.Width - Dpi.S(8), Dpi.S(18));
                        using (var pPath = UiHelper.GetRoundedPath(pRect, 3))
                        using (var pb = new SolidBrush(pBg))
                        using (var pf = new SolidBrush(pFg))
                        using (var pFont = UiHelper.DrawingFont(this, 8.5f, FontStyle.Bold))
                        {
                            g.FillPath(pb, pPath);
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(pillText, pFont, pf, pRect, sf);
                        }
                    }
                    else if (dt.Date < DateTime.Today && HolidayService.IsWorkday(dt))
                    {
                        var pRect = new Rectangle(rect.X + Dpi.S(4), rect.Bottom - Dpi.S(23), rect.Width - Dpi.S(8), Dpi.S(18));
                        using (var pPath = UiHelper.GetRoundedPath(pRect, 3))
                        using (var pb = new SolidBrush(ColorTranslator.FromHtml("#FEE2E2")))
                        using (var pf = new SolidBrush(ColorTranslator.FromHtml("#DC2626")))
                        using (var pFont = UiHelper.DrawingFont(this, 8.5f, FontStyle.Bold))
                        {
                            g.FillPath(pb, pPath);
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("缺卡", pFont, pf, pRect, sf);
                        }
                    }
                }

                if (Focused && isSelected)
                {
                    var focusRect = rect;
                    focusRect.Inflate(-Dpi.S(3), -Dpi.S(3));
                    ControlPaint.DrawFocusRectangle(g, focusRect, ColorTranslator.FromHtml("#1D4ED8"), bg);
                }
            }
        }
    }

    // Modal Dialog for Editing / Backfilling Attendance Record
    public class EditRecordDialog : DpiAwareForm
    {
        private readonly DpiMetrics Dpi;
        private ExcelStorage store;
        public DateTime TargetDate { get; private set; }

        private TextBox txtStart;
        private TextBox txtFinish;
        private ComboBox cboKind;
        private TextBox txtNote;

        private Button btnClear;
        private Button btnSave;
        private Button btnCancel;
        private Label lblStartError;
        private Label lblFinishError;

        public EditRecordDialog(ExcelStorage storage, DateTime targetDate)
        {
            Dpi = new DpiMetrics(this);
            this.store = storage;
            this.TargetDate = targetDate.Date;
            this.AutoScaleMode = AutoScaleMode.None;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = TargetDate == DateTime.Today ? "编辑今日打卡" : (TargetDate < DateTime.Today ? "补录打卡" : "录入打卡");
            this.Font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular);
            this.BackColor = Color.White;
            this.ForeColor = ColorTranslator.FromHtml("#1E293B");
            this.ClientSize = Dpi.S(380, 334);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            // Date Header
            string tag = HolidayService.GetDayTag(TargetDate);
            bool isWork = HolidayService.IsWorkday(TargetDate);
            string dayDesc = isWork ? (tag == "班" ? "调休工作日" : "正常工作日") : (string.IsNullOrEmpty(tag) ? "周末休息" : "法定节假日 (" + tag + ")");

            var lblHeaderDate = new Label
            {
                Text = string.Format("{0:yyyy年MM月dd日} {1}  ({2})", TargetDate, ExcelStorage.GetChineseWeekdayShort(TargetDate), dayDesc),
                Font = UiHelper.DrawingFont(this, 11f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = Dpi.P(24, 16),
                Size = Dpi.S(332, 48),
                AutoSize = false
            };
            this.Controls.Add(lblHeaderDate);

            int formY = 76;

            // Start Time
            var lblStart = new Label { Text = "上班:", Location = Dpi.P(24, formY + 4), Size = Dpi.S(50, 24) };
            txtStart = new TextBox { Location = Dpi.P(80, formY), Size = Dpi.S(276, 26), Font = UiHelper.DrawingFont(this, 10.5f, FontStyle.Regular), TextAlign = HorizontalAlignment.Center };
            lblStartError = new Label { Location = Dpi.P(80, formY + 27), Size = Dpi.S(276, 20), ForeColor = ColorTranslator.FromHtml("#DC2626"), Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular), Visible = false, AccessibleRole = AccessibleRole.Alert };
            txtStart.TextChanged += (s, e) => lblStartError.Visible = false;
            this.Controls.AddRange(new Control[] { lblStart, txtStart, lblStartError });

            formY += 52;

            // Finish Time
            var lblFinish = new Label { Text = "下班:", Location = Dpi.P(24, formY + 4), Size = Dpi.S(50, 24) };
            txtFinish = new TextBox { Location = Dpi.P(80, formY), Size = Dpi.S(276, 26), Font = UiHelper.DrawingFont(this, 10.5f, FontStyle.Regular), TextAlign = HorizontalAlignment.Center };
            lblFinishError = new Label { Location = Dpi.P(80, formY + 27), Size = Dpi.S(276, 20), ForeColor = ColorTranslator.FromHtml("#DC2626"), Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular), Visible = false, AccessibleRole = AccessibleRole.Alert };
            txtFinish.TextChanged += (s, e) => lblFinishError.Visible = false;
            this.Controls.AddRange(new Control[] { lblFinish, txtFinish, lblFinishError });

            formY += 56;

            // Attendance Type
            var lblKind = new Label { Text = "类型:", Location = Dpi.P(24, formY + 4), Size = Dpi.S(50, 24) };
            cboKind = new ComboBox { Location = Dpi.P(80, formY), Size = Dpi.S(276, 26), DropDownStyle = ComboBoxStyle.DropDownList, Font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular) };
            cboKind.Items.AddRange(Array.ConvertAll(ExcelStorage.DAY_KINDS, item => (object)item));
            cboKind.SelectedIndex = 0;
            this.Controls.AddRange(new Control[] { lblKind, cboKind });

            formY += 44;

            // Note
            var lblNote = new Label { Text = "备注:", Location = Dpi.P(24, formY + 4), Size = Dpi.S(50, 24) };
            txtNote = new TextBox { Location = Dpi.P(80, formY), Size = Dpi.S(276, 26), Font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular) };
            this.Controls.AddRange(new Control[] { lblNote, txtNote });

            formY += 52;

            // Bottom Buttons
            btnClear = UiHelper.CreateButton(this, "删除当天记录", Dpi.P(24, formY), Dpi.S(120, 34));
            btnClear.ForeColor = ColorTranslator.FromHtml("#DC2626");
            btnClear.BackColor = ColorTranslator.FromHtml("#FEF2F2");
            btnClear.Click += (s, e) =>
            {
                string details = string.Format(
                    "确定删除 {0:yyyy年MM月dd日} 的整条记录吗？\n上班时间、下班时间、出勤类型和备注都会被删除。",
                    TargetDate);
                if (MessageBox.Show(this, details, "删除当天记录", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    store.DeleteDay(TargetDate);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            btnSave = UiHelper.CreateButton(this, "保存", Dpi.P(156, formY), Dpi.S(96, 34), true);
            btnSave.Click += (s, e) => SaveAndClose();

            btnCancel = UiHelper.CreateButton(this, "取消", Dpi.P(264, formY), Dpi.S(92, 34));
            btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.AddRange(new Control[] { btnClear, btnSave, btnCancel });
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
            this.Shown += (s, e) =>
            {
                TextBox timeBox = txtStart;
                timeBox.Focus();
                timeBox.SelectAll();
            };
        }

        private void LoadData()
        {
            bool hasRecord = store.Records.TryGetValue(TargetDate, out var ent);
            if (ent == null) ent = new Entry();
            btnClear.Enabled = hasRecord;
            txtStart.Text = ent.StartClock;
            txtFinish.Text = ent.FinishClock;
            string kind = string.IsNullOrEmpty(ent.Kind) ? "自动" : ent.Kind;
            if (!cboKind.Items.Contains(kind)) cboKind.Items.Add(kind);
            cboKind.SelectedItem = kind;
            if (cboKind.SelectedIndex < 0) cboKind.SelectedIndex = 0;
            txtNote.Text = ent.Note ?? "";
        }

        private static TimeSpan? ParseInputTime(string raw)
        {
            string value = (raw ?? "").Trim().Replace('：', ':');
            if (Regex.IsMatch(value, @"^[0-9]{4}$"))
                value = value.Insert(2, ":");
            if (!Regex.IsMatch(value, @"^[0-9]{1,2}:[0-9]{1,2}$")) return null;
            return Entry.ParseTime(value);
        }
        private void SaveAndClose()
        {
            var st = ParseInputTime(txtStart.Text);
            var fn = ParseInputTime(txtFinish.Text);

            if (!string.IsNullOrWhiteSpace(txtStart.Text) && !st.HasValue)
            {
                lblStartError.Text = "请输入有效时间，如 09:00、9:00 或 0900。";
                lblStartError.Visible = true;
                txtStart.Focus();
                txtStart.SelectAll();
                return;
            }
            if (!string.IsNullOrWhiteSpace(txtFinish.Text) && !fn.HasValue)
            {
                lblFinishError.Text = "请输入有效时间，如 18:00、18:0 或 1800。";
                lblFinishError.Visible = true;
                txtFinish.Focus();
                txtFinish.SelectAll();
                return;
            }

            string kind = cboKind.SelectedItem != null ? cboKind.SelectedItem.ToString() : "自动";
            string note = txtNote.Text.Trim();

            try
            {
                store.SaveDay(TargetDate, st, fn, kind, note);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Settings Dialog with Integrated Excel & Backup Quick Links
    public class SettingsForm : DpiAwareForm
    {
        private readonly DpiMetrics Dpi;
        public double DailyTarget { get; private set; }
        public string StandardStart { get; private set; }
        public string StandardFinish { get; private set; }

        private ExcelStorage store;
        private TextBox txtTarget;
        private TextBox txtStdStart;
        private TextBox txtStdFinish;
        private Button btnOpenExcel;
        private Button btnOpenFolder;
        private Button btnBackupFolder;
        private Label lblStorageTip;
        private ToolTip settingsToolTip;
        private Button btnOk;
        private Button btnCancel;

        public SettingsForm(ExcelStorage storage)
        {
            Dpi = new DpiMetrics(this);
            this.store = storage;
            this.DailyTarget = storage.DailyTarget;
            this.StandardStart = string.IsNullOrEmpty(storage.StandardStart) ? "09:00" : storage.StandardStart;
            this.StandardFinish = string.IsNullOrEmpty(storage.StandardFinish) ? "18:00" : storage.StandardFinish;
            this.AutoScaleMode = AutoScaleMode.None;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "设置";
            this.Font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular);
            this.BackColor = Color.White;
            this.ForeColor = ColorTranslator.FromHtml("#1E293B");
            this.ClientSize = Dpi.S(360, 410);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            var lblRulesSection = new Label
            {
                Text = "考勤规则",
                Location = Dpi.P(24, 12),
                AutoSize = true,
                Font = UiHelper.DrawingFont(this, 10f, FontStyle.Bold)
            };
            var lbl1 = new Label { Text = "每日目标工时 (h):", Location = Dpi.P(24, 42), Size = Dpi.S(165, 24), TextAlign = ContentAlignment.MiddleLeft };
            txtTarget = new TextBox { Text = DailyTarget.ToString(CultureInfo.InvariantCulture), Location = Dpi.P(200, 40), Size = Dpi.S(125, 24), TextAlign = HorizontalAlignment.Center };
            var lblTargetHelp = new Label
            {
                Text = "用于计算本月目标工时和完成比例。",
                Location = Dpi.P(24, 66),
                Size = Dpi.S(310, 20),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular)
            };

            var lbl2 = new Label { Text = "标准上班时间:", Location = Dpi.P(24, 94), Size = Dpi.S(165, 24), TextAlign = ContentAlignment.MiddleLeft };
            txtStdStart = new TextBox { Text = StandardStart, Location = Dpi.P(200, 94), Size = Dpi.S(125, 24), TextAlign = HorizontalAlignment.Center };

            var lbl3 = new Label { Text = "标准下班时间:", Location = Dpi.P(24, 128), Size = Dpi.S(165, 24), TextAlign = ContentAlignment.MiddleLeft };
            txtStdFinish = new TextBox { Text = StandardFinish, Location = Dpi.P(200, 128), Size = Dpi.S(125, 24), TextAlign = HorizontalAlignment.Center };
            var lblStandardTimeHelp = new Label
            {
                Text = "标准时间仅保存设置，不会自动打卡或修改记录。",
                Location = Dpi.P(24, 154),
                Size = Dpi.S(310, 38),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular)
            };
            var separator = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = Dpi.P(24, 197),
                Size = Dpi.S(310, 2)
            };
            var lblDataSection = new Label
            {
                Text = "数据管理",
                Location = Dpi.P(24, 205),
                AutoSize = true,
                Font = UiHelper.DrawingFont(this, 10f, FontStyle.Bold)
            };

            // Quick utility shortcuts with generous widths
            btnOpenExcel = UiHelper.CreateButton(this, "打开 Excel 表格", Dpi.P(24, 232), Dpi.S(145, 34));
            btnOpenExcel.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(store.BookPath)) Process.Start(new ProcessStartInfo(store.BookPath) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show("无法打开文件: " + ex.Message); }
            };

            btnOpenFolder = UiHelper.CreateButton(this, "打开数据文件夹", Dpi.P(180, 232), Dpi.S(145, 34));
            btnOpenFolder.Click += (s, e) =>
            {
                try
                {
                    if (!Directory.Exists(store.FolderPath)) Directory.CreateDirectory(store.FolderPath);
                    Process.Start(new ProcessStartInfo("explorer.exe", store.FolderPath) { UseShellExecute = true });
                }
                catch { }
            };

            btnBackupFolder = UiHelper.CreateButton(this, "打开备份文件夹", Dpi.P(24, 274), Dpi.S(145, 34));
            btnBackupFolder.Click += (s, e) =>
            {
                try
                {
                    string bDir = Path.Combine(store.FolderPath, "backups");
                    if (!Directory.Exists(bDir)) Directory.CreateDirectory(bDir);
                    Process.Start(new ProcessStartInfo("explorer.exe", bDir) { UseShellExecute = true });
                }
                catch { }
            };

            lblStorageTip = new Label
            {
                Text = "数据位置：" + store.FolderPath,
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Location = Dpi.P(24, 314),
                Size = Dpi.S(310, 30),
                AutoEllipsis = true,
                AccessibleName = "数据保存位置",
                AccessibleDescription = store.FolderPath
            };
            settingsToolTip = new ToolTip();
            settingsToolTip.SetToolTip(lblStorageTip, store.FolderPath);

            btnOk = UiHelper.CreateButton(this, "保存", Dpi.P(140, 360), Dpi.S(95, 34), true);
            btnOk.Click += (s, e) =>
            {
                if (!double.TryParse(txtTarget.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out double dt) ||
                    double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0 || dt > 24)
                {
                    MessageBox.Show(this, "每日目标工时需大于 0 且不超过 24 小时。", "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtTarget.Focus();
                    txtTarget.SelectAll();
                    return;
                }

                string startText = txtStdStart.Text.Trim().Replace('：', ':');
                if (!DateTime.TryParseExact(startText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    MessageBox.Show(this, "标准上班时间格式应为 HH:mm，例如 09:00。", "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtStdStart.Focus();
                    txtStdStart.SelectAll();
                    return;
                }

                string finishText = txtStdFinish.Text.Trim().Replace('：', ':');
                if (!DateTime.TryParseExact(finishText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    MessageBox.Show(this, "标准下班时间格式应为 HH:mm，例如 18:00。", "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtStdFinish.Focus();
                    txtStdFinish.SelectAll();
                    return;
                }

                DailyTarget = dt;
                StandardStart = startText;
                StandardFinish = finishText;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = UiHelper.CreateButton(this, "取消", Dpi.P(245, 360), Dpi.S(85, 34));
            btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.AddRange(new Control[] { lblRulesSection, lbl1, txtTarget, lblTargetHelp, lbl2, txtStdStart, lbl3, txtStdFinish, lblStandardTimeHelp, separator, lblDataSection, btnOpenExcel, btnOpenFolder, btnBackupFolder, lblStorageTip, btnOk, btnCancel });
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }

    // Main Form: Unified Single-Page "打卡器"
    public class MainForm : DpiAwareForm
    {
        private readonly DpiMetrics Dpi;
        private TableLayoutPanel mainLayout;
        private Panel calendarPanel;
        private Panel overviewPanel;
        private bool arrangingLayout;

        private ExcelStorage store;

        // Top Header
        private GearButton btnSettings;
        private ToolTip toolTipTop;

        // Left Area: Month Calendar
        private Button btnPrevMonth;
        private Label lblCalendarTitle;
        private Button btnNextMonth;
        private Button btnJumpToday;
        private AttendanceCalendar calControl;

        // Right Area: Today Punch
        private Label lblTodayHeader;
        private Label lblLiveStatusBadge;
        private TodayPunchControl todayPunchControl;

        // Right Area: Month Stats
        private Label lblMonthTitle;
        private Label lblWorkdayTag;
        private ModernProgressBar prgMonth;
        private Label lblPrgText;
        private Label lblProgressTitle;
        private MonthKpiControl kpiMonthControl;

        private Timer timerTicker;
        private Timer dayChangeTimer;
        private DateTime displayedDay = DateTime.Today;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;
        private bool isExplicitExit = false;
        private bool shownTrayBalloonOnce = false;

        public MainForm(ExcelStorage storage)
        {
            Dpi = new DpiMetrics(this);
            store = storage;
            this.AutoScaleMode = AutoScaleMode.None;

            InitializeComponent();
            UpdateCalendarHeader();
            calControl.BindStorage(store);
            RefreshTodayPunch();
            RefreshMonthOverview();

            timerTicker = new Timer { Interval = 1000 };
            timerTicker.Tick += (s, e) => OnSecondTick();
            timerTicker.Start();
            dayChangeTimer = new Timer { Interval = 10 * 60 * 1000 };
            dayChangeTimer.Tick += (s, e) => RefreshDayIfChanged();
            dayChangeTimer.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "下班了";
            this.Font = UiHelper.DrawingFont(this, 9.5f, FontStyle.Regular);
            this.BackColor = Color.White;
            this.ForeColor = ColorTranslator.FromHtml("#1E293B");

            // Default content size, with room for both columns at the minimum window size.
            this.ClientSize = Dpi.S(900, 416);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = SizeFromClientSize(Dpi.S(900, 416));
            this.StartPosition = FormStartPosition.CenterScreen;

            toolTipTop = new ToolTip();

            int leftX = 20;
            int rightX = 535;
            int rightWidth = 345;
            int topRowY = 16;
            int headerH = 28;

            // ================= Left Column Header: Month Navigation =================
            btnPrevMonth = UiHelper.CreateButton(this, "‹", Dpi.P(leftX, topRowY), Dpi.S(32, headerH));
            btnPrevMonth.Click += (s, e) => calControl.PrevMonth();

            lblCalendarTitle = new Label
            {
                Text = "2026 年 10 月",
                Font = UiHelper.DrawingFont(this, 12f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = Dpi.P(leftX + 38, topRowY),
                Size = Dpi.S(200, headerH),
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnNextMonth = UiHelper.CreateButton(this, "›", Dpi.P(leftX + 242, topRowY), Dpi.S(32, headerH));
            btnNextMonth.Click += (s, e) => calControl.NextMonth();

            btnJumpToday = UiHelper.CreateButton(this, "今天", Dpi.P(leftX + 422, topRowY), Dpi.S(70, headerH));
            foreach (var button in new[] { btnPrevMonth, btnNextMonth, btnJumpToday })
            {
                button.BackColor = Color.White;
                button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#F1F5F9");
                button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#E2E8F0");
            }
            btnJumpToday.Click += (s, e) => { calControl.SelectDate(DateTime.Today, false); UpdateCalendarHeader(); RefreshMonthOverview(); };

            this.Controls.AddRange(new Control[] { btnPrevMonth, lblCalendarTitle, btnNextMonth, btnJumpToday });

            // Calendar grid starts at Y = 52, height = 424 -> bottom ends at 476
            calControl = new AttendanceCalendar
            {
                Location = Dpi.P(leftX, 52),
                Size = Dpi.S(492, 424),
                AccessibleDescription = "使用方向键移动日期，回车补录或编辑所选日期。"
            };
            calControl.DateSelected += (dt) => OpenEditDialog(dt);
            calControl.MonthChanged += (dt) =>
            {
                UpdateCalendarHeader();
                RefreshMonthOverview();
            };
            this.Controls.Add(calControl);

            // ================= Right Column Header: Title, Live Pill Badge, [补], [⚙] =================
            lblTodayHeader = new Label
            {
                Text = "今日打卡",
                Font = UiHelper.DrawingFont(this, 12f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = Dpi.P(rightX, topRowY),
                Size = Dpi.S(90, headerH),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // In-progress live pill badge in header row (Zero vertical wasted space!)
            lblLiveStatusBadge = new Label
            {
                Text = "",
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2563EB"),
                BackColor = ColorTranslator.FromHtml("#EFF6FF"),
                Location = Dpi.P(rightX + 96, topRowY + 2),
                Size = Dpi.S(165, headerH - 4),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            btnSettings = new GearButton
            {
                Location = Dpi.P(rightX + rightWidth - 34, topRowY),
                Size = Dpi.S(34, headerH)
            };
            btnSettings.Click += (s, e) => ShowSettings();
            toolTipTop.SetToolTip(btnSettings, "设置");

            this.Controls.AddRange(new Control[] { lblTodayHeader, lblLiveStatusBadge, btnSettings });

            // ================= Right Column: Today Punch Cards =================
            // Starts at Y = 52, height = 100 -> bottom ends at 152
            todayPunchControl = new TodayPunchControl
            {
                Location = Dpi.P(rightX, 52),
                Size = Dpi.S(rightWidth, 100)
            };
            todayPunchControl.StartClicked += (s, e) => QuickPunch(true, true);
            todayPunchControl.FinishClicked += (s, e) => QuickPunch(false, true);
            todayPunchControl.TimeEdited += SaveInlinePunch;
            this.Controls.Add(todayPunchControl);

            // ================= Right Column: Month Stats =================
            int statHeaderY = 166;
            lblMonthTitle = new Label
            {
                Text = "本月统计",
                Font = UiHelper.DrawingFont(this, 12f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = Dpi.P(rightX, statHeaderY),
                Size = Dpi.S(140, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblWorkdayTag = new Label
            {
                Text = "工作日 18 天",
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1D4ED8"),
                BackColor = Color.Transparent,
                Location = Dpi.P(rightX + rightWidth - 140, statHeaderY),
                Size = Dpi.S(140, 24),
                TextAlign = ContentAlignment.MiddleRight,
                AccessibleName = "本月工作日"
            };
            this.Controls.AddRange(new Control[] { lblMonthTitle, lblWorkdayTag });

            int prgY = 196;
            prgMonth = new ModernProgressBar
            {
                Location = Dpi.P(rightX, prgY + 3),
                Size = Dpi.S(rightWidth - 95, 8),
                Percentage = 0.0
            };
            lblPrgText = new Label
            {
                Text = "月目标 0%",
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                Location = Dpi.P(rightX + rightWidth - 90, prgY - 2),
                Size = Dpi.S(90, 18),
                TextAlign = ContentAlignment.MiddleRight,
                AccessibleName = "本月工时目标完成比例"
            };
            toolTipTop.SetToolTip(prgMonth, "已记录工时 ÷ 本月全部工作日的目标工时；达到 100% 表示完成本月目标。超出目标时百分比会继续增加。");
            toolTipTop.SetToolTip(lblPrgText, "月目标完成比例 = 已记录工时 ÷ 本月目标工时。");
            lblProgressTitle = new Label
            {
                Text = "月目标完成度",
                Font = UiHelper.DrawingFont(this, 8.5f, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.AddRange(new Control[] { prgMonth, lblProgressTitle, lblPrgText });

            // KPI Dashboard: starts at Y = 220, height = 256 -> bottom ends at EXACTLY 476!
            kpiMonthControl = new MonthKpiControl
            {
                Location = Dpi.P(rightX, 220),
                Size = Dpi.S(rightWidth, 256)
            };
            this.Controls.Add(kpiMonthControl);
            InitializeLayoutContainers();
            InitTrayIcon();
            LayoutMainContent();
        }

        private void InitializeLayoutContainers()
        {
            mainLayout = new TableLayoutPanel
            {
                Margin = Padding.Empty, Padding = Padding.Empty,
                ColumnCount = 3, RowCount = 1, BackColor = Color.White,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58.78f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi.S(20)));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41.22f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            calendarPanel = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            overviewPanel = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            calendarPanel.Controls.AddRange(new Control[] { btnPrevMonth, lblCalendarTitle,
                btnNextMonth, btnJumpToday, calControl });
            overviewPanel.Controls.AddRange(new Control[] { lblTodayHeader, lblLiveStatusBadge,
                btnSettings, todayPunchControl, lblMonthTitle, lblWorkdayTag,
                prgMonth, lblProgressTitle, lblPrgText, kpiMonthControl });
            mainLayout.Controls.Add(calendarPanel, 0, 0);
            mainLayout.Controls.Add(overviewPanel, 2, 0);
            Controls.Add(mainLayout);
            calendarPanel.SizeChanged += (s, e) => LayoutMainContent();
            overviewPanel.SizeChanged += (s, e) => LayoutMainContent();
        }

        private void LayoutMainContent()
        {
            if (arrangingLayout || Dpi == null || mainLayout == null ||
                WindowState == FormWindowState.Minimized) return;
            arrangingLayout = true;
            SuspendLayout();
            calendarPanel.SuspendLayout();
            overviewPanel.SuspendLayout();
            try
            {
                bool stacked = ClientSize.Width < Dpi.S(900);
                int virtualWidth = Math.Max(ClientSize.Width, Dpi.S(stacked ? 500 : 900));
                int contentWidth = Math.Min(virtualWidth, Dpi.S(1100));
                int calendarHeight = Dpi.S(76 + calControl.VisibleWeekCount * 58);
                int overviewHeight = Dpi.S(380);
                int desiredHeight = stacked ? calendarHeight + overviewHeight + Dpi.S(36) :
                    Math.Max(Dpi.S(416), Dpi.S(62 + calControl.VisibleWeekCount * 62));
                int contentHeight = desiredHeight;
                AutoScrollMinSize = new Size(Dpi.S(stacked ? 500 : 900), desiredHeight);
                int margin = Dpi.S(20);
                mainLayout.SuspendLayout();
                if (stacked)
                {
                    mainLayout.RowCount = 2;
                    mainLayout.RowStyles.Clear();
                    mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, calendarHeight));
                    mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, overviewHeight));
                    mainLayout.ColumnStyles[0].Width = 100;
                    mainLayout.ColumnStyles[1].Width = 0;
                    mainLayout.ColumnStyles[2].Width = 0;
                    mainLayout.SetCellPosition(overviewPanel, new TableLayoutPanelCellPosition(0, 1));
                }
                else
                {
                    mainLayout.SetCellPosition(overviewPanel, new TableLayoutPanelCellPosition(2, 0));
                    mainLayout.RowCount = 1;
                    mainLayout.RowStyles.Clear();
                    mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    mainLayout.ColumnStyles[0].Width = 58.78f;
                    mainLayout.ColumnStyles[1].Width = Dpi.S(20);
                    mainLayout.ColumnStyles[2].Width = 41.22f;
                }
                mainLayout.SetBounds(Math.Max(0, (ClientSize.Width - contentWidth) / 2) + margin + AutoScrollPosition.X,
                    Dpi.S(16) + AutoScrollPosition.Y,
                    Math.Max(1, contentWidth - margin * 2),
                    Math.Max(1, contentHeight - Dpi.S(36)));
                mainLayout.ResumeLayout(true);                mainLayout.PerformLayout();
                int leftWidth = calendarPanel.ClientSize.Width;
                int rightWidth = overviewPanel.ClientSize.Width;

                btnPrevMonth.SetBounds(0, 0, Dpi.S(32), Dpi.S(28));
                int monthTitleWidth = TextRenderer.MeasureText(lblCalendarTitle.Text, lblCalendarTitle.Font).Width + Dpi.S(8);
                lblCalendarTitle.SetBounds(Dpi.S(40), 0, monthTitleWidth, Dpi.S(28));
                btnNextMonth.SetBounds(Dpi.S(48) + monthTitleWidth, 0, Dpi.S(32), Dpi.S(28));
                btnJumpToday.SetBounds(leftWidth - Dpi.S(70), 0, Dpi.S(70), Dpi.S(28));
                calControl.SetBounds(0, Dpi.S(36), leftWidth,
                    Math.Max(1, calendarPanel.ClientSize.Height - Dpi.S(stacked ? 50 : 36)));

                lblTodayHeader.SetBounds(0, 0, Dpi.S(90), Dpi.S(28));
                int badgeWidth = Math.Max(1, rightWidth - Dpi.S(142));
                lblLiveStatusBadge.SetBounds(Dpi.S(96), Dpi.S(2), badgeWidth, Dpi.S(24));
                btnSettings.SetBounds(rightWidth - Dpi.S(34), 0, Dpi.S(34), Dpi.S(28));
                todayPunchControl.SetBounds(0, Dpi.S(36), rightWidth, Dpi.S(76));

                lblMonthTitle.SetBounds(0, Dpi.S(122), Dpi.S(140), Dpi.S(24));
                lblWorkdayTag.SetBounds(rightWidth - Dpi.S(140), Dpi.S(122), Dpi.S(140), Dpi.S(24));
                prgMonth.SetBounds(0, Dpi.S(152), rightWidth, Dpi.S(8));
                lblProgressTitle.SetBounds(0, Dpi.S(168), rightWidth - Dpi.S(100), Dpi.S(18));
                lblPrgText.SetBounds(rightWidth - Dpi.S(90), Dpi.S(168), Dpi.S(90), Dpi.S(18));
                kpiMonthControl.SetBounds(0, Dpi.S(194), rightWidth, overviewPanel.ClientSize.Height - Dpi.S(194));
            }
            finally
            {
                calendarPanel.ResumeLayout(false);
                overviewPanel.ResumeLayout(false);
                ResumeLayout(false);
                arrangingLayout = false;
            }
            todayPunchControl.RepositionEditor();
            mainLayout.Invalidate(true);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            SetContentMinimum(Dpi.S(900, Math.Max(416, 62 + calControl.VisibleWeekCount * 62)));
            LayoutMainContent();
        }
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            LayoutMainContent();
        }
        private void UpdateCalendarHeader()
        {
            lblCalendarTitle.Text = string.Format("{0} 年 {1} 月", calControl.DisplayMonth.Year, calControl.DisplayMonth.Month);
            SetContentMinimum(Dpi.S(900, Math.Max(416, 62 + calControl.VisibleWeekCount * 62)));
            LayoutMainContent();
        }

        private void RefreshTodayPunch()
        {
            store.Records.TryGetValue(DateTime.Today, out var entry);
            if (entry == null) entry = new Entry();

            todayPunchControl.UpdatePunch(entry.StartClock, entry.Start.HasValue, entry.FinishClock, entry.Finish.HasValue);

            if (entry.Start.HasValue && !entry.Finish.HasValue)
            {
                var now = DateTime.Now.TimeOfDay;
                int elapsed = (int)Math.Max(0, Math.Floor((now - entry.Start.Value).TotalMinutes));
                int h = elapsed / 60;
                int m = elapsed % 60;
                lblLiveStatusBadge.Text = string.Format("⏱️ 在岗 {0}h{1:D2}m", h, m);
                lblLiveStatusBadge.Visible = true;
            }
            else
            {
                lblLiveStatusBadge.Visible = false;
            }
        }

        private void QuickPunch(bool isStart, bool inlineConfirmed = false)
        {
            if (RefreshDayIfChanged()) return;
            var nowTime = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0);
            store.Records.TryGetValue(DateTime.Today, out var entry);
            TimeSpan? existingTime = isStart ? (entry != null ? entry.Start : null) : (entry != null ? entry.Finish : null);
            if (existingTime.HasValue && !inlineConfirmed)
            {
                string actionName = isStart ? "上班" : "下班";
                string message = string.Format("今天已有{0}打卡时间 {1}。\n要用当前时间 {2} 覆盖吗？", actionName,
                    existingTime.Value.ToString(@"hh\:mm"), nowTime.ToString(@"hh\:mm"));
                DialogResult result = this.Visible
                    ? MessageBox.Show(this, message, "确认修改打卡时间", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                    : MessageBox.Show(message, "确认修改打卡时间", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }

            TimeSpan? st = isStart ? nowTime : (entry != null ? entry.Start : null);
            TimeSpan? fn = !isStart ? nowTime : (entry != null ? entry.Finish : null);
            string kind = entry != null ? entry.Kind : "自动";
            string note = entry != null ? entry.Note : "";

            try
            {
                store.SaveDay(DateTime.Today, st, fn, kind, note);
                RefreshTodayPunch();
                RefreshMonthOverview();
                calControl.Invalidate();

                if (!this.Visible && notifyIcon != null)
                {
                    string action = isStart ? "上班打卡" : "下班打卡";
                    notifyIcon.ShowBalloonTip(1500, action + "成功", string.Format("时间: {0:D2}:{1:D2}", nowTime.Hours, nowTime.Minutes), ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                if (!this.Visible && notifyIcon != null)
                {
                    notifyIcon.ShowBalloonTip(2000, "打卡失败", ex.Message, ToolTipIcon.Warning);
                }
                else
                {
                    MessageBox.Show("保存打卡失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SaveInlinePunch(bool start, TimeSpan? time)
        {
            if (RefreshDayIfChanged()) return;
            store.Records.TryGetValue(DateTime.Today, out var entry);
            if (entry == null) entry = new Entry();
            try
            {
                store.SaveDay(DateTime.Today, start ? time : entry.Start, start ? entry.Finish : time, entry.Kind, entry.Note);
                RefreshTodayPunch();
                RefreshMonthOverview();
                calControl.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存时间失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void OpenEditDialog(DateTime dt)
        {
            using (var dlg = new EditRecordDialog(store, dt))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshTodayPunch();
                    RefreshMonthOverview();
                    calControl.Invalidate();
                }
            }
        }

        private void RefreshMonthOverview()
        {
            int year = calControl.DisplayMonth.Year;
            int month = calControl.DisplayMonth.Month;
            lblMonthTitle.Text = year == DateTime.Today.Year && month == DateTime.Today.Month
                ? "本月统计"
                : string.Format("{0}年{1}月统计", year, month);
            var sum = store.Summary(year, month);
            lblWorkdayTag.Text = string.Format("工作日 {0} 天", sum.TotalWorkdays);

            double actualH = Math.Round(sum.ActualMinutes / 60.0, 1);
            double targetH = Math.Round(sum.TargetMinutes / 60.0, 1);
            double remainH = Math.Round(sum.RemainingMinutes / 60.0, 1);

            int actualDays = 0;
            foreach (var kv in store.Records)
            {
                if (kv.Key.Year == year && kv.Key.Month == month && (kv.Value.WorkedMinutes ?? 0) > 0)
                {
                    actualDays++;
                }
            }

            double pct = targetH > 0 ? (actualH / targetH) * 100.0 : 0.0;
            prgMonth.Percentage = pct;
            lblPrgText.Text = string.Format("月目标 {0:F0}%", pct);
            double excessHours = Math.Max(0.0, (sum.ActualMinutes - sum.TargetMinutes) / 60.0);
            lblProgressTitle.Text = excessHours > 0 ? string.Format("超额 {0:F1}h", excessHours) : "月目标完成度";
            lblProgressTitle.ForeColor = ColorTranslator.FromHtml(excessHours > 0 ? "#B45309" : "#64748B");
            lblPrgText.ForeColor = ColorTranslator.FromHtml(pct >= 100.0 ? "#15803D" : "#64748B");
            prgMonth.Invalidate();

            kpiMonthControl.UpdateStats(
                actualHours: actualH,
                actualDays: actualDays,
                targetHours: targetH,
                remainHours: remainH,
                remainDays: sum.RemainingWorkdays,
                dailyReqHours: sum.DailyAverageRequiredHours,
                standardDailyHours: store.DailyTarget,
                deltaMinutes: sum.DeltaMinutes
            );
        }

        private bool RefreshDayIfChanged()
        {
            DateTime today = DateTime.Today;
            if (today == displayedDay) return false;
            bool followToday = calControl.SelectedDate == displayedDay;
            displayedDay = today;
            todayPunchControl.ResetDayInteraction();
            if (followToday) calControl.SelectDate(today, false);
            UpdateCalendarHeader();
            RefreshTodayPunch();
            RefreshMonthOverview();
            calControl.Invalidate();
            return true;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (todayPunchControl != null && calControl != null) RefreshDayIfChanged();
        }

        private void OnSecondTick()
        {
            store.Records.TryGetValue(DateTime.Today, out var entry);
            if (entry != null && entry.Start.HasValue && !entry.Finish.HasValue)
            {
                var now = DateTime.Now.TimeOfDay;
                int elapsed = (int)Math.Max(0, Math.Floor((now - entry.Start.Value).TotalMinutes));
                int h = elapsed / 60;
                int m = elapsed % 60;
                lblLiveStatusBadge.Text = string.Format("⏱️ 在岗 {0}h{1:D2}m", h, m);
                if (!lblLiveStatusBadge.Visible) lblLiveStatusBadge.Visible = true;
            }
            else
            {
                if (lblLiveStatusBadge.Visible) lblLiveStatusBadge.Visible = false;
            }
        }

        private void ShowSettings()
        {
            using (var dlg = new SettingsForm(store))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    store.SaveConfig(dlg.DailyTarget, dlg.StandardStart, dlg.StandardFinish);
                    RefreshTodayPunch();
                    RefreshMonthOverview();
                }
            }
        }

        private void InitTrayIcon()
        {
            Icon appIcon = null;
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    appIcon = new Icon(icoPath);
                }
            }
            catch { }

            if (appIcon == null)
            {
                try
                {
                    appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }
            }

            if (appIcon == null)
            {
                appIcon = SystemIcons.Application;
            }
            this.Icon = appIcon;

            trayMenu = new ContextMenuStrip();
            trayMenu.Font = UiHelper.DrawingFont(this, 9f, FontStyle.Regular);

            var itemShow = new ToolStripMenuItem("打开程序");
            itemShow.Font = UiHelper.DrawingFont(this, 9f, FontStyle.Bold);
            itemShow.Click += (s, e) => RestoreFromTray();

            var itemPunchStart = new ToolStripMenuItem("上班打卡");
            itemPunchStart.Click += (s, e) => QuickPunch(true);

            var itemPunchFinish = new ToolStripMenuItem("下班打卡");
            itemPunchFinish.Click += (s, e) => QuickPunch(false);

            var itemExit = new ToolStripMenuItem("退出");
            itemExit.Click += (s, e) =>
            {
                isExplicitExit = true;
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
                Application.Exit();
            };

            trayMenu.Items.Add(itemShow);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemPunchStart);
            trayMenu.Items.Add(itemPunchFinish);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(itemExit);

            notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "下班了",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    RestoreFromTray();
                }
            };
            notifyIcon.DoubleClick += (s, e) =>
            {
                RestoreFromTray();
            };
        }

        internal void RestoreFromTray()
        {
            RefreshDayIfChanged();
            if (!this.Visible)
            {
                this.Show();
            }
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();
            this.BringToFront();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!isExplicitExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                if (!shownTrayBalloonOnce)
                {
                    shownTrayBalloonOnce = true;
                    notifyIcon.ShowBalloonTip(2000, "下班了已隐藏到托盘", "程序仍在后台运行，点击托盘图标可重新打开主界面。", ToolTipIcon.Info);
                }
                return;
            }

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutMainContent();
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                if (!shownTrayBalloonOnce)
                {
                    shownTrayBalloonOnce = true;
                    notifyIcon.ShowBalloonTip(2000, "下班了已隐藏到托盘", "程序仍在后台运行，点击托盘图标可重新打开主界面。", ToolTipIcon.Info);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timerTicker != null) timerTicker.Dispose();
                if (dayChangeTimer != null) dayChangeTimer.Dispose();
                if (notifyIcon != null) notifyIcon.Dispose();
                if (trayMenu != null) trayMenu.Dispose();
                if (toolTipTop != null) toolTipTop.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetCompatibleTextRenderingDefault(false);
            string userId = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            string instanceName = @"Local\OffWork." + userId;
            using (var activate = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, instanceName + ".Activate"))
            using (var instance = new System.Threading.Mutex(true, instanceName, out bool firstInstance))
            {
                if (!firstInstance)
                {
                    activate.Set();
                    return;
                }
                System.Threading.RegisteredWaitHandle activationWait = null;
                try
                {
                    Application.EnableVisualStyles();
                    using (var window = new MainForm(new ExcelStorage()))
                    {
                        // Create the UI handle before receiving signals; an early signal stays pending.
                        var windowHandle = window.Handle;
                        activationWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(activate, (state, timedOut) =>
                        {
                            try
                            {
                                window.BeginInvoke(new Action(() =>
                                {
                                    if (!window.IsDisposed) window.RestoreFromTray();
                                }));
                            }
                            catch (InvalidOperationException) { }
                        }, null, System.Threading.Timeout.Infinite, false);
                        Application.Run(window);
                    }
                }
                finally
                {
                    if (activationWait != null) activationWait.Unregister(null);
                    instance.ReleaseMutex();
                }
            }
        }
    }
}
