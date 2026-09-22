using QingLan.Core;
using System.Net;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "QingLanTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var builtin = Path.GetFullPath(args[0]);
var legacy = Path.Combine(root, "legacy");
Directory.CreateDirectory(legacy);
string text = "这是一页测试心事。\r\n换行 | 分隔符 \\ 路径，以及😀。";
var written = new DateOnly(2026, 9, 1);
var reveal = new DateOnly(2026, 9, 13);
var escaped = text.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace("|", "\\p");
var oldId = Library.LegacyJournalId(written, reveal, text);
File.WriteAllText(Path.Combine(legacy, "journal_notes.txt"), $"# DailyEssayJournal v1\n2026-09-01|2026-09-13|{escaped}\n");
File.WriteAllText(Path.Combine(legacy, "favorites.txt"), $"# DailyEssayFavorites v2\ndemo:1\n{oldId}\nonline:2026-09-13:42\n");
File.WriteAllText(Path.Combine(legacy, "online_articles.txt"), "# DailyEssayOnlineCache v1\n2026-09-13|42|网络文章|作者|摘要|正文\\n第二段\n");
File.WriteAllText(Path.Combine(legacy, "reader_settings.txt"), "3 0 1\n");
var data = Path.Combine(root, "new");
var lib = new Library(data); lib.Load(builtin, legacy);
int count = 0;
void Check(bool test, string label) { if (!test) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); count++; }
Check(lib.Builtins.Count == 3 && lib.Builtins.All(a => a.Id.StartsWith("demo:") && a.Content.Length > 200)
    && lib.Builtins.Select(a => a.Id).Distinct().Count() == 3, "Three distinct public-edition demo essays load in full");
Check(lib.Journals.Single().Text == text && lib.Favorites.Count == 3, "Legacy Unicode, escapes, stable favourite keys migrated");
Check(lib.ForDate(reveal).Id == oldId && lib.ForDate(reveal).Author.Contains("2026年9月1日"), "Surprise overrides daily essay and retains written date");
Check(lib.Settings.FontLevel == 3, "Legacy font preference retained");
Check(File.ReadAllText(Path.Combine(legacy, "journal_notes.txt")).Contains(escaped), "Original files unchanged");
var added = lib.AddJournal("新的一页\n未来再见。", new DateTime(2026, 9, 13));
Check(added.Reveal >= reveal.AddDays(3) && added.Reveal <= reveal.AddDays(10), "Surprise scheduled in 3–10 days");
for (int i = 0; i < 15; i++) lib.AddJournal("测试序列 " + i, new DateTime(2026, 9, 13));
Check(lib.Journals.Select(n => n.Reveal).Distinct().Count() == lib.Journals.Count, "Surprise dates never collide");
lib.ToggleFavorite(added.AsArticle());
var restarted = new Library(data); restarted.Load(builtin, legacy);
Check(restarted.Journals.Any(n => n.Id == added.Id) && restarted.Favorites.Contains(added.Id), "Journal and favourites persist across restart");
restarted.DeleteJournal(oldId);
Check(restarted.ForDate(reveal).Id == "online:2026-09-13:42" && !restarted.Favorites.Contains(oldId), "Delete removes surprise and favourite, restores online daily article");
var again = new Library(data); again.Load(builtin, legacy);
Check(again.Journals.All(n => n.Id != oldId) && !again.Favorites.Contains(oldId), "Deleted journal remains gone after restart");
int before = again.Journals.Count;
using (var locked = new FileStream(Path.Combine(data, "journals.json"), FileMode.Open, FileAccess.Read, FileShare.None))
{
    bool failed = false;
    try { again.AddJournal("不能丢失", DateTime.Now); } catch (IOException) { failed = true; }
    Check(failed && again.Journals.Count == before, "Write failure leaves in-memory and on-disk journal unchanged");
}
Check(Directory.GetFiles(data, "*.tmp").Length == 0, "Failed save leaves no incomplete temporary files");
again.SetSettings(new ReaderSettings(2, "楷体"));
var settingsCheck = new Library(data); settingsCheck.Load(builtin, legacy);
Check(settingsCheck.Settings == new ReaderSettings(2, "楷体"), "Font family and size persist");
using (var onlineSource = new EssaySource(new FakeHandler()))
{
    var fetched = await onlineSource.Fetch(reveal, CancellationToken.None);
    Check(fetched.Kind == "online" && fetched.Author == "朱自清" && fetched.Content.Length > 7000, "Online service parses attribution and keeps full text");
    again.AddOnline(fetched);
    Check(again.ForDate(reveal).Id == fetched.Id, "Fresh online article replaces daily offline fallback");
}
using (var offlineSource = new EssaySource(new FakeHandler(true)))
{
    bool failed = false;
    try { await offlineSource.Fetch(reveal, CancellationToken.None); } catch (HttpRequestException) { failed = true; }
    Check(failed, "Network failure is surfaced, never labelled online success");
}
Console.WriteLine($"ALL {count} CHECKS PASSED. Isolated fixtures: {root}");

sealed class FakeHandler(bool fail = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (fail) throw new HttpRequestException("Simulated offline");
        string json = request.RequestUri!.Query.Contains("categorymembers")
            ? "{\"query\":{\"categorymembers\":[{\"pageid\":123,\"title\":\"测试散文\"}]}}"
            : JsonSerializer.Serialize(new { query = new { pages = new[] { new { extract = new string('文', 8000), categories = new[] { new { title = "Category:朱自清" } } } } } });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
