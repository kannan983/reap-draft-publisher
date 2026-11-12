# CONTEXT.md - for AI assistants (Claude Code etc.)

Read this before helping with this repo. It explains what the app is for, how it is tested, and how to read its
output. The person running the app is usually a REAP team member on a **Windows laptop**, testing against the real
REAP YouTube channels. The maintainer (GitHub `kannan983`) builds the app on Linux and cannot see her screen, so
**exact, copied output is what matters**.

## What the app is for

REAP publishes training courses on YouTube: **12 language channels** (English, Hindi, Tamil, Telugu, Kannada,
Malayalam, Bengali, Gujarati, Marathi, Punjabi, Assamese, Odia). A course has ~20 videos named by chapter:
`<Course> Course Introduction`, `Chapter 1 - Topic`, `Chapter 1A - Topic`, `Chapter 1B - ...`, `Chapter 2 - ...`.

Workflow:
1. Videos are uploaded by hand in **YouTube Studio**. They land as **Draft** ("Saved as private"); finishing each one
   needs 4 Studio steps (Details, Video elements, Checks, Visibility). This is the slow part.
2. **REAP Course Publisher** (this repo): turn drafts into **Unlisted** videos and add them to the course playlist in
   chapter order.
3. **REAP YouTube Manager** (github.com/kannan983/reap--youtube-manager): rename titles and export the playlist to
   Excel. Runs only after step 2.

## Current status: v0.1.0 = Draft Test only

The open question: **can the YouTube Data API finish a Studio draft?** Google's docs do not say. v0.1.0 tests it on
ONE video:
- `videos.list(part=snippet,status,processingDetails)` (1 quota unit) - read the video.
- `videos.update(part=status)` (50 units) with `privacyStatus=unlisted`, `selfDeclaredMadeForKids=false`, and the
  other writable status fields copied from the read (the update replaces all writable fields of the part).
- Then the person checks YouTube Studio: **"Unlisted"** = the API can finish drafts (next: whole-course publishing);
  **still "Draft"** = it cannot (fallback: Studio upload defaults or a batch-publish browser extension).

## How to help the person test

Follow `README.md` > User guide. Short version:
1. Download `REAP_Course_Publisher.exe` from Releases, run it (SmartScreen: More info > Run anyway).
2. Tab **1. Authentication**: pick the language, pick/Browse the auth folder for that language (the same folder used by
   REAP YouTube Manager, containing `oauth_client.json` + `api.txt`), **Authorize with Token** (or **Authorize
   (Browser)** the first time). Green light bottom-left = signed in.
3. Check the blue header: **Language**, **Auth folder**, **YouTube channel** must all be the same language.
4. Tab **2. Draft Test**: paste ONE draft video link > **1. Read video** > **2. Make Unlisted** > Yes.
5. Refresh YouTube Studio > Content and note the video's Visibility.
6. **Copy test report** and send it to the maintainer with one added line: `Studio shows: Unlisted` or `Studio shows: Draft`.

## Reading the test report

| Line in the report | Meaning | What to do |
|---|---|---|
| `Read OK. Nothing was changed.` | Read worked | Continue with Make Unlisted |
| `NOT FOUND` | Wrong link, deleted, or the video is on another channel / Google account | Check the link and the header's channel |
| `WARNING: video belongs to channel X, but you are signed in to Y` | Signed in to the wrong channel | Switch language/auth folder, or sign in again with the right account |
| `API RESULT: privacy is now 'unlisted'.` | Google accepted the change | Check Studio (that is the real answer) |
| `ERROR ... reason=quotaExceeded` | Daily API quota (10,000 units/project) used up | Wait until the reset (~1:30 PM India time) |
| `ERROR ... reason=forbidden` / `insufficientPermissions` | The login cannot edit this video | Authorize (Browser) again with the channel owner's account |
| `ERROR ... reason=invalid_grant` / "saved login expired" | Saved token revoked/expired | Authorize (Browser); if it repeats every ~7 days the Google Cloud OAuth app is in "Testing" mode -> publish it "In production" |
| any other `ERROR` | Unknown | Send the full report unchanged |

## Logs and files on the Windows laptop

- Logs: `%LOCALAPPDATA%\REAP_Course_Publisher\logs\*.log` (newest file = latest run). Also the in-app **Log** tab
  (**Show debug details**, **Copy all**). Every test-report line is also in the log, prefixed `[DraftTest]`.
- Settings: `%LOCALAPPDATA%\REAP_Course_Publisher\settings.json` (language, theme, auth folder per language). On first
  start it copies the auth-folder list from REAP YouTube Manager's settings.
- Saved Google login: inside each auth folder, `Google.Apis.Auth.OAuth2.Responses.TokenResponse-user`
  (or `token.json` from the old Python app).

## Rules for AI assistants

- **Never print, paste, commit or upload** `oauth_client.json`, `api.txt`, token files, or their contents. Logs and the
  test report never contain them; keep it that way.
- Do not run the test on more than one video until the maintainer has seen the first report.
- When something fails, ask for the **exact** report/log text (Copy test report / Copy all), not a paraphrase.
- The app only builds for **Windows x64** (GitHub Actions on tag push). Building from source needs the .NET 10 SDK:
  `cd CoursePublisher && dotnet run` (works on Windows and Linux); `dotnet run -- --selftest` runs the logic checks.
- Code map: `CoursePublisher/DraftTest.cs` (the test), `MainWindow.cs` (window, sign-in), `Core.cs` (pure logic +
  self-test), `Infra.cs` (logging, settings), `HelpText.cs` (in-app help). Keep `HelpText.cs`, `README.md` and this
  file in sync when behaviour changes.
