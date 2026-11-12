# REAP Course Publisher

Windows desktop app for the REAP YouTube channels (one channel per language). It finishes the step after uploading:

1. Upload the course videos in **YouTube Studio** (as today). They show up as **Draft**.
2. **REAP Course Publisher** (this app): turn the drafts into **Unlisted** videos and put them in the course playlist
   in chapter order.
3. **[REAP YouTube Manager](https://github.com/kannan983/reap--youtube-manager)**: export the playlist to Excel,
   once the playlist is complete.

**[Download the latest Windows exe](https://github.com/kannan983/reap-draft-publisher/releases/latest)**
(Windows 10/11, 64-bit, single file, nothing to install)

> **Status: v0.1.0 is a test build.** It only has the **1-video Draft Test**: it checks whether the YouTube API can
> finish a Studio draft as Unlisted. Whole-course publishing and playlist ordering are built after the test result.

---

## User guide

### Install

1. Download `REAP_Course_Publisher.exe` from **Releases** and double-click it. Nothing to install.
2. If Windows says **"Windows protected your PC"**: **More info** > **Run anyway** (the exe is not code-signed).

### Sign in (same auth folders as REAP YouTube Manager)

- The app uses the **same per-language auth folders** as REAP YouTube Manager
  (`oauth_client.json` + `api.txt` + the saved login).
- On first start it **copies the folder list from REAP YouTube Manager automatically**, so usually there is nothing to set up.
- Otherwise: pick the **Channel Language**, **Browse...** to that language's auth folder, then **Authorize with Token**
  (or **Authorize (Browser)** the first time).
- Always check the blue header: **Language**, **Auth folder** and **YouTube channel** must all be the same language.

### Draft Test (tab 2)

1. In YouTube Studio > Content, open **one** draft video and copy its link (e.g. `https://youtu.be/...`).
2. Paste it and click **1. Read video** (1 unit). Nothing is changed.
3. Click **2. Make Unlisted** and confirm (50 units). Only this video changes: **Unlisted** + **not made for kids**.
4. Refresh YouTube Studio > Content: does it show **Unlisted** or still **Draft**?
5. Click **Copy test report**, paste it in a message and add what Studio showed in step 4.

**List recent uploads** (about 3 units) lists the last 50 uploads with their privacy, to check whether drafts are
visible to the app at all.

Daily limit: 10,000 units per Google Cloud project (reset around 1:30 PM India time). The test uses about 55.

### Logs and reporting

- **Copy test report** is the main thing to send. Everything is also in the **Log** tab and the log files at
  `%LOCALAPPDATA%\REAP_Course_Publisher\logs` (full path, **Copy path** and **Open logs folder** in the Log and About tabs).
- Logs never contain passwords or API keys.

### Text size and theme

Eye icon **Text size** (100% to 200%, Ctrl +/-/0) and **Theme** (System / Light / Dark) in the header.

---

## Developer guide

AI assistants (Claude Code etc.): read [CONTEXT.md](CONTEXT.md) first (`CLAUDE.md` imports it).

C# / .NET 10, Avalonia 11, `Google.Apis.YouTube.v3`. Built from the same base as REAP YouTube Manager.

```
CoursePublisher/
├── Program.cs        entry point, theme, global error logging, --selftest
├── Core.cs           pure logic (languages, video-ID parsing, chapter sorting, title rules) + self-test
├── Infra.cs          AppInfo, Log, Settings (+ first-run import of REAP YouTube Manager's auth folders)
├── MainWindow.cs     window, header, sign-in, Log/About tabs
├── DraftTest.cs      Draft Test tab: read video, videos.update(status) to unlisted, list uploads, test report
└── HelpText.cs       in-app Help text (keep in sync with this README)
```

```
cd CoursePublisher
dotnet run                                              # run (Windows or Linux)
dotnet run -- --selftest                                # logic self-test
dotnet publish -c Release -r win-x64 -o ../publish      # single Windows exe
```

**Release**: bump `<Version>` in `CoursePublisher.csproj`, update `.github/release-notes.md`, then
`git tag v0.1.1 && git push origin v0.1.1`. GitHub Actions builds **only Windows x64** and attaches the exe + SHA256.

**API notes**
- `videos.update` replaces every writable field of the parts it sends, so the test reads the video first and sends the
  existing `status` values back, changing only `privacyStatus` and `selfDeclaredMadeForKids` (the self-test checks that
  `false` is really serialized).
- Costs: `videos.list` 1, `videos.update` 50, `playlistItems.insert` 50, `playlists.insert` 50 units.

## License

[MIT](LICENSE). Copyright (c) 2026 kannan983.
