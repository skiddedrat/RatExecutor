#nullable disable
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace ScriptExecutorUI
{
    public partial class ProcessPickerDialog : Window
    {
        public uint SelectedPid { get; private set; } = 0;

        public ProcessPickerDialog()
        {
            InitializeComponent();
            LoadRobloxProcesses();
        }

        private void LoadRobloxProcesses()
        {
            var processes = Process.GetProcessesByName("RobloxPlayerBeta");
            var items = new List<ProcessItem>();
            foreach (var p in processes)
            {
                try
                {
                    items.Add(new ProcessItem
                    {
                        Pid = (uint)p.Id,
                        Name = p.ProcessName
                    });
                }
                catch { /* skip inaccessible */ }
            }
            ProcessListBox.ItemsSource = items;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListBox.SelectedItem is ProcessItem item)
            {
                SelectedPid = item.Pid;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a process first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class ProcessItem
        {
            public uint Pid { get; set; }
            public string Name { get; set; }
        }
    }
}