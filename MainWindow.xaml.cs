#nullable disable
using SynapseZ;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace ScriptExecutorUI
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AccordionSection> Sections { get; set; } = new ObservableCollection<AccordionSection>();
        private uint _selectedPid = 0;
        private CancellationTokenSource _cts = null;
        private ScrollViewer _editorScrollViewer;
        private readonly string[] _suncSuggestions =
        {
            "print", "warn", "error", "getgenv", "getrenv", "getgc", "getinstances",
            "getnilinstances", "getconnections", "hookfunction", "hookmetamethod",
            "newcclosure", "checkcaller", "loadstring", "getsenv", "getloadedmodules",
            "getrunningscripts", "getscriptbytecode", "debug.getinfo", "debug.getupvalues",
            "debug.getconstants", "setthreadidentity", "getthreadidentity", "setclipboard",
            "rconsoleshow", "rconsoleprint", "rconsolewarn", "rconsoleerr", "WebSocket.connect",
            "function", "local", "if", "then", "elseif", "else", "for", "while", "repeat", "until",
            "pcall", "xpcall", "pairs", "ipairs", "table.insert", "table.remove", "task.wait", "task.spawn",
            "game:GetService", "workspace", "game.Players", "game.ReplicatedStorage", "game.ServerStorage",
            "game:GetService(\"Players\")", "game:GetService(\"TweenService\")", "game:GetService(\"RunService\")",
            "game:GetService(\"UserInputService\")", "game:GetService(\"ReplicatedStorage\")",
            "Instance.new", "Vector3.new", "CFrame.new", "UDim2.new", "Color3.fromRGB",
            "math.clamp", "math.floor", "math.random", "string.format", "string.split",
            "table.find", "table.clear", "task.delay", "task.defer", "task.cancel",
            "RemoteEvent:FireServer", "RemoteEvent:FireClient", "RemoteEvent:FireAllClients",
            "RemoteFunction:InvokeServer", "RunService.Heartbeat:Connect", "RunService.RenderStepped:Connect",
            "Players.PlayerAdded:Connect", "CharacterAdded:Connect", "Touched:Connect", "Changed:Connect",
            "Humanoid", "Animator", "TweenInfo.new", "Enum", "RaycastParams.new", "workspace:Raycast"
        };

        public MainWindow()
        {
            InitializeComponent();
            LoadAccordionData();
            ApplyStaticGlowEffects();
            AppendConsole($"[Ready] Console initialized at {DateTime.Now:HH:mm:ss}\n", Colors.LightSkyBlue);

            // Start automatic session tracking
            SynapseZAPI2.StartInstancesTimer();

            // Wire session events
            SynapseZAPI2.SessionAdded += OnSessionAdded;
            SynapseZAPI2.SessionRemoved += OnSessionRemoved;
            SynapseZAPI2.SessionOutput += OnSessionOutput;
            DetectRobloxAndConnection();
        }

        private void LoadAccordionData()
        {
            Sections = new ObservableCollection<AccordionSection>
            {
                new AccordionSection { Name = "Tabs", Items = { "Main.lua", "AutoFarm.lua", "ESP.lua" } },
                new AccordionSection { Name = "Saved Scripts", Items = { "Universal.lua", "Dex.lua", "DarkHub.lua" } },
                new AccordionSection { Name = "Auto-execute", Items = { "On attach", "On injection", "Custom event" } }
            };
            AccordionItems.ItemsSource = Sections;
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
                SelectedPidTxt.Text = $"PID: {_selectedPid}";
                AppendConsole($"Selected PID: {_selectedPid}\n", Colors.Cyan);
            }
        }
        private void DetectRobloxAndConnection()
        {
            try
            {
                var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                if (robloxProcesses.Length == 0)
                {
                    ConnectionStatusTxt.Text = "No Roblox detected";
                    return;
                }

                var injected = SynapseZAPI.GetSynzRobloxInstances();
                if (injected.Count > 0)
                {
                    _selectedPid = (uint)injected[0].Id;
                    SelectedPidTxt.Text = $"PID: {_selectedPid}";
                    ConnectionStatusTxt.Text = $"Connected to: {_selectedPid}";
                    AppendConsole($"[Connected] Successful connection to PID {_selectedPid}\n", Colors.LimeGreen);
                }
                else
                {
                    ConnectionStatusTxt.Text = "Roblox detected (not injected)";
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
                SelectedPidTxt.Text = $"PID: {_selectedPid}";
                ConnectionStatusTxt.Text = $"Connected to: {_selectedPid}";
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
                0 => Colors.White,       // print
                1 => Colors.Cyan,        // info
                2 => Colors.Orange,      // warn
                3 => Colors.Red,         // error
                _ => Colors.Gray
            };
            string label = type switch
            {
                0 => "[print]",
                1 => "[info]",
                2 => "[warn]",
                3 => "[error]",
                _ => "[?]"
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
            var confirm = MessageBox.Show("Are you sure you want to clear the editor text?", "Clear editor", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
            CodeEditor.Clear();
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Are you sure you want to clear console output?", "Clear console", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        private void PasteEditor_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                var caret = CodeEditor.CaretIndex;
                CodeEditor.Text = CodeEditor.Text.Insert(caret, Clipboard.GetText());
                CodeEditor.CaretIndex = caret + Clipboard.GetText().Length;
            }
        }

        private void FormatEditor_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Text = CodeEditor.Text.Replace("\t", "    ");
            AppendConsole("[Info] Replaced tabs with spaces.\n", Colors.LightSkyBlue);
        }

        private void CodeEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateLineNumbers();
            var caret = CodeEditor.CaretIndex;
            var text = CodeEditor.Text;
            var start = caret - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
            {
                start--;
            }

            start++;
            if (start >= caret || start < 0 || caret > text.Length)
            {
                SuggestionPopup.Visibility = Visibility.Collapsed;
                return;
            }

            var token = text.Substring(start, caret - start);
            if (token.Length < 1)
            {
                SuggestionPopup.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _suncSuggestions
                .Where(x => x.StartsWith(token, StringComparison.OrdinalIgnoreCase) || x.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(10)
                .ToList();
            if (matches.Count == 0)
            {
                SuggestionPopup.Visibility = Visibility.Collapsed;
                return;
            }

            SuggestionListBox.ItemsSource = matches;
            SuggestionListBox.SelectedIndex = 0;
            PositionSuggestionPopup();
            SuggestionPopup.Visibility = Visibility.Visible;
        }

        private void SuggestionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionListBox.SelectedItem is string selected)
            {
                InsertSuggestion(selected);
            }
        }

        private void InsertSuggestion(string selected)
        {
            var caret = CodeEditor.CaretIndex;
            var text = CodeEditor.Text;
            var start = caret - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '.' || text[start] == '_'))
            {
                start--;
            }
            start++;

            CodeEditor.Text = text.Remove(start, caret - start).Insert(start, selected);
            CodeEditor.CaretIndex = start + selected.Length;
            SuggestionPopup.Visibility = Visibility.Collapsed;
        }

        private void CodeEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == "(")
            {
                var caret = CodeEditor.CaretIndex;
                CodeEditor.Text = CodeEditor.Text.Insert(caret, "()");
                CodeEditor.CaretIndex = caret + 1;
                e.Handled = true;
                return;
            }

            if (e.Text == "\"")
            {
                var caret = CodeEditor.CaretIndex;
                CodeEditor.Text = CodeEditor.Text.Insert(caret, "\"\"");
                CodeEditor.CaretIndex = caret + 1;
                e.Handled = true;
            }
        }

        private void CodeEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !SuggestionPopup.IsVisible)
            {
                HandleAutoIndentEnter();
                e.Handled = true;
                return;
            }

            if (!SuggestionPopup.IsVisible)
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
            else if (e.Key == Key.Enter && SuggestionListBox.SelectedItem is string selectedSuggestion)
            {
                InsertSuggestion(selectedSuggestion);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && SuggestionListBox.SelectedItem is string selected)
            {
                InsertSuggestion(selected);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SuggestionPopup.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void UpdateLineNumbers()
        {
            var lineCount = Math.Max(1, CodeEditor.LineCount);
            LineNumbersText.Text = string.Join("\n", Enumerable.Range(1, lineCount));
        }

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
            _editorScrollViewer = FindVisualChild<ScrollViewer>(CodeEditor);
            if (_editorScrollViewer != null)
            {
                _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
            }
        }

        private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            LineNumbersText.RenderTransform = new TranslateTransform(0, -e.VerticalOffset);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                    return typed;
                var found = FindVisualChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (SuggestionPopup.IsVisible)
            {
                PositionSuggestionPopup();
            }
        }

        private void PositionSuggestionPopup()
        {
            var caretRect = CodeEditor.GetRectFromCharacterIndex(CodeEditor.CaretIndex, true);
            if (caretRect.IsEmpty)
                return;

            SuggestionPopup.Placement = PlacementMode.Relative;
            SuggestionPopup.HorizontalOffset = Math.Max(12, caretRect.X + 8);
            SuggestionPopup.VerticalOffset = Math.Max(16, caretRect.Y + caretRect.Height + 6);
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
                trimmed.EndsWith("repeat", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("{", StringComparison.OrdinalIgnoreCase);

            var extraIndent = opensBlock ? "    " : string.Empty;
            var insert = "\n" + currentIndent + extraIndent;

            CodeEditor.Text = text.Insert(caret, insert);
            CodeEditor.CaretIndex = caret + insert.Length;
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
