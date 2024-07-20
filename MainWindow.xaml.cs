using System.Text;
using System.Windows;
using System.Collections.ObjectModel;
using NLog;
using SoulsModTranslator.core;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.Forms.MessageBox;
using System.IO;
using AdonisUI.Controls;
using Button = System.Windows.Controls.Button;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using System.Diagnostics;
using System.Windows.Navigation;

namespace SoulsModTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        private static readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "db");
        private static readonly string GlossaryPath = Path.Combine(Directory.GetCurrentDirectory(), "glossaries");
        private static readonly string SoftwareName = "魂游MOD翻译工具 v2.7";

        private static void ShowTaskResult(bool success, string succMsg, string failMsg)
        {
            var caption = success ? "提示" : "错误";
            var icon = success ? MessageBoxImage.Information : MessageBoxImage.Error;
            AdonisUI.Controls.MessageBox.Show(success ? succMsg : failMsg, caption, MessageBoxButton.OK, icon);
        }

        private static List<string> LoadDbFiles()
        {
            Logger.Info($"数据库路径是：{DbPath}");
            if (!Directory.Exists(DbPath))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(DbPath);
            return (from file in files where Path.GetExtension(file).Equals(".json") select Path.GetFileName(file)).ToList();
        }


        private void SwitchTab(string name)
        {
            var tabButtons = new List<Button>
            {
                TranslateTab, ToolTab, AboutTab
            };
            var panels = new List<System.Windows.Controls.StackPanel>
            {
                TranslateStackPanel, ToolStackPanel, AboutStackPanel
            };

            for (var i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].IsEnabled = !tabButtons[i].Name.Contains(name);
                panels[i].Visibility = panels[i].Name.Contains(name) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private ObservableCollection<string> Glossaries { get; set; }

        private List<string> DbList { get; set; }

        public MainWindow()
        {
            Logger.Info("\n\n\n===========================New Instance===================================");
            InitializeComponent();
            //
            Glossaries = new ObservableCollection<string>();
            GlossaryListBox.ItemsSource = Glossaries;
            //
            DbList = LoadDbFiles();
            DbComboBox.ItemsSource = DbList;
            if (DbList.Count > 0)
                DbComboBox.SelectedIndex = 0;
            //setup
            this.AllowDrop = true;
            SwitchTab("Translate");
            this.Title = SoftwareName;
            AboutTitleLabel.Content = SoftwareName + "  By hhhxiao";

            if (DbList.Count != 0) return;
            ShowTaskResult(false, "", "找不到数据库文件，请检查软件完整性");
            this.Close();
        }

        private void SelectPathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
                ModPathTextBox.Text = dialog.SelectedPath;
        }

        //导出数据库
        private async void ExportDbBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var keyPath = "";
            var valuePath = "";
            var savePath = "";
            var keyDialog = new FolderBrowserDialog();
            keyDialog.Description = "选择源语言路径(engus)";
            var valueDialog = new FolderBrowserDialog();
            valueDialog.Description = "选择目标语言路径(zhocn)";
            var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Json文件(*.json)|*";
            saveDialog.FileName = "Untitled.json";
            var keyResult = keyDialog.ShowDialog();
            if (keyResult != System.Windows.Forms.DialogResult.OK) return;
            var valueResult = valueDialog.ShowDialog();
            if (valueResult != System.Windows.Forms.DialogResult.OK) return;
            var saveResult = saveDialog.ShowDialog();
            if (saveResult != System.Windows.Forms.DialogResult.OK) return;
            valuePath = valueDialog.SelectedPath;
            keyPath = keyDialog.SelectedPath;
            savePath = saveDialog.FileName;
            var res = await Task.Run(() => DbTool.CreateDb(keyPath, valuePath, savePath));
            ShowTaskResult(res, "导出成功", "导出失败");

        }

        private async void MergeDbBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.InitialDirectory = DbPath;
            dialog.Filter = "Json 文件 (*.json)|*.json|所有文件|*.*";
            dialog.Multiselect = true;
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            //save path
            var savePath = "";
            var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Json文件(*.json)|*";
            saveDialog.FileName = "Untitled.json";
            var saveResult = saveDialog.ShowDialog();
            if (saveResult != System.Windows.Forms.DialogResult.OK) return;
            savePath = saveDialog.FileName;
            var res = await Task.Run(() => DbTool.MergeDB(dialog.FileNames, savePath));
            ShowTaskResult(res, "合并成功", "合并失败");
        }
        private void TranslateTab_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                SwitchTab(btn.Name.Replace("Tab", ""));
            }
        }

        private void GlossaryAdd_onClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.InitialDirectory = GlossaryPath;
            dialog.Filter = "Json 文件 (*.json)|*.json|所有文件|*.*";
            dialog.Multiselect = true;
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            foreach (var item in dialog.FileNames)
            {
                if (!Glossaries.Contains(item))
                {
                    Glossaries.Add(item);
                }
            }
        }

        private void GlossaryRemove_onClick(object sender, RoutedEventArgs e)
        {
            var selectedItems = GlossaryListBox.SelectedItems.Cast<string>().ToList();

            // 遍历选中的项，从ObservableCollection中移除
            foreach (var selectedItem in selectedItems)
            {
                Glossaries.Remove(selectedItem);
            }
        }


        //生成新的文本文件
        private async void GenerateBtn_onClick(object sender, RoutedEventArgs e)
        {
            var modRootPath = ModPathTextBox.Text;
            var dbPath = Path.Combine(DbPath, DbList[DbComboBox.SelectedIndex]);
            if (modRootPath.Length == 0)
            {
                ShowTaskResult(false, "", "请先设置msg目录");
                return;
            }

            if (dbPath.Length == 0)
            {
                ShowTaskResult(false, "", "数据库为空，请检查软件完整性");
                return;
            }

            var dialog = new OpenFileDialog();
            dialog.Filter = "Excel 文件 (*.xlsx)|*.xlsx|文本文件 (*.txt)|*.txt";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var res = await Task.Run(() => Translator.Translate(modRootPath, dbPath, dialog.FileName));
            ShowTaskResult(res, "生成成功", "生成失败");
        }

        //导出未翻译文本
        private async void ExportBtn_onClick(object sender, RoutedEventArgs e)
        {
            var modRootPath = ModPathTextBox.Text;
            var dbPath = Path.Combine(DbPath, DbList[DbComboBox.SelectedIndex]);
            if (modRootPath.Length == 0)
            {
                ShowTaskResult(false, "", "请先设置msg目录");
                return;
            }

            if (dbPath.Length == 0)
            {
                ShowTaskResult(false, "", "数据库为空，请检查软件完整性");
                return;
            }

            //导出未翻译文本
            var res = await Task.Run(() => Translator.Export(modRootPath, dbPath));
            if (!res.Success)
            {
                ShowTaskResult(false, "", "导出失败");
                return;
            }

            //术语表预处理
            var useGlossary = UseGlossaryCheckBox.IsChecked ?? false;
            if (useGlossary)
            {
                var glossary = new Glossary(IgnoreCaseCheckBox.IsChecked ?? false);
                if (!glossary.Load(this.Glossaries.ToList()))
                {
                    Logger.Warn("无法加载术语表");
                }
                else
                {
                    res = glossary.Process(res);
                }
            }
            //写入磁盘
            var exportAsExcel = UseExcelCheckBox.IsChecked ?? false;
            var resort = AutoSortCheckBox.IsChecked ?? false;
            var dialog = new SaveFileDialog();
            dialog.Filter = exportAsExcel ? "Excel表格文件(*.xlsx)|*" : "文本文件(*.txt)|*";
            dialog.FileName = exportAsExcel ? "text.xlsx" : "text.txt";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var result = await Task.Run(() => TextExporter.Export(dialog.FileName, res, exportAsExcel, resort, false));
            Logger.Info($"成功导出未翻译文本 {dialog.FileName}");
            ShowTaskResult(result, "导出成功", "导出失败");
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Unable to open link: {ex.Message}");
            }
        }
    }
}