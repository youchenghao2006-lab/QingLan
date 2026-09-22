using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QingLan.Core;

public sealed class EssaySource : IDisposable
{
    private readonly HttpClient client;
    public EssaySource(HttpMessageHandler? handler = null)
    {
        client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QingLanReader/2.0 (personal desktop reading application)");
    }

    private async Task<JsonDocument> Query(string query, CancellationToken token)
    {
        using var response = await client.GetAsync("https://zh.wikisource.org/w/api.php?format=json&formatversion=2&" + query, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (json.RootElement.TryGetProperty("error", out _)) { json.Dispose(); throw new InvalidDataException("在线文库暂时无法提供文章"); }
        return json;
    }

    public async Task<Article> Fetch(DateOnly date, CancellationToken token)
    {
        string[] categories = ["叙事性散文", "写景游记类散文", "议论性散文", "纪念人物类散文"];
        for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
        {
            var category = categories[(date.DayNumber + categoryIndex) % categories.Length];
            using var listing = await Query("action=query&list=categorymembers&cmnamespace=0&cmlimit=100&cmtitle=" + Uri.EscapeDataString("Category:" + category), token);
            var pages = listing.RootElement.GetProperty("query").GetProperty("categorymembers").EnumerateArray().ToArray();
            if (pages.Length == 0) continue;
            int first = (date.DayNumber * 37 + 11) % pages.Length;
            for (int attempt = 0; attempt < Math.Min(4, pages.Length); attempt++)
            {
                token.ThrowIfCancellationRequested();
                var page = pages[(first + attempt) % pages.Length];
                long id = page.GetProperty("pageid").GetInt64();
                using var detail = await Query($"action=query&prop=extracts%7Ccategories&explaintext=1&exsectionformat=plain&cllimit=max&pageids={id}", token);
                var info = detail.RootElement.GetProperty("query").GetProperty("pages")[0];
                if (!info.TryGetProperty("extract", out var extract)) continue;
                string content = (extract.GetString() ?? "").Replace("\r", "").Trim();
                content = Regex.Replace(content, "\n{3,}", "\n\n");
                if (content.Length < 350) continue;
                var title = page.GetProperty("title").GetString() ?? "无题";
                string author = "中文维基文库";
                string[] authors = ["朱自清", "魯迅", "鲁迅", "徐志摩", "周作人", "郁達夫", "郁达夫", "胡適", "老舍", "冰心", "梁實秋", "巴金", "茅盾", "沈從文", "聞一多", "林語堂", "葉聖陶"];
                if (info.TryGetProperty("categories", out var cats))
                {
                    var names = cats.EnumerateArray().Select(c => c.GetProperty("title").GetString()).ToHashSet();
                    author = authors.FirstOrDefault(a => names.Contains("Category:" + a)) ?? author;
                }
                return new Article { Id = $"online:{date:yyyy-MM-dd}:{id}", Date = date, Kind = "online", Title = title,
                    Author = author, Content = content, Summary = Library.Preview(content), SourceUrl = $"https://zh.wikisource.org/?curid={id}" };
            }
        }
        throw new InvalidDataException("在线文库暂时没有返回可阅读的正文。");
    }
    public void Dispose() => client.Dispose();
}
