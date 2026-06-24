using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// Representa un elemento en el árbol de exploración (Base de Datos, Tabla o Columna).
    /// Soporta carga bajo demanda (Lazy Loading) al expandirse en la UI.
    /// </summary>
    public class DatabaseTreeItem : BaseViewModel
    {
        private string _name;
        private string _type; // "Database", "Table", "Column", "Dummy", "Error"
        private bool _isExpanded;
        private bool _isLoaded;
        private ObservableCollection<DatabaseTreeItem> _items = new();
        private readonly Func<DatabaseTreeItem, Task> _loadChildrenFunc;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value && !_isLoaded && _loadChildrenFunc != null)
                {
                    LoadChildrenAsync();
                }
            }
        }

        public ObservableCollection<DatabaseTreeItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public DatabaseTreeItem(string name, string type, Func<DatabaseTreeItem, Task> loadChildrenFunc = null, bool hasDummyChild = false)
        {
            Name = name;
            Type = type;
            _loadChildrenFunc = loadChildrenFunc;

            if (hasDummyChild)
            {
                Items.Add(new DatabaseTreeItem("Cargando...", "Dummy"));
            }
        }

        private async void LoadChildrenAsync()
        {
            try
            {
                Items.Clear();

                if (_loadChildrenFunc != null)
                {
                    await _loadChildrenFunc(this);
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                Items.Clear();
                Items.Add(new DatabaseTreeItem($"Error: {ex.Message}", "Error"));
            }
        }
    }
}
