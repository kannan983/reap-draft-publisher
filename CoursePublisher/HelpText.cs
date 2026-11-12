namespace CoursePublisher;

/// Text of the in-app Help tab. Keep in sync with README.md.
static class HelpText
{
    public static readonly (string Title, string Body)[] Sections =
    [
        ("Where this app fits",
         "1. Upload the course videos in YouTube Studio (as today). They show up as 'Draft'.\n" +
         "2. REAP Course Publisher (this app): turn the drafts into Unlisted videos and put them in the course playlist in chapter order.\n" +
         "3. REAP YouTube Manager: export the playlist to Excel (only after step 2, when the playlist is complete).\n\n" +
         "Current version: only the 1-video Draft Test (tab 2). Whole-course publishing comes after the test result."),

        ("Check these 3 things before changing anything",
         "The blue header always shows the Language badge, the Auth folder and the YouTube channel name.\n" +
         "All three must belong to the same language."),

        ("Sign in (same folders as REAP YouTube Manager)",
         "This app uses the SAME auth folders as REAP YouTube Manager (oauth_client.json + api.txt + saved login).\n" +
         "On the first start it copies the folder list from REAP YouTube Manager automatically, so usually nothing to set up.\n" +
         "Otherwise: pick the Channel Language, Browse to that language's auth folder, then 'Authorize with Token'\n" +
         "(or 'Authorize (Browser)' the first time). The light at the bottom-left turns green when signed in."),

        ("Draft Test (tab 2)",
         "1. In YouTube Studio > Content, open ONE draft video and copy its link (e.g. https://youtu.be/...).\n" +
         "2. Paste it and click '1. Read video' (1 unit). Nothing is changed.\n" +
         "3. Click '2. Make Unlisted' and confirm (50 units). Only this one video changes: Unlisted + not made for kids.\n" +
         "4. Refresh YouTube Studio > Content: does it show 'Unlisted' or still 'Draft'?\n" +
         "5. Click 'Copy test report', paste it in your message and add what Studio showed.\n" +
         "'List recent uploads' (about 3 units) shows the last 50 uploads with privacy, to see if drafts are visible to the app.\n" +
         "Daily limit: 10,000 units per Google Cloud project, reset around 1:30 PM India time."),

        ("Text size (eye icon) - reduce eye strain",
         "Eye icon 'Text size' in the header: 100% to 200%. Keyboard: Ctrl + (bigger), Ctrl - (smaller), Ctrl 0 (reset)."),

        ("Problems and reporting",
         "'NOT FOUND': wrong link, or the video belongs to another channel / Google account. Check the header.\n" +
         "'WARNING: video belongs to channel ...': you are signed in to a different channel than the video's.\n" +
         "Errors show Google's exact reason (e.g. quotaExceeded, forbidden) in the report.\n" +
         "Always send the 'Copy test report' text, or the newest file from the logs folder (path in the Log and About tabs).\n" +
         "Logs never contain passwords or API keys."),
    ];
}
