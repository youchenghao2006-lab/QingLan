using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QingLan.Core;

namespace QingLan.Views;

public sealed partial class CampusPage : Page
{
    private MainWindow? shell;
    private CampusStore Store => shell!.Campus;
    private int week = 1;
    private bool allMemos;
    private bool allCourses;
    private bool dialogOpen;
    private ContentDialog? activeDialog;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private static readonly string[] Days = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
    private static readonly string[] Tints = ["#DDEEEF", "#E6EBDC", "#E8E4F3", "#F5E9D8", "#F1E0E5", "#DFE9F8"];
    private static readonly string[] ColorNames = ["湖蓝", "草木绿", "浅藤紫", "暖杏", "樱花粉", "晴空蓝"];
    public CampusPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => DateBadge.Visibility = e.NewSize.Width < 670 ? Visibility.Collapsed : Visibility.Visible;
        timer.Tick += (_, _) => { if (!dialogOpen) Render(); };
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        shell = ((PageContext)e.Parameter).Shell;
        Store.Load();
        week = Math.Clamp(Store.WeekFor(DateOnly.FromDateTime(DateTime.Today)), 1, Store.Data.TotalWeeks);
        Render(); timer.Start();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { timer.Stop(); activeDialog?.Hide(); }
    private static SolidColorBrush Brush(string hex)
    {
        uint rgb = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return new(Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    }
    private static TextBlock Text(string value, double size = 14, string color = "#223F49") => new() { Text = value, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap };
    private static void Place(Grid grid, FrameworkElement child, int row, int column) { Grid.SetRow(child, row); Grid.SetColumn(child, column); grid.Children.Add(child); }
    private void Render()
    {
        if (shell is null) return;
        CampusError.IsOpen = !Store.IsReady; CampusError.Message = Store.LoadError;
        CampusContent.Visibility = Store.IsReady && Store.Data.SemesterConfigured ? Visibility.Visible : Visibility.Collapsed;
        SetupPanel.Visibility = Store.IsReady && !Store.Data.SemesterConfigured ? Visibility.Visible : Visibility.Collapsed;
        RetryLoadButton.Visibility = Store.IsReady ? Visibility.Collapsed : Visibility.Visible;
        if (!Store.IsReady) return;
        var data = Store.Data;
        week = Math.Clamp(week, 1, data.TotalWeeks);
        var today = DateOnly.FromDateTime(DateTime.Today);
        int current = Store.WeekFor(today);
        string semesterState = today < data.SemesterStart ? "学期尚未开始" : today > data.SemesterEnd ? "本学期已结束" : $"当前学期第 {current} 周 · {(current % 2 == 0 ? "双周" : "单周")}";
        SemesterSummary.Text = data.SemesterConfigured ? $"{semesterState}   /   {data.SemesterStart:yyyy.MM.dd} — {data.SemesterEnd:yyyy.MM.dd}   /   共 {data.TotalWeeks} 周" : "尚未设置学期 · 请先选择学期开始和结束日期";
        TodayNumber.Text = today.ToString("MM.dd"); TodayWeekday.Text = Days[((int)today.DayOfWeek + 6) % 7];
        if (!data.SemesterConfigured) return;
        WeekLabel.Text = $"第 {week} 周 · {(week % 2 == 0 ? "双周" : "单周")}";
        PreviousWeek.IsEnabled = week > 1; NextWeek.IsEnabled = week < data.TotalWeeks;
        var monday = data.FirstMonday.AddDays((week - 1) * 7);
        WeekHint.Text = $"{monday:MM.dd} — {monday.AddDays(6):MM.dd}  ·  橙色下划线：当天有备忘录  ·  点击格子添加或编辑课程，窄窗口可左右滑动。";
        Timetable.Children.Clear(); Timetable.RowDefinitions.Clear(); Timetable.ColumnDefinitions.Clear();
        Timetable.ColumnDefinitions.Add(new() { Width = new GridLength(84) });
        for (int day = 1; day <= 7; day++) Timetable.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        Timetable.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Place(Timetable, Text("时段", 12, "#657D84"), 0, 0);
        for (int day = 1; day <= 7; day++)
        {
            var date = monday.AddDays(day - 1);
            var heading = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            heading.Children.Add(Text(Days[day - 1], 14)); heading.Children.Add(Text(date.ToString("MM.dd") + (date == today ? " · 今天" : ""), 11, "#657D84"));
            int memoCount = Store.MemoCountOn(date);
            var marker = new Border { Height = 4, Width = 44, CornerRadius = new(2), Background = memoCount > 0 ? Brush("#D97542") : new SolidColorBrush(Colors.Transparent), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            heading.Children.Add(marker);
            var dayHeader = new Border { Child = heading, Padding = new Thickness(6, 10, 6, 10), Background = date == today ? Brush("#DDEEEF") : null, CornerRadius = new(12) };
            string memoHint = $"{date:yyyy.MM.dd} {Days[day - 1]} · {(memoCount > 0 ? $"{memoCount} 项备忘录" : "暂无备忘录")}";
            AutomationProperties.SetName(dayHeader, memoHint); AutomationProperties.SetName(marker, memoCount > 0 ? "备忘录日期标记" : "");
            ToolTipService.SetToolTip(dayHeader, memoHint);
            Place(Timetable, dayHeader, 0, day);
        }
        int row = 1;
        foreach (var period in data.Periods.OrderBy(p => p.Start))
        {
            Timetable.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var times = new StackPanel { Spacing = 7, Margin = new Thickness(6, 18, 4, 12) };
            times.Children.Add(Text(period.Name, 13)); times.Children.Add(Text($"{period.Start:HH:mm}\n{period.End:HH:mm}", 12, "#657D84"));
            Place(Timetable, times, row, 0);
            for (int day = 1; day <= 7; day++)
            {
                int selectedDay = day;
                var course = data.Courses.FirstOrDefault(c => c.Day == day && c.PeriodId == period.Id && WeekPattern.Parse(c.Weeks, data.TotalWeeks).Contains(week));
                var cell = new Button { MinHeight = 132, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(11, 13, 9, 12),
                    BorderThickness = new Thickness(0), Background = Brush(course is null ? "#F0F5F3" : Tints[course.Color]) };
                var cellDate = monday.AddDays(day - 1);
                if (cellDate < data.SemesterStart || cellDate > data.SemesterEnd) { course = null; cell.IsEnabled = false; cell.Opacity = 0.4; }
                if (course is null) cell.Content = new TextBlock { Text = "＋", Foreground = Brush("#B1C5C3"), FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 31, 0, 0) };
                else
                {
                    var content = new StackPanel { Spacing = 7 };
                    var name = Text(course.Name, 15); name.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; name.MaxLines = 3; name.TextTrimming = TextTrimming.CharacterEllipsis;
                    content.Children.Add(name);
                    if (course.Teacher.Length > 0) content.Children.Add(Text(course.Teacher, 12, "#526E75"));
                    if (course.Room.Length > 0) content.Children.Add(Text(course.Room, 12, "#526E75"));
                    content.Children.Add(Text(course.Weeks, 11, "#657D84")); cell.Content = content;
                }
                string label = !cell.IsEnabled ? "学期日期范围之外" : course is null ? $"添加{Days[day - 1]} {period.Name}课程" : $"{course.Name}，{course.Teacher}，{course.Room}，{course.Weeks}，点击编辑";
                AutomationProperties.SetName(cell, label); ToolTipService.SetToolTip(cell, label);
                cell.Click += async (_, _) => await EditCourse(course, selectedDay, period.Id);
                Place(Timetable, cell, row, day);
            }
            row++;
        }
        AllCoursesButton.Content = $"{(allCourses ? "收起" : "展开")}全部课程（{data.Courses.Length}） · 包含其他周次";
        AllCoursesPanel.Visibility = allCourses ? Visibility.Visible : Visibility.Collapsed; AllCoursesPanel.Children.Clear();
        if (allCourses)
        {
            if (data.Courses.Length == 0) AllCoursesPanel.Children.Add(Text("还没有课程。先设置学期和时段，再点击空格添加吧。", 13, "#657D84"));
            foreach (var course in data.Courses.OrderBy(c => c.Day).ThenBy(c => data.Periods.First(p => p.Id == c.PeriodId).Start))
            {
                var period = data.Periods.First(p => p.Id == course.PeriodId);
                var button = new Button { Content = Text($"{course.Name}   ·   {Days[course.Day - 1]} {period.Name}   ·   {course.Weeks}\n{course.Teacher}   {course.Room}"), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brush(Tints[course.Color]) };
                button.Click += async (_, _) => await EditCourse(course); AllCoursesPanel.Children.Add(button);
            }
        }
        RenderMemos();
    }
    private void RenderMemos()
    {
        MemoList.Children.Clear();
        var memos = Store.Upcoming(allMemos ? 5000 : 3);
        MemoCount.Text = $"共 {Store.Data.Memos.Length} 项待处理 · {(allMemos ? "显示全部" : "显示最早的三项")}";
        AllMemosButton.Content = allMemos ? "收起，只看最近三项 ↑" : $"查看全部备忘录（{Store.Data.Memos.Length}） →";
        AllMemosButton.Visibility = Store.Data.Memos.Length > 3 || allMemos ? Visibility.Visible : Visibility.Collapsed;
        if (memos.Count == 0) MemoList.Children.Add(new Border { Background = Brush("#F1F6F3"), CornerRadius = new(14), Padding = new Thickness(20), Child = Text("暂时没有待办。把考试、社团活动或小约定记在这里吧。", 14, "#657D84") });
        foreach (var memo in memos)
        {
            var now = DateTime.Now;
            bool ongoing = memo.End is not null && memo.Start <= now && memo.End >= now;
            string state = ongoing ? "进行中" : (memo.End ?? memo.Start) < now ? "已过时间 · 待处理" : "即将到来";
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var content = new StackPanel { Spacing = 7 };
            content.Children.Add(Text(state, 11, ongoing ? "#326B7B" : state.StartsWith("已过") ? "#9A663A" : "#657D84"));
            var title = Text(memo.Event, 16); title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; title.MaxLines = 3; title.TextTrimming = TextTrimming.CharacterEllipsis; content.Children.Add(title);
            content.Children.Add(Text(FormatMemoTime(memo), 13, "#657D84"));
            var edit = new Button { Content = content, Background = Brush("#F2F6F4"), BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16, 14, 12, 14) };
            edit.Click += async (_, _) => await EditMemo(memo); AutomationProperties.SetName(edit, "编辑备忘录：" + memo.Event);
            var done = new Button { Content = "✓", FontSize = 18, Padding = new Thickness(13, 9, 13, 9), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(done, "确认后删除备忘录：" + memo.Event); ToolTipService.SetToolTip(done, "处理并删除（需确认）");
            done.Click += async (_, _) => await DeleteMemo(memo);
            Place(grid, edit, 0, 0); Place(grid, done, 0, 1); MemoList.Children.Add(grid);
        }
    }
    private static string FormatMemoTime(CampusMemo memo) => memo.End is null ? memo.Start.ToString("yyyy.MM.dd  HH:mm") : memo.Start.Date == memo.End.Value.Date ? $"{memo.Start:yyyy.MM.dd  HH:mm} — {memo.End:HH:mm}" : $"{memo.Start:yyyy.MM.dd  HH:mm} — {memo.End:yyyy.MM.dd  HH:mm}";
    private void PreviousWeek_Click(object sender, RoutedEventArgs e) { week--; Render(); }
    private void NextWeek_Click(object sender, RoutedEventArgs e) { week++; Render(); }
    private void TodayWeek_Click(object sender, RoutedEventArgs e) { week = Store.WeekFor(DateOnly.FromDateTime(DateTime.Today)); Render(); }
    private void AllCourses_Click(object sender, RoutedEventArgs e) { allCourses = !allCourses; Render(); }
    private void AllMemos_Click(object sender, RoutedEventArgs e) { allMemos = !allMemos; RenderMemos(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) { Store.Load(); Render(); }
    private async void AddCourse_Click(object sender, RoutedEventArgs e) => await EditCourse();
    private async void AddMemo_Click(object sender, RoutedEventArgs e) => await EditMemo();
    private async void Semester_Click(object sender, RoutedEventArgs e) => await EditSemester();
    private async void Periods_Click(object sender, RoutedEventArgs e) => await EditPeriods();

    private async Task<ContentDialogResult> ShowDialog(ContentDialog dialog)
    {
        if (dialogOpen || !IsLoaded) return ContentDialogResult.None;
        dialogOpen = true; activeDialog = dialog;
        dialog.XamlRoot = XamlRoot; dialog.RequestedTheme = ElementTheme.Light;
        try { return await dialog.ShowAsync(); }
        catch (Exception ex) { shell?.ShowNotice("对话框暂时无法打开：" + ex.Message, true); return ContentDialogResult.None; }
        finally { dialogOpen = false; activeDialog = null; if (IsLoaded) Render(); }
    }
    private static (ContentDialog Dialog, StackPanel Form, InfoBar Error) Editor(string title)
    {
        var form = new StackPanel { Spacing = 14, MinWidth = 280, MaxWidth = 520 };
        var error = new InfoBar { IsOpen = false, IsClosable = true, Severity = InfoBarSeverity.Error };
        form.Children.Add(error);
        var dialog = new ContentDialog { Title = title, PrimaryButtonText = "保存", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 460 } };
        return (dialog, form, error);
    }
    private static void SaveOnPrimary(ContentDialog dialog, InfoBar error, Action save) => dialog.PrimaryButtonClick += (_, args) =>
    {
        try { save(); } catch (Exception ex) { args.Cancel = true; error.Message = ex.Message; error.IsOpen = true; if (dialog.Content is ScrollViewer scroll) scroll.ChangeView(null, 0, null, true); }
    };
    private static TextBox Field(string header, string value, int max = 80) => new() { Header = header, Text = value, MaxLength = max, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static ComboBox Select(string header, IEnumerable<string> items, int index)
    {
        var box = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) box.Items.Add(item);
        box.SelectedIndex = index; return box;
    }
    private static CalendarDatePicker DateField(string header, DateTime date) => new() { Header = header, Date = new DateTimeOffset(date.Date), DateFormat = "{year.full}年{month.integer}月{day.integer}日", MinDate = new DateTimeOffset(new DateTime(1900, 1, 1)), MaxDate = new DateTimeOffset(new DateTime(2100, 12, 31)), HorizontalAlignment = HorizontalAlignment.Stretch };
    private async Task EditSemester()
    {
        var (dialog, form, error) = Editor("学期设置");
        form.Children.Add(Text("包含开学日的那一周算第一周，每周从周一到周日；总周数自动计算。周次变短时，请先调整超出范围的课程。", 13, "#657D84"));
        var start = DateField("学期开始日期", Store.Data.SemesterStart.ToDateTime(TimeOnly.MinValue));
        var end = DateField("学期结束日期", Store.Data.SemesterEnd.ToDateTime(TimeOnly.MinValue));
        var preview = Text("", 13, "#326B7B");
        void Preview()
        {
            if (start.Date is null || end.Date is null) { preview.Text = "请选择起止日期"; return; }
            var first = DateOnly.FromDateTime(start.Date.Value.DateTime); var last = DateOnly.FromDateTime(end.Date.Value.DateTime);
            preview.Text = last < first ? "结束日期不能早于开始日期" : $"共 {(last.DayNumber - CampusStore.Monday(first).DayNumber) / 7 + 1} 周 · 第一周周一为 {CampusStore.Monday(first):yyyy.MM.dd}";
        }
        start.DateChanged += (_, _) => Preview(); end.DateChanged += (_, _) => Preview(); Preview();
        form.Children.Add(start); form.Children.Add(end); form.Children.Add(preview);
        SaveOnPrimary(dialog, error, () =>
        {
            if (start.Date is null || end.Date is null) throw new ArgumentException("请选择学期开始和结束日期。");
            Store.SetSemester(DateOnly.FromDateTime(start.Date.Value.DateTime), DateOnly.FromDateTime(end.Date.Value.DateTime));
            week = Math.Clamp(Store.WeekFor(DateOnly.FromDateTime(DateTime.Today)), 1, Store.Data.TotalWeeks);
        });
        await ShowDialog(dialog);
    }
    private async Task EditCourse(CampusCourse? course = null, int day = 1, string? periodId = null)
    {
        if (dialogOpen || !Store.IsReady) return;
        var (dialog, form, error) = Editor(course is null ? "添加课程" : "编辑课程");
        var periods = Store.Data.Periods.OrderBy(p => p.Start).ToArray();
        var name = Field("课程名称 *", course?.Name ?? "");
        var teacher = Field("老师", course?.Teacher ?? "", 60); var room = Field("教室", course?.Room ?? "");
        var days = Select("星期", Days, (course?.Day ?? day) - 1);
        var period = Select("上课时段", periods.Select(p => $"{p.Name}  {p.Start:HH:mm}–{p.End:HH:mm}"), Math.Max(0, Array.FindIndex(periods, p => p.Id == (course?.PeriodId ?? periodId))));
        var weeks = Field("上课周次 *", course?.Weeks ?? "每周", 200);
        var colors = Select("课程色签", ColorNames, course?.Color ?? 0);
        var people = new Grid { ColumnSpacing = 12 }; people.ColumnDefinitions.Add(new()); people.ColumnDefinitions.Add(new()); Place(people, teacher, 0, 0); Place(people, room, 0, 1);
        var when = new Grid { ColumnSpacing = 12 }; when.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); when.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) }); Place(when, days, 0, 0); Place(when, period, 0, 1);
        foreach (var element in new UIElement[] { name, people, when, weeks }) form.Children.Add(element);
        form.Children.Add(Text("支持：每周 / 单周 / 双周 / 1-4周 / 1-8单周 / 1-4,7,9-12。\n同一时段可添加单双周不同的课程，实际周次重叠会提示冲突。", 12, "#657D84"));
        form.Children.Add(colors);
        SaveOnPrimary(dialog, error, () =>
        {
            if (days.SelectedIndex < 0 || period.SelectedIndex < 0 || colors.SelectedIndex < 0) throw new ArgumentException("请选择星期、时段和颜色。");
            Store.SaveCourse(new(course?.Id ?? Guid.NewGuid().ToString("N"), name.Text, teacher.Text, room.Text, days.SelectedIndex + 1, periods[period.SelectedIndex].Id, weeks.Text, colors.SelectedIndex));
        });
        if (course is not null) dialog.SecondaryButtonText = "删除课程…";
        if (await ShowDialog(dialog) == ContentDialogResult.Secondary && course is not null)
            await ConfirmDelete("删除这门课程？", $"“{course.Name}”在所有周次的安排都会删除。", () => Store.DeleteCourse(course.Id));
    }
    private async Task EditMemo(CampusMemo? memo = null)
    {
        if (dialogOpen || !Store.IsReady) return;
        var (dialog, form, error) = Editor(memo is null ? "记一件事" : "编辑备忘录");
        var startTime = memo?.Start ?? DateTime.Now.AddHours(1);
        var endTime = memo?.End ?? startTime.AddHours(1);
        var title = Field("事件 *", memo?.Event ?? "", 500); title.AcceptsReturn = true; title.TextWrapping = TextWrapping.Wrap; title.MinHeight = 90;
        var date = DateField("开始日期", startTime); var time = Field("开始时间（24 小时制）", startTime.ToString("HH:mm"), 5);
        var range = new CheckBox { Content = "这是一个时间段（可跨天）", IsChecked = memo?.End is not null };
        var end = new StackPanel { Spacing = 14, Visibility = range.IsChecked == true ? Visibility.Visible : Visibility.Collapsed };
        var endDate = DateField("结束日期", endTime); var endClock = Field("结束时间（24 小时制）", endTime.ToString("HH:mm"), 5);
        end.Children.Add(endDate); end.Children.Add(endClock);
        range.Checked += (_, _) => end.Visibility = Visibility.Visible; range.Unchecked += (_, _) => end.Visibility = Visibility.Collapsed;
        foreach (var element in new UIElement[] { title, date, time, range, end }) form.Children.Add(element);
        form.Children.Add(Text("单次事项，不自动重复。保存后按时间排列；确认删除后不再出现。", 12, "#657D84"));
        SaveOnPrimary(dialog, error, () =>
        {
            if (date.Date is null) throw new ArgumentException("请选择开始日期。");
            DateTime start = date.Date.Value.Date.Add(CampusStore.ParseTime(time.Text).ToTimeSpan());
            DateTime? finish = null;
            if (range.IsChecked == true)
            {
                if (endDate.Date is null) throw new ArgumentException("请选择结束日期。");
                finish = endDate.Date.Value.Date.Add(CampusStore.ParseTime(endClock.Text).ToTimeSpan());
            }
            Store.SaveMemo(new(memo?.Id ?? Guid.NewGuid().ToString("N"), title.Text, start, finish));
        });
        await ShowDialog(dialog);
    }
    private async Task DeleteMemo(CampusMemo memo) => await ConfirmDelete("处理并删除这项备忘录？", $"{memo.Event}\n{FormatMemoTime(memo)}\n\n确认后会从近期列表和全部备忘录中删除，重新打开也不会再次出现。", () => Store.DeleteMemo(memo.Id));
    private async Task ConfirmDelete(string title, string content, Action delete)
    {
        var (dialog, form, error) = Editor(title);
        form.Children.Add(Text(content)); dialog.PrimaryButtonText = "确认删除"; dialog.DefaultButton = ContentDialogButton.Close;
        SaveOnPrimary(dialog, error, delete); await ShowDialog(dialog);
    }
    private async Task EditPeriods()
    {
        if (dialogOpen || !Store.IsReady) return;
        var (dialog, form, error) = Editor("自定义上课时段");
        form.Children.Add(Text("每行是课表的一行，可设为一节课或连堂课。时间不能重叠；已有课程的时段须先调整课程再移除。", 13, "#657D84"));
        var rows = new StackPanel { Spacing = 16 };
        var fields = new List<(string Id, TextBox Name, TextBox Start, TextBox End, Border Row)>();
        void AddRow(ClassPeriod p)
        {
            var stack = new StackPanel { Spacing = 8 };
            var name = Field("时段名称", p.Name, 30);
            var start = Field("开始（HH:mm）", p.Start.ToString("HH:mm"), 5); var end = Field("结束（HH:mm）", p.End.ToString("HH:mm"), 5);
            var times = new Grid { ColumnSpacing = 12 }; times.ColumnDefinitions.Add(new()); times.ColumnDefinitions.Add(new()); Place(times, start, 0, 0); Place(times, end, 0, 1);
            var remove = new Button { Content = "移除此时段", Padding = new Thickness(10, 6, 10, 6), HorizontalAlignment = HorizontalAlignment.Right };
            stack.Children.Add(name); stack.Children.Add(times); stack.Children.Add(remove);
            var row = new Border { Background = Brush("#F1F6F3"), Padding = new Thickness(12), CornerRadius = new(12), Child = stack };
            fields.Add((p.Id, name, start, end, row)); rows.Children.Add(row);
            remove.Click += (_, _) =>
            {
                if (Store.Data.Courses.Any(c => c.PeriodId == p.Id)) { error.Message = "这个时段已有课程，请先编辑或删除相关课程。"; error.IsOpen = true; return; }
                fields.RemoveAll(f => f.Id == p.Id); rows.Children.Remove(row);
            };
        }
        foreach (var period in Store.Data.Periods.OrderBy(p => p.Start)) AddRow(period);
        form.Children.Add(rows);
        var add = new Button { Content = "＋ 增加一个时段", HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) =>
        {
            if (fields.Count >= 20) { error.Message = "最多可设置 20 个时段。"; error.IsOpen = true; return; }
            AddRow(new(Guid.NewGuid().ToString("N"), "新时段", new TimeOnly(21, 0), new TimeOnly(21, 45)));
        };
        form.Children.Add(add);
        SaveOnPrimary(dialog, error, () => Store.SetPeriods(fields.Select(f => new ClassPeriod(f.Id, f.Name.Text.Trim(), CampusStore.ParseTime(f.Start.Text), CampusStore.ParseTime(f.End.Text)))));
        await ShowDialog(dialog);
    }
}
