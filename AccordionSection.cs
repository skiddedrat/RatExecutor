#nullable disable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScriptExecutorUI
{
    public class AccordionSection : INotifyPropertyChanged
    {
        private bool _isExpanded;
        public string Name { get; set; }
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
        public ObservableCollection<string> Items { get; set; } = new ObservableCollection<string>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}