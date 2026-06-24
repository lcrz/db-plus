using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using DbClient.Core;
using DbClient.Wpf.Models;
using DbClient.Wpf.Services;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel principal para el shell de la aplicación.
    /// Administra la transición entre el Gestor de Conexiones (desconectado) y el Editor SQL (conectado).
    /// </summary>
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IDatabasePlugin _databasePlugin;

        // Propiedades de Estado
        private bool _isConnected;
        private bool _isBusy;
        private string _statusMessage = "Listo.";
        private Guid _activeConnectionId;
        
        // Perfil de conexión activo
        private string _activeConnectionName;
        private string _activeServerInfo;
        private bool _isInitialConnectionLoad;

        // Gestión y Filtrado de Bases de Datos
        private readonly ObservableCollection<string> _databases = new();
        private string _databaseSearchText = string.Empty;
        private string _selectedDatabase;

        // Gestión y Filtrado de Tablas
        private readonly ObservableCollection<DatabaseTreeItem> _tables = new();
        private string _tableSearchText = string.Empty;

        // Pestaña activa en la UI principal
        private int _selectedTabIndex;
        private bool _isStructureEditorActive;
        private bool _isTableDataActive;

        // Estructura de Tabla Activa
        private string _activeTableStructureName;
        private ObservableCollection<ColumnStructureViewModel> _columnsStructureList = new();
        private readonly List<string> _deletedColumnNames = new();
        private string _activeTableComment;
        private string _originalTableComment;
        private ObservableCollection<IndexSchema> _indexesList = new();
        private ObservableCollection<ForeignKeySchema> _foreignKeysList = new();

        public string ActiveTableComment
        {
            get => _activeTableComment;
            set => SetProperty(ref _activeTableComment, value);
        }

        public string OriginalTableComment
        {
            get => _originalTableComment;
            set => SetProperty(ref _originalTableComment, value);
        }

        // Sub-ViewModels
        public ConnectionManagerViewModel ConnectionManager { get; }
        public SqlEditorViewModel SqlEditor { get; }
        public ErDiagramViewModel ErDiagram { get; }
        public ObservableCollection<TableDataViewModel> TableDataTabs { get; } = new();

        private TableDataViewModel _selectedTableDataTab;
        public TableDataViewModel SelectedTableDataTab
        {
            get => _selectedTableDataTab;
            set
            {
                if (SetProperty(ref _selectedTableDataTab, value))
                {
                    SaveTableTabsState();
                }
            }
        }

        private readonly AppDataStorageService _appDataStorageService;
        private readonly ConnectionStorageService _connectionStorageService;
        private bool _isLoadingTableTabs;

        private readonly ObservableCollection<string> _savedQueries = new();
        public ObservableCollection<string> SavedQueries => _savedQueries;

        private string _selectedSavedQuery;
        public string SelectedSavedQuery
        {
            get => _selectedSavedQuery;
            set => SetProperty(ref _selectedSavedQuery, value);
        }

        // Búsqueda Rápida (Ctrl + P)
        private bool _isQuickSearchOpen;
        public bool IsQuickSearchOpen
        {
            get => _isQuickSearchOpen;
            set => SetProperty(ref _isQuickSearchOpen, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    UpdateQuickSearchFilter();
                }
            }
        }

        public ObservableCollection<QuickSearchItem> FilteredSearchItems { get; } = new();

        private QuickSearchItem _selectedSearchItem;
        public QuickSearchItem SelectedSearchItem
        {
            get => _selectedSearchItem;
            set => SetProperty(ref _selectedSearchItem, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(IsDisconnected));
                }
            }
        }

        public bool IsDisconnected => !IsConnected;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ActiveConnectionName
        {
            get => _activeConnectionName;
            set => SetProperty(ref _activeConnectionName, value);
        }

        public string ActiveServerInfo
        {
            get => _activeServerInfo;
            set => SetProperty(ref _activeServerInfo, value);
        }

        // Vistas de Colección Filtradas para WPF Binding
        public ICollectionView DatabasesView { get; }
        public ICollectionView TablesView { get; }

        public string DatabaseSearchText
        {
            get => _databaseSearchText;
            set
            {
                if (SetProperty(ref _databaseSearchText, value))
                {
                    DatabasesView.Refresh();
                }
            }
        }

        public string SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (SetProperty(ref _selectedDatabase, value))
                {
                    SqlEditor.SaveTabsState();
                    SaveTableTabsState();
                    ErDiagram?.OnDatabaseChanged(value);

                    if (!string.IsNullOrEmpty(value))
                    {
                        _ = LoadTablesForSelectedDatabaseAsync(value);
                    }
                    else
                    {
                        _tables.Clear();
                        OnPropertyChanged(nameof(TablesCountInfo));
                    }

                    // Refresh databases view to clear/unapply the filter since SelectedDatabase has changed
                    DatabasesView.Refresh();
                }
            }
        }

        public string TableSearchText
        {
            get => _tableSearchText;
            set
            {
                if (SetProperty(ref _tableSearchText, value))
                {
                    TablesView.Refresh();
                    OnPropertyChanged(nameof(TablesCountInfo));
                }
            }
        }

        public string TablesCountInfo
        {
            get
            {
                int total = _tables.Count;
                int visible = 0;
                foreach (var _ in TablesView)
                {
                    visible++;
                }
                return $"ENTITIES  {visible} / {total}";
            }
        }

        public string ProviderName => _databasePlugin.PluginName;

        // Selección de pestaña e integración de vista
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    if (value == 4)
                    {
                        _ = ErDiagram?.LoadDiagramIfNeededAsync(SelectedDatabase);
                    }
                }
            }
        }

        public bool IsStructureEditorActive
        {
            get => _isStructureEditorActive;
            set => SetProperty(ref _isStructureEditorActive, value);
        }

        public bool IsTableDataActive
        {
            get => _isTableDataActive;
            set => SetProperty(ref _isTableDataActive, value);
        }

        public string ActiveTableStructureName
        {
            get => _activeTableStructureName;
            set => SetProperty(ref _activeTableStructureName, value);
        }

        public ObservableCollection<ColumnStructureViewModel> ColumnsStructureList
        {
            get => _columnsStructureList;
            set => SetProperty(ref _columnsStructureList, value);
        }

        public ObservableCollection<IndexSchema> IndexesList
        {
            get => _indexesList;
            set => SetProperty(ref _indexesList, value);
        }

        public ObservableCollection<ForeignKeySchema> ForeignKeysList
        {
            get => _foreignKeysList;
            set => SetProperty(ref _foreignKeysList, value);
        }

        // Comandos del Espacio de Trabajo
        public ICommand DisconnectCommand { get; }
        public ICommand RefreshSchemaCommand { get; }

        // Comandos del Menú Contextual de Tablas
        public ICommand ViewDataCommand { get; }
        public ICommand ViewStructureCommand { get; }
        public ICommand CopyTableNameCommand { get; }
        public ICommand ShowCreateSqlCommand { get; }
        public ICommand TruncateTableCommand { get; }
        public ICommand DropTableCommand { get; }

        // Comandos del Diseñador de Estructura de Tabla
        public ICommand AddColumnCommand { get; }
        public ICommand DeleteColumnCommand { get; }
        public ICommand SaveStructureCommand { get; }
        public ICommand CloseStructureCommand { get; }
        public ICommand DeleteIndexCommand { get; }
        public ICommand DeleteForeignKeyCommand { get; }

        // Comandos del Visor de Datos
        public ICommand CloseTableDataTabCommand { get; }
        public ICommand CloseOtherTableDataTabsCommand { get; }
        public ICommand CloseTableDataTabsToTheRightCommand { get; }
        public ICommand CloseAllTableDataTabsCommand { get; }

        // Comandos de Consultas Guardadas
        public ICommand LoadSavedQueriesCommand { get; }
        public ICommand OpenSavedQueryCommand { get; }
        public ICommand DeleteSavedQueryCommand { get; }

        // Comandos de Búsqueda Rápida (Ctrl + P)
        public ICommand ShowQuickSearchCommand { get; }
        public ICommand CloseQuickSearchCommand { get; }
        public ICommand ExecuteQuickSearchItemCommand { get; }

        public MainWindowViewModel(
            IDatabasePlugin databasePlugin, 
            ConnectionManagerViewModel connectionManager,
            SqlEditorViewModel sqlEditor,
            AppDataStorageService appDataStorageService,
            ConnectionStorageService connectionStorageService)
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            ConnectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            SqlEditor = sqlEditor ?? throw new ArgumentNullException(nameof(sqlEditor));
            _appDataStorageService = appDataStorageService ?? throw new ArgumentNullException(nameof(appDataStorageService));
            _connectionStorageService = connectionStorageService ?? throw new ArgumentNullException(nameof(connectionStorageService));
            ErDiagram = new ErDiagramViewModel(databasePlugin, this);

            // Configurar vistas de colección filtradas
            DatabasesView = CollectionViewSource.GetDefaultView(_databases);
            DatabasesView.Filter = FilterDatabases;

            TablesView = CollectionViewSource.GetDefaultView(_tables);
            TablesView.Filter = FilterTables;

            // Escuchar el evento de conexión establecida del ConnectionManager
            ConnectionManager.ConnectionEstablished += OnConnectionEstablished;

            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => IsConnected && !IsBusy);
            RefreshSchemaCommand = new RelayCommand(async () => await RefreshSchemaAsync(), () => IsConnected && !IsBusy);

            // Comandos contextuales de Tablas
            ViewDataCommand = new RelayCommand<DatabaseTreeItem>(async (t) => await ViewDataAsync(t), (t) => IsConnected && !IsBusy);
            ViewStructureCommand = new RelayCommand<DatabaseTreeItem>(async (t) => await ViewStructureAsync(t), (t) => IsConnected && !IsBusy);
            CopyTableNameCommand = new RelayCommand<DatabaseTreeItem>(CopyTableName, (t) => IsConnected);
            ShowCreateSqlCommand = new RelayCommand<DatabaseTreeItem>(async (t) => await ShowCreateSqlAsync(t), (t) => IsConnected && !IsBusy);
            TruncateTableCommand = new RelayCommand<DatabaseTreeItem>(async (t) => await TruncateTableAsync(t), (t) => IsConnected && !IsBusy);
            DropTableCommand = new RelayCommand<DatabaseTreeItem>(async (t) => await DropTableAsync(t), (t) => IsConnected && !IsBusy);

            // Comandos del Diseñador de Estructuras
            AddColumnCommand = new RelayCommand(AddColumn, () => IsConnected && !IsBusy && IsStructureEditorActive);
            DeleteColumnCommand = new RelayCommand<ColumnStructureViewModel>(DeleteColumn, (c) => IsConnected && !IsBusy && IsStructureEditorActive && c != null);
            SaveStructureCommand = new RelayCommand(async () => await SaveStructureAsync(), () => IsConnected && !IsBusy && IsStructureEditorActive);
            CloseStructureCommand = new RelayCommand(CloseStructure, () => IsConnected && !IsBusy);
            DeleteIndexCommand = new RelayCommand<IndexSchema>(async (idx) => await DeleteIndexAsync(idx), (idx) => IsConnected && !IsBusy && IsStructureEditorActive && idx != null);
            DeleteForeignKeyCommand = new RelayCommand<ForeignKeySchema>(async (fk) => await DeleteForeignKeyAsync(fk), (fk) => IsConnected && !IsBusy && IsStructureEditorActive && fk != null);

            // Comandos del Visor de Datos
            CloseTableDataTabCommand = new RelayCommand<TableDataViewModel>(CloseTableDataTab);
            CloseOtherTableDataTabsCommand = new RelayCommand<TableDataViewModel>(CloseOtherTableDataTabs);
            CloseTableDataTabsToTheRightCommand = new RelayCommand<TableDataViewModel>(CloseTableDataTabsToTheRight);
            CloseAllTableDataTabsCommand = new RelayCommand(CloseAllTableDataTabs);

            // Comandos de Consultas Guardadas
            LoadSavedQueriesCommand = new RelayCommand(LoadSavedQueries);
            OpenSavedQueryCommand = new RelayCommand<string>(OpenSavedQuery);
            DeleteSavedQueryCommand = new RelayCommand<string>(DeleteSavedQuery);

            // Comandos de Búsqueda Rápida (Ctrl + P)
            ShowQuickSearchCommand = new RelayCommand(ShowQuickSearch, () => IsConnected);
            CloseQuickSearchCommand = new RelayCommand(CloseQuickSearch, () => IsQuickSearchOpen);
            ExecuteQuickSearchItemCommand = new RelayCommand<QuickSearchItem>(ExecuteQuickSearchItem);

            SqlEditor.QuerySaved += LoadSavedQueries;

            IsConnected = false;
            LoadSavedQueries();
        }

        private bool FilterDatabases(object item)
        {
            if (string.IsNullOrWhiteSpace(DatabaseSearchText)) return true;
            if (DatabaseSearchText == SelectedDatabase) return true;
            string db = item as string;
            return db != null && db.Contains(DatabaseSearchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool FilterTables(object item)
        {
            if (string.IsNullOrWhiteSpace(TableSearchText)) return true;
            DatabaseTreeItem tableNode = item as DatabaseTreeItem;
            return tableNode != null && tableNode.Name.Contains(TableSearchText, StringComparison.OrdinalIgnoreCase);
        }

        private async void OnConnectionEstablished(Models.ConnectionDetails connectionDetails)
        {
            IsBusy = true;
            _activeConnectionId = connectionDetails.Id;
            ActiveConnectionName = connectionDetails.Name;
            ActiveServerInfo = $"{connectionDetails.Server}:{connectionDetails.Port} ({connectionDetails.Username})";
            StatusMessage = $"Conectado a MySQL - {ActiveConnectionName}";
            
            // Limpiar listas anteriores
            _databases.Clear();
            _tables.Clear();
            DatabaseSearchText = string.Empty;
            TableSearchText = string.Empty;

            SqlEditor.Tables = new System.Collections.Generic.List<string>();

            // Limpiar estructura
            ColumnsStructureList.Clear();
            _deletedColumnNames.Clear();
            ActiveTableStructureName = string.Empty;
            ActiveTableComment = string.Empty;
            OriginalTableComment = string.Empty;
            IsStructureEditorActive = false;

            // Limpiar visor de datos
            TableDataTabs.Clear();
            SelectedTableDataTab = null;
            IsTableDataActive = false;

            try
            {
                // Iniciar la carga de bases de datos
                await LoadDatabasesAsync();
                
                _isInitialConnectionLoad = true;

                // Pre-seleccionar la primera base de datos o la inicial configurada
                var savedState = _appDataStorageService.LoadViewStateForConnection(_activeConnectionId.ToString());
                string dbToSelect = null;
                if (savedState != null && !string.IsNullOrEmpty(savedState.SelectedDatabase) && _databases.Contains(savedState.SelectedDatabase))
                {
                    dbToSelect = savedState.SelectedDatabase;
                }
                else if (!string.IsNullOrWhiteSpace(connectionDetails.DatabaseName) && _databases.Contains(connectionDetails.DatabaseName))
                {
                    dbToSelect = connectionDetails.DatabaseName;
                }
                else if (_databases.Count > 0)
                {
                    dbToSelect = _databases[0];
                }

                if (dbToSelect != null)
                {
                    SelectedDatabase = dbToSelect;
                }
                else
                {
                    _isInitialConnectionLoad = false;
                }

                IsConnected = true;
                SelectedTabIndex = 0; // Mostrar pestaña de Editor SQL al arrancar
            }
            catch (Exception ex)
            {
                _isInitialConnectionLoad = false;
                StatusMessage = $"Error al cargar metadatos: {ex.Message}";
                IsConnected = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisconnectAsync()
        {
            IsBusy = true;
            StatusMessage = "Cerrando conexión...";
            try
            {
                SaveViewState();
                SqlEditor.SaveTabsState();
                SaveTableTabsState();
                await _databasePlugin.DisconnectAsync();
                IsConnected = false;
                StatusMessage = "Desconectado.";
                
                _databases.Clear();
                _tables.Clear();
                DatabaseSearchText = string.Empty;
                TableSearchText = string.Empty;
                
                SqlEditor.Tabs.Clear();
                SqlEditor.Tables = new System.Collections.Generic.List<string>();

                ColumnsStructureList.Clear();
                _deletedColumnNames.Clear();
                IndexesList.Clear();
                ForeignKeysList.Clear();
                ActiveTableStructureName = string.Empty;
                ActiveTableComment = string.Empty;
                OriginalTableComment = string.Empty;
                IsStructureEditorActive = false;

                TableDataTabs.Clear();
                SelectedTableDataTab = null;
                IsTableDataActive = false;
                ErDiagram?.Clear();
                
                ActiveConnectionName = string.Empty;
                ActiveServerInfo = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al desconectar: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDatabasesAsync()
        {
            var dbs = await _databasePlugin.GetDatabasesAsync();
            foreach (var db in dbs)
            {
                _databases.Add(db);
            }
        }

        private async Task LoadTablesForSelectedDatabaseAsync(string databaseName)
        {
            IsBusy = true;
            StatusMessage = $"Cargando tablas de '{databaseName}'...";
            _tables.Clear();
            TableSearchText = string.Empty;

            try
            {
                var tablesList = await _databasePlugin.GetTablesAsync(databaseName);
                
                // Actualizar las tablas en el editor SQL para autocompletado inmediato
                SqlEditor.Tables = tablesList;

                foreach (var table in tablesList)
                {
                    _tables.Add(new DatabaseTreeItem(
                        table,
                        "Table",
                        async (node) => await LoadColumnsAsync(node),
                        hasDummyChild: true
                    ));
                }

                OnPropertyChanged(nameof(TablesCountInfo));
                
                // Cargar las pestañas del editor SQL para este servidor y base de datos
                SqlEditor.LoadTabs(_activeConnectionId.ToString(), databaseName);

                // Cargar las pestañas de datos de tablas para este servidor y base de datos
                LoadTableTabs(_activeConnectionId.ToString(), databaseName);

                // Restaurar el estado de vista del servidor si es la carga inicial
                if (_isInitialConnectionLoad)
                {
                    var savedState = _appDataStorageService.LoadViewStateForConnection(_activeConnectionId.ToString());
                    if (savedState != null)
                    {
                        // Restauramos filtro de tablas
                        if (!string.IsNullOrEmpty(savedState.TableSearchText))
                        {
                            TableSearchText = savedState.TableSearchText;
                        }

                        // Restauramos tablas expandidas
                        if (savedState.ExpandedTables != null && savedState.ExpandedTables.Count > 0)
                        {
                            foreach (var tableName in savedState.ExpandedTables)
                            {
                                var tableItem = _tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
                                if (tableItem != null)
                                {
                                    tableItem.IsExpanded = true;
                                }
                            }
                        }
                    }
                    _isInitialConnectionLoad = false;
                }

                StatusMessage = $"Listo. Tablas de '{databaseName}' cargadas con éxito.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar tablas de '{databaseName}': {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadColumnsAsync(DatabaseTreeItem tableNode)
        {
            try
            {
                var columns = await _databasePlugin.GetColumnsAsync(tableNode.Name);
                foreach (var col in columns)
                {
                    tableNode.Items.Add(new DatabaseTreeItem(col, "Column"));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al listar columnas de '{tableNode.Name}': {ex.Message}";
                throw;
            }
        }

        private async Task RefreshSchemaAsync()
        {
            if (string.IsNullOrEmpty(SelectedDatabase)) return;

            IsBusy = true;
            StatusMessage = "Refrescando esquema de base de datos...";
            try
            {
                string currentDb = SelectedDatabase;

                // Limpiar y recargar base de datos
                _databases.Clear();
                await LoadDatabasesAsync();

                if (_databases.Contains(currentDb))
                {
                    SelectedDatabase = currentDb;
                    await LoadTablesForSelectedDatabaseAsync(currentDb);
                }
                else if (_databases.Count > 0)
                {
                    SelectedDatabase = _databases[0];
                }
                else
                {
                    SelectedDatabase = null;
                    _tables.Clear();
                    OnPropertyChanged(nameof(TablesCountInfo));
                }

                StatusMessage = "Esquema refrescado correctamente.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al refrescar esquema: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Operaciones de Menú Contextual
        private async Task ViewDataAsync(DatabaseTreeItem tableNode)
        {
            if (tableNode == null) return;

            var existingTab = System.Linq.Enumerable.FirstOrDefault(TableDataTabs, t => string.Equals(t.TableName, tableNode.Name, StringComparison.OrdinalIgnoreCase));
            if (existingTab != null)
            {
                SelectedTableDataTab = existingTab;
            }
            else
            {
                var newTab = CreateTableDataTab();
                TableDataTabs.Add(newTab);
                SelectedTableDataTab = newTab;
                await newTab.InitializeAsync(SelectedDatabase, tableNode.Name);
            }

            IsTableDataActive = true;
            SelectedTabIndex = 3; // Focus Interactive Table Data Viewer tab
        }

        private TableDataViewModel CreateTableDataTab()
        {
            var tab = new TableDataViewModel(_databasePlugin, _connectionStorageService);
            tab.RequestOpenTableTab += OnRequestOpenTableTab;
            return tab;
        }

        private async void OnRequestOpenTableTab(string referencedTable, string referencedColumn, string value)
        {
            var existingTab = System.Linq.Enumerable.FirstOrDefault(TableDataTabs, t => string.Equals(t.TableName, referencedTable, StringComparison.OrdinalIgnoreCase));
            if (existingTab != null)
            {
                SelectedTableDataTab = existingTab;
                await existingTab.InitializeWithFilterAsync(SelectedDatabase, referencedTable, referencedColumn, value);
            }
            else
            {
                var newTab = CreateTableDataTab();
                TableDataTabs.Add(newTab);
                SelectedTableDataTab = newTab;
                await newTab.InitializeWithFilterAsync(SelectedDatabase, referencedTable, referencedColumn, value);
            }

            IsTableDataActive = true;
            SelectedTabIndex = 3; // Focus Interactive Table Data Viewer tab
        }

        private void CopyTableName(DatabaseTreeItem node)
        {
            if (node == null) return;
            try
            {
                System.Windows.Clipboard.SetText(node.Name);
                string typeDisplay = node.Type switch
                {
                    "Database" => "base de datos",
                    "Table" => "tabla",
                    "Column" => "columna",
                    _ => "elemento"
                };
                StatusMessage = $"Nombre de {typeDisplay} '{node.Name}' copiado al portapapeles.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al copiar al portapapeles: {ex.Message}";
            }
        }

        private async Task ShowCreateSqlAsync(DatabaseTreeItem tableNode)
        {
            if (tableNode == null) return;
            IsBusy = true;
            StatusMessage = "Obteniendo definición SQL (DDL)...";
            try
            {
                await _databasePlugin.GetTablesAsync(SelectedDatabase);
                var query = $"SHOW CREATE TABLE `{SelectedDatabase}`.`{tableNode.Name}`;";
                var dt = await _databasePlugin.ExecuteQueryAsync(query);
                if (dt.Rows.Count > 0)
                {
                    string createSql = dt.Rows[0][1].ToString();
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var win = new Views.SqlPreviewWindow(createSql)
                        {
                            Owner = System.Windows.Application.Current.MainWindow
                        };
                        win.ShowDialog();
                    });
                    StatusMessage = "Listo.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al obtener definición SQL: {ex.Message}";
                System.Windows.MessageBox.Show($"Error al obtener DDL: {ex.Message}", "Error DDL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TruncateTableAsync(DatabaseTreeItem tableNode)
        {
            if (tableNode == null) return;
            var res = System.Windows.MessageBox.Show(
                $"¿Está seguro de que desea vaciar (TRUNCATE) la tabla '{tableNode.Name}'? Esto borrará permanentemente todos sus registros y datos.",
                "Confirmar Vaciar Tabla",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                IsBusy = true;
                StatusMessage = $"Vaciando tabla '{tableNode.Name}'...";
                try
                {
                    await _databasePlugin.GetTablesAsync(SelectedDatabase);
                    await _databasePlugin.ExecuteQueryAsync($"TRUNCATE TABLE `{SelectedDatabase}`.`{tableNode.Name}`;");
                    StatusMessage = $"Tabla '{tableNode.Name}' vaciada correctamente.";
                    System.Windows.MessageBox.Show($"La tabla '{tableNode.Name}' se ha vaciado con éxito.", "Vaciado Completado", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error al vaciar tabla: {ex.Message}";
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error Truncate", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task DropTableAsync(DatabaseTreeItem tableNode)
        {
            if (tableNode == null) return;
            var res = System.Windows.MessageBox.Show(
                $"¿Está seguro de que desea eliminar (DROP) la tabla '{tableNode.Name}' permanentemente? Se eliminarán su estructura y todos sus datos de forma irreversible.",
                "Confirmar Eliminar Tabla",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Stop
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                IsBusy = true;
                StatusMessage = $"Eliminando tabla '{tableNode.Name}'...";
                try
                {
                    await _databasePlugin.GetTablesAsync(SelectedDatabase);
                    await _databasePlugin.ExecuteQueryAsync($"DROP TABLE `{SelectedDatabase}`.`{tableNode.Name}`;");
                    StatusMessage = $"Tabla '{tableNode.Name}' eliminada con éxito.";
                    await LoadTablesForSelectedDatabaseAsync(SelectedDatabase);
                    System.Windows.MessageBox.Show($"La tabla '{tableNode.Name}' ha sido eliminada.", "Tabla Eliminada", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error al eliminar tabla: {ex.Message}";
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error Drop", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ViewStructureAsync(DatabaseTreeItem tableNode)
        {
            if (tableNode == null) return;
            IsBusy = true;
            StatusMessage = $"Cargando estructura de '{tableNode.Name}'...";
            ActiveTableStructureName = tableNode.Name;
            ColumnsStructureList.Clear();
            _deletedColumnNames.Clear();
            IndexesList.Clear();
            ForeignKeysList.Clear();

            try
            {
                var schema = await _databasePlugin.GetTableSchemaAsync(SelectedDatabase, tableNode.Name);
                ActiveTableComment = schema.Description;
                OriginalTableComment = schema.Description;

                foreach (var col in schema.Columns)
                {
                    ColumnsStructureList.Add(new ColumnStructureViewModel
                    {
                        Field = col.Name,
                        OriginalField = col.Name,
                        Type = col.Type,
                        OriginalType = col.Type,
                        IsNullable = col.IsNullable,
                        OriginalIsNullable = col.IsNullable,
                        IsPrimaryKey = col.IsPrimaryKey,
                        DefaultVal = col.DefaultValue,
                        OriginalDefaultVal = col.DefaultValue,
                        Extra = col.Extra,
                        IsNew = false,
                        IsDeleted = false
                    });
                }

                if (schema.Indexes != null)
                {
                    foreach (var idx in schema.Indexes)
                    {
                        IndexesList.Add(idx);
                    }
                }

                if (schema.ForeignKeys != null)
                {
                    foreach (var fk in schema.ForeignKeys)
                    {
                        ForeignKeysList.Add(fk);
                    }
                }

                IsStructureEditorActive = true;
                SelectedTabIndex = 2; // Abrir la pestaña del diseñador de estructuras
                StatusMessage = $"Estructura de '{tableNode.Name}' cargada con éxito.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar estructura: {ex.Message}";
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error de Estructura", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Operaciones de Diseñador de Estructuras
        private void CloseStructure()
        {
            IsStructureEditorActive = false;
            SelectedTabIndex = 0; // Return to SQL Editor
        }

        private void CloseTableDataTab(TableDataViewModel tab)
        {
            if (tab == null) return;
            int index = TableDataTabs.IndexOf(tab);
            TableDataTabs.Remove(tab);

            if (SelectedTableDataTab == tab || SelectedTableDataTab == null)
            {
                if (TableDataTabs.Count > 0)
                {
                    SelectedTableDataTab = TableDataTabs[Math.Min(index, TableDataTabs.Count - 1)];
                }
                else
                {
                    IsTableDataActive = false;
                    SelectedTabIndex = 0; // Return to SQL Editor
                }
            }
            else
            {
                SaveTableTabsState();
            }
        }

        private void CloseOtherTableDataTabs(TableDataViewModel tab)
        {
            if (tab == null) return;
            var toRemove = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(TableDataTabs, t => t != tab));
            foreach (var t in toRemove)
            {
                TableDataTabs.Remove(t);
            }
            SelectedTableDataTab = tab;
            SaveTableTabsState();
        }

        private void CloseTableDataTabsToTheRight(TableDataViewModel tab)
        {
            if (tab == null) return;
            int idx = TableDataTabs.IndexOf(tab);
            if (idx < 0) return;

            var toRemove = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Skip(TableDataTabs, idx + 1));
            foreach (var t in toRemove)
            {
                TableDataTabs.Remove(t);
            }
            SelectedTableDataTab = tab;
            SaveTableTabsState();
        }

        private void CloseAllTableDataTabs()
        {
            TableDataTabs.Clear();
            IsTableDataActive = false;
            SelectedTabIndex = 0; // Return to SQL Editor
        }

        public void SaveTableTabsState()
        {
            if (_isLoadingTableTabs)
                return;

            if (string.IsNullOrEmpty(_activeConnectionId.ToString()) || string.IsNullOrEmpty(SelectedDatabase))
                return;

            var list = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(TableDataTabs, t => new SavedTableTabState
            {
                TableName = t.TableName,
                IsActive = t == SelectedTableDataTab
            }));

            _appDataStorageService.SaveTableTabsForConnection(_activeConnectionId.ToString(), SelectedDatabase, list);
        }

        public void LoadTableTabs(string connectionId, string databaseName)
        {
            _isLoadingTableTabs = true;
            try
            {
                TableDataTabs.Clear();
                var saved = _appDataStorageService.LoadTableTabsForConnection(connectionId, databaseName);
                if (saved != null && saved.Count > 0)
                {
                    TableDataViewModel activeTab = null;
                    foreach (var s in saved)
                    {
                        var tab = CreateTableDataTab();
                        TableDataTabs.Add(tab);
                        _ = tab.InitializeAsync(databaseName, s.TableName);
                        if (s.IsActive)
                        {
                            activeTab = tab;
                        }
                    }
                    SelectedTableDataTab = activeTab ?? TableDataTabs.FirstOrDefault();
                    IsTableDataActive = true;
                }
                else
                {
                    IsTableDataActive = false;
                }
            }
            finally
            {
                _isLoadingTableTabs = false;
            }
        }

        private void AddColumn()
        {
            ColumnsStructureList.Add(new ColumnStructureViewModel
            {
                Field = "nueva_columna",
                Type = "varchar(50)",
                IsNullable = true,
                IsNew = true
            });
        }

        private void DeleteColumn(ColumnStructureViewModel col)
        {
            if (col == null) return;
            if (!col.IsNew)
            {
                _deletedColumnNames.Add(col.OriginalField);
            }
            ColumnsStructureList.Remove(col);
        }

        private async Task DeleteIndexAsync(IndexSchema index)
        {
            if (index == null) return;
            var res = System.Windows.MessageBox.Show(
                $"¿Está seguro de que desea eliminar el índice '{index.Name}'?",
                "Confirmar Eliminar Índice",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                IsBusy = true;
                StatusMessage = $"Eliminando índice '{index.Name}'...";
                try
                {
                    await _databasePlugin.DropIndexAsync(SelectedDatabase, ActiveTableStructureName, index.Name);
                    StatusMessage = $"Índice '{index.Name}' eliminado correctamente.";
                    
                    // Recargar estructura visual
                    var dummyNode = new DatabaseTreeItem(ActiveTableStructureName, "Table");
                    await ViewStructureAsync(dummyNode);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error al eliminar índice: {ex.Message}";
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error al Eliminar Índice", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task DeleteForeignKeyAsync(ForeignKeySchema fk)
        {
            if (fk == null) return;
            var res = System.Windows.MessageBox.Show(
                $"¿Está seguro de que desea eliminar la llave foránea '{fk.Name}'?",
                "Confirmar Eliminar Llave Foránea",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                IsBusy = true;
                StatusMessage = $"Eliminando llave foránea '{fk.Name}'...";
                try
                {
                    await _databasePlugin.DropForeignKeyAsync(SelectedDatabase, ActiveTableStructureName, fk.Name);
                    StatusMessage = $"Llave foránea '{fk.Name}' eliminada correctamente.";
                    
                    // Recargar estructura visual
                    var dummyNode = new DatabaseTreeItem(ActiveTableStructureName, "Table");
                    await ViewStructureAsync(dummyNode);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error al eliminar llave foránea: {ex.Message}";
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error al Eliminar Llave Foránea", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task SaveStructureAsync()
        {
            if (string.IsNullOrEmpty(ActiveTableStructureName)) return;

            IsBusy = true;
            StatusMessage = "Guardando cambios en la estructura...";

            var alteration = new TableAlteration
            {
                OriginalDescription = OriginalTableComment,
                NewDescription = ActiveTableComment
            };

            // 1. Procesar columnas eliminadas
            foreach (var colName in _deletedColumnNames)
            {
                alteration.ColumnAlterations.Add(new ColumnAlteration
                {
                    Type = AlterationType.Drop,
                    OriginalName = colName
                });
            }

            // 2. Procesar columnas añadidas y modificadas
            foreach (var col in ColumnsStructureList)
            {
                if (string.IsNullOrWhiteSpace(col.Field))
                {
                    System.Windows.MessageBox.Show("El nombre del campo no puede estar vacío.", "Validación de Estructura", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    IsBusy = false;
                    return;
                }

                var colSchema = new ColumnSchema
                {
                    Name = col.Field,
                    Type = col.Type,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = col.IsPrimaryKey,
                    DefaultValue = col.DefaultVal,
                    Extra = col.Extra
                };

                if (col.IsNew)
                {
                    alteration.ColumnAlterations.Add(new ColumnAlteration
                    {
                        Type = AlterationType.Add,
                        Column = colSchema
                    });
                }
                else
                {
                    bool nameChanged = col.Field != col.OriginalField;
                    bool typeChanged = col.Type != col.OriginalType;
                    bool nullChanged = col.IsNullable != col.OriginalIsNullable;
                    bool defaultChanged = col.DefaultVal != col.OriginalDefaultVal;

                    if (nameChanged || typeChanged || nullChanged || defaultChanged)
                    {
                        alteration.ColumnAlterations.Add(new ColumnAlteration
                        {
                            Type = AlterationType.Modify,
                            OriginalName = col.OriginalField,
                            Column = colSchema
                        });
                    }
                }
            }

            bool commentChanged = OriginalTableComment != ActiveTableComment;
            if (alteration.ColumnAlterations.Count == 0 && !commentChanged)
            {
                System.Windows.MessageBox.Show("No se detectaron cambios en la estructura o descripción de la tabla.", "Sin Cambios", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                IsBusy = false;
                return;
            }

            try
            {
                await _databasePlugin.AlterTableAsync(SelectedDatabase, ActiveTableStructureName, alteration);

                _deletedColumnNames.Clear();
                StatusMessage = "Estructura de la tabla actualizada con éxito.";
                System.Windows.MessageBox.Show("Los cambios en la estructura de la tabla se han aplicado correctamente.", "Guardado Exitoso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Recargar estructura visual
                var dummyNode = new DatabaseTreeItem(ActiveTableStructureName, "Table");
                await ViewStructureAsync(dummyNode);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al aplicar cambios de estructura: {ex.Message}";
                System.Windows.MessageBox.Show($"Error al guardar estructura: {ex.Message}", "Error al Guardar", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadSavedQueries()
        {
            _savedQueries.Clear();
            try
            {
                var names = _connectionStorageService.GetSavedQueryNames();
                foreach (var name in names)
                {
                    _savedQueries.Add(name);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar consultas guardadas: {ex.Message}";
            }
        }

        private void OpenSavedQuery(string queryName)
        {
            if (string.IsNullOrWhiteSpace(queryName)) return;
            try
            {
                string sqlText = _connectionStorageService.LoadSavedQuery(queryName);
                SqlEditor.AddNewTab(queryName, sqlText);
                SelectedTabIndex = 0; // Cambiar a la pestaña del editor SQL
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al abrir la consulta '{queryName}': {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void DeleteSavedQuery(string queryName)
        {
            if (string.IsNullOrWhiteSpace(queryName)) return;
            var res = System.Windows.MessageBox.Show(
                $"¿Está seguro de que desea eliminar permanentemente la consulta guardada '{queryName}'?",
                "Confirmar Eliminación",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );
            if (res == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    _connectionStorageService.DeleteSavedQuery(queryName);
                    LoadSavedQueries();
                    StatusMessage = $"Consulta '{queryName}' eliminada.";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error al eliminar la consulta: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ShowQuickSearch()
        {
            SearchQuery = string.Empty;
            IsQuickSearchOpen = true;
            UpdateQuickSearchFilter();
        }

        private void CloseQuickSearch()
        {
            IsQuickSearchOpen = false;
        }

        private void ExecuteQuickSearchItem(QuickSearchItem item)
        {
            if (item == null) return;
            IsQuickSearchOpen = false;

            if (item.Type == "Table" && item.Data is DatabaseTreeItem tableNode)
            {
                _ = ViewDataAsync(tableNode);
            }
            else if (item.Type == "Query" && item.Data is string queryName)
            {
                OpenSavedQuery(queryName);
            }
        }

        private List<QuickSearchItem> GetAllQuickSearchItems()
        {
            var list = new List<QuickSearchItem>();
            
            // Add tables
            foreach (var tableNode in _tables)
            {
                list.Add(new QuickSearchItem
                {
                    Name = tableNode.Name,
                    Type = "Table",
                    Data = tableNode
                });
            }

            // Add saved queries
            foreach (var queryName in SavedQueries)
            {
                list.Add(new QuickSearchItem
                {
                    Name = queryName,
                    Type = "Query",
                    Data = queryName
                });
            }

            return list;
        }

        private void UpdateQuickSearchFilter()
        {
            FilteredSearchItems.Clear();
            var allItems = GetAllQuickSearchItems();

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                foreach (var item in allItems)
                {
                    FilteredSearchItems.Add(item);
                }
                if (FilteredSearchItems.Count > 0)
                {
                    SelectedSearchItem = FilteredSearchItems[0];
                }
                return;
            }

            var terms = SearchQuery.ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in allItems)
            {
                var itemNameLower = item.Name.ToLowerInvariant();
                bool isMatch = true;
                foreach (var term in terms)
                {
                    if (!itemNameLower.Contains(term))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    FilteredSearchItems.Add(item);
                }
            }

            if (FilteredSearchItems.Count > 0)
            {
                SelectedSearchItem = FilteredSearchItems[0];
            }
        }

        public void SaveViewState()
        {
            if (!IsConnected || _activeConnectionId == Guid.Empty) return;

            var expandedTables = new List<string>();
            foreach (var tableItem in _tables)
            {
                if (tableItem.Type == "Table" && tableItem.IsExpanded)
                {
                    expandedTables.Add(tableItem.Name);
                }
            }

            var state = new ServerViewState
            {
                SelectedDatabase = SelectedDatabase ?? string.Empty,
                TableSearchText = TableSearchText ?? string.Empty,
                ExpandedTables = expandedTables
            };

            _appDataStorageService.SaveViewStateForConnection(_activeConnectionId.ToString(), state);
        }
    }
}
