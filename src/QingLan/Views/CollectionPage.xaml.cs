using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QingLan.Core;
namespace QingLan.Views;
public sealed partial class CollectionPage : Page
{
    private MainWindow? shell;
    private bool journals;
    public CollectionPage() { InitializeComponent(); }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var context = (PageContext)e.Parameter; shell = context.Shell; journals = context.Section == "journals";
        shell.LibraryChanged += Update; Update();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { if (shell is not null) shell.LibraryChanged -= Update; }
    private void Update()
    {
        if (shell is null) return;
        var items = journals ? shell.Library.Journals.OrderByDescending(n => n.Written).Select(n => n.AsArticle()).ToList()
            : shell.Library.All.Where(a => shell.Library.Favorites.Contains(a.Id)).Reverse().ToList();
        Heading.Text = journals ? "随心记" : "我的收藏";
        Subheading.Text = journals ? $"共 {items.Count} 页心事 · 写下的日期，会随文字一起留下。" : $"共 {items.Count} 篇珍藏 · 留住那些读来心动的文字。";
        WriteButton.Visibility = journals ? Visibility.Visible : Visibility.Collapsed;
        ArticleList.ItemsSource = items;
        EmptyPanel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = journals ? "此处，等你落笔" : "等一篇让你心动的文章";
        EmptyDescription.Text = journals ? "从主页或文章中写下随心记。它保存在这里，也会在未来某一天悄悄重现。" : "阅读时点一下收藏，喜欢的文字就会在这里等你。";
    }
    private void ArticleList_ItemClick(object sender, ItemClickEventArgs e) { if (e.ClickedItem is Article article) shell?.OpenArticle(article); }
    private void Read_Click(object sender, RoutedEventArgs e) { if (((MenuFlyoutItem)sender).Tag is Article article) shell?.OpenArticle(article); }
    private async void Write_Click(object sender, RoutedEventArgs e) { if (shell is not null) await shell.WriteJournal(); }
    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || ((MenuFlyoutItem)sender).Tag is not Article article) return;
        try { shell.Library.ToggleFavorite(article); shell.Changed(); } catch (Exception ex) { shell.ShowNotice("收藏保存失败：" + ex.Message, true); }
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (shell is null || ((MenuFlyoutItem)sender).Tag is not Article article) return;
        if (article.Kind == "journal") await shell.DeleteJournal(article);
        else shell.ShowNotice("这是散文收藏，可以使用“收藏 / 取消收藏”移出收藏夹。");
    }
}
