using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Google;
using Google.Apis.YouTube.v3.Data;

namespace CoursePublisher;

/// "Draft Test" tab: checks whether the API can finish a YouTube Studio draft by setting it to Unlisted.
/// Everything it does goes into a copyable report (and the log file) so a remote user can send it back.
public partial class MainWindow
{
    readonly TextBox _testVideoBox = new() { Watermark = "Video link or ID, e.g. https://youtu.be/A2fdlBpPYxc" };
    readonly Button _testReadBtn = new() { Content = "1. Read video (1 unit)" };
    readonly Button _testPublishBtn = new() { Content = "2. Make Unlisted (50 units)", IsEnabled = false };
    readonly Button _testListBtn = new() { Content = "List recent uploads (about 3 units)" };
    readonly Button _testCopyBtn = new() { Content = "Copy test report" };
    readonly TextBox _testOut = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = Mono, FontSize = 12 };
    readonly StringBuilder _testReport = new();
    Video? _testBefore;
    string? _channelId;
    int _quotaUsed;

    Control BuildDraftTestTab()
    {
        _testReadBtn.Click += async (_, _) => await TestRead();
        _testPublishBtn.Click += async (_, _) => await TestPublish();
        _testListBtn.Click += async (_, _) => await TestListUploads();
        _testCopyBtn.Click += async (_, _) => { await Clipboard!.SetTextAsync(_testReport.ToString()); Status("Test report copied. Paste it in your message."); };

        var top = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                Panel("What this test does",
                    "Checks if the app can finish a Studio 'Draft' video by making it Unlisted (not made for kids), without the 4 Studio steps.\n" +
                    "1. In YouTube Studio > Content, open ONE draft video and copy its link (e.g. https://youtu.be/...).\n" +
                    "2. Paste it below and click '1. Read video'. Nothing is changed yet.\n" +
                    "3. Click '2. Make Unlisted' and confirm. Only this one video is changed.\n" +
                    "4. In YouTube Studio > Content, refresh: does the video show 'Unlisted' or still 'Draft'?\n" +
                    "5. Click 'Copy test report', paste it in your message, and add what Studio showed in step 4."),
                Grid3("Video:", _testVideoBox, _testReadBtn),
                Wrap(_testPublishBtn, _testListBtn, _testCopyBtn),
            }
        };
        return new DockPanel { Margin = new Thickness(12), Children = { Dock(top, Avalonia.Controls.Dock.Top), _testOut } };
    }

    void Report(string line)
    {
        _testReport.AppendLine(line);
        _testOut.Text = _testReport.ToString();
        _testOut.CaretIndex = _testOut.Text.Length;
        Log.Info("[DraftTest] " + line);
    }

    void ReportHeader(string what)
    {
        _testReport.Clear();
        Report($"=== REAP Draft Test: {what} ===");
        Report($"App v{AppInfo.Version} | {RuntimeInformation.OSDescription} | {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Report($"Language: {_settings.Language} | Channel: {_channelTitle ?? "?"} ({_channelId ?? "?"}) | Auth folder: {System.IO.Path.GetFileName(_authFolder?.TrimEnd('/', '\\') ?? "")}");
    }

    string Pretty(object o)
    {
        using var doc = JsonDocument.Parse(_ytOAuth!.Serializer.Serialize(o));
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    void ReportApiError(string step, Exception e)
    {
        Log.Error($"[DraftTest] {step} failed: {e}");
        if (e is GoogleApiException g)
        {
            Report($"ERROR in {step}: HTTP {(int)g.HttpStatusCode} - {g.Error?.Message ?? g.Message}");
            foreach (var er in g.Error?.Errors ?? []) Report($"  reason={er.Reason} domain={er.Domain} message={er.Message}");
        }
        else Report($"ERROR in {step}: {e.GetType().Name}: {e.Message}");
        Report($"Quota used this session: {_quotaUsed} units.");
    }

    async Task<Video?> ReadVideo(string id, string label)
    {
        var req = _ytOAuth!.Videos.List("snippet,status,processingDetails");
        req.Id = id;
        var res = await req.ExecuteAsync();
        _quotaUsed += 1;
        var v = res.Items?.FirstOrDefault();
        if (v == null)
        {
            Report($"{label}: video {id} NOT FOUND. Wrong link, deleted, or it belongs to another channel/Google account.");
            return null;
        }
        Report($"{label}: \"{v.Snippet.Title}\"");
        Report($"  privacy={v.Status.PrivacyStatus} uploadStatus={v.Status.UploadStatus} madeForKids={v.Status.MadeForKids?.ToString() ?? "unset"} " +
               $"selfDeclaredMadeForKids={v.Status.SelfDeclaredMadeForKids?.ToString() ?? "unset"} processing={v.ProcessingDetails?.ProcessingStatus ?? "?"}");
        if (_channelId != null && v.Snippet.ChannelId != _channelId)
            Report($"  WARNING: video belongs to channel {v.Snippet.ChannelId}, but you are signed in to {_channelId}.");
        Report($"  {label} status JSON:\n{Pretty(v.Status)}");
        return v;
    }

    async Task TestRead()
    {
        var id = Core.ParseVideoId(_testVideoBox.Text ?? "");
        if (id == null) { await Message("Draft Test", "That doesn't look like a YouTube video link or ID."); return; }
        _testPublishBtn.IsEnabled = false;
        ReportHeader($"video {id}");
        try
        {
            _testBefore = await ReadVideo(id, "BEFORE");
            _testPublishBtn.IsEnabled = _testBefore != null;
            if (_testBefore != null) Report("Read OK. Nothing was changed. Next: '2. Make Unlisted'.");
        }
        catch (Exception e) { ReportApiError("read", e); }
    }

    async Task TestPublish()
    {
        var v = _testBefore;
        if (v == null) return;
        if (!await Message("Draft Test",
                $"Change this ONE video to Unlisted and 'Not made for kids'?\n\n{v.Snippet.Title}\n{v.Id}\nChannel: {_channelTitle}\n\nUses 50 of the 10,000 daily units.",
                yesNo: true)) return;
        _testPublishBtn.IsEnabled = false;

        // videos.update replaces every writable field in "status", so send the current values back
        // and change only privacy + audience. publishAt is left out (only valid with private).
        var s = v.Status;
        var body = new Video
        {
            Id = v.Id,
            Status = new VideoStatus
            {
                PrivacyStatus = "unlisted",
                SelfDeclaredMadeForKids = false,
                Embeddable = s.Embeddable,
                License = s.License,
                PublicStatsViewable = s.PublicStatsViewable,
                ContainsSyntheticMedia = s.ContainsSyntheticMedia,
            }
        };
        Report($"REQUEST videos.update(part=status):\n{Pretty(body)}");
        try
        {
            var r = await _ytOAuth!.Videos.Update(body, "status").ExecuteAsync();
            _quotaUsed += 50;
            Report($"RESPONSE status:\n{Pretty(r.Status)}");
            await Task.Delay(3000);
            var after = await ReadVideo(v.Id, "AFTER");
            Report(after?.Status.PrivacyStatus == "unlisted"
                ? "API RESULT: privacy is now 'unlisted'."
                : $"API RESULT: privacy is '{after?.Status.PrivacyStatus ?? "?"}', NOT unlisted.");
            Report("NEXT: open YouTube Studio > Content, refresh, and look at this video's Visibility column.");
            Report("      Write 'Studio shows: Unlisted' or 'Studio shows: Draft' under this report when you send it.");
        }
        catch (Exception e) { ReportApiError("update", e); return; }
        Report($"Quota used this session: {_quotaUsed} units.");
    }

    /// Shows the last 50 uploads with privacy, so we learn whether Studio drafts are visible to the API at all.
    async Task TestListUploads()
    {
        ReportHeader("recent uploads");
        try
        {
            var chReq = _ytOAuth!.Channels.List("contentDetails"); chReq.Mine = true;
            var uploads = (await chReq.ExecuteAsync()).Items?.FirstOrDefault()?.ContentDetails.RelatedPlaylists.Uploads;
            _quotaUsed += 1;
            if (uploads == null) { Report("No uploads playlist found for this channel."); return; }

            var plReq = _ytOAuth.PlaylistItems.List("contentDetails"); plReq.PlaylistId = uploads; plReq.MaxResults = 50;
            var ids = (await plReq.ExecuteAsync()).Items.Select(i => i.ContentDetails.VideoId).ToList();
            _quotaUsed += 1;
            Report($"Uploads playlist {uploads}: {ids.Count} most recent videos.");
            if (ids.Count == 0) return;

            var vReq = _ytOAuth.Videos.List("snippet,status"); vReq.Id = string.Join(",", ids);
            var vids = (await vReq.ExecuteAsync()).Items;
            _quotaUsed += 1;
            foreach (var x in vids)
                Report($"  {x.Id}  {x.Status.PrivacyStatus,-9} upload={x.Status.UploadStatus,-9} kids={x.Status.SelfDeclaredMadeForKids?.ToString() ?? "unset",-5}  {x.Snippet.Title}");
            Report($"Private: {vids.Count(x => x.Status.PrivacyStatus == "private")}, unlisted: {vids.Count(x => x.Status.PrivacyStatus == "unlisted")}, public: {vids.Count(x => x.Status.PrivacyStatus == "public")}.");
            Report("Compare with Studio: do the 'Draft' videos appear in this list (as private)?");
            Report($"Quota used this session: {_quotaUsed} units.");
        }
        catch (Exception e) { ReportApiError("list uploads", e); }
    }
}
