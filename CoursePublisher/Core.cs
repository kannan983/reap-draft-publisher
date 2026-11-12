using System.Text.RegularExpressions;

namespace CoursePublisher;

public record VideoInfo(string VideoId, string Title, string Description);

public record ExcelRow(string CourseCode, string ChapterName, string YouTubeUrl, string VideoTitle,
                       string VideoDescription, string OrderNo, string LanguageCode)
{
    public string[] Cells => [CourseCode, ChapterName, YouTubeUrl, VideoTitle, VideoDescription, OrderNo, LanguageCode];
}

/// Pure logic ported 1:1 from gen3_youtube_gem.py.
public static class Core
{
    public const string CourseIntroSuffix = " Course Introduction";
    public static readonly string[] ExcelHeaders =
        ["Course Code", "Chapter Name", "YouTube URL", "Video Title", "Video Description", "OrderNo in Chapter", "Language Code"];

    public static readonly (string Name, string Code)[] Languages =
    [
        ("English", "en"), ("Hindi", "hi"), ("Tamil", "ta"), ("Telugu", "te"), ("Kannada", "kn"), ("Malayalam", "ml"),
        ("Bengali", "bn"), ("Gujarati", "gu"), ("Marathi", "mr"), ("Punjabi", "pa"), ("Assamese", "as"), ("Odia", "or"),
    ];

    public static string LanguageCode(string name) => Languages.FirstOrDefault(l => l.Name == name).Code ?? "";

    /// Guess the channel language from an auth folder name like "reap_English" or "REAP Assamees".
    public static string? DetectLanguage(string folderPath)
    {
        var name = folderPath.TrimEnd('/', '\\').Split('/', '\\')[^1].ToLowerInvariant();
        if (name.Contains("assamees")) return "Assamese";
        if (name.Contains("oriya")) return "Odia";
        return Languages.Select(l => l.Name).FirstOrDefault(n => name.Contains(n.ToLowerInvariant()));
    }

    const RegexOptions I = RegexOptions.IgnoreCase;
    static readonly Regex TitleFormat = new(@"^Chapter \d+[A-Z]? - .*", I);
    static readonly Regex ChapterRx = new(@"^Chapter (\d+)([A-Z]?) - .*", I);
    static readonly Regex BaseChapterRx = new(@"^Chapter (\d+) - .*", I);
    static readonly Regex FixRx = new(@"^(.*? \d+[A-Z]?)\s+(.*)");

    // Python int() accepts any Unicode digit; int.Parse does not.
    static int Num(string digits) => digits.Aggregate(0, (a, c) => a * 10 + (int)char.GetNumericValue(c));

    static bool IsIntro(string title) => title.EndsWith(CourseIntroSuffix, StringComparison.OrdinalIgnoreCase);

    public static string SanitizeFilename(string text, string replaceSpaceWith = "_", int maxLength = 150)
    {
        text = Regex.Replace(text.Trim(), @"\s+", " ").Replace(" ", replaceSpaceWith);
        var s = Regex.Replace(text, @"[\\/*?:""<>|]", "");
        return (s.Length > maxLength ? s[..maxLength] : s).Trim();
    }

    /// Tab 2: returns (corrected title, new description, needs correction).
    public static (string Corrected, string Description, bool Needs) CorrectTitle(string original)
    {
        if (IsIntro(original)) return (original, original, false);
        if (TitleFormat.IsMatch(original))
        {
            var parts = original.Split(" - ", 2);
            return (original, parts.Length > 1 ? parts[1].Trim() : "", false);
        }
        var m = FixRx.Match(original);
        return m.Success
            ? ($"{m.Groups[1].Value} - {m.Groups[2].Value}", m.Groups[2].Value.Trim(), true)
            : ($"{original} - ", "", true);
    }

    static (int Group, int Num, string Letter, string Title) SortKey(string videoTitle)
    {
        var title = videoTitle.Trim();
        if (IsIntro(title)) return (0, 0, "", title);
        var b = BaseChapterRx.Match(title);
        var s = ChapterRx.Match(title);
        if (b.Success && !(s.Success && s.Groups[2].Value != "")) return (1, Num(b.Groups[1].Value), "", title);
        if (s.Success) return (1, Num(s.Groups[1].Value), s.Groups[2].Value.ToUpperInvariant(), title);
        return (2, 0, "", title);
    }

    static readonly Regex VideoIdRx = new(@"(?:v=|youtu\.be/|/video/|/shorts/|^)([A-Za-z0-9_-]{11})(?:[^A-Za-z0-9_-]|$)");

    /// Accepts a bare video ID or a youtu.be / watch?v= / Studio / Shorts link.
    public static string? ParseVideoId(string input)
    {
        var m = VideoIdRx.Match(input.Trim());
        return m.Success ? m.Groups[1].Value : null;
    }

    public static string CourseCode(string playlistTitle) => playlistTitle.Split('_')[0];

    /// Tab 3: build the Excel rows for a playlist.
    public static List<ExcelRow> BuildExcelRows(string playlistTitle, IEnumerable<VideoInfo> videos)
    {
        var parts = playlistTitle.Split('_');
        var courseCode = parts[0];
        var langCode = parts.Length > 1 && parts[^1].Length == 2 ? parts[^1] : "N/A";

        var sorted = videos
            .Select(v => (v, k: SortKey(v.Title)))
            .OrderBy(x => x.k.Group).ThenBy(x => x.k.Num)
            .ThenBy(x => x.k.Letter, StringComparer.Ordinal).ThenBy(x => x.k.Title, StringComparer.Ordinal)
            .Select(x => x.v).ToList();

        var baseTitles = new Dictionary<int, string>();
        foreach (var v in sorted)
        {
            var b = BaseChapterRx.Match(v.Title);
            if (!b.Success) continue;
            var s = ChapterRx.Match(v.Title);
            if (s.Success && s.Groups[2].Value == "") baseTitles[Num(b.Groups[1].Value)] = v.Title;
        }

        var rows = new List<ExcelRow>();
        int currentBase = -1, subCount = 1;
        foreach (var v in sorted)
        {
            string chapterName, orderNo;
            var b = BaseChapterRx.Match(v.Title);
            var s = ChapterRx.Match(v.Title);
            if (IsIntro(v.Title))
            {
                chapterName = ""; orderNo = ""; currentBase = -1;
            }
            else if (b.Success && !(s.Success && s.Groups[2].Value != ""))
            {
                chapterName = v.Title; orderNo = "0"; currentBase = Num(b.Groups[1].Value); subCount = 1;
            }
            else if (s.Success)
            {
                var n = Num(s.Groups[1].Value);
                chapterName = baseTitles.GetValueOrDefault(n, v.Title);
                if (n == currentBase) orderNo = (subCount++).ToString();
                else { orderNo = "1"; currentBase = n; subCount = 2; }
            }
            else
            {
                chapterName = v.Title; orderNo = ""; currentBase = -1;
            }
            rows.Add(new ExcelRow(courseCode, chapterName, $"https://youtu.be/{v.VideoId}", v.Title, v.Description, orderNo, langCode));
        }
        return rows;
    }

    public static int SelfTest()
    {
        void Check(bool ok, string what) { if (!ok) throw new Exception("FAIL: " + what); Console.WriteLine("ok   " + what); }
        try
        {
            Check(SanitizeFilename("Installation & Servicing of CCTV Camera, Security Alarm\n& Smoke Detector")
                  == "Installation_&_Servicing_of_CCTV_Camera,_Security_Alarm_&_Smoke_Detector", "sanitize strips newline");
            Check(SanitizeFilename("a:b?c") == "abc", "sanitize strips illegal chars");
            Check(CorrectTitle("Chapter 2A Wiring basics") == ("Chapter 2A - Wiring basics", "Wiring basics", true), "fix missing dash");
            Check(CorrectTitle("Chapter 1 - Intro") == ("Chapter 1 - Intro", "Intro", false), "keep good title");
            Check(CorrectTitle("CCTV Course Introduction").Needs == false, "intro untouched");

            var rows = BuildExcelRows("NARQ30056_en", [
                new("v3", "Chapter 1B - Two", "d"),
                new("v1", "Chapter 1 - Base", "d"),
                new("v0", "CCTV Course Introduction", "d"),
                new("v2", "Chapter 1A - One", "d"),
                new("v4", "Chapter 2 - Next", "d"),
            ]);
            Check(string.Join(",", rows.Select(r => r.YouTubeUrl[^2..])) == "v0,v1,v2,v3,v4", "sort order");
            Check(string.Join(",", rows.Select(r => r.OrderNo)) == ",0,1,2,0", "order numbers");
            Check(rows[2].ChapterName == "Chapter 1 - Base", "sub-chapter uses base title");
            Check(rows[0].CourseCode == "NARQ30056" && rows[0].LanguageCode == "en", "course/lang code");
            Check(DetectLanguage(@"D:\1. Youtube-maager-IITM-main\REAP English\reap_English") == "English", "detect language (Windows path)");
            Check(DetectLanguage("/x/REAP Assamees/reap_Assamees/") == "Assamese", "detect Assamees alias");
            Check(DetectLanguage("/x/auth") == null, "detect nothing");
            Check(LanguageCode("Odia") == "or", "language code");
            Check(ParseVideoId("A2fdlBpPYxc") == "A2fdlBpPYxc", "video id bare");
            Check(ParseVideoId("https://youtu.be/A2fdlBpPYxc") == "A2fdlBpPYxc", "video id youtu.be");
            Check(ParseVideoId("https://www.youtube.com/watch?v=A2fdlBpPYxc&t=3") == "A2fdlBpPYxc", "video id watch");
            Check(ParseVideoId("https://studio.youtube.com/video/A2fdlBpPYxc/edit") == "A2fdlBpPYxc", "video id studio");
            Check(ParseVideoId("hello world") == null, "video id invalid");
            var json = Google.Apis.Json.NewtonsoftJsonSerializer.Instance.Serialize(
                new Google.Apis.YouTube.v3.Data.VideoStatus { PrivacyStatus = "unlisted", SelfDeclaredMadeForKids = false });
            Check(json.Contains("\"selfDeclaredMadeForKids\":false") && json.Contains("\"privacyStatus\":\"unlisted\""), "madeForKids=false is sent: " + json);
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e.Message); return 1; }
    }
}
