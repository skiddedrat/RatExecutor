#nullable disable
using SynapseZ;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Shapes;
using System.Windows.Media.TextFormatting;

namespace ScriptExecutorUI
{
    public partial class MainWindow : Window
    {
        private sealed class SuggestionItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Signature { get; set; }
            public string DocumentationUrl { get; set; }
            public override string ToString() => Name;
        }
        private sealed class EditorTabState
        {
            public string Title { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        public ObservableCollection<AccordionSection> Sections { get; set; } = new ObservableCollection<AccordionSection>();
        private uint _selectedPid = 0;
        private CancellationTokenSource _cts = null;
        private ScrollViewer _editorScrollViewer;
        private ScrollViewer _overlayScrollViewer;
        private bool _isRealtimeHelperEnabled = true;
        private Rectangle _caretOverlay;
        private DispatcherTimer _caretBlinkTimer;
        private string _lastText = "";
        private int _lastCaretIndex = 0;
        private readonly object _syncLock = new object();
        private readonly ObservableCollection<EditorTabState> _editorTabs = new ObservableCollection<EditorTabState>();
        private readonly ObservableCollection<string> _savedFiles = new ObservableCollection<string>();
        private EditorTabState _activeEditorTab;
        private bool _isSwitchingTabs;
        private const int MaxEditorTabs = 100;

        private double MeasureSpaceWidth()
        {
            var testRun = new Run(" ");
            testRun.FontFamily = new FontFamily("Consolas");
            testRun.FontSize = 13;
            var formatted = new FormattedText(
                " ",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                13,
                Brushes.Black);
            return formatted.Width;
        }
        private readonly SuggestionItem[] _suncSuggestions =
        {
            new SuggestionItem { Name = "print ", Signature = "print(...: T...) : () ", Description = "Prints all provided values to the output. ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#print " },
            new SuggestionItem { Name = "warn ", Description = "Output warning message. ", Signature = "warn(...: T...) : () ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#warn " },
            new SuggestionItem { Name = "error ", Description = "Throw an error and stop execution. ", Signature = "error(message: string, level: number?) : () ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#error " },
            new SuggestionItem { Name = "task.wait ", Description = "Yield current thread for duration. ", Signature = "task.wait(duration: number?) : number ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/libraries/task#wait " },
            new SuggestionItem { Name = "task.spawn ", Description = "Run function asynchronously. ", Signature = "task.spawn(f: function, ...: any) : thread ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/libraries/task#spawn " },
            new SuggestionItem { Name = "task.delay ", Description = "Run function after delay. ", Signature = "task.delay(duration: number, f: function, ...: any) : thread ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/libraries/task#delay " },
            new SuggestionItem { Name = "pairs ", Signature = "pairs(t: table) : (function, table, any) ", Description = "Iterate key/value table pairs. ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#pairs " },
            new SuggestionItem { Name = "ipairs ", Description = "Iterate array-like table values. ", Signature = "ipairs(t: table) : (function, table, number) ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#ipairs " },
            new SuggestionItem { Name = "pcall ", Signature = "pcall(f: function, ...: any) : (boolean, ...any) ", Description = "Call function in protected mode. ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#pcall " },
            new SuggestionItem { Name = "xpcall ", Description = "Protected call with custom handler. ", Signature = "xpcall(f: function, msgh: function, ...: any) : (boolean, ...any) ", DocumentationUrl = "https://create.roblox.com/docs/reference/engine/globals/LuaGlobals#xpcall " },
            new SuggestionItem { Name = "game:GetService(\"Players\") ", Description = "Get Players service. " },
            new SuggestionItem { Name = "game:GetService(\"RunService\") ", Description = "Get RunService. " },
            new SuggestionItem { Name = "game:GetService(\"TweenService\") ", Description = "Get TweenService. " },
            new SuggestionItem { Name = "game:GetService(\"ReplicatedStorage\") ", Description = "Get shared storage service. " },
            new SuggestionItem { Name = "workspace ", Description = "Top-level 3D world container. " },
            new SuggestionItem { Name = "Instance.new ", Description = "Create a new Roblox instance. ", Signature = "Instance.new(className: string, parent: Instance?) : Instance " },
            new SuggestionItem { Name = "Vector3.new ", Description = "Create 3D vector value. ", Signature = "Vector3.new(x: number, y: number, z: number) : Vector3 " },
            new SuggestionItem { Name = "CFrame.new ", Description = "Create position/orientation frame. ", Signature = "CFrame.new(x: number, y: number, z: number) : CFrame " },
            new SuggestionItem { Name = "UDim2.new ", Description = "Create 2D UI dimension. ", Signature = "UDim2.new(xScale: number, xOffset: number, yScale: number, yOffset: number) : UDim2 " },
            new SuggestionItem { Name = "Color3.fromRGB ", Description = "Create RGB color. ", Signature = "Color3.fromRGB(r: number, g: number, b: number) : Color3 " },
            new SuggestionItem { Name = "workspace:Raycast ", Description = "Cast ray into the world. ", Signature = "workspace:Raycast(origin: Vector3, direction: Vector3, params: RaycastParams?) : RaycastResult? " },
            new SuggestionItem { Name = "RaycastParams.new ", Description = "Configure raycast filters. ", Signature = "RaycastParams.new() : RaycastParams " },
            new SuggestionItem { Name = "RemoteEvent:FireServer ", Description = "Send event from client to server. " },
            new SuggestionItem { Name = "RemoteFunction:InvokeServer ", Description = "Invoke remote function on server. " },
            new SuggestionItem { Name = "RunService.Heartbeat:Connect ", Description = "Run callback each frame heartbeat. " },
            new SuggestionItem { Name = "RunService.RenderStepped:Connect ", Description = "Run callback each render step. " },
            new SuggestionItem { Name = "Players.PlayerAdded:Connect ", Description = "Callback when player joins. " },
            new SuggestionItem { Name = "player.CharacterAdded:Connect ", Description = "Callback when character spawns. " },
            new SuggestionItem { Name = "part.Touched:Connect ", Description = "Callback when part is touched. " },
            new SuggestionItem { Name = "Instance.Changed:Connect ", Description = "Callback when property changes. " },
            new SuggestionItem { Name = "table.insert ", Description = "Insert value into table. ", Signature = "table.insert(t: table, value: any) : () " },
            new SuggestionItem { Name = "table.remove ", Description = "Remove value from table. ", Signature = "table.remove(t: table, index: number?) : any " },
            new SuggestionItem { Name = "table.find ", Description = "Find value index in table. ", Signature = "table.find(t: table, value: any, init: number?) : number? " },
            new SuggestionItem { Name = "string.split ", Description = "Split string by separator. ", Signature = "string.split(s: string, sep: string) : {string} " },
            new SuggestionItem { Name = "string.format ", Description = "Format string with placeholders. ", Signature = "string.format(fmt: string, ...: any) : string " },
            new SuggestionItem { Name = "math.clamp ", Description = "Clamp number to min/max. ", Signature = "math.clamp(n: number, min: number, max: number) : number " },
            new SuggestionItem { Name = "math.floor ", Description = "Round down number. ", Signature = "math.floor(n: number) : number " },
            new SuggestionItem { Name = "math.random ", Description = "Generate random number. ", Signature = "math.random(m: number?, n: number?) : number " },
            new SuggestionItem { Name = "TweenInfo.new ", Description = "Create tween configuration. ", Signature = "TweenInfo.new(time: number, easingStyle: Enum.EasingStyle, easingDirection: Enum.EasingDirection) : TweenInfo " },
            new SuggestionItem { Name = "Humanoid:MoveTo ", Description = "Move humanoid to target point. ", Signature = "Humanoid:MoveTo(location: Vector3) : () " },
            new SuggestionItem { Name = "Humanoid.Jump ", Description = "Trigger humanoid jump. " },
            new SuggestionItem { Name = "function ", Description = "Declare a function block. " },
            new SuggestionItem { Name = "local ", Description = "Declare local variable. " },
            new SuggestionItem { Name = "if then ", Description = "Conditional logic block. " },
            new SuggestionItem { Name = "for do ", Description = "Looping construct. " },
            new SuggestionItem { Name = "while do ", Description = "While-loop construct. " },
            new SuggestionItem { Name = "repeat until ", Description = "Repeat loop construct. " }
        };

        public MainWindow()
        {
            InitializeComponent();
            LoadAccordionData();
            ApplyStaticGlowEffects();
            AppendConsole($"[Ready] Console initialized at {DateTime.Now:HH:mm:ss}\n", Colors.LightSkyBlue);

            SynapseZAPI2.StartInstancesTimer();

            SynapseZAPI2.SessionAdded += OnSessionAdded;
            SynapseZAPI2.SessionRemoved += OnSessionRemoved;
            SynapseZAPI2.SessionOutput += OnSessionOutput;
            DetectRobloxAndConnection();
            InitializeEditorTabs();
            SavedFilesList.ItemsSource = _savedFiles;
        }

        private void InitializeEditorTabs()
        {
            EditorTabsList.ItemsSource = _editorTabs;
            AddNewEditorTab();
        }

        private void AddEditorTab_Click(object sender, RoutedEventArgs e) => AddNewEditorTab();

        private void AddNewEditorTab()
        {
            if (_editorTabs.Count >= MaxEditorTabs)
            {
                AppendConsole("[Warn] Maximum of 100 tabs reached.\n", Colors.Orange);
                return;
            }

            PersistActiveEditorTab();
            var tab = new EditorTabState { Title = $"Untitled-{_editorTabs.Count + 1}" };
            _editorTabs.Add(tab);
            ActivateEditorTab(tab);
        }

        private void EditorTabItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is EditorTabState tab)
                ActivateEditorTab(tab);
        }

        private void CloseEditorTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not EditorTabState tab)
                return;
            if (_editorTabs.Count <= 1)
            {
                AppendConsole("[Warn] Keep at least one tab open.\n", Colors.Orange);
                return;
            }

            var closingActive = ReferenceEquals(_activeEditorTab, tab);
            var index = _editorTabs.IndexOf(tab);
            _editorTabs.Remove(tab);
            if (closingActive)
            {
                var nextIndex = Math.Max(0, Math.Min(index, _editorTabs.Count - 1));
                ActivateEditorTab(_editorTabs[nextIndex]);
            }
            UpdateEditorTabSlider();
        }

        private void ActivateEditorTab(EditorTabState tab)
        {
            PersistActiveEditorTab();
            _activeEditorTab = tab;
            _isSwitchingTabs = true;
            CodeEditor.Text = tab.Content ?? string.Empty;
            _isSwitchingTabs = false;
            CodeEditor.CaretIndex = CodeEditor.Text.Length;
            UpdateEditorTabSlider();
        }

        private void PersistActiveEditorTab()
        {
            if (_activeEditorTab != null)
                _activeEditorTab.Content = CodeEditor.Text ?? string.Empty;
        }

        private void UpdateEditorTabSlider()
        {
            if (EditorTabsScrollViewer == null || EditorTabsSlider == null)
                return;
            var max = Math.Max(0, EditorTabsScrollViewer.ExtentWidth - EditorTabsScrollViewer.ViewportWidth);
            EditorTabsSlider.Maximum = max;
            if (EditorTabsSlider.Value > max)
                EditorTabsSlider.Value = max;
        }

        private void EditorTabsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            EditorTabsScrollViewer?.ScrollToHorizontalOffset(e.NewValue);
        }

        private void EditorTabsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateEditorTabSlider();
            if (EditorTabsSlider != null && Math.Abs(EditorTabsSlider.Value - e.HorizontalOffset) > 0.5)
                EditorTabsSlider.Value = e.HorizontalOffset;
        }

        private void LoadAccordionData()
        {
            Sections = new ObservableCollection<AccordionSection>();
        }

        private void ImportScriptFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Script/Text Files (*.lua;*.txt)|*.lua;*.txt"
            };

            if (picker.ShowDialog() != true)
                return;

            foreach (var file in picker.FileNames.Where(File.Exists))
            {
                if (!_savedFiles.Contains(file))
                    _savedFiles.Add(file);
            }
        }

        private void SavedFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SavedFilesList.SelectedItem is not string filePath || !File.Exists(filePath))
                return;

            var result = MessageBox.Show($"Open file '{IOPath.GetFileName(filePath)}' in a new tab? ", "Open saved file ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            if (_editorTabs.Count >= MaxEditorTabs)
            {
                AppendConsole("[Warn] Maximum of 100 tabs reached.\n", Colors.Orange);
                return;
            }

            PersistActiveEditorTab();
            var tab = new EditorTabState
            {
                Title = IOPath.GetFileName(filePath),
                Content = File.ReadAllText(filePath)
            };
            _editorTabs.Add(tab);
            ActivateEditorTab(tab);
        }

        private void ApplyStaticGlowEffects()
        {
            MainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0xA0, 0x20, 0xF0),
                ShadowDepth = 0,
                BlurRadius = 22,
                Opacity = 0.85
            };
        }

        private void ProcessSelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProcessPickerDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedPid != 0)
            {
                _selectedPid = dialog.SelectedPid;
                SelectedPidTxt.Text = $"PID: {_selectedPid} ";
                AppendConsole($"Selected PID: {_selectedPid}\n", Colors.Cyan);
            }
        }

        private void LaunchRoblox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "roblox://",
                    UseShellExecute = true
                });
                AppendConsole("[Info] Launching Roblox...\n", Colors.LightSkyBlue);
            }
            catch (Exception ex)
            {
                AppendConsole($"[Warn] Could not launch Roblox protocol: {ex.Message}. Opening Roblox website instead.\n", Colors.Orange);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.roblox.com/home",
                    UseShellExecute = true
                });
            }
        }

        private void DetectRobloxAndConnection()
        {
            try
            {
                var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                if (robloxProcesses.Length == 0)
                {
                    ConnectionStatusTxt.Text = "No Roblox detected ";
                    return;
                }

                var injected = SynapseZAPI.GetSynzRobloxInstances();
                if (injected.Count > 0)
                {
                    _selectedPid = (uint)injected[0].Id;
                    SelectedPidTxt.Text = $"PID: {_selectedPid} ";
                    ConnectionStatusTxt.Text = $"Connected to: {_selectedPid} ";
                    AppendConsole($"[Connected] Successful connection to PID {_selectedPid}\n", Colors.LimeGreen);
                }
                else
                {
                    ConnectionStatusTxt.Text = "Roblox detected (not injected) ";
                    AppendConsole("[Info] Roblox detected, waiting for Synapse Z injection.\n", Colors.Orange);
                }
            }
            catch (Exception ex)
            {
                AppendConsole($"[Warn] Detection failed: {ex.Message}\n", Colors.Orange);
            }
        }

        private void ExecuteScript_Click(object sender, RoutedEventArgs e)
        {
            string script = CodeEditor.Text.Trim();
            if (string.IsNullOrEmpty(script))
            {
                AppendConsole("[Error] No script to execute.\n", Colors.Red);
                return;
            }

            try
            {
                int result = SynapseZAPI.Execute(script, (int)_selectedPid);
                if (result == 0)
                    AppendConsole("[Info] Script executed successfully.\n", Colors.LimeGreen);
                else
                {
                    string err = SynapseZAPI.GetLatestErrorMessage();
                    AppendConsole($"[Error] Execution failed (code {result}): {err}\n", Colors.Red);
                }
            }
            catch (Exception ex)
            {
                AppendConsole($"[Fatal] {ex.Message}\n", Colors.Red);
            }
        }

        private void AppendConsole(string text, Color color)
        {
            Dispatcher.Invoke(() =>
            {
                var range = new TextRange(ConsoleOutput.Document.ContentEnd, ConsoleOutput.Document.ContentEnd);
                var line = text.StartsWith("[") ? $"{DateTime.Now:HH:mm:ss} {text}" : text;
                range.Text = line;
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
                ConsoleOutput.ScrollToEnd();
            });
        }

        private void OnSessionAdded(SynapseZAPI2.SynapseSession session)
        {
            Dispatcher.Invoke(() =>
            {
                _selectedPid = (uint)session.Pid;
                SelectedPidTxt.Text = $"PID: {_selectedPid} ";
                ConnectionStatusTxt.Text = $"Connected to: {_selectedPid} ";
                AppendConsole($"[Session Added] Menu successful connection to: {session.Pid}\n", Colors.Cyan);
            });
        }

        private void OnSessionRemoved(SynapseZAPI2.SynapseSession session)
        {
            Dispatcher.Invoke(() =>
                AppendConsole($"[Session Removed] PID: {session.Pid}\n", Colors.DarkGray));
        }

        private void OnSessionOutput(SynapseZAPI2.SynapseSession session, int type, string output)
        {
            Color color = type switch
            {
                0 => Colors.White,
                1 => Colors.Cyan,
                2 => Colors.Orange,
                3 => Colors.Red,
                _ => Colors.Gray
            };
            string label = type switch
            {
                0 => "[print] ",
                1 => "[info] ",
                2 => "[warn] ",
                3 => "[error] ",
                _ => "[?] "
            };
            Dispatcher.Invoke(() =>
                AppendConsole($"{label} {output}\n", color));
        }

        private void EditorTab_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Visibility = Visibility.Visible;
            EditorPanel.Visibility = Visibility.Visible;
            ConsoleOutput.Visibility = Visibility.Collapsed;
            ThemePanel.Visibility = Visibility.Collapsed;
            EditorTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            EditorTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            ConsoleTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            ConsoleTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            SettingsTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            SettingsTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void ConsoleTab_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            ConsoleOutput.Visibility = Visibility.Visible;
            ThemePanel.Visibility = Visibility.Collapsed;
            ConsoleTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            ConsoleTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            EditorTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            EditorTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            SettingsTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            SettingsTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void ClearEditor_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Are you sure you want to clear the editor text? ", "Clear editor ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            CodeEditor.Clear();
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Are you sure you want to clear console output? ", "Clear console ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            ConsoleOutput.Document.Blocks.Clear();
            AppendConsole("[Info] Console cleared.\n", Colors.DarkGray);
        }

        private void CopyEditor_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CodeEditor.Text))
                Clipboard.SetText(CodeEditor.Text);
        }

        private void FormatEditor_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Text = CodeEditor.Text.Replace("\t", "     ");
            AppendConsole("[Info] Replaced tabs with spaces.\n", Colors.LightSkyBlue);
        }

        private static bool IsCaretInsideString(string text, int caret)
        {
            bool inString = false;
            char quote = '\0';
            bool escaped = false;
            for (int i = 0; i < Math.Min(caret, text.Length); i++)
            {
                var c = text[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (!inString && (c == '"' || c == '\'')) { inString = true; quote = c; continue; }
                if (inString && c == quote) { inString = false; quote = '\0'; }
            }
            return inString;
        }
            
        private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSwitchingTabs && _activeEditorTab != null)
                _activeEditorTab.Content = CodeEditor.Text ?? string.Empty;
            UpdateLineNumbers();
            UpdateMiniMap();
            UpdateCaretPosition();
            UpdatePinnedScope();
            ValidateBasicSyntax();
            UpdateSyntaxHighlighting();


            if (!_isRealtimeHelperEnabled)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            var caret = CodeEditor.CaretIndex;
            var text = CodeEditor.Text;

            if (caret > text.Length || caret == 0)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            var start = caret - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
                start--;
            start++;

            if (start >= caret || start < 0)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            var token = text.Substring(start, caret - start);
            if (token.Length < 1 || IsCaretInsideString(text, caret))
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            var matches = _suncSuggestions
                .Where(x => x.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x.Name.Length)
                .Take(12)
                .ToList();

            if (matches.Count == 0)
            {
                SuggestionPopup.IsOpen = false;
                return;
            }

            foreach (var match in matches)
            {
                if (string.IsNullOrWhiteSpace(match.Signature))
                    match.Signature = match.Name;
                if (string.IsNullOrWhiteSpace(match.DocumentationUrl))
                    match.DocumentationUrl = "https://create.roblox.com/docs";
            }

            SuggestionListBox.ItemsSource = matches;
            SuggestionListBox.SelectedIndex = 0;
            SuggestionListBox_SelectionChanged(SuggestionListBox, null);
            PositionSuggestionPopup();
            SuggestionPopup.IsOpen = true;

            
            UpdateCaretPosition();
        }

        private void SuggestionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionListBox.SelectedItem is SuggestionItem selected)
                InsertSuggestion(selected.Name);
        }

        private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionListBox.SelectedItem is not SuggestionItem selected)
                return;

            SuggestionSignatureText.Text = selected.Signature ?? selected.Name;
            SuggestionDescriptionText.Text = selected.Description ?? string.Empty;
            SuggestionDocsLink.Tag = selected.DocumentationUrl;
        }

        private void SuggestionDocsLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionDocsLink.Tag is not string url || string.IsNullOrWhiteSpace(url))
                return;

            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void InsertSuggestion(string selected)
        {
            var caret = CodeEditor.CaretIndex;
            var text = CodeEditor.Text;
            var start = caret - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
                start--;
            start++;

            CodeEditor.Text = text.Remove(start, caret - start).Insert(start, selected);
            CodeEditor.CaretIndex = start + selected.Length;
            SuggestionPopup.IsOpen = false;
            UpdateCaretPosition(); 
        }

        private void CodeEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == "(")
            {
                var caret = CodeEditor.CaretIndex;
                CodeEditor.Text = CodeEditor.Text.Insert(caret, "()");
                CodeEditor.CaretIndex = caret + 1; 
                e.Handled = true;
                UpdateCaretPosition(); 
                return;
            }

            if (e.Text == "\"")
            {
                var caret = CodeEditor.CaretIndex;
                CodeEditor.Text = CodeEditor.Text.Insert(caret, "\"\"");
                CodeEditor.CaretIndex = caret + 1;
                e.Handled = true;
                UpdateCaretPosition();
            }
        }

        private void CodeEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            UpdateCaretPosition();
            if (e.Key == Key.Enter && !SuggestionPopup.IsOpen && AutoIndentToggle?.IsChecked == true)
            {
                HandleAutoIndentEnter();
                e.Handled = true;
                UpdateCaretPosition();
                return;
            }

            if (!SuggestionPopup.IsOpen)
                return;

            if (e.Key == Key.Down)
            {
                if (SuggestionListBox.SelectedIndex < SuggestionListBox.Items.Count - 1)
                    SuggestionListBox.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (SuggestionListBox.SelectedIndex > 0)
                    SuggestionListBox.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && SuggestionListBox.SelectedItem is SuggestionItem selectedSuggestion)
            {
                InsertSuggestion(selectedSuggestion.Name);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && SuggestionListBox.SelectedItem is SuggestionItem selected)
            {
                InsertSuggestion(selected.Name);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SuggestionPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void UpdateLineNumbers()
        {
            var text = CodeEditor.Text;
            var lineCount = Math.Max(1, text.Split('\n').Length);

            // Calculate max width needed (e.g., "1000" → 4 digits)
            int maxDigits = (int)Math.Floor(Math.Log10(lineCount)) + 1;
            int padding = Math.Max(2, maxDigits); // min 2 chars wide (for "1 ", "10", etc.)

            // Build line numbers 
            var lines = Enumerable.Range(1, lineCount)
                .Select(i => i.ToString().PadLeft(padding, ' '))
                .ToArray();

            LineNumbersText.Text = string.Join("\n", lines);

            // Sync vertical scroll (critical!)
            if (_editorScrollViewer != null)
                LineNumbersText.RenderTransform = new TranslateTransform(0, -_editorScrollViewer.VerticalOffset);
        }

        private void UpdateMiniMapFast()
        {
            var text = CodeEditor.Text;
            if (string.IsNullOrEmpty(text)) { MiniMapPreview.Text = ""; return; }


            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var sampled = lines.Where((_, i) => i % 2 == 0).Take(25);
            var preview = string.Join("\n", sampled.Select(l => l.Length > 40 ? l.Substring(0, 40) : l));
            MiniMapPreview.Text = preview;
        }

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
                
            _editorScrollViewer = FindVisualChild<ScrollViewer>(CodeEditor);
            if (_editorScrollViewer != null)
                _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
               

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _overlayScrollViewer = FindVisualChild<ScrollViewer>(SyntaxOverlay);
                UpdateSyntaxHighlighting();

                _caretOverlay = FindName("CaretOverlay") as Rectangle;
                if (_caretOverlay != null)
                {
                    _caretBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    _caretBlinkTimer.Tick += (s, ev) =>
                    {
                        _caretOverlay.Visibility = _caretOverlay.Visibility == Visibility.Visible
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    };
                    _caretBlinkTimer.Start();
                }

                UpdateCaretPosition(); 
            }));
        }

        private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            LineNumbersText.RenderTransform = new TranslateTransform(0, -e.VerticalOffset);
            if (_overlayScrollViewer != null)
            {
                _overlayScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                _overlayScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
            SyncMiniMapScroll(e);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdatePinnedScope();
            if (SuggestionPopup.IsOpen)
                PositionSuggestionPopup();
            UpdateCaretPosition(); 
        }

        private void PositionSuggestionPopup()
        {
            var caretRect = CodeEditor.GetRectFromCharacterIndex(CodeEditor.CaretIndex, true);
            if (caretRect.IsEmpty) return;

            var editorPos = CodeEditor.TranslatePoint(new Point(0, 0), this);

            double x = caretRect.X + 8;
            double y = caretRect.Y + caretRect.Height + 6;

            x = Math.Max(12, x);
            y = Math.Max(16, y);

            SuggestionPopup.PlacementTarget = CodeEditor;
            SuggestionPopup.Placement = PlacementMode.RelativePoint;
            SuggestionPopup.HorizontalOffset = x;
            SuggestionPopup.VerticalOffset = y;
        }

        private void HandleAutoIndentEnter()
        {
            var caret = CodeEditor.CaretIndex;
            var text = CodeEditor.Text;

            var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1));
            lineStart = lineStart == -1 ? 0 : lineStart + 1;
            var currentLine = text.Substring(lineStart, caret - lineStart);

            var currentIndent = new string(currentLine.TakeWhile(char.IsWhiteSpace).ToArray());
            var trimmed = currentLine.TrimEnd();

            bool opensBlock =
                trimmed.EndsWith("then", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("do", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("function", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("{", StringComparison.OrdinalIgnoreCase);

            var extraIndent = opensBlock ? "     " : string.Empty;

            if (opensBlock)
            {
                var insert = "\n" + currentIndent + extraIndent + "\n" + currentIndent + "end";
                CodeEditor.Text = text.Insert(caret, insert);
                CodeEditor.CaretIndex = caret + ("\n" + currentIndent + extraIndent).Length;
                return;
            }

            var singleLineInsert = "\n" + currentIndent;
            CodeEditor.Text = text.Insert(caret, singleLineInsert);
            CodeEditor.CaretIndex = caret + singleLineInsert.Length;
        }

        private void RealtimeHelperToggle_Changed(object sender, RoutedEventArgs e)
        {
            _isRealtimeHelperEnabled = RealtimeHelperToggle.IsChecked == true;
            if (!_isRealtimeHelperEnabled)
                SuggestionPopup.IsOpen = false;
        }

        private void WordWrapToggle_Changed(object sender, RoutedEventArgs e)
        {
            var wrap = WordWrapToggle?.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
            CodeEditor.TextWrapping = wrap;
            SyntaxOverlay.Document.TextAlignment = TextAlignment.Left;
        }

        private void MiniMapToggle_Changed(object sender, RoutedEventArgs e)
        {
            MiniMapPreview.Visibility = MiniMapToggle?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EditorFontSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CodeEditor == null || SyntaxOverlay == null || MiniMapPreview == null)
                return;
            CodeEditor.FontSize = e.NewValue;
            SyntaxOverlay.FontSize = e.NewValue;
            MiniMapPreview.FontSize = Math.Max(5, e.NewValue * 0.45);
            UpdateCaretPosition();
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            WordWrapToggle_Changed(sender, e);
            MiniMapToggle_Changed(sender, e);
            EditorFontSlider_ValueChanged(EditorFontSlider, new RoutedPropertyChangedEventArgs<double>(EditorFontSlider.Value, EditorFontSlider.Value));
            AppendConsole("[Info] Settings applied.\n", Colors.LightSkyBlue);
        }

        private void UpdateMiniMap()
        {
            var lines = CodeEditor.Text.Split('\n');
            if (lines.Length == 0)
            {
                MiniMapPreview.Text = string.Empty;
                return;
            }

            MiniMapPreview.Text = string.Join("\n", lines.Select(l =>
            {
                var trimmed = l.Trim();
                if (trimmed.Length > 56) trimmed = trimmed.Substring(0, 56);
                return trimmed;
            }));
        }

        private void SyncMiniMapScroll(ScrollChangedEventArgs e)
        {
            var miniScroll = FindVisualChild<ScrollViewer>(MiniMapPreview);
            if (miniScroll == null || _editorScrollViewer == null || _editorScrollViewer.ExtentHeight <= 0)
                return;

            var ratio = e.VerticalOffset / Math.Max(1.0, _editorScrollViewer.ScrollableHeight);
            var target = ratio * miniScroll.ScrollableHeight;
            miniScroll.ScrollToVerticalOffset(target);
        }

        private void UpdatePinnedScope()
        {
            var caret = CodeEditor.CaretIndex;
            var upToCaret = CodeEditor.Text.Substring(0, Math.Max(0, Math.Min(caret, CodeEditor.Text.Length)));
            var lines = upToCaret.Split('\n');

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("function", StringComparison.OrdinalIgnoreCase))
                {
                    PinnedScopeText.Text = $"Scope: {line} ";
                    return;
                }
                if (line.StartsWith("local function", StringComparison.OrdinalIgnoreCase))
                {
                    PinnedScopeText.Text = $"Scope: {line} ";
                    return;
                }
            }

            PinnedScopeText.Text = "Scope: Global ";
        }

        private void UpdateSyntaxHighlighting()
        {
            var text = CodeEditor.Text ?? string.Empty;
            SyntaxOverlay.Document.Blocks.Clear();

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
                PageWidth = 100000.0,   
                ColumnWidth = 100000.0
            };

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            if (string.IsNullOrEmpty(text) || text.Length > 50000)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    paragraph.Inlines.Add(new Run(text) { Foreground = Brushes.White });
                }
                doc.Blocks.Add(paragraph);
                SyntaxOverlay.Document = doc;
                return;
            }

            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "function", "local", "if", "then", "else", "elseif", "end", "for", "while", "do", "repeat", "until", "return", "break" };

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                // Single-line comment: --
                if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
                {
                    int start = i;
                    while (i < text.Length && text[i] != '\n') i++;
                    paragraph.Inlines.Add(new Run(text.Substring(start, i - start)) { Foreground = Brushes.Gray });
                    continue;
                }

                // String literals
                if (c == '"' || c == '\'')
                {
                    char q = c;
                    int start = i++;
                    bool esc = false;
                    while (i < text.Length)
                    {
                        if (esc) { esc = false; i++; continue; }
                        if (text[i] == '\\') { esc = true; i++; continue; }
                        if (text[i] == q) { i++; break; }
                        i++;
                    }
                    paragraph.Inlines.Add(new Run(text.Substring(start, i - start))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xE6, 0xA3))
                    });
                    continue;
                }

                // Identifiers and keywords
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i++;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    var token = text.Substring(start, i - start);
                    var run = new Run(token);
                    run.Foreground = keywords.Contains(token)
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x8C))
                        : new SolidColorBrush(Color.FromRgb(0xF3, 0xF0, 0xFF));
                    paragraph.Inlines.Add(run);
                    continue;
                }

                // Numbers
                if (char.IsDigit(c))
                {
                    int start = i++;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                    paragraph.Inlines.Add(new Run(text.Substring(start, i - start))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x8C))
                    });
                    continue;
                }

                // Everything else
                paragraph.Inlines.Add(new Run(c.ToString())
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF0, 0xFF))
                });
                i++;
            }

            doc.Blocks.Add(paragraph);
            SyntaxOverlay.Document = doc;
        }

        private void ValidateBasicSyntax()
        {
            var text = CodeEditor.Text;
            int paren = 0, brace = 0;
            foreach (var c in text)
            {
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
            }

            var starts = text.Split('\n').Count(l =>
                l.TrimStart().StartsWith("function", StringComparison.OrdinalIgnoreCase) ||
                l.TrimStart().StartsWith("if", StringComparison.OrdinalIgnoreCase) ||
                l.TrimStart().StartsWith("for", StringComparison.OrdinalIgnoreCase) ||
                l.TrimStart().StartsWith("while", StringComparison.OrdinalIgnoreCase));

            var ends = text.Split('\n').Count(l => l.Trim().Equals("end", StringComparison.OrdinalIgnoreCase));

            if (paren < 0 || brace < 0)
            {
                SyntaxStatusText.Text = "Syntax status: Unexpected closing bracket. ";
                SyntaxStatusText.Foreground = Brushes.OrangeRed;
            }
            else if (paren > 0 || brace > 0 || ends < starts)
            {
                SyntaxStatusText.Text = "Syntax status: Possibly missing closing token. ";
                SyntaxStatusText.Foreground = Brushes.Orange;
            }
            else
            {
                SyntaxStatusText.Text = "Syntax status: Looks valid. ";
                SyntaxStatusText.Foreground = Brushes.LightGreen;
            }
        }

        private void SettingsTab_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            ConsoleOutput.Visibility = Visibility.Collapsed;
            ThemePanel.Visibility = Visibility.Visible;
            SettingsTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            SettingsTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            EditorTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            EditorTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            ConsoleTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            ConsoleTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void ApplyThemeAccent(string accentHex, string editorStart, string editorMid, string editorEnd)
        {
            var accent = (Color)ColorConverter.ConvertFromString(accentHex);
            ((SolidColorBrush)Resources["NeonPurple"]).Color = accent;
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, accent.R, accent.G, accent.B));
            MainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = accent,
                ShadowDepth = 0,
                BlurRadius = 24,
                Opacity = 0.75
            };

            if (Resources["EditorAnimatedBackgroundBrush"] is LinearGradientBrush brush)
            {
                brush.GradientStops[0].Color = (Color)ColorConverter.ConvertFromString(editorStart);
                brush.GradientStops[1].Color = (Color)ColorConverter.ConvertFromString(editorMid);
                brush.GradientStops[2].Color = (Color)ColorConverter.ConvertFromString(editorEnd);
            }
        }

        private void ThemePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string theme)
                return;

            switch (theme)
            {
                case "Ocean":
                    ApplyThemeAccent("#1FC8FF", "#071825", "#0B2E40", "#154E68");
                    break;
                case "Sunset":
                    ApplyThemeAccent("#FF6A3D", "#26110B", "#4A1E12", "#6E2B14");
                    break;
                case "Forest":
                    ApplyThemeAccent("#3DDC84", "#0D1E16", "#163125", "#204B36");
                    break;
                default:
                    ApplyThemeAccent("#A020F0", "#101020", "#14142A", "#1F1235");
                    break;
            }
        }
        private void UpdateCaretPosition()
        {
            if (_caretOverlay == null || !CodeEditor.IsFocused) return;

            try
            {
                var rect = CodeEditor.GetRectFromCharacterIndex(CodeEditor.CaretIndex, false);
                if (rect.IsEmpty) return;

                var grid = CodeEditor.Parent as Grid;
                if (grid == null) return;

                var point = CodeEditor.TranslatePoint(new Point(rect.Left, rect.Top), grid);

                // Consolas 13pt has ~1px right-side bearing — 1.8px is perfect
                double offsetX = 1.8;
                double lineHeight = 15.6;
                double caretTop = point.Y + (lineHeight - rect.Height) / 2;

                _caretOverlay.Margin = new Thickness(point.X + offsetX, caretTop, 0, 0);
                _caretOverlay.Height = lineHeight;
                _caretOverlay.Visibility = Visibility.Visible;
            }
            catch { }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SynapseZAPI2.StopInstancesTimer();
            Application.Current.Shutdown();
        }
    }
}
