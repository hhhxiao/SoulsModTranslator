using System.Windows;
using System.Collections.ObjectModel;
using NLog;
using System.IO;
using AdonisUI.Controls;
using AdonisUI;
using Button = System.Windows.Controls.Button;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using System.Windows.Navigation;
using NLog.Targets;
using SMT.core;

namespace SMT.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        private static readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "db");
        private static readonly string GlossaryPath = Path.Combine(Directory.GetCurrentDirectory(), "glossaries");
        private static readonly string SoftwareName = "魂游MOD翻译工具 v2.16.2";

        private static MemoryTarget MemoryTarget = new MemoryTarget
        {
            Name = "memory",
            Layout = "[${level}] ${message}"
        };

        private static void ShowTaskResult(bool success, string succMsg, string failMsg)
        {
            if (success)
            {
                AdonisUI.Controls.MessageBox.Show(succMsg, "提示",
                    AdonisUI.Controls.MessageBoxButton.OK, MessageBoxImage.Information);
                MemoryTarget.Logs.Clear();
                return;
            }

            var log = String.Join("\n", MemoryTarget.Logs.ToArray());
            var messageBox = new MessageBoxModel
            {
                Text = failMsg + "\n\n" + log,
                Caption = "错误",
                Icon = MessageBoxImage.Information,
                Buttons =
                    new[]
                    {
                        AdonisUI.Controls.MessageBoxButtons.Custom("复制日志并前往Github反馈", "github"),
                        AdonisUI.Controls.MessageBoxButtons.Custom("复制日志并前往b站反馈", "bilibili"),
                        AdonisUI.Controls.MessageBoxButtons.Custom("关闭", "close"),
                    },
            };
            AdonisUI.Controls.MessageBox.Show(messageBox);
            if (messageBox.Result == AdonisUI.Controls.MessageBoxResult.Custom)
            {
                var prompt = "[请在这里礼貌且清晰地描述你遇到的问题。] 以下是错误消息和日志。\n";
                var text = "错误消息: " + failMsg + "\n日志：\n" + log;
                if (messageBox.ButtonPressed.Id.ToString() == "github")
                {
                    System.Windows.Clipboard.SetText(prompt + "```\n" + text + "\n```");
                    Utils.OpenUrl("https://github.com/hhhxiao/SoulsModTranslator/issues/new");
                }
                else if (messageBox.ButtonPressed.Id.ToString() == "bilibili")
                {
                    System.Windows.Clipboard.SetText(prompt + text);
                    Utils.OpenUrl("https://www.bilibili.com/video/BV17p421Q7qJ/");
                }
            }

            MemoryTarget.Logs.Clear();
        }

        public void CreateArrayLogger()
        {
            var config = LogManager.Configuration;
            config.AddTarget(MemoryTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, MemoryTarget);
            LogManager.Configuration = config;
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

        private List<string> DbList = new List<string>();

        private bool ValidateModPath(string path)
        {
            if (!string.IsNullOrEmpty(path)) return true;
            ShowTaskResult(false, "", "请先设置msg目录");
            return false;
        }

        private bool ValidateDbPath(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return true;
            ShowTaskResult(false, "", "数据库为空，请检查软件完整性");
            return false;
        }

        private bool ValidateAiConfig(AiConfigData config)
        {
            if (string.IsNullOrEmpty(config.BaseUrl))
            {
                ShowTaskResult(false, "", "请先设置 AI Base URL");
                return false;
            }
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                ShowTaskResult(false, "", "请先设置 AI API 密钥");
                return false;
            }
            if (string.IsNullOrEmpty(config.ModelName))
            {
                ShowTaskResult(false, "", "请先设置 AI 模型名称");
                return false;
            }
            if (string.IsNullOrEmpty(config.CustomPrompt))
            {
                ShowTaskResult(false, "", "请先设置 AI 自定义提示词");
                return false;
            }
            return true;
        }

        //初始化
        public MainWindow()
        {
            Logger.Info("\n\n===========================New Instance===================================");
            CreateArrayLogger();
            Logger.Info(SoftwareName);
            InitializeComponent();
            //
            Glossaries = new ObservableCollection<string>();
            GlossaryListBox.ItemsSource = Glossaries;
            RefreshDBListUI();
            //加载 AI 配置
            LoadAiConfig();
            //setup
            this.AllowDrop = true;
            SwitchTab("Translate");
            this.Title = SoftwareName;
            AboutTitleLabel.Content = SoftwareName + "  By hhhxiao";
            if (DbList.Count != 0) return;
            ShowTaskResult(false, "", "找不到数据库文件，请检查软件完整性");
            this.Close();
        }

        //翻译模式切换
        private void ManualModeBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Logger.Info("切换到手动翻译");
            ManualModeBtn.Style = (Style)FindResource(AdonisUI.Styles.AccentButton);
            AiModeBtn.Style = null;
            ManualTranslatePanel.Visibility = Visibility.Visible;
            AiTranslatePanel.Visibility = Visibility.Collapsed;
        }

        private void AiModeBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Logger.Info("切换到 AI 翻译");
            AiModeBtn.Style = (Style)FindResource(AdonisUI.Styles.AccentButton);
            ManualModeBtn.Style = null;
            ManualTranslatePanel.Visibility = Visibility.Collapsed;
            AiTranslatePanel.Visibility = Visibility.Visible;
        }


        //切换Tab
        private void ChangeTab_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                SwitchTab(btn.Name.Replace("Tab", ""));
            }
        }


        //MAIN
        private void RefreshDBListUI()
        {
            Logger.Info($"数据库根目录：{DbPath}");
            if (!Directory.Exists(DbPath))
            {
                return;
            }

            var files = Directory.GetFiles(DbPath);
            DbList = (from file in files where Path.GetExtension(file).Equals(".json") select Path.GetFileName(file))
                .ToList();
            DbComboBox.ItemsSource = DbList;
            if (DbList.Count > 0)
                DbComboBox.SelectedIndex = 0;
        }


        private void SelectPathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
                ModPathTextBox.Text = dialog.SelectedPath;
        }


        private void RefreshDBBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshDBListUI();
        }


        private void GlossaryAdd_onClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                InitialDirectory = GlossaryPath,
                Filter = "Json 文件 (*.json)|*.json|所有文件|*.*",
                Multiselect = true
            };
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
            foreach (var selectedItem in selectedItems)
            {
                Glossaries.Remove(selectedItem);
            }
        }


        //测试大模型连接
        private async void TestApiBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Logger.Info("测试大模型连接");
            var config = new AiConfigData
            {
                BaseUrl = BaseUrlTextBox.Text.Trim(),
                ApiKey = ApiKeyTextBox.Text.Trim(),
                ModelName = ModelNameTextBox.Text.Trim(),
                CustomPrompt = CustomPromptTextBox.Text.Trim()
            };

            if (!ValidateAiConfig(config)) return;

            try
            {
                var reasoningEffort = ((System.Windows.Controls.ComboBoxItem)ReasoningEffortComboBox.SelectedItem)?.Content?.ToString() ?? "medium";
                var enableDeepThink = DeepThinkCheckBox.IsChecked ?? false;

                var result = await Task.Run(() =>
                    AiClient.SendChatRequestAsync(config.BaseUrl, config.ApiKey, config.ModelName, config.CustomPrompt, reasoningEffort, enableDeepThink));

                Logger.Info("API 测试成功");
                ShowTaskResult(true, "API 连接成功！\n\n返回内容：\n" + result, "");
            }
            catch (Exception ex)
            {
                Logger.Error($"API 测试失败: {ex.Message}");
                ShowTaskResult(false, "", "API 测试失败：\n" + ex.Message);
            }
        }

        private void SaveAiConfig()
        {
            var reasoningEffort = ((System.Windows.Controls.ComboBoxItem)ReasoningEffortComboBox.SelectedItem)?.Content?.ToString() ?? "medium";
            var data = new AiConfigData
            {
                BaseUrl = BaseUrlTextBox.Text.Trim(),
                ApiKey = ApiKeyTextBox.Text.Trim(),
                ModelName = ModelNameTextBox.Text.Trim(),
                ReasoningEffort = reasoningEffort,
                EnableDeepThink = DeepThinkCheckBox.IsChecked ?? false,
                CustomPrompt = CustomPromptTextBox.Text.Trim()
            };
            AiConfig.Save(data);
        }

        private void LoadAiConfig()
        {
            var data = AiConfig.Load();
            BaseUrlTextBox.Text = data.BaseUrl;
            ApiKeyTextBox.Text = data.ApiKey;
            ModelNameTextBox.Text = data.ModelName;
            CustomPromptTextBox.Text = data.CustomPrompt;
            DeepThinkCheckBox.IsChecked = data.EnableDeepThink;
            //设置推理等级选中项
            foreach (var item in ReasoningEffortComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem &&
                    comboItem.Content?.ToString() == data.ReasoningEffort)
                {
                    ReasoningEffortComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveConfigBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Logger.Info("手动保存 AI 配置");
            SaveAiConfig();
            Logger.Info("AI 配置已保存");
            ShowTaskResult(true, "配置已保存", "");
        }

        private void LoadConfigBtn_OnClick(object sender, RoutedEventArgs e)
        {
            Logger.Info("手动加载 AI 配置");
            LoadAiConfig();
            Logger.Info("AI 配置已加载");
            ShowTaskResult(true, "配置已加载", "");
        }


        //导出未翻译文本
        private async void ExportBtn_onClick(object sender, RoutedEventArgs e)
        {
            var modRootPath = ModPathTextBox.Text;
            var dbPath = Path.Combine(DbPath, DbList[DbComboBox.SelectedIndex]);
            var keepText = DoNotSplitTextBox.IsChecked ?? false;
            var replaceNewLine = MarkNewLineCheckBox.IsChecked ?? false;
            if (!ValidateModPath(modRootPath)) return;
            if (!ValidateDbPath(dbPath)) return;

            //导出未翻译文本
            var res = await Task.Run(() => Translator.Export(modRootPath, dbPath, keepText));
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
            var markSource = MarkSourceCheckBox.IsChecked ?? false;
            var split = MultiFileCheckBox.IsChecked ?? false;
            int maxLine = split ? (int)MaxLineSlider.Value : 10000000;
            var dialog = new SaveFileDialog
            {
                Filter = exportAsExcel ? "Excel表格文件(*.xlsx)|*" : "文本文件(*.txt)|*",
                FileName = exportAsExcel ? "text.xlsx" : "text.txt"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var result = await Task.Run(() =>
                TextExporter.Export(dialog.FileName, res, exportAsExcel, resort, markSource, replaceNewLine, false,
                    maxLine));
            Logger.Info($"成功导出未翻译文本：{dialog.FileName}");
            ShowTaskResult(result, "导出成功", "导出失败");
        }


        //AI 翻译
        private async void AiTranslateBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var modRootPath = ModPathTextBox.Text;
            var dbPath = Path.Combine(DbPath, DbList.Count > 0 ? DbList[DbComboBox.SelectedIndex] : "");

            if (!ValidateModPath(modRootPath)) return;
            if (!ValidateDbPath(dbPath)) return;

            Logger.Info("AI 翻译开始：导出未翻译文本");
            var keepText = DoNotSplitTextBox.IsChecked ?? false;
            var res = await Task.Run(() => Translator.Export(modRootPath, dbPath, keepText));
            if (!res.Success)
            {
                ShowTaskResult(false, "", "导出未翻译文本失败");
                return;
            }

            Logger.Info($"导出成功，共 {res.SentenceList.Count} 条待翻译文本");

            // 基于上次翻译结果继续
            if (ContinueTranslateCheckBox.IsChecked == true)
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "JSON 文件(*.json)|*.json",
                    FileName = "translated.json"
                };
                if (openDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var prevResult = Utils.LoadJsonToObject<ExportResult>(openDialog.FileName);
                if (prevResult == null)
                {
                    ShowTaskResult(false, "", "无法加载上次翻译结果文件");
                    return;
                }

                var prevIds = new HashSet<long>(prevResult.SentenceList.Select(i => i.GlobalId));
                foreach (var item in res.SentenceList)
                {
                    if (prevIds.Contains(item.GlobalId))
                        res.TranslatedIds.Add(item.GlobalId);
                }

                Logger.Info($"基于上次翻译结果，已标记 {res.TranslatedIds.Count} 条已翻译文本，仍需翻译 {res.SentenceList.Count - res.TranslatedIds.Count} 条");
            }

            var config = new AiConfigData
            {
                BaseUrl = BaseUrlTextBox.Text.Trim(),
                ApiKey = ApiKeyTextBox.Text.Trim(),
                ModelName = ModelNameTextBox.Text.Trim(),
                ReasoningEffort =
                    ((System.Windows.Controls.ComboBoxItem)ReasoningEffortComboBox.SelectedItem)?.Content?.ToString() ??
                    "medium",
                EnableDeepThink = DeepThinkCheckBox.IsChecked ?? false,
                CustomPrompt = CustomPromptTextBox.Text.Trim()
            };

            if (!ValidateAiConfig(config)) return;

            Logger.Info("开始调用 AI 翻译");
            SaveAiConfig();

            // 弹出进度窗口
            var progressWindow = new ProgressWindow
            {
                Owner = this
            };
            progressWindow.Show();

            try
            {
                var progress = new Progress<TranslationProgress>(p => progressWindow.Report(p));
                var token = progressWindow.CancellationTokenSource.Token;
                var (success, translated) = await Task.Run(
                    () => AiClient.TranslateWithAiAsync(res, config, progress, token), token);
                CloseProgressWindowSafely(progressWindow);
                Activate();

                if (success)
                {
                    ShowTaskResult(true, "AI 翻译完成", "");
                }
                else if (translated.SentenceList.Count > 0)
                {
                    // 部分完成，询问是否保存
                    var saveResult = AdonisUI.Controls.MessageBox.Show(
                        $"已取消翻译，已翻译 {translated.SentenceList.Count} 条，临时结果不会丢失。是否保存已翻译的内容？",
                        "保存已翻译内容",
                        AdonisUI.Controls.MessageBoxButton.YesNo,
                        AdonisUI.Controls.MessageBoxImage.Question);

                    if (saveResult == AdonisUI.Controls.MessageBoxResult.Yes)
                    {
                        var dialog = new SaveFileDialog
                        {
                            Filter = "JSON 文件(*.json)|*.json",
                            FileName = "translated.json"
                        };
                        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            try
                            {
                                Utils.SaveObjectAsJson(translated, dialog.FileName);
                                Logger.Info($"已保存已翻译内容至：{dialog.FileName}");
                                ShowTaskResult(true, "已保存", "");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"保存已翻译内容失败: {ex.Message}");
                                ShowTaskResult(false, "", "保存失败：" + ex.Message);
                            }
                        }
                    }
                }
                else
                {
                    ShowTaskResult(false, "", "AI 翻译失败");
                }
            }
            catch (Exception ex)
            {
                CloseProgressWindowSafely(progressWindow);
                Activate();
                Logger.Error($"AI 翻译异常: {ex.Message}");
                ShowTaskResult(false, "", "AI 翻译异常：" + ex.Message);
            }
        }

        private static void CloseProgressWindowSafely(ProgressWindow w)
        {
            try
            {
                w.AllowClosingWithoutConfirm = true;
                if (w.IsVisible) w.Close();
            }
            catch
            {
                // ignore
            }
        }


        //生成新的文本文件
        private async void GenerateBtn_onClick(object sender, RoutedEventArgs e)
        {
            var modRootPath = ModPathTextBox.Text;
            var dbPath = Path.Combine(DbPath, DbList[DbComboBox.SelectedIndex]);
            var keepText = DoNotSplitTextBox.IsChecked ?? false;
            var multiLang = MultiLangCheckBox.IsChecked ?? false;

            if (!ValidateModPath(modRootPath)) return;
            if (!ValidateDbPath(dbPath)) return;

            var dialog = new OpenFileDialog();
            dialog.Filter = "Excel 文件 (*.xlsx)|*.xlsx|文本文件 (*.txt)|*.txt";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var res = await Task.Run(() =>
                Translator.Translate(modRootPath, dbPath, dialog.FileNames, keepText, multiLang, false));
            ShowTaskResult(res, "生成成功", "生成失败");
        }

        //TOOLS
        //db generation
        private async void ExportDbBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var keyDialog = new FolderBrowserDialog
            {
                Description = "选择源语言路径(engus)"
            };
            var valueDialog = new FolderBrowserDialog
            {
                Description = "选择目标语言路径(zhocn)"
            };
            var saveDialog = new SaveFileDialog
            {
                InitialDirectory = DbPath,
                Filter = "Json文件(*.json)|*",
                FileName = "Untitled.json"
            };

            if (keyDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (valueDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var res = await Task.Run(() => DataBase.CreateDb(keyDialog.SelectedPath, valueDialog.SelectedPath, saveDialog.FileName));
            ShowTaskResult(res, "导出成功", "导出失败");
        }


        //db merge
        private async void MergeDbBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                InitialDirectory = DbPath,
                Filter = "Json 文件 (*.json)|*.json|所有文件|*.*",
                Multiselect = true
            };
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return;
            //save path
            var saveDialog = new SaveFileDialog
            {
                Filter = "Json文件(*.json)|*",
                FileName = "Untitled.json"
            };
            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var res = await Task.Run(() => DataBase.MergeDataBase(dialog.FileNames, saveDialog.FileName));
            ShowTaskResult(res, "合并成功", "合并失败");
        }

        private async void CN2TWBtn_onClick(object sender, RoutedEventArgs e)
        {
            await ConvertCNTW("zhoTW", "选择简中语言文件路径(内含.msgbnd.dcx文件)",
                "选择导出的繁中语言文件路径");
        }

        private async void TW2CNBtn_onClick(object sender, RoutedEventArgs e)
        {
            await ConvertCNTW("zhoCN", "选择繁中语言文件路径(内含.msgbnd.dcx文件)",
                "选择导出的简中语言文件路径");
        }

        private async Task ConvertCNTW(string targetLang, string inputDesc, string outputDesc)
        {
            var inputDialog = new FolderBrowserDialog { Description = inputDesc };
            var outputDialog = new FolderBrowserDialog { Description = outputDesc };
            if (inputDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (outputDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var res = await Task.Run(() =>
                LangFileSet.CNTWConvert(targetLang, inputDialog.SelectedPath, outputDialog.SelectedPath));
            ShowTaskResult(res, "转换成功", "转换失败");
        }

        private async void DumpLangFile_OnClick(object sender, RoutedEventArgs e)
        {
            var inputDialog = new FolderBrowserDialog
            {
                Description = "选择源语言路径(engus，zhocn等)"
            };
            var outputDialog = new FolderBrowserDialog
            {
                Description = "选择导出目录"
            };
            if (inputDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (outputDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var res = await Task.Run(() => LangFileSet.Dump(inputDialog.SelectedPath, outputDialog.SelectedPath));
            ShowTaskResult(res, "导出成功", "导出失败");
        }

        //About
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Utils.OpenUrl(e.Uri.AbsoluteUri);
        }
    }
}