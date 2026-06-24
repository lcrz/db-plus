using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using DbClient.Core;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que gestiona la visualización interactiva y edición de datos de una tabla.
    /// Soporta filtrado visual dinámico, ordenación y guardado de inserciones, ediciones y borrados.
    /// </summary>
    public class TableDataViewModel : BaseViewModel
    {
        public event Action<string, string, string> RequestOpenTableTab;
        private readonly IDatabasePlugin _databasePlugin;
        private readonly Services.ConnectionStorageService _connectionStorageService;
        private string _selectedDatabase;
        private string _tableName;
        private DataTable _queryResults;
        private DataRowView _selectedRow;
        private bool _isBusy;
        private bool _isConnected;
        private string _statusMessage = "Listo.";

        private readonly List<string> _primaryKeys = new();
        private readonly Dictionary<string, (string ReferencedTable, string ReferencedColumn)> _foreignKeys = new(StringComparer.OrdinalIgnoreCase);

        private bool _isUpdatingRowFromJson = false;
        private bool _isUpdatingJsonFromRow = false;

        public string SelectedDatabase
        {
            get => _selectedDatabase;
            set => SetProperty(ref _selectedDatabase, value);
        }

        public string TableName
        {
            get => _tableName;
            set => SetProperty(ref _tableName, value);
        }

        public DataTable QueryResults
        {
            get => _queryResults;
            set
            {
                if (_queryResults != null)
                {
                    _queryResults.ColumnChanged -= OnTableColumnChanged;
                }
                if (SetProperty(ref _queryResults, value))
                {
                    if (_queryResults != null)
                    {
                        _queryResults.ColumnChanged += OnTableColumnChanged;
                    }
                }
            }
        }

        public DataRowView SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    _ = UpdateJsonDetailsAsync(value);
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Listas auxiliares para la interfaz
        public ObservableCollection<ColumnStructureViewModel> Columns { get; } = new();
        public ObservableCollection<string> AvailableColumns { get; } = new();
        public List<string> AvailableOperators { get; }
        public List<string> AvailableLogicalOperators { get; }
        public ObservableCollection<TableFilterRow> Filters { get; } = new();
        public ObservableCollection<JsonNodeViewModel> JsonNodes { get; } = new();

        private string _jsonFilterText = string.Empty;
        public string JsonFilterText
        {
            get => _jsonFilterText;
            set
            {
                if (SetProperty(ref _jsonFilterText, value))
                {
                    ApplyJsonFilter(value);
                }
            }
        }

        public JsonCopyMode SelectedJsonCopyMode
        {
            get
            {
                if (_connectionStorageService != null && Enum.TryParse<JsonCopyMode>(_connectionStorageService.CurrentSettings.JsonCopyMode, out var mode))
                {
                    return mode;
                }
                return JsonCopyMode.KeyOnly;
            }
            set
            {
                if (_connectionStorageService != null)
                {
                    _connectionStorageService.CurrentSettings.JsonCopyMode = value.ToString();
                    _connectionStorageService.SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public List<JsonCopyModeOption> AvailableCopyModes { get; } = new()
        {
            new JsonCopyModeOption(JsonCopyMode.KeyOnly, "Copiar solo key"),
            new JsonCopyModeOption(JsonCopyMode.KeyAndValueJson, "Copiar key + valor (JSON)"),
            new JsonCopyModeOption(JsonCopyMode.KeyAndSubtreeJson, "Copiar key + sub-nodos (JSON)")
        };

        // Comandos de Interacción
        public ICommand LoadDataCommand { get; }
        public ICommand AddFilterRowCommand { get; }
        public ICommand RemoveFilterRowCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ApplyFilterOnEnterCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand InsertRowCommand { get; }
        public ICommand DeleteRowCommand { get; }
        public ICommand CopyAsSqlCommand { get; }
        public ICommand CopyAsJsonCommand { get; }
        public ICommand CopyAsCsvCommand { get; }
        public ICommand CopyNodeKeyCommand { get; }
        public ICommand CopyNodeKeyAndValueCommand { get; }
        public ICommand CopyNodeKeyAndSubtreeCommand { get; }

        public TableDataViewModel(IDatabasePlugin databasePlugin, Services.ConnectionStorageService connectionStorageService)
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            _connectionStorageService = connectionStorageService ?? throw new ArgumentNullException(nameof(connectionStorageService));

            AvailableLogicalOperators = new List<string> { "AND", "OR" };

            AvailableOperators = new List<string>
            {
                "equals",
                "does not equal",
                "like",
                "not like",
                "less than",
                "less than or equal",
                "greater than",
                "greater than or equal",
                "in",
                "is null",
                "is not null"
            };

            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync(), () => IsConnected && !IsBusy);
            AddFilterRowCommand = new RelayCommand(AddFilterRow);
            RemoveFilterRowCommand = new RelayCommand<TableFilterRow>(RemoveFilterRow);
            ApplyFiltersCommand = new RelayCommand(async () => await LoadDataAsync());
            ApplyFilterOnEnterCommand = new RelayCommand<TableFilterRow>(
                async (row) => await LoadDataAsync(),
                (row) => row != null && !string.IsNullOrWhiteSpace(row.Value)
            );
            SaveChangesCommand = new RelayCommand(async () => await SaveChangesAsync(), () => IsConnected && !IsBusy);
            InsertRowCommand = new RelayCommand(InsertRow, () => IsConnected && !IsBusy && QueryResults != null);
            DeleteRowCommand = new RelayCommand(DeleteRow, () => IsConnected && !IsBusy && SelectedRow != null);
            CopyAsSqlCommand = new RelayCommand<System.Collections.IList>(CopyAsSql);
            CopyAsJsonCommand = new RelayCommand<System.Collections.IList>(CopyAsJson);
            CopyAsCsvCommand = new RelayCommand<System.Collections.IList>(CopyAsCsv);
            CopyNodeKeyCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyOnly));
            CopyNodeKeyAndValueCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyAndValueJson));
            CopyNodeKeyAndSubtreeCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyAndSubtreeJson));
        }

        private void CopyAsSql(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            foreach (var item in selectedItems)
            {
                if (item is DataRowView rowView)
                {
                    var row = rowView.Row;
                    var columns = new List<string>();
                    var values = new List<string>();
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        columns.Add($"`{col.ColumnName}`");
                        if (row.IsNull(col))
                        {
                            values.Add("NULL");
                        }
                        else
                        {
                            var val = row[col].ToString().Replace("'", "''");
                            values.Add($"'{val}'");
                        }
                    }
                    sb.AppendLine($"INSERT INTO `{SelectedDatabase}`.`{TableName}` ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
                }
            }
            if (sb.Length > 0)
            {
                System.Windows.Clipboard.SetText(sb.ToString());
                StatusMessage = $"{selectedItems.Count} filas copiadas como SQL.";
            }
        }

        private void CopyAsJson(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            var list = new List<Dictionary<string, object>>();
            foreach (var item in selectedItems)
            {
                if (item is DataRowView rowView)
                {
                    var row = rowView.Row;
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        dict[col.ColumnName] = row.IsNull(col) ? null : row[col];
                    }
                    list.Add(dict);
                }
            }
            
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(list, jsonOptions);
            System.Windows.Clipboard.SetText(json);
            StatusMessage = $"{selectedItems.Count} filas copiadas como JSON.";
        }

        private void CopyAsCsv(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            
            if (selectedItems[0] is DataRowView firstRowView)
            {
                var headers = new List<string>();
                foreach (DataColumn col in firstRowView.Row.Table.Columns)
                {
                    headers.Add($"\"{col.ColumnName.Replace("\"", "\"\"")}\"");
                }
                sb.AppendLine(string.Join(",", headers));
            }

            foreach (var item in selectedItems)
            {
                if (item is DataRowView rowView)
                {
                    var row = rowView.Row;
                    var values = new List<string>();
                    foreach (DataColumn col in row.Table.Columns)
                    {
                        if (row.IsNull(col))
                        {
                            values.Add("");
                        }
                        else
                        {
                            var val = row[col].ToString().Replace("\"", "\"\"");
                            values.Add($"\"{val}\"");
                        }
                    }
                    sb.AppendLine(string.Join(",", values));
                }
            }
            if (sb.Length > 0)
            {
                System.Windows.Clipboard.SetText(sb.ToString());
                StatusMessage = $"{selectedItems.Count} filas copiadas como CSV.";
            }
        }

        public ColumnStructureViewModel GetColumnInfo(string columnName)
        {
            foreach (var col in Columns)
            {
                if (string.Equals(col.Field, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return col;
                }
            }
            return null;
        }

        public bool TryGetForeignKeyAssociation(string columnName, out string referencedTable, out string referencedColumn)
        {
            referencedTable = null;
            referencedColumn = null;
            if (_foreignKeys.TryGetValue(columnName, out var relation))
            {
                referencedTable = relation.ReferencedTable;
                referencedColumn = relation.ReferencedColumn;
                return true;
            }
            return false;
        }

        public void NavigateToForeignKey(DataRowView rowView, string columnName)
        {
            if (rowView == null || string.IsNullOrEmpty(columnName)) return;
            if (_foreignKeys.TryGetValue(columnName, out var relation))
            {
                var val = rowView[columnName];
                if (val != DBNull.Value && val != null)
                {
                    RequestOpenTableTab?.Invoke(relation.ReferencedTable, relation.ReferencedColumn, val.ToString());
                }
            }
        }

        public async Task InitializeAsync(string databaseName, string tableName)
        {
            SelectedDatabase = databaseName;
            TableName = tableName;
            IsConnected = true;

            // 1. Cargar el esquema primero para poblar AvailableColumns
            await LoadSchemaAsync();

            // 2. Cargar las relaciones reales de clave foránea desde la base de datos
            await LoadForeignKeysAsync();

            // 3. Limpiar y agregar el primer filtro por defecto con la columna inicial
            Filters.Clear();
            AddFilterRow();

            // 4. Limpiar visor json previo
            JsonNodes.Clear();
            JsonFilterText = string.Empty;

            // 5. Cargar registros
            await LoadDataAsync();
        }

        public async Task InitializeWithFilterAsync(string databaseName, string tableName, string filterCol, string filterVal)
        {
            SelectedDatabase = databaseName;
            TableName = tableName;
            IsConnected = true;

            await LoadSchemaAsync();
            await LoadForeignKeysAsync();

            Filters.Clear();
            Filters.Add(new TableFilterRow
            {
                IsLogicOperatorVisible = false,
                ColumnName = filterCol,
                Operator = "equals",
                Value = filterVal
            });

            JsonNodes.Clear();
            JsonFilterText = string.Empty;

            await LoadDataAsync();
        }

        private async Task LoadSchemaAsync()
        {
            Columns.Clear();
            _primaryKeys.Clear();
            AvailableColumns.Clear();

            try
            {
                var query = $"SHOW COLUMNS FROM `{SelectedDatabase}`.`{TableName}`;";
                var dt = await _databasePlugin.ExecuteQueryAsync(query);

                foreach (DataRow row in dt.Rows)
                {
                    string field = row["Field"].ToString();
                    string type = row["Type"].ToString();
                    bool isNull = string.Equals(row["Null"].ToString(), "YES", StringComparison.OrdinalIgnoreCase);
                    bool isKey = string.Equals(row["Key"].ToString(), "PRI", StringComparison.OrdinalIgnoreCase);
                    string defaultVal = row["Default"] == DBNull.Value ? null : row["Default"].ToString();
                    string extra = row["Extra"].ToString();

                    Columns.Add(new ColumnStructureViewModel
                    {
                        Field = field,
                        Type = type,
                        IsNullable = isNull,
                        IsPrimaryKey = isKey,
                        DefaultVal = defaultVal,
                        Extra = extra
                    });

                    AvailableColumns.Add(field);

                    if (isKey)
                    {
                        _primaryKeys.Add(field);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar metadatos de tabla: {ex.Message}";
            }
        }

        public async Task LoadDataAsync()
        {
            IsBusy = true;
            StatusMessage = "Cargando datos...";
            QueryResults = null;

            try
            {
                var sql = BuildSelectQuery();
                var dt = await _databasePlugin.ExecuteQueryAsync(sql);
                if (dt != null)
                {
                    dt.Constraints.Clear();
                    foreach (DataColumn column in dt.Columns)
                    {
                        column.AllowDBNull = true;
                        column.Unique = false;
                        column.ReadOnly = false;
                    }
                }
                QueryResults = dt;
                StatusMessage = $"Listo. Se cargaron {(dt?.Rows.Count ?? 0)} registros.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar datos: {ex.Message}";
                System.Windows.MessageBox.Show($"Error al cargar datos: {ex.Message}", "Error de Consulta", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string BuildSelectQuery()
        {
            var selectSql = $"SELECT * FROM `{SelectedDatabase}`.`{TableName}`";
            var conditions = new List<string>();

            for (int i = 0; i < Filters.Count; i++)
            {
                var filter = Filters[i];
                if (string.IsNullOrWhiteSpace(filter.ColumnName)) continue;

                // Omitir filtros con valores vacíos si el operador requiere valor
                bool requiresValue = filter.Operator != "is null" && filter.Operator != "is not null";
                if (requiresValue && string.IsNullOrWhiteSpace(filter.Value)) continue;

                var cond = BuildConditionSql(filter);
                if (string.IsNullOrEmpty(cond)) continue;

                if (conditions.Count == 0)
                {
                    conditions.Add(cond);
                }
                else
                {
                    conditions.Add($"{filter.LogicOperator} {cond}");
                }
            }

            if (conditions.Count > 0)
            {
                selectSql += " WHERE " + string.Join(" ", conditions);
            }

            selectSql += " LIMIT 1000;";
            return selectSql;
        }

        private string BuildConditionSql(TableFilterRow filter)
        {
            var col = $"`{filter.ColumnName}`";
            var op = filter.Operator;
            var val = filter.Value;

            switch (op)
            {
                case "equals":
                    return $"{col} = '{EscapeSqlString(val)}'";
                case "does not equal":
                    return $"{col} != '{EscapeSqlString(val)}'";
                case "like":
                    return $"{col} LIKE '%{EscapeSqlString(val)}%'";
                case "not like":
                    return $"{col} NOT LIKE '%{EscapeSqlString(val)}%'";
                case "less than":
                    return $"{col} < '{EscapeSqlString(val)}'";
                case "less than or equal":
                    return $"{col} <= '{EscapeSqlString(val)}'";
                case "greater than":
                    return $"{col} > '{EscapeSqlString(val)}'";
                case "greater than or equal":
                    return $"{col} >= '{EscapeSqlString(val)}'";
                case "in":
                    return $"{col} IN ({val})";
                case "is null":
                    return $"{col} IS NULL";
                case "is not null":
                    return $"{col} IS NOT NULL";
                default:
                    return string.Empty;
            }
        }

        private void AddFilterRow()
        {
            var newFilter = new TableFilterRow
            {
                IsLogicOperatorVisible = Filters.Count > 0,
                ColumnName = AvailableColumns.Count > 0 ? AvailableColumns[0] : string.Empty
            };
            Filters.Add(newFilter);
        }

        private void RemoveFilterRow(TableFilterRow filter)
        {
            if (filter == null) return;
            Filters.Remove(filter);

            if (Filters.Count > 0)
            {
                Filters[0].IsLogicOperatorVisible = false;
            }
        }

        private void InsertRow()
        {
            if (QueryResults == null) return;
            var newRow = QueryResults.NewRow();
            QueryResults.Rows.Add(newRow);
            StatusMessage = "Fila insertada localmente. Complete los datos y haga clic en Guardar.";
        }

        private void DeleteRow()
        {
            if (SelectedRow == null) return;

            var confirm = System.Windows.MessageBox.Show(
                "¿Está seguro de que desea eliminar la fila seleccionada? El cambio se aplicará a la base de datos al guardar.",
                "Confirmar Eliminación",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );

            if (confirm == System.Windows.MessageBoxResult.Yes)
            {
                SelectedRow.Row.Delete();
                StatusMessage = "Fila marcada para eliminar. Haga clic en Guardar.";
            }
        }

        private async Task SaveChangesAsync()
        {
            if (QueryResults == null) return;
            IsBusy = true;
            StatusMessage = "Guardando cambios en la base de datos...";

            var ddlStatements = new List<string>();
            var dt = QueryResults;

            try
            {
                // Scanner de DataTable de cambios
                var changes = dt.GetChanges();
                if (changes != null)
                {
                    foreach (DataRow row in changes.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted)
                        {
                            var whereParts = new List<string>();
                            if (_primaryKeys.Count > 0)
                            {
                                foreach (var pk in _primaryKeys)
                                {
                                    var origPkVal = row[pk, DataRowVersion.Original];
                                    whereParts.Add($"`{pk}` = {EscapeSqlValue(origPkVal)}");
                                }
                            }
                            else
                            {
                                foreach (DataColumn col in changes.Columns)
                                {
                                    var origVal = row[col, DataRowVersion.Original];
                                    whereParts.Add($"`{col.ColumnName}` = {EscapeSqlValue(origVal)}");
                                }
                            }
                            var sql = $"DELETE FROM `{SelectedDatabase}`.`{TableName}` WHERE {string.Join(" AND ", whereParts)} LIMIT 1;";
                            ddlStatements.Add(sql);
                        }
                        else if (row.RowState == DataRowState.Added)
                        {
                            var insertCols = new List<string>();
                            var insertVals = new List<string>();
                            foreach (DataColumn col in changes.Columns)
                            {
                                var val = row[col, DataRowVersion.Current];
                                if (val != DBNull.Value)
                                {
                                    insertCols.Add($"`{col.ColumnName}`");
                                    insertVals.Add(EscapeSqlValue(val));
                                }
                            }
                            if (insertCols.Count > 0)
                            {
                                var sql = $"INSERT INTO `{SelectedDatabase}`.`{TableName}` ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});";
                                ddlStatements.Add(sql);
                            }
                        }
                        else if (row.RowState == DataRowState.Modified)
                        {
                            var sets = new List<string>();
                            foreach (DataColumn col in changes.Columns)
                            {
                                var origVal = row[col, DataRowVersion.Original];
                                var newVal = row[col, DataRowVersion.Current];
                                if (!Equals(origVal, newVal))
                                {
                                    sets.Add($"`{col.ColumnName}` = {EscapeSqlValue(newVal)}");
                                }
                            }
                            if (sets.Count > 0)
                            {
                                var whereParts = new List<string>();
                                if (_primaryKeys.Count > 0)
                                {
                                    foreach (var pk in _primaryKeys)
                                    {
                                        var origPkVal = row[pk, DataRowVersion.Original];
                                        whereParts.Add($"`{pk}` = {EscapeSqlValue(origPkVal)}");
                                    }
                                }
                                else
                                {
                                    foreach (DataColumn col in changes.Columns)
                                    {
                                        var origVal = row[col, DataRowVersion.Original];
                                        whereParts.Add($"`{col.ColumnName}` = {EscapeSqlValue(origVal)}");
                                    }
                                }
                                var sql = $"UPDATE `{SelectedDatabase}`.`{TableName}` SET {string.Join(", ", sets)} WHERE {string.Join(" AND ", whereParts)} LIMIT 1;";
                                ddlStatements.Add(sql);
                            }
                        }
                    }
                }

                if (ddlStatements.Count == 0)
                {
                    System.Windows.MessageBox.Show("No se detectaron cambios para guardar.", "Sin Cambios", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    StatusMessage = "Listo.";
                    return;
                }

                // Ejecutar sentencias secuenciales sobre el plugin
                foreach (var sql in ddlStatements)
                {
                    await _databasePlugin.ExecuteQueryAsync(sql);
                }

                StatusMessage = "Cambios aplicados correctamente.";
                System.Windows.MessageBox.Show("Los cambios se han aplicado correctamente en la base de datos.", "Guardado Completado", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Recargar desde la base de datos
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al guardar cambios: {ex.Message}";
                System.Windows.MessageBox.Show($"Error al guardar cambios: {ex.Message}", "Error al Guardar", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string EscapeSqlValue(object val)
        {
            if (val == null || val == DBNull.Value) return "NULL";
            if (val is bool b) return b ? "1" : "0";
            return $"'{val.ToString().Replace("'", "''")}'";
        }

        private string EscapeSqlString(string val)
        {
            return val?.Replace("'", "''") ?? string.Empty;
        }

        // Operaciones para el Visor de JSON y Claves Foráneas
        private async Task LoadForeignKeysAsync()
        {
            _foreignKeys.Clear();
            try
            {
                var sql = $@"
                    SELECT 
                        COLUMN_NAME, 
                        REFERENCED_TABLE_NAME, 
                        REFERENCED_COLUMN_NAME 
                    FROM 
                        information_schema.KEY_COLUMN_USAGE 
                    WHERE 
                        TABLE_SCHEMA = '{SelectedDatabase}' 
                        AND TABLE_NAME = '{TableName}' 
                        AND REFERENCED_TABLE_NAME IS NOT NULL;";
                var dt = await _databasePlugin.ExecuteQueryAsync(sql);
                foreach (DataRow row in dt.Rows)
                {
                    string colName = row["COLUMN_NAME"].ToString();
                    string refTable = row["REFERENCED_TABLE_NAME"].ToString();
                    string refCol = row["REFERENCED_COLUMN_NAME"].ToString();
                    _foreignKeys[colName] = (refTable, refCol);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar claves foráneas: {ex.Message}");
            }
        }

        private async Task UpdateJsonDetailsAsync(DataRowView selectedRowView)
        {
            JsonNodes.Clear();
            if (selectedRowView == null) return;

            var row = selectedRowView.Row;
            var dt = row.Table;

            // Nodo raíz del registro actual
            var rootNode = new JsonNodeViewModel { Key = TableName, IsExpanded = true };

            foreach (DataColumn col in dt.Columns)
            {
                var colName = col.ColumnName;
                var val = row[colName];

                var fieldNode = new JsonNodeViewModel
                {
                    Key = colName,
                    Value = val == DBNull.Value ? null : val,
                    IsEditable = true
                };

                fieldNode.ValueChangedAction = (node) =>
                {
                    if (_isUpdatingJsonFromRow) return;
                    if (SelectedRow == null) return;

                    try
                    {
                        _isUpdatingRowFromJson = true;
                        var r = SelectedRow.Row;
                        var column = r.Table.Columns[node.Key];
                        if (column != null)
                        {
                            object targetValue;
                            if (node.Value == null || string.IsNullOrWhiteSpace(node.Value.ToString()))
                            {
                                targetValue = DBNull.Value;
                            }
                            else
                            {
                                try
                                {
                                    targetValue = Convert.ChangeType(node.Value, column.DataType);
                                }
                                catch
                                {
                                    targetValue = node.Value;
                                }
                            }
                            r[node.Key] = targetValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al actualizar fila desde JSON: {ex.Message}");
                    }
                    finally
                    {
                        _isUpdatingRowFromJson = false;
                    }
                };

                rootNode.Children.Add(fieldNode);

                // Si la columna tiene una relación de clave foránea activa y tiene valor
                if (val != DBNull.Value && _foreignKeys.TryGetValue(colName, out var relation))
                {
                    var subRow = await LoadReferencedRecordAsync(relation.ReferencedTable, relation.ReferencedColumn, val);
                    if (subRow != null)
                    {
                        var detailsNode = new JsonNodeViewModel { Key = $"{colName}_datos" };
                        foreach (DataColumn subCol in subRow.Table.Columns)
                        {
                            var subVal = subRow[subCol.ColumnName];
                            detailsNode.Children.Add(new JsonNodeViewModel
                            {
                                Key = subCol.ColumnName,
                                Value = subVal == DBNull.Value ? null : subVal,
                                IsEditable = false
                            });
                        }
                        rootNode.Children.Add(detailsNode);
                    }
                }
            }

            JsonNodes.Add(rootNode);

            // Re-aplicar el filtro de texto si estuviese activo
            if (!string.IsNullOrEmpty(JsonFilterText))
            {
                ApplyJsonFilter(JsonFilterText);
            }
        }

        private void OnTableColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (_isUpdatingRowFromJson) return;

            if (SelectedRow != null && e.Row == SelectedRow.Row)
            {
                _ = UpdateJsonDetailsFromRowChangeAsync(e.Column.ColumnName, e.ProposedValue);
            }
        }

        private async Task UpdateJsonDetailsFromRowChangeAsync(string columnName, object proposedValue)
        {
            _isUpdatingJsonFromRow = true;
            try
            {
                if (JsonNodes.Count > 0)
                {
                    var root = JsonNodes[0];
                    foreach (var child in root.Children)
                    {
                        if (child.Key == columnName)
                        {
                            child.Value = proposedValue == DBNull.Value ? null : proposedValue;
                            break;
                        }
                    }
                }

                // Si la columna modificada es clave foránea, recargamos los datos relacionados completos
                if (_foreignKeys.ContainsKey(columnName) && SelectedRow != null)
                {
                    await UpdateJsonDetailsAsync(SelectedRow);
                }
            }
            finally
            {
                _isUpdatingJsonFromRow = false;
            }
        }

        private async Task<DataRow> LoadReferencedRecordAsync(string refTable, string refCol, object val)
        {
            try
            {
                var sql = $"SELECT * FROM `{SelectedDatabase}`.`{refTable}` WHERE `{refCol}` = {EscapeSqlValue(val)} LIMIT 1;";
                var dt = await _databasePlugin.ExecuteQueryAsync(sql);
                if (dt.Rows.Count > 0)
                {
                    return dt.Rows[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar registro referenciado en {refTable}: {ex.Message}");
            }
            return null;
        }

        private void ApplyJsonFilter(string text)
        {
            foreach (var node in JsonNodes)
            {
                FilterNode(node, text, 0);
            }
        }

        private bool FilterNode(JsonNodeViewModel node, string text, int level)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                node.IsVisible = true;
                foreach (var child in node.Children)
                {
                    FilterNode(child, text, level + 1);
                }
                return true;
            }

            if (level == 0)
            {
                // El nodo raíz (nombre de la tabla) no se filtra por sí mismo,
                // sino que su visibilidad depende de si algún hijo coincide.
                bool anyChildMatches = false;
                foreach (var child in node.Children)
                {
                    if (FilterNode(child, text, level + 1))
                    {
                        anyChildMatches = true;
                    }
                }

                node.IsVisible = anyChildMatches;
                if (anyChildMatches)
                {
                    node.IsExpanded = true;
                }
                return node.IsVisible;
            }
            else if (level == 1)
            {
                // Primer nivel de propiedades: aquí se aplica el filtro de búsqueda.
                bool selfMatches = node.Key.Contains(text, StringComparison.OrdinalIgnoreCase);
                node.IsVisible = selfMatches;

                // Si coincide el primer nivel, mostramos todo su subárbol completo.
                // Si no coincide, ocultamos todo su subárbol.
                SetSubtreeVisible(node, selfMatches);

                return selfMatches;
            }
            else
            {
                // Niveles inferiores (segundo nivel en adelante).
                // Su visibilidad ya está determinada por el primer nivel.
                return node.IsVisible;
            }
        }

        private void SetSubtreeVisible(JsonNodeViewModel node, bool visible)
        {
            foreach (var child in node.Children)
            {
                child.IsVisible = visible;
                SetSubtreeVisible(child, visible);
            }
        }

        public void CopyJsonNodeToClipboard(JsonNodeViewModel node)
        {
            CopyJsonNodeWithMode(node, SelectedJsonCopyMode);
        }

        public void CopyJsonNodeWithMode(JsonNodeViewModel node, JsonCopyMode mode)
        {
            if (node == null) return;
            
            try
            {
                string textToCopy = "";
                switch (mode)
                {
                    case JsonCopyMode.KeyOnly:
                        textToCopy = node.Key;
                        break;
                    case JsonCopyMode.KeyAndValueJson:
                        textToCopy = SerializeNode(node, includeSubtree: false);
                        break;
                    case JsonCopyMode.KeyAndSubtreeJson:
                        textToCopy = SerializeNode(node, includeSubtree: true);
                        break;
                }
                
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    System.Windows.Clipboard.SetText(textToCopy);
                    StatusMessage = $"Copiado al portapapeles: {node.Key}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al copiar: {ex.Message}";
            }
        }

        private string SerializeNode(JsonNodeViewModel node, bool includeSubtree)
        {
            var serializedValue = ToJsonSerializable(node, includeSubtree);
            var wrapper = new Dictionary<string, object>
            {
                { node.Key, serializedValue }
            };
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return System.Text.Json.JsonSerializer.Serialize(wrapper, options);
        }

        private object ToJsonSerializable(JsonNodeViewModel node, bool includeSubtree)
        {
            if (!node.IsObject)
            {
                return node.Value;
            }
            
            var dict = new Dictionary<string, object>();
            foreach (var child in node.Children)
            {
                if (child.IsObject)
                {
                    if (includeSubtree)
                    {
                        dict[child.Key] = ToJsonSerializable(child, true);
                    }
                }
                else
                {
                    dict[child.Key] = child.Value;
                }
            }
            return dict;
        }
    }

    public enum JsonCopyMode
    {
        KeyOnly,
        KeyAndValueJson,
        KeyAndSubtreeJson
    }

    public class JsonCopyModeOption
    {
        public JsonCopyMode Mode { get; }
        public string DisplayName { get; }

        public JsonCopyModeOption(JsonCopyMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }
    }
}
