using QingLan.Core;

var root = Path.Combine(Path.GetTempPath(), "QingLanCampusTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var store = new CampusStore(root); store.Load();
int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
void Reject(Action action, string label) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, label); }
Check(store.IsReady && !store.Data.SemesterConfigured && !File.Exists(Path.Combine(root, "campus.json")), "First launch requires semester setup, does not invent saved data");
Reject(() => store.SaveCourse(new("x", "未设置学期", "", "", 1, "p1", "每周")), "Cannot add course before semester setup");
store.SetSemester(new(2026, 9, 2), new(2027, 1, 15));
Check(store.Data.FirstMonday == new DateOnly(2026, 8, 31) && store.Data.TotalWeeks == 20, "Start/end dates compute partial first/final weeks across year");
Check(store.WeekFor(new(2026, 9, 6)) == 1 && store.WeekFor(new(2026, 9, 7)) == 2 && store.WeekFor(new(2026, 8, 30)) == 0, "Academic week changes on Monday, independent of calendar year");
Reject(() => store.SetSemester(new(2026, 10, 1), new(2026, 9, 1)), "Invalid semester interval rejected");
Reject(() => store.SetSemester(new(2026, 1, 1), new(2028, 1, 1)), "Overlong semester rejected");
Check(WeekPattern.Parse("单周", 20).SequenceEqual(Enumerable.Range(1, 20).Where(w => w % 2 == 1)), "Odd weeks");
Check(WeekPattern.Parse("双周", 20).SequenceEqual(Enumerable.Range(1, 20).Where(w => w % 2 == 0)), "Even weeks");
Check(WeekPattern.Parse("1-4周，7、9-12双周", 20).SequenceEqual(new[] { 1, 2, 3, 4, 7, 10, 12 }), "Mixed Chinese separators, ranges and parity");
Check(WeekPattern.Parse("1-8单周", 20).SequenceEqual(new[] {1, 3, 5, 7}), "Parity within custom range");
Check(WeekPattern.Parse("每周", 3).SequenceEqual(new[] {1, 2, 3}), "Every-week follows semester length");
foreach (var invalid in new[] { "", "0", "21", "4-1", "1,,2", "1-", "abc", "2单周", "99999999999999999", "1,2," }) Reject(() => WeekPattern.Parse(invalid, 20), "Reject invalid weeks: " + invalid);
var odd = new CampusCourse("odd", "高等数学", "陈老师", "教学楼 A201", 3, "p1", "单周", 0);
var even = new CampusCourse("even", "大学英语", "林老师", "B308", 3, "p1", "双周", 2);
store.SaveCourse(odd); store.SaveCourse(even);
Check(store.Data.Courses.Length == 2, "Different odd/even courses share a cell");
Reject(() => store.SaveCourse(new("conflict", "课程冲突", "", "", 3, "p1", "3-5")), "Actual intersecting weeks rejected");
Check(store.Data.Courses.Length == 2, "Failed course save preserves state");
store.SaveCourse(odd with { Teacher = "王老师", Room = "C101", Weeks = "1,3" });
Check(store.Data.Courses.Length == 2 && store.Data.Courses.Single(c => c.Id == "odd").Teacher == "王老师", "Course edits replace, not duplicate");
Reject(() => store.SetPeriods(store.Data.Periods.Where(p => p.Id != "p1")), "Cannot remove a period used by a course");
Reject(() => store.SetPeriods([new("a", "上午", new(8, 0), new(10, 0)), new("b", "重叠", new(9, 0), new(11, 0))]), "Overlapping custom periods rejected");
Reject(() => store.SetPeriods([new("p1", "倒序", new(10, 0), new(9, 0))]), "Reversed period rejected");
store.SetPeriods(store.Data.Periods.Select(p => p.Id == "p1" ? p with { Name = "早八连堂", Start = new(8, 10), End = new(9, 40) } : p));
Check(store.Data.Courses.Length == 2 && store.Data.Periods[0].Name == "早八连堂", "Period edit preserves course links");
var at = new DateTime(2026, 9, 13, 10, 0, 0);
foreach (int i in new[] {4, 2, 0, 3, 1}) store.SaveMemo(new("memo" + i, "事项 " + i, at.AddHours(i), i == 2 ? at.AddDays(1) : null));
Check(store.Upcoming().Select(m => m.Id).SequenceEqual(new[] { "memo0", "memo1", "memo2" }), "Earliest three memos sorted independently of creation order");
store.DeleteMemo("memo1");
Check(store.Upcoming().Select(m => m.Id).SequenceEqual(new[] { "memo0", "memo2", "memo3" }), "Confirmed deletion fills next upcoming slot");
var reopened = new CampusStore(root); reopened.Load();
Check(reopened.IsReady && reopened.Upcoming().All(m => m.Id != "memo1") && reopened.Data.Memos.Length == 4, "Deleted memo never reappears after restart");
Check(reopened.Data.Memos.Single(m => m.Id == "memo2").End == at.AddDays(1), "Cross-day event interval persists");
reopened.SaveMemo(new("memo0", "已修改事项", at.AddHours(8)));
Check(reopened.Data.Memos.Length == 4 && reopened.Upcoming()[0].Id == "memo2", "Memo edits reorder without duplication");
Reject(() => reopened.SaveMemo(new("bad", "", at)), "Empty event rejected");
Reject(() => reopened.SaveMemo(new("bad", "倒序", at, at.AddMinutes(-1))), "Invalid event interval rejected");
Reject(() => reopened.SaveMemo(new("bad", "零长度区间", at, at)), "Zero-length interval rejected");
Reject(() => reopened.SetSemester(new(2026, 9, 2), new(2026, 9, 3)), "Shortening term refuses out-of-range existing course weeks");
reopened.DeleteCourse("odd");
var afterDelete = new CampusStore(root); afterDelete.Load();
Check(afterDelete.Data.Courses.All(c => c.Id != "odd"), "Course deletion persists");
bool stale = false;
try { store.DeleteMemo("memo4"); } catch (IOException) { stale = true; }
Check(stale, "Stale second app cannot overwrite newer edits or resurrect deletions");
string contents = File.ReadAllText(Path.Combine(root, "campus.json"));
int before = afterDelete.Data.Memos.Length;
using (var locked = new FileStream(Path.Combine(root, "campus.json"), FileMode.Open, FileAccess.Read, FileShare.None))
{
    bool failed = false;
    try { afterDelete.DeleteMemo("memo2"); } catch (IOException) { failed = true; }
    Check(failed && afterDelete.Data.Memos.Length == before, "Failed delete keeps memory and disk unchanged");
}
Check(File.ReadAllText(Path.Combine(root, "campus.json")) == contents && Directory.GetFiles(root, "*.tmp").Length == 0, "Failed save leaves original JSON and no temporary fragments");
var brokenDir = Path.Combine(root, "broken"); Directory.CreateDirectory(brokenDir);
File.WriteAllText(Path.Combine(brokenDir, "campus.json"), "{broken");
var broken = new CampusStore(brokenDir); broken.Load();
Check(!broken.IsReady && broken.LoadError is not null, "Corrupt file surfaces an error, never silently clears data");
bool blocked = false;
try { broken.SaveMemo(new("x", "不能覆盖", at)); } catch (IOException) { blocked = true; }
Check(blocked && File.ReadAllText(Path.Combine(brokenDir, "campus.json")) == "{broken", "Corrupt data cannot be overwritten by editor");
Check(CampusStore.ParseTime("8:00") == new TimeOnly(8, 0) && CampusStore.ParseTime("23:59") == new TimeOnly(23, 59), "Custom 24-hour times parse");
Reject(() => CampusStore.ParseTime("24:60"), "Invalid clock rejected");
var markers = new CampusStore(Path.Combine(root, "markers")); markers.Load();
markers.SaveMemo(new("single", "周日事项", new(2026, 9, 13, 12, 0, 0)));
markers.SaveMemo(new("range", "跨周事项", new(2026, 9, 13, 23, 0, 0), new(2026, 9, 15, 0, 0, 0)));
Check(markers.MemoCountOn(new(2026, 9, 13)) == 2 && markers.MemoCountOn(new(2026, 9, 14)) == 1, "Underline counts single and cross-day events on their actual dates");
Check(markers.MemoCountOn(new(2026, 9, 15)) == 0, "Midnight interval end does not mark the following day");
Check(markers.MemoCountOn(new(2026, 9, 6)) == 0 && markers.MemoCountOn(new(2026, 9, 20)) == 0, "Same weekday in another week is not marked");
markers.DeleteMemo("single");
Check(markers.MemoCountOn(new(2026, 9, 13)) == 1, "Deleting one event keeps underline if another event remains");
markers.DeleteMemo("range");
Check(markers.MemoCountOn(new(2026, 9, 13)) == 0 && markers.MemoCountOn(new(2026, 9, 14)) == 0, "Deleting last event removes all affected day markers");
var markerRestart = new CampusStore(Path.Combine(root, "markers")); markerRestart.Load();
Check(markerRestart.MemoCountOn(new(2026, 9, 13)) == 0, "Deleted date markers do not return after restart");
Console.WriteLine($"ALL {checks} CHECKS PASSED. Isolated fixtures: {root}");
if (args.Contains("--ui-fixture"))
{
    var ui = new CampusStore(Path.Combine(root, "ui")); ui.Load();
    ui.SetSemester(new(2026, 8, 31), new(2027, 1, 17));
    ui.SaveCourse(new("math", "高等数学", "陈老师", "教学楼 A201", 1, "p1", "每周", 0));
    ui.SaveCourse(new("english", "大学英语", "林老师", "外语楼 308", 2, "p2", "1-16", 2));
    ui.SaveCourse(new("code", "程序设计", "周老师", "实验楼 402", 3, "p3", "双周", 1));
    ui.SaveCourse(new("sport", "大学体育", "刘老师", "体育馆", 4, "p4", "单周", 3));
    ui.SaveCourse(new("physics", "大学物理", "李老师", "教学楼 B102", 5, "p1", "每周", 5));
    ui.SaveCourse(new("art", "艺术鉴赏", "赵老师", "人文楼 201", 5, "p5", "1-4,7-12", 4));
    ui.SaveMemo(new("m1", "提交高数作业", DateTime.Now.AddHours(2)));
    ui.SaveMemo(new("m2", "英语学习小组 · 图书馆二楼", DateTime.Now.AddHours(4), DateTime.Now.AddHours(5)));
    ui.SaveMemo(new("m3", "社团招新面试", DateTime.Now.AddDays(1)));
    ui.SaveMemo(new("m4", "领取新教材", DateTime.Now.AddDays(2)));
    Console.WriteLine("UI_FIXTURE=" + Path.Combine(root, "ui"));
}
