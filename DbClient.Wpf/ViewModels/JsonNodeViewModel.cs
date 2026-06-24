using System.Collections.ObjectModel;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// Representa un nodo en el visor jerárquico de JSON (campos, valores, sub-registros).
    /// </summary>
    public class JsonNodeViewModel : BaseViewModel
    {
        private string _key;
        private object _value;
        private bool _isExpanded = false;
        private bool _isVisible = true;
        private ObservableCollection<JsonNodeViewModel> _children = new();

        private bool _isEditable;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public object Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    ValueChangedAction?.Invoke(this);
                }
            }
        }

        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value);
        }

        public System.Action<JsonNodeViewModel> ValueChangedAction { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public ObservableCollection<JsonNodeViewModel> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        public bool IsObject => Children.Count > 0;
    }
}
