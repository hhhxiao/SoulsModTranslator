using System.ComponentModel;
using System.Windows;
using AdonisUI.Controls;
using SMT.core;

namespace SMT.WPF;

public partial class ProgressWindow : AdonisWindow
{
    public CancellationTokenSource CancellationTokenSource { get; } = new();
    public bool AllowClosingWithoutConfirm { get; set; }

    public ProgressWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClosingWithoutConfirm || CancellationTokenSource.IsCancellationRequested)
            return;

        var result = AdonisUI.Controls.MessageBox.Show(
            "确定要取消翻译吗？取消后可以保存已翻译的内容，并在下次继续。",
            "确认取消",
            AdonisUI.Controls.MessageBoxButton.YesNo,
            AdonisUI.Controls.MessageBoxImage.Warning);

        if (result == AdonisUI.Controls.MessageBoxResult.Yes)
        {
            CancelBtn.IsEnabled = false;
            CancelBtn.Content = "正在取消...";
            CancellationTokenSource.Cancel();
        }
        else
        {
            e.Cancel = true;
        }
    }

    public void Report(TranslationProgress progress)
    {
        ProgressLabel.Content = $"正在翻译：{progress.Current} / {progress.Total}";
        ProgressBar.Value = (double)progress.Current / progress.Total * 100;
        CurrentTextTextBox.Text = progress.CurrentText;
    }

    private void CancelBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var result = AdonisUI.Controls.MessageBox.Show(
            "确定要取消翻译吗？取消后可以保存已翻译的内容，并在下次继续。",
            "确认取消",
            AdonisUI.Controls.MessageBoxButton.YesNo,
            AdonisUI.Controls.MessageBoxImage.Warning);

        if (result == AdonisUI.Controls.MessageBoxResult.Yes)
        {
            CancelBtn.IsEnabled = false;
            CancelBtn.Content = "正在取消...";
            CancellationTokenSource.Cancel();
        }
    }
}
