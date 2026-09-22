using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using QingLan.Core;
using QingLan.Views;

namespace QingLan;

public sealed partial class MainWindow : Window
{
    private readonly EssaySource source = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer dayTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Library library;
    private bool ready;
    private bool fetching;
    private DateOnly today = DateOnly.FromDateTime(DateTime.Now);
    public event Action? LibraryChanged;
    public string SourceStatus { get; private set; } = "正在连接在线文库…";
    public Library Library => library;
    public CampusStore Campus { get; }

    public MainWindow()
    {
        InitializeComponent();
        Title = "青岚 · 每日散文";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleRegion);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 860));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 34, 63, 73);
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 101, 125, 132);
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var size = AppWindow.Size;
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + Math.Max(0, (area.Width - size.Width) / 2), area.Y + Math.Max(0, (area.Height - size.Height) / 2)));
        var dataOverride = Environment.GetEnvironmentVariable("QINGLAN_DATA_DIR");
        // Keep the public edition separate from the original app and its private data.
        library = new Library(dataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QingLanCommunity"));
        Campus = new CampusStore(library.DataDirectory);
        Root.Loaded += Start;
        Navigation.PaneOpened += (_, _) => UpdatePanePoem();
        Navigation.PaneClosed += (_, _) => UpdatePanePoem();
        Navigation.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => UpdatePanePoem());
        dayTimer.Tick += async (_, _) =>
        {
            var now = DateOnly.FromDateTime(DateTime.Now);
            if (now == today) return;
            today = now;
            LibraryChanged?.Invoke();
            await RefreshOnline();
        };
        Closed += (_, _) => { lifetime.Cancel(); dayTimer.Stop(); source.Dispose(); };
    }

    private async void Start(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Start;
        try
        {
            // Legacy import is opt-in: the public sample library is not the original library.
            var legacy = Environment.GetEnvironmentVariable("QINGLAN_LEGACY_DIR") ?? Path.Combine(library.DataDirectory, "legacy-import");
            await Task.Run(() => library.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "builtins.json"), legacy));
            await Task.Run(Campus.Load);
            ready = true;
            LoadingPanel.Visibility = Visibility.Collapsed;
            OpenSection("home");
            dayTimer.Start();
            if (!string.IsNullOrEmpty(library.LoadNotice)) ShowNotice(library.LoadNotice);
            await RefreshOnline();
        }
        catch (Exception ex) { LoadingPanel.Visibility = Visibility.Collapsed; ShowNotice("书页暂时无法打开：" + ex.Message, true); }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (ready && args.SelectedItem is NavigationViewItem item) OpenSection(item.Tag?.ToString() ?? "home");
    }
    private void Navigation_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        UpdatePanePoem();
    }
    private void UpdatePanePoem()
    {
        if (PanePoemTitle is null) return;
        bool wide = Navigation.IsPaneOpen;
        PanePoem.Orientation = wide ? Orientation.Vertical : Orientation.Horizontal;
        PanePoem.HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        PanePoem.Margin = wide ? new Thickness(20, 0, 8, 24) : new Thickness(10, 0, 8, 24);
        PanePoemTitle.Text = wide ? "一日一文" : "一\n日\n一\n文";
        PanePoemSubtitle.Text = wide ? "与自己，慢慢相逢。" : "与\n自\n己\n，\n慢\n慢\n相\n逢\n。";
    }
    private void FullScreen_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args) { ToggleFullScreen(); args.Handled = true; }
    private void Escape_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen) { ToggleFullScreen(); args.Handled = true; }
    }
    private void OpenSection(string section)
    {
        var type = section switch { "home" => typeof(HomePage), "campus" => typeof(CampusPage), _ => typeof(CollectionPage) };
        Pages.Navigate(type, new PageContext(this, section), new EntranceNavigationTransitionInfo());
        Pages.BackStack.Clear();
    }
    public void OpenArticle(Article article) => Pages.Navigate(typeof(ReaderPage), new PageContext(this, "reader", article), new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    public void GoBack() { if (Pages.CanGoBack) Pages.GoBack(); else OpenSection("home"); }
    public void Changed() => LibraryChanged?.Invoke();
    public void ShowNotice(string message, bool error = false)
    {
        Notice.Severity = error ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        Notice.Message = message;
        Notice.IsOpen = true;
    }
    public async Task RefreshOnline(bool force = false)
    {
        if (fetching || !ready) return;
        if (!force && library.Online.Any(a => a.Date == today))
        {
            SourceStatus = "今日文章已下载 · 可离线阅读";
            Changed();
            return;
        }
        fetching = true;
        SourceStatus = "正在从维基文库寻找今日文章…";
        Changed();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            var article = await source.Fetch(today, timeout.Token);
            library.AddOnline(article);
            SourceStatus = "今日文章来自中文维基文库";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            SourceStatus = "暂未连上文库 · 正在展示已保存或离线文章";
            if (force && !lifetime.IsCancellationRequested) ShowNotice("在线取文未完成，可稍后重试。" + (ex is OperationCanceledException ? "连接超时。" : ex.Message));
        }
        catch (Exception ex) { SourceStatus = "在线文章保存失败"; ShowNotice(ex.Message, true); }
        finally { fetching = false; if (!lifetime.IsCancellationRequested) Changed(); }
    }

    public async Task WriteJournal(Article? article = null)
    {
        var dialog = new JournalEditor(library, article) { XamlRoot = Root.XamlRoot, RequestedTheme = ElementTheme.Light };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) { Changed(); ShowNotice("已收好这页心事。未来的某一天，它会成为与你重逢的一篇散文。"); }
    }
    public async Task DeleteJournal(Article article)
    {
        var confirm = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "删除这篇随心记？", Content = "它会同时从随心记、收藏和未来的惊喜中移除。", PrimaryButtonText = "删除", CloseButtonText = "留下", DefaultButton = ContentDialogButton.Close, RequestedTheme = ElementTheme.Light };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        try { library.DeleteJournal(article.Id); GoBackIfReading(article); Changed(); ShowNotice("这篇随心记已删除。"); }
        catch (Exception ex) { ShowNotice("删除未完成，内容仍然保留。" + ex.Message, true); }
    }
    private void GoBackIfReading(Article article) { if (Pages.Content is ReaderPage reader && reader.ArticleId == article.Id) GoBack(); }
    public void ToggleFullScreen()
    {
        AppWindow.SetPresenter(AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen);
    }
}

public sealed record PageContext(MainWindow Shell, string Section, Article? Article = null);
