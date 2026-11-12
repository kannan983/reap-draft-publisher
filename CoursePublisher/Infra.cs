using System.Reflection;
using System.Text.Json;

namespace CoursePublisher;

public static class AppInfo
{
    public const string Name = "REAP Course Publisher";
    public const string RepoUrl = "https://github.com/kannan983/reap-draft-publisher";
    public static readonly string Version = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "?";
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "REAP_Course_Publisher");
}

public enum LogLevel { Debug, Info, Warn, Error }

/// File + in-memory log. Starts at app launch; keeps the last MaxRecent lines for the Log tab.
public static class Log
{
    const int MaxRecent = 3000, KeepDays = 30;
    public static readonly string Dir = Path.Combine(AppInfo.DataDir, "logs");
    public static string? FilePath { get; private set; }
    public static event Action<LogLevel, string>? Written;

    static readonly object Gate = new();
    static readonly Queue<(LogLevel Level, string Line)> Recent = new();
    static StreamWriter? _w;

    public static void Init()
    {
        Directory.CreateDirectory(Dir);
        foreach (var f in Directory.GetFiles(Dir, "*.log"))
            if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-KeepDays))
                try { File.Delete(f); } catch { /* in use or locked: skip */ }
        FilePath = Path.Combine(Dir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
        _w = new StreamWriter(FilePath) { AutoFlush = true };
    }

    public static void Close() { lock (Gate) { _w?.Dispose(); _w = null; } }

    public static List<(LogLevel Level, string Line)> Snapshot() { lock (Gate) return Recent.ToList(); }

    static void Write(LogLevel level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} - {level.ToString().ToUpperInvariant()} - {msg}";
        lock (Gate)
        {
            Console.WriteLine(line);
            _w?.WriteLine(line);
            Recent.Enqueue((level, line));
            while (Recent.Count > MaxRecent) Recent.Dequeue();
        }
        Written?.Invoke(level, line);
    }

    public static void Debug(string m) => Write(LogLevel.Debug, m);
    public static void Info(string m) => Write(LogLevel.Info, m);
    public static void Warn(string m) => Write(LogLevel.Warn, m);
    public static void Error(string m) => Write(LogLevel.Error, m);
}

public class Settings
{
    public string Language { get; set; } = "English";
    public string Theme { get; set; } = "System";
    /// Language name -> that channel's auth folder (one folder per language).
    public Dictionary<string, string> AuthFolders { get; set; } = new();
    public bool AutoSignIn { get; set; } = true;
    public bool VerboseLog { get; set; }
    public double TextScale { get; set; } = 1.0;

    public static Settings Current { get; private set; } = new();
    static string FilePath => Path.Combine(AppInfo.DataDir, "settings.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
            else ImportFromReapManager();
            Log.Debug($"Settings loaded from {FilePath}");
        }
        catch (Exception e) { Log.Warn($"Settings file unreadable, using defaults: {e.Message}"); Current = new(); }
    }

    /// First run: reuse the auth folders already set up in REAP YouTube Manager (same oauth_client.json + saved login).
    static void ImportFromReapManager()
    {
        var other = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "REAP_YouTube_Manager", "settings.json");
        if (!File.Exists(other)) return;
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(other));
        if (s == null) return;
        Current = new Settings { Language = s.Language, Theme = s.Theme, TextScale = s.TextScale, AuthFolders = s.AuthFolders };
        Log.Info($"First run: imported {s.AuthFolders.Count} auth folder(s) from REAP YouTube Manager: {string.Join(", ", s.AuthFolders.Keys)}");
        Current.Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { Log.Error($"Settings save failed: {e.Message}"); }
    }
}
