using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QingLan.Core;

public sealed record ClassPeriod(string Id, string Name, TimeOnly Start, TimeOnly End);
public sealed record CampusCourse(string Id, string Name, string Teacher, string Room, int Day, string PeriodId, string Weeks, int Color = 0);
public sealed record CampusMemo(string Id, string Event, DateTime Start, DateTime? End = null);
public sealed record CampusData(DateOnly SemesterStart, DateOnly SemesterEnd, ClassPeriod[] Periods, CampusCourse[] Courses, CampusMemo[] Memos, bool SemesterConfigured = false, int Version = 1)
{
    [System.Text.Json.Serialization.JsonIgnore] public DateOnly FirstMonday => CampusStore.Monday(SemesterStart);
    [System.Text.Json.Serialization.JsonIgnore] public int TotalWeeks => (SemesterEnd.DayNumber - FirstMonday.DayNumber) / 7 + 1;
}

public static class WeekPattern
{
    public static int[] Parse(string expression, int totalWeeks)
    {
        if (totalWeeks is < 1 or > 60) throw new ArgumentException("学期周数须为 1–60。");
        string text = Regex.Replace(expression ?? "", @"\s", "").Replace('，', ',').Replace('、', ',').Replace("至", "-").Replace('—', '-').Replace('～', '-').Replace('~', '-');
        if (text.Length == 0 || text.Length > 200) throw new ArgumentException("请填写周次，例如：每周、单周、双周、1-4、1-8单周、1-4,7,9-12。");
        var weeks = new SortedSet<int>();
        foreach (var token in text.Split(','))
        {
            var match = Regex.Match(token, @"^(?:(\d+)(?:-(\d+))?(?:周)?)?(每周|全周|单周|双周|单|双)?$");
            if (!match.Success || token.Length == 0) throw new ArgumentException("周次格式有误。可用：每周、单周、双周、1-4周、1-8单周、1-4,7,9-12。");
            bool hasStart = match.Groups[1].Success;
            if (!hasStart && !match.Groups[3].Success) throw new ArgumentException("周次不能为空。");
            if (hasStart && !int.TryParse(match.Groups[1].Value, out _)) throw new ArgumentException("周次数字过大。");
            if (match.Groups[2].Success && !int.TryParse(match.Groups[2].Value, out _)) throw new ArgumentException("周次数字过大。");
            int start = hasStart ? int.Parse(match.Groups[1].Value) : 1;
            int end = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : hasStart ? start : totalWeeks;
            if (start < 1 || end > totalWeeks || start > end) throw new ArgumentException($"周次须在 1–{totalWeeks} 内，起始周不能大于结束周。");
            string parity = match.Groups[3].Value;
            for (int week = start; week <= end; week++)
                if ((!parity.StartsWith('单') || week % 2 == 1) && (!parity.StartsWith('双') || week % 2 == 0)) weeks.Add(week);
        }
        if (weeks.Count == 0) throw new ArgumentException("这个周次条件没有任何上课周，请调整。");
        return weeks.ToArray();
    }
}

public sealed class CampusStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string path;
    private string? loadedText;
    public CampusData Data { get; private set; } = Defaults(DateOnly.FromDateTime(DateTime.Today));
    public string? LoadError { get; private set; }
    public bool IsReady { get; private set; }
    public CampusStore(string folder) => path = Path.Combine(folder, "campus.json");
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static CampusData Defaults(DateOnly today) => new(Monday(today), Monday(today).AddDays(139),
    [new("p1", "第 1–2 节", new(8, 0), new(9, 35)), new("p2", "第 3–4 节", new(9, 55), new(11, 30)),
     new("p3", "第 5–6 节", new(14, 0), new(15, 35)), new("p4", "第 7–8 节", new(15, 55), new(17, 30)),
     new("p5", "第 9–10 节", new(19, 0), new(20, 35))], [], []);
    public int WeekFor(DateOnly date) => (int)Math.Floor((date.DayNumber - Data.FirstMonday.DayNumber) / 7d) + 1;
    public IReadOnlyList<CampusMemo> Upcoming(int count = 3) => Data.Memos.OrderBy(m => m.Start).ThenBy(m => m.Id, StringComparer.Ordinal).Take(count).ToArray();
    public int MemoCountOn(DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        // Intervals are [start, end): an event ending exactly at midnight does not mark the following day.
        return Data.Memos.Count(m => m.End is null ? m.Start >= start && m.Start < end : m.Start < end && m.End > start);
    }
    public void Load()
    {
        IsReady = false;
        try
        {
            string? contents = File.Exists(path) ? File.ReadAllText(path) : null;
            var next = contents is null ? Defaults(DateOnly.FromDateTime(DateTime.Today)) : JsonSerializer.Deserialize<CampusData>(contents, Json) ?? throw new InvalidDataException("文件内容为空。");
            Validate(next);
            Data = next; loadedText = contents; LoadError = null; IsReady = true;
        }
        catch (Exception ex) { LoadError = "校园日程读取失败，原文件已保留，暂不允许保存以免覆盖数据。" + ex.Message; }
    }
    public void SetSemester(DateOnly start, DateOnly end) => Save(Data with { SemesterStart = start, SemesterEnd = end, SemesterConfigured = true });
    public void SetPeriods(IEnumerable<ClassPeriod> periods) => Save(Data with { Periods = periods.OrderBy(p => p.Start).ToArray() });
    public void SaveCourse(CampusCourse course)
    {
        if (!Data.SemesterConfigured) throw new ArgumentException("请先设置学期开始和结束日期。");
        Save(Data with { Courses = Data.Courses.Where(c => c.Id != course.Id).Append(course with { Name = course.Name.Trim(), Teacher = course.Teacher.Trim(), Room = course.Room.Trim(), Weeks = course.Weeks.Trim() }).ToArray() });
    }
    public void DeleteCourse(string id) => Save(Data with { Courses = Data.Courses.Where(c => c.Id != id).ToArray() });
    public void SaveMemo(CampusMemo memo) => Save(Data with { Memos = Data.Memos.Where(m => m.Id != memo.Id).Append(memo with { Event = memo.Event.Trim() }).ToArray() });
    public void DeleteMemo(string id) => Save(Data with { Memos = Data.Memos.Where(m => m.Id != id).ToArray() });

    private void Save(CampusData next)
    {
        if (!IsReady) throw new IOException(LoadError ?? "校园日程尚未载入。");
        Validate(next);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Serialize writers and reject stale edits from another running copy of the app.
        using var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string? disk = File.Exists(path) ? File.ReadAllText(path) : null;
        if (disk != loadedText) throw new IOException("另一个窗口已更新校园日程。请取消编辑，点击页面的“刷新”后重试。");
        string contents = JsonSerializer.Serialize(next, Json);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contents);
                file.Write(bytes); file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
            Data = next; loadedText = contents;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void Validate(CampusData data)
    {
        if (data.Version != 1) throw new ArgumentException("日程数据版本不受支持。");
        if (data.SemesterStart.Year is < 1900 or > 2100 || data.SemesterEnd.Year is < 1900 or > 2100) throw new ArgumentException("学期起止日期须为 1900–2100 年内。");
        if (data.SemesterEnd < data.SemesterStart) throw new ArgumentException("学期结束日期不能早于开始日期。");
        if (data.TotalWeeks is < 1 or > 60) throw new ArgumentException("学期长度须在 1–60 周内，请检查起止日期。");
        if (data.Periods is null || data.Courses is null || data.Memos is null) throw new ArgumentException("日程数据不完整。");
        if (data.Periods.Length is < 1 or > 20) throw new ArgumentException("请保留 1–20 个上课时段。");
        if (data.Courses.Length > 500 || data.Memos.Length > 5000) throw new ArgumentException("日程数量超过上限，请先整理旧内容。");
        static void Unique(IEnumerable<string> ids) { var all = ids.ToArray(); if (all.Any(string.IsNullOrWhiteSpace) || all.Distinct().Count() != all.Length) throw new ArgumentException("日程标识重复或为空。"); }
        Unique(data.Periods.Select(p => p.Id)); Unique(data.Courses.Select(c => c.Id)); Unique(data.Memos.Select(m => m.Id));
        var periods = data.Periods.OrderBy(p => p.Start).ToArray();
        for (int i = 0; i < periods.Length; i++)
        {
            var p = periods[i];
            if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 30) throw new ArgumentException("时段名称须为 1–30 字。");
            if (p.End <= p.Start) throw new ArgumentException($"“{p.Name}”的结束时间须晚于开始时间，课表时段不跨日。");
            if (i > 0 && p.Start < periods[i - 1].End) throw new ArgumentException($"“{p.Name}”与上一时段重叠。");
        }
        var occupied = new HashSet<(int Day, string Period, int Week)>();
        foreach (var course in data.Courses)
        {
            if (string.IsNullOrWhiteSpace(course.Name) || course.Name.Length > 80 || course.Teacher is null || course.Room is null || course.Teacher.Length > 60 || course.Room.Length > 80) throw new ArgumentException("请填写课程名称（至多 80 字），老师和教室可以留空。");
            if (course.Day is < 1 or > 7 || !periods.Any(p => p.Id == course.PeriodId)) throw new ArgumentException("课程对应的时段不存在。删除时段前，请先调整该时段的课程。");
            if (course.Color is < 0 or > 5) throw new ArgumentException("课程颜色无效。");
            foreach (int week in WeekPattern.Parse(course.Weeks, data.TotalWeeks))
                if (!occupied.Add((course.Day, course.PeriodId, week))) throw new ArgumentException($"“{course.Name}”在第 {week} 周与已有课程冲突；可用单双周错开。");
        }
        foreach (var memo in data.Memos)
        {
            if (string.IsNullOrWhiteSpace(memo.Event) || memo.Event.Length > 500) throw new ArgumentException("备忘录事件须为 1–500 字。");
            if (memo.Start.Year is < 1900 or > 2100 || memo.End?.Year is < 1900 or > 2100) throw new ArgumentException("备忘录日期须在 1900–2100 年内。");
            if (memo.End is not null && memo.End <= memo.Start) throw new ArgumentException("结束时间必须晚于开始时间，可选择跨天时间段。");
        }
    }
    public static TimeOnly ParseTime(string value) => TimeOnly.TryParseExact(value.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : throw new ArgumentException("时间格式请用 24 小时制，例如 08:00 或 14:30。");
}
