using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QingLan.Core;

public sealed record Article
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Content { get; init; } = "";
    public string Kind { get; init; } = "builtin";
    public DateOnly Date { get; init; }
    public string SourceUrl { get; init; } = "";
    public string SourceLabel => Kind switch { "journal" => "时光来信", "online" => "维基文库", _ => "离线文库" };
    public string ReadingTime => $"约 {Math.Max(1, (int)Math.Ceiling(Content.Length / 450d))} 分钟";
}

public sealed record Journal(string Id, DateOnly Written, DateOnly Reveal, string Text)
{
    public Article AsArticle() => new()
    {
        Id = Id, Title = "来自过去的一页", Author = $"自己 · 写于 {Written:yyyy年M月d日}",
        Summary = Library.Preview(Text), Content = Text, Kind = "journal", Date = Reveal
    };
}

public sealed record ReaderSettings(int FontLevel = 1, string Font = "宋体");

public sealed class Library
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string folder;
    public string DataDirectory => folder;
    public List<Article> Builtins { get; private set; } = [];
    public List<Article> Online { get; private set; } = [];
    public List<Journal> Journals { get; private set; } = [];
    public HashSet<string> Favorites { get; private set; } = [];
    public ReaderSettings Settings { get; private set; } = new();
    public string LoadNotice { get; private set; } = "";
    public IEnumerable<Article> All => Builtins.Concat(Journals.Select(n => n.AsArticle())).Concat(Online);

    public Library(string folder) { this.folder = folder; }

    public void Load(string builtinPath, string legacyFolder)
    {
        Directory.CreateDirectory(folder);
        Builtins = JsonSerializer.Deserialize<List<Article>>(File.ReadAllText(builtinPath), Json) ?? throw new InvalidDataException("离线文库为空");
        if (Builtins.Count == 0) throw new InvalidDataException("离线文库为空");
        if (!File.Exists(Path.Combine(folder, "migration-v1.json"))) ImportLegacy(legacyFolder);
        Journals = Read<List<Journal>>("journals.json", []);
        Online = Read<List<Article>>("online.json", []);
        Favorites = Read<HashSet<string>>("favorites.json", []);
        Settings = Read("settings.json", new ReaderSettings());
        Settings = Settings with { FontLevel = Math.Clamp(Settings.FontLevel, 0, 3) };
        // A deleted journal must not remain reachable through a stale favourite file.
        var valid = All.Select(a => a.Id).ToHashSet();
        Favorites.IntersectWith(valid);
    }

    private T Read<T>(string name, T fallback)
    {
        var path = Path.Combine(folder, name);
        if (!File.Exists(path)) return fallback;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? throw new JsonException("文件内容为空"); }
        catch (JsonException)
        {
            var backup = path + ".bak";
            if (!File.Exists(backup)) throw new InvalidDataException($"{name} 无法读取，原文件已保留，请勿覆盖。");
            var recovered = JsonSerializer.Deserialize<T>(File.ReadAllText(backup), Json) ?? throw new InvalidDataException($"{name} 备份无法读取");
            File.Copy(path, path + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            File.Copy(backup, path, true);
            LoadNotice = "一份数据文件异常，已保留原文件并从备份恢复。";
            return recovered;
        }
    }

    private void Save<T>(string name, T value)
    {
        var path = Path.Combine(folder, name);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, Json);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void ToggleFavorite(Article article)
    {
        var next = new HashSet<string>(Favorites);
        if (!next.Add(article.Id)) next.Remove(article.Id);
        Save("favorites.json", next);
        Favorites = next;
    }

    public Journal AddJournal(string text, DateTime now)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("先写下一点文字吧。");
        if (text.Length > 30000) throw new ArgumentException("随心记最多支持 30000 字。");
        var today = DateOnly.FromDateTime(now);
        var reveal = today.AddDays(3 + Random.Shared.Next(8));
        while (Journals.Any(n => n.Reveal == reveal)) reveal = reveal.AddDays(1);
        var note = new Journal("journal:" + Guid.NewGuid().ToString("N"), today, reveal, text);
        var next = Journals.Append(note).ToList();
        Save("journals.json", next);
        Journals = next;
        return note;
    }

    public void DeleteJournal(string id)
    {
        var next = Journals.Where(n => n.Id != id).ToList();
        Save("journals.json", next);
        Journals = next;
        Favorites.Remove(id);
        // Journals are authoritative; a stale favourite is filtered at every load.
        try { Save("favorites.json", Favorites); }
        catch (IOException) { LoadNotice = "随心记已删除，收藏清理将在下次启动时完成。"; }
        catch (UnauthorizedAccessException) { LoadNotice = "随心记已删除，收藏清理将在下次启动时完成。"; }
    }

    public void SetSettings(ReaderSettings next) { Save("settings.json", next); Settings = next; }
    public void AddOnline(Article article)
    {
        var next = Online.Where(a => a.Id != article.Id).Append(article).ToList();
        Save("online.json", next);
        Online = next;
    }

    public Article ForDate(DateOnly date)
    {
        var note = Journals.FirstOrDefault(n => n.Reveal == date);
        if (note is not null) return note.AsArticle();
        var online = Online.LastOrDefault(a => a.Date == date);
        if (online is not null) return online;
        var serial = date.DayNumber - new DateOnly(1601, 1, 1).DayNumber;
        return Builtins[(serial + 23) % Builtins.Count] with { Date = date };
    }

    public static string Preview(string value)
    {
        value = value.Replace("\r", "").Replace("\n", " ").Trim();
        return value.Length > 96 ? value[..96] + "……" : value;
    }

    public static string Unescape(string value)
    {
        var output = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
                output.Append(value[++i] switch { 'n' => "\r\n", 'p' => "|", var c => c.ToString() });
            else output.Append(value[i]);
        }
        return output.ToString();
    }

    public static string LegacyJournalId(DateOnly written, DateOnly reveal, string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace("|", "\\p");
        ulong hash = 14695981039346656037UL;
        foreach (char c in escaped) hash = unchecked((hash ^ c) * 1099511628211UL);
        return $"journal:{written:yyyy-MM-dd}:{reveal:yyyy-MM-dd}:{hash}";
    }

    private void ImportLegacy(string legacy)
    {
        var notes = new List<Journal>();
        var online = new List<Article>();
        var favorites = new HashSet<string>();
        string[] Rows(string name) => File.Exists(Path.Combine(legacy, name))
            ? File.ReadAllLines(Path.Combine(legacy, name)).Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith('#')).ToArray() : [];
        // Keep an untouched snapshot, and write the completion marker last for retryability.
        if (Directory.Exists(legacy))
        {
            var snapshot = Path.Combine(folder, "旧版数据备份");
            Directory.CreateDirectory(snapshot);
            foreach (var file in Directory.EnumerateFiles(legacy, "*.txt"))
            {
                var dest = Path.Combine(snapshot, Path.GetFileName(file));
                if (!File.Exists(dest)) File.Copy(file, dest);
            }
        }
        foreach (var row in Rows("journal_notes.txt"))
        {
            var f = row.Split('|', 3);
            if (f.Length != 3 || !DateOnly.TryParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var written)
                || !DateOnly.TryParseExact(f[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var reveal))
                throw new InvalidDataException("旧版随心记格式异常，已保留原数据，迁移未覆盖任何内容。");
            var body = Unescape(f[2]);
            notes.Add(new Journal(LegacyJournalId(written, reveal, body), written, reveal, body));
        }
        foreach (var row in Rows("online_articles.txt"))
        {
            var f = row.Split('|');
            if (f.Length != 6 || !DateOnly.TryParse(f[0], out var date) || !uint.TryParse(f[1], out var pageId))
                throw new InvalidDataException("旧版在线缓存格式异常，已保留原数据。");
            online.Add(new Article { Id = $"online:{f[0]}:{pageId}", Date = date, Title = Unescape(f[2]), Author = Unescape(f[3]),
                Summary = Unescape(f[4]), Content = Unescape(f[5]), Kind = "online", SourceUrl = $"https://zh.wikisource.org/?curid={pageId}" });
        }
        var legacyOrder = Builtins.Concat(notes.Select(n => n.AsArticle())).Concat(online).ToList();
        foreach (var row in Rows("favorites.txt"))
        {
            if (int.TryParse(row, out int index) && index >= 0 && index < legacyOrder.Count) favorites.Add(legacyOrder[index].Id);
            else favorites.Add(row.Trim());
        }
        var setting = Rows("reader_settings.txt").FirstOrDefault()?.Split(' ').FirstOrDefault();
        Save("journals.json", notes);
        Save("online.json", online);
        Save("favorites.json", favorites);
        Save("settings.json", new ReaderSettings(int.TryParse(setting, out var level) ? Math.Clamp(level, 0, 3) : 1));
        Save("migration-v1.json", new { Completed = DateTime.Now, Journals = notes.Count, Online = online.Count, Favorites = favorites.Count });
    }
}
