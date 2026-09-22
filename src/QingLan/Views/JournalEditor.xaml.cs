using Microsoft.UI.Xaml.Controls;
using QingLan.Core;
namespace QingLan.Views;
public sealed partial class JournalEditor : ContentDialog
{
    private readonly Library library;
    private bool saved;
    private bool discardArmed;
    public JournalEditor(Library library, Article? source)
    {
        InitializeComponent(); this.library = library;
        ContextLabel.Text = source is null ? "一页心事，一封未来的信。" : "读《" + source.Title + "》有感";
        WrittenDate.Text = DateTime.Now.ToString("写于 yyyy 年 M 月 d 日");
        IsPrimaryButtonEnabled = false;
    }
    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Count is null) return;
        Count.Text = $"{Editor.Text.Length} / 30000";
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(Editor.Text);
        discardArmed = false; CloseButtonText = "暂不保存";
    }
    private void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try { library.AddJournal(Editor.Text, DateTime.Now); saved = true; }
        catch (Exception ex) { args.Cancel = true; ErrorMessage.Message = "保存未完成，文字仍在这里。" + ex.Message; ErrorMessage.IsOpen = true; }
    }
    private void Editor_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (!saved && !string.IsNullOrWhiteSpace(Editor.Text) && !discardArmed)
        {
            args.Cancel = true; discardArmed = true; CloseButtonText = "确认放弃文字";
            ErrorMessage.Severity = InfoBarSeverity.Warning;
            ErrorMessage.Message = "这一页尚未保存。继续写，或再次点击“确认放弃文字”。"; ErrorMessage.IsOpen = true;
        }
    }
}
