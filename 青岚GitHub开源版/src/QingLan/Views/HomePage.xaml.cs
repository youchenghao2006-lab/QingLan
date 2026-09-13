using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QingLan.Core;
namespace QingLan.Views;
public sealed partial class HomePage : Page
{
    private MainWindow? shell;
    private Article? today;
    public HomePage() { InitializeComponent(); SizeChanged += (_, e) => { bool wide = e.NewSize.Width >= 740; ArtColumn.Width = new GridLength(wide ? .9 : 0, GridUnitType.Star); ArtPanel.Visibility = wide ? Visibility.Visible : Visibility.Collapsed; ContentPanel.Margin = new Thickness(e.NewSize.Width < 650 ? 18 : 32, 22, e.NewSize.Width < 650 ? 18 : 32, 28); }; }
    protected override void OnNavigatedTo(NavigationEventArgs e) { shell = ((PageContext)e.Parameter).Shell; shell.LibraryChanged += Update; Update(); }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { if (shell is not null) shell.LibraryChanged -= Update; }
    private void Update()
    {
        if (shell is null) return;
        var date = DateOnly.FromDateTime(DateTime.Now);
        today = shell.Library.ForDate(date);
        DateLabel.Text = DateTime.Now.ToString("yyyy 年 M 月 d 日  ·  dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        TodayTitle.Text = today.Title; TodayAuthor.Text = today.Author + "   ·   " + today.ReadingTime;
        TodaySummary.Text = today.Summary;
        TodayTag.Text = today.Kind == "journal" ? "时 光 来 信  ·  惊 喜" : "今 日 一 文";
        TodayFavorite.Content = shell.Library.Favorites.Contains(today.Id) ? "已收藏 ★" : "收藏 ☆";
        SourceText.Text = shell.SourceStatus;
        HistoryList.ItemsSource = Enumerable.Range(1, 59).Select(i => { var d = date.AddDays(-i); return new HistoryItem(d.ToString("dd"), d.ToString("yyyy.MM"), shell.Library.ForDate(d)); }).ToList();
    }
    private void Read_Click(object sender, RoutedEventArgs e) { if (today is not null) shell?.OpenArticle(today); }
    private void History_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is HistoryItem item) shell?.OpenArticle(item.Article); }
    private async void Write_Click(object sender, RoutedEventArgs e) { if (shell is not null) await shell.WriteJournal(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) { if (shell is not null) await shell.RefreshOnline(true); }
    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || today is null) return;
        try { shell.Library.ToggleFavorite(today); shell.Changed(); } catch (Exception ex) { shell.ShowNotice("收藏保存失败：" + ex.Message, true); }
    }
}
public sealed record HistoryItem(string Day, string Month, Article Article);
