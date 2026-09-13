using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QingLan.Core;
namespace QingLan.Views;
public sealed partial class ReaderPage : Page
{
    private MainWindow? shell;
    private Article? article;
    private bool readingOptionsOpen;
    public string? ArticleId => article?.Id;
    public ReaderPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => { var p = e.NewSize.Width < 650 ? 24 : 52; Paper.Padding = new Thickness(p, 38, p, 44); };
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var context = (PageContext)e.Parameter; shell = context.Shell; article = context.Article!;
        ArticleTitle.Text = article.Title;
        SourceTag.Text = article.Kind == "journal" ? "时 光 来 信" : article.SourceLabel;
        ArticleMeta.Text = article.Author + "   ·   " + article.ReadingTime;
        Body.Blocks.Clear();
        foreach (var part in article.Content.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 18) };
            paragraph.Inlines.Add(new Run { Text = part }); Body.Blocks.Add(paragraph);
        }
        SourceLink.Visibility = article.SourceUrl.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (Uri.TryCreate(article.SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "zh.wikisource.org") SourceLink.NavigateUri = uri;
        DeleteButton.Visibility = article.Kind == "journal" ? Visibility.Visible : Visibility.Collapsed;
        ApplyFont(); UpdateFavorite();
        shell.LibraryChanged += UpdateFavorite;
        ReaderScroll.ChangeView(null, 0, null, true);
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (shell is not null) shell.LibraryChanged -= UpdateFavorite;
    }
    private void UpdateFavorite() { if (shell is not null && article is not null) FavoriteButton.Label = shell.Library.Favorites.Contains(article.Id) ? "已收藏" : "收藏"; }
    private void Back_Click(object sender, RoutedEventArgs e) => shell?.GoBack();
    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || article is null) return;
        try { shell.Library.ToggleFavorite(article); shell.Changed(); } catch (Exception ex) { shell.ShowNotice("收藏保存失败：" + ex.Message, true); }
    }
    private async void Write_Click(object sender, RoutedEventArgs e) { if (shell is not null) await shell.WriteJournal(article); }
    private async void Delete_Click(object sender, RoutedEventArgs e) { if (shell is not null && article is not null) await shell.DeleteJournal(article); }
    private void FontSize_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || sender is not Button { Tag: string value } || !int.TryParse(value, out int level) || level is < 0 or > 3) return;
        SaveFont(shell.Library.Settings with { FontLevel = level });
    }
    private void FontFamily_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || sender is not Button { Tag: string family } || family is not ("宋体" or "楷体" or "仿宋")) return;
        SaveFont(shell.Library.Settings with { Font = family });
    }
    private void SaveFont(ReaderSettings settings)
    {
        if (shell is null) return;
        try
        {
            shell.Library.SetSettings(settings);
            ApplyFont();
        }
        catch (Exception ex) { shell.ShowNotice("字体设置保存失败：" + ex.Message, true); }
    }
    private void ApplyFont()
    {
        if (shell is null) return;
        var settings = shell.Library.Settings;
        double[] sizes = [18, 21, 25, 29];
        double size = sizes[Math.Clamp(settings.FontLevel, 0, 3)];
        var family = new FontFamily(settings.Font switch { "楷体" => "KaiTi", "仿宋" => "FangSong", _ => "SimSun" });
        Body.FontSize = size; Body.LineHeight = size * 1.9; Body.FontFamily = family;
        // Update existing text elements as well as the container, including cached pages.
        foreach (var block in Body.Blocks)
        {
            block.FontSize = size; block.FontFamily = family;
            if (block is Paragraph paragraph)
            {
                paragraph.LineHeight = size * 1.9;
                foreach (var inline in paragraph.Inlines) { inline.FontSize = size; inline.FontFamily = family; }
            }
        }
        Body.InvalidateMeasure();
        Button[] sizeButtons = [SmallSize, NormalSize, LargeSize, ExtraLargeSize];
        for (int i = 0; i < sizeButtons.Length; i++) MarkSelected(sizeButtons[i], i == settings.FontLevel);
        foreach (var button in new[] { SongFont, KaiFont, FangFont }) MarkSelected(button, button.Tag?.ToString() == settings.Font);
        FontStatus.Text = $"当前正文：{settings.Font} · {size:0} 号（已保存）";
    }
    private static void MarkSelected(Button button, bool selected)
    {
        button.Background = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 218, 235, 236) : Windows.UI.Color.FromArgb(190, 255, 255, 255));
        button.BorderBrush = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(255, 50, 107, 123) : Windows.UI.Color.FromArgb(112, 157, 183, 189));
        button.BorderThickness = new Thickness(selected ? 2 : 1);
    }
    private void ReadingOptions_Opened(object? sender, object e) { readingOptionsOpen = true; ApplyFont(); }
    private void ReadingOptions_Closed(object? sender, object e) { readingOptionsOpen = false; }
    private void ReaderScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) => ProgressLabel.Text = $"阅读 {(ReaderScroll.ScrollableHeight <= 0 ? 100 : Math.Clamp(ReaderScroll.VerticalOffset / ReaderScroll.ScrollableHeight * 100, 0, 100)):0}%";
    private void FullScreen_Click(object sender, RoutedEventArgs e) => shell?.ToggleFullScreen();
    private void FullScreen_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { shell?.ToggleFullScreen(); args.Handled = true; }
    private void Escape_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (readingOptionsOpen) { ReadingOptions.Hide(); args.Handled = true; return; }
        if (shell?.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen) shell.ToggleFullScreen();
        else shell?.GoBack(); args.Handled = true;
    }
}
