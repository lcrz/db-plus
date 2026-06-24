using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using DbClient.Core;

namespace DbClient.Wpf.ViewModels
{
    public class ColumnNodeViewModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
    }

    public class TableNodeViewModel : BaseViewModel
    {
        private double _x;
        private double _y;
        private bool _isHighlighted;
        private bool _isVisible = true;
        private double _opacity = 1.0;
        private bool _areColumnsVisible = true;

        public string TableName { get; set; }
        public ObservableCollection<ColumnNodeViewModel> Columns { get; set; } = new();

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Width { get; set; } = 220;

        public double Height => AreColumnsVisible ? (35 + (Columns.Count * 22) + 12) : 38;

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        public bool AreColumnsVisible
        {
            get => _areColumnsVisible;
            set
            {
                if (SetProperty(ref _areColumnsVisible, value))
                {
                    OnPropertyChanged(nameof(Height));
                }
            }
        }
    }

    public class RelationshipConnectionViewModel : BaseViewModel, IDisposable
    {
        private double _startX;
        private double _startY;
        private double _endX;
        private double _endY;
        private bool _isHighlighted;
        private bool _isVisible = true;
        private double _opacity = 0.5;

        public TableNodeViewModel SourceTable { get; }
        public TableNodeViewModel TargetTable { get; }
        public string SourceColumn { get; }
        public string TargetColumn { get; }

        public double StartX
        {
            get => _startX;
            set => SetProperty(ref _startX, value);
        }

        public double StartY
        {
            get => _startY;
            set => SetProperty(ref _startY, value);
        }

        public double EndX
        {
            get => _endX;
            set => SetProperty(ref _endX, value);
        }

        public double EndY
        {
            get => _endY;
            set => SetProperty(ref _endY, value);
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        public RelationshipConnectionViewModel(TableNodeViewModel sourceTable, TableNodeViewModel targetTable, string sourceColumn, string targetColumn)
        {
            SourceTable = sourceTable ?? throw new ArgumentNullException(nameof(sourceTable));
            TargetTable = targetTable ?? throw new ArgumentNullException(nameof(targetTable));
            SourceColumn = sourceColumn;
            TargetColumn = targetColumn;

            SourceTable.PropertyChanged += OnTablePropertyChanged;
            TargetTable.PropertyChanged += OnTablePropertyChanged;

            IsVisible = SourceTable.IsVisible && TargetTable.IsVisible;
            UpdateCoordinates();
        }

        private void OnTablePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TableNodeViewModel.X) || 
                e.PropertyName == nameof(TableNodeViewModel.Y) ||
                e.PropertyName == nameof(TableNodeViewModel.IsHighlighted) ||
                e.PropertyName == nameof(TableNodeViewModel.AreColumnsVisible))
            {
                UpdateCoordinates();
            }
            else if (e.PropertyName == nameof(TableNodeViewModel.IsVisible))
            {
                IsVisible = SourceTable.IsVisible && TargetTable.IsVisible;
            }
        }

        public void UpdateCoordinates()
        {
            UpdateCoordinates(false);
        }

        public void UpdateCoordinates(bool isSearchActive)
        {
            if (SourceTable == null || TargetTable == null) return;

            StartX = SourceTable.X + SourceTable.Width / 2;
            StartY = SourceTable.Y + SourceTable.Height / 2;

            EndX = TargetTable.X + TargetTable.Width / 2;
            EndY = TargetTable.Y + TargetTable.Height / 2;

            IsHighlighted = SourceTable.IsHighlighted || TargetTable.IsHighlighted;

            if (isSearchActive)
            {
                Opacity = IsHighlighted ? 1.0 : 0.08;
            }
            else
            {
                Opacity = 0.5;
            }
        }

        public void Dispose()
        {
            if (SourceTable != null)
                SourceTable.PropertyChanged -= OnTablePropertyChanged;
            if (TargetTable != null)
                TargetTable.PropertyChanged -= OnTablePropertyChanged;
        }
    }

    public class ErDiagramViewModel : BaseViewModel
    {
        private readonly IDatabasePlugin _databasePlugin;
        private readonly MainWindowViewModel _mainViewModel;
        private bool _isLoading;
        private string _errorMessage;
        private double _zoomScale = 1.0;
        private double _panX = 0.0;
        private double _panY = 0.0;
        private string _searchQuery = string.Empty;
        private string _tableListSearchQuery = string.Empty;
        private string _currentDatabase;
        private bool _isLoaded;

        public ObservableCollection<TableNodeViewModel> Tables { get; } = new();
        public ObservableCollection<RelationshipConnectionViewModel> Connections { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public double ZoomScale
        {
            get => _zoomScale;
            set => SetProperty(ref _zoomScale, Math.Max(0.1, Math.Min(3.0, value)));
        }

        public double PanX
        {
            get => _panX;
            set => SetProperty(ref _panX, value);
        }

        public double PanY
        {
            get => _panY;
            set => SetProperty(ref _panY, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplySearchFilter();
                }
            }
        }

        public string TableListSearchQuery
        {
            get => _tableListSearchQuery;
            set
            {
                if (SetProperty(ref _tableListSearchQuery, value))
                {
                    TablesListView.Refresh();
                }
            }
        }

        public ICollectionView TablesListView { get; }

        public ICommand LoadDiagramCommand { get; }
        public ICommand AutoLayoutCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand SelectAllTablesCommand { get; }
        public ICommand UnselectAllTablesCommand { get; }
        public ICommand ShowOnlyThisAndRelatedCommand { get; }
        public ICommand ToggleColumnsVisibilityCommand { get; }
        public ICommand ToggleAllColumnsVisibilityCommand { get; }

        public ErDiagramViewModel(IDatabasePlugin databasePlugin, MainWindowViewModel mainViewModel)
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

            TablesListView = CollectionViewSource.GetDefaultView(Tables);
            TablesListView.Filter = FilterTablesList;

            LoadDiagramCommand = new RelayCommand(async () => await LoadDiagramAsync(_mainViewModel.SelectedDatabase), () => _mainViewModel.IsConnected && !IsLoading && !string.IsNullOrEmpty(_mainViewModel.SelectedDatabase));
            AutoLayoutCommand = new RelayCommand(AutoLayout, () => Tables.Count > 0);
            ZoomInCommand = new RelayCommand(() => ZoomScale += 0.1);
            ZoomOutCommand = new RelayCommand(() => ZoomScale -= 0.1);
            ResetZoomCommand = new RelayCommand(ResetZoom);
            SelectAllTablesCommand = new RelayCommand(() => ToggleAllTables(true), () => Tables.Count > 0);
            UnselectAllTablesCommand = new RelayCommand(() => ToggleAllTables(false), () => Tables.Count > 0);
            ShowOnlyThisAndRelatedCommand = new RelayCommand<TableNodeViewModel>(ShowOnlyThisAndRelated, (t) => t != null);
            ToggleColumnsVisibilityCommand = new RelayCommand<TableNodeViewModel>(ToggleColumnsVisibility, (t) => t != null);
            ToggleAllColumnsVisibilityCommand = new RelayCommand<TableNodeViewModel>(ToggleAllColumnsVisibility, (t) => t != null);
        }

        private bool FilterTablesList(object item)
        {
            if (string.IsNullOrWhiteSpace(TableListSearchQuery)) return true;
            var tableNode = item as TableNodeViewModel;
            return tableNode != null && tableNode.TableName.Contains(TableListSearchQuery, StringComparison.OrdinalIgnoreCase);
        }

        public async Task LoadDiagramIfNeededAsync(string database)
        {
            if (string.IsNullOrEmpty(database))
            {
                Clear();
                return;
            }

            if (_currentDatabase != database || !_isLoaded)
            {
                await LoadDiagramAsync(database);
            }
        }

        public void OnDatabaseChanged(string database)
        {
            Clear();
            _currentDatabase = database;
            _isLoaded = false;
        }

        public void Clear()
        {
            foreach (var conn in Connections)
            {
                conn.Dispose();
            }
            Connections.Clear();
            Tables.Clear();
            ZoomScale = 1.0;
            PanX = 0.0;
            PanY = 0.0;
            SearchQuery = string.Empty;
            TableListSearchQuery = string.Empty;
            ErrorMessage = null;
            _isLoaded = false;
        }

        public async Task LoadDiagramAsync(string database)
        {
            if (string.IsNullOrEmpty(database)) return;

            IsLoading = true;
            ErrorMessage = null;
            _mainViewModel.StatusMessage = "Cargando esquema para Diagrama ER...";

            try
            {
                Clear();
                _currentDatabase = database;

                var tableNames = await _databasePlugin.GetTablesAsync(database);
                var schemas = await _databasePlugin.GetTableSchemasAsync(database, tableNames);

                // 1. Create Table ViewModels
                var tableMap = new Dictionary<string, TableNodeViewModel>(StringComparer.OrdinalIgnoreCase);
                foreach (var schema in schemas)
                {
                    var tableNode = new TableNodeViewModel
                    {
                        TableName = schema.TableName
                    };

                    foreach (var col in schema.Columns)
                    {
                        // Check if it's a foreign key based on schema constraints
                        bool isFk = schema.ForeignKeys.Any(fk => 
                            fk.Columns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .Contains(col.Name, StringComparer.OrdinalIgnoreCase));

                        tableNode.Columns.Add(new ColumnNodeViewModel
                        {
                            Name = col.Name,
                            Type = col.Type,
                            IsPrimaryKey = col.IsPrimaryKey,
                            IsForeignKey = isFk
                        });
                    }

                    Tables.Add(tableNode);
                    tableMap[schema.TableName] = tableNode;
                }

                // 2. Create Connections ViewModels
                foreach (var schema in schemas)
                {
                    if (schema.ForeignKeys == null) continue;

                    foreach (var fk in schema.ForeignKeys)
                    {
                        if (string.IsNullOrEmpty(fk.ReferencedTable)) continue;

                        if (tableMap.TryGetValue(schema.TableName, out var sourceTable) && 
                            tableMap.TryGetValue(fk.ReferencedTable, out var targetTable))
                        {
                            var conn = new RelationshipConnectionViewModel(
                                sourceTable, 
                                targetTable, 
                                fk.Columns, 
                                fk.ReferencedColumns
                            );
                            Connections.Add(conn);
                        }
                    }
                }

                // 3. Arrange tables in initial layout
                AutoLayout();

                _isLoaded = true;
                _mainViewModel.StatusMessage = $"Diagrama ER cargado correctamente con {Tables.Count} tablas.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error al cargar diagrama: {ex.Message}";
                _mainViewModel.StatusMessage = ErrorMessage;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplySearchFilter()
        {
            bool isSearchActive = !string.IsNullOrWhiteSpace(SearchQuery);
            if (!isSearchActive)
            {
                foreach (var table in Tables)
                {
                    table.IsVisible = true;
                    table.IsHighlighted = false;
                    table.Opacity = 1.0;
                }
            }
            else
            {
                foreach (var table in Tables)
                {
                    bool matches = table.TableName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
                    table.IsVisible = true;
                    table.IsHighlighted = matches;
                    table.Opacity = matches ? 1.0 : 0.15;
                }
            }

            foreach (var conn in Connections)
            {
                conn.UpdateCoordinates(isSearchActive);
            }
        }

        private void ToggleAllTables(bool visible)
        {
            foreach (var table in Tables)
            {
                table.IsVisible = visible;
            }
        }

        private void ShowOnlyThisAndRelated(TableNodeViewModel targetNode)
        {
            if (targetNode == null) return;

            var relatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetNode.TableName };

            foreach (var conn in Connections)
            {
                if (string.Equals(conn.SourceTable.TableName, targetNode.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    relatedTables.Add(conn.TargetTable.TableName);
                }
                else if (string.Equals(conn.TargetTable.TableName, targetNode.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    relatedTables.Add(conn.SourceTable.TableName);
                }
            }

            foreach (var table in Tables)
            {
                table.IsVisible = relatedTables.Contains(table.TableName);
            }
        }

        private void ToggleColumnsVisibility(TableNodeViewModel node)
        {
            if (node == null) return;

            node.AreColumnsVisible = !node.AreColumnsVisible;

            foreach (var conn in Connections)
            {
                conn.UpdateCoordinates();
            }
        }

        private void ToggleAllColumnsVisibility(TableNodeViewModel referenceNode)
        {
            if (referenceNode == null) return;

            bool targetVisibility = !referenceNode.AreColumnsVisible;

            foreach (var table in Tables)
            {
                table.AreColumnsVisible = targetVisibility;
            }

            foreach (var conn in Connections)
            {
                conn.UpdateCoordinates();
            }
        }

        public void AutoLayout()
        {
            if (Tables.Count == 0) return;

            // Only layout visible tables!
            var visibleTables = Tables.Where(t => t.IsVisible).ToList();
            if (visibleTables.Count == 0) return;

            int count = visibleTables.Count;
            int cols = (int)Math.Ceiling(Math.Sqrt(count));

            double spacingX = 350;
            double spacingY = 320;

            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;

                visibleTables[i].X = 50 + c * spacingX;
                visibleTables[i].Y = 50 + r * spacingY;
            }

            ResetZoom();
        }

        private void ResetZoom()
        {
            ZoomScale = 1.0;
            PanX = 0.0;
            PanY = 0.0;
        }
    }
}
