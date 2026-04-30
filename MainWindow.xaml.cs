#nullable disable
using SynapseZ;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ScriptExecutorUI
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AccordionSection> Sections { get; set; } = new ObservableCollection<AccordionSection>();
        private uint _selectedPid = 0;
        private CancellationTokenSource _cts = null;

        public MainWindow()
        {
            InitializeComponent();
            LoadAccordionData();
            ApplyStaticGlowEffects();

            // Start automatic session tracking
            SynapseZAPI2.StartInstancesTimer();

            // Wire session events
            SynapseZAPI2.SessionAdded += OnSessionAdded;
            SynapseZAPI2.SessionRemoved += OnSessionRemoved;
            SynapseZAPI2.SessionOutput += OnSessionOutput;
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
                range.Text = text;
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
                ConsoleOutput.ScrollToEnd();
            });
        }

        private void OnSessionAdded(SynapseZAPI2.SynapseSession session)
        {
            Dispatcher.Invoke(() =>
                AppendConsole($"[Session Added] PID: {session.Pid}\n", Colors.Cyan));
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
            ConsoleOutput.Visibility = Visibility.Collapsed;
            EditorTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            EditorTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            ConsoleTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            ConsoleTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void ConsoleTab_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Visibility = Visibility.Collapsed;
            ConsoleOutput.Visibility = Visibility.Visible;
            ConsoleTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            ConsoleTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            EditorTabBtn.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            EditorTabBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void ClearEditor_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Clear();
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