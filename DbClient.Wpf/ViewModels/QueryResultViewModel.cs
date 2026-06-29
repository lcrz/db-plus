using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que representa un único tab de resultado para una consulta SQL ejecutada.
    /// </summary>
    public class QueryResultViewModel : BaseViewModel
    {
        private string _title;
        private DataTable _dataTable;
        private string _message;
        private DataRowView _selectedRow;
        private string _jsonFilterText = string.Empty;
        private bool _isJsonPanelVisible = false;

        /// <summary>
        /// Título de la pestaña de resultado (ej. "Resultado 1", "Mensaje 1").
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Conjunto de datos para consultas SELECT.
        /// </summary>
        public DataTable DataTable
        {
            get => _dataTable;
            set
            {
                if (SetProperty(ref _dataTable, value))
                {
                    OnPropertyChanged(nameof(IsTable));
                }
            }
        }

        /// <summary>
        /// Mensaje informativo para consultas DML o de estado.
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Indica si este resultado contiene un conjunto de datos (tabla) para mostrar en un DataGrid.
        /// </summary>
        public bool IsTable => DataTable != null;

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

        public bool IsJsonPanelVisible
        {
            get => _isJsonPanelVisible;
            set => SetProperty(ref _isJsonPanelVisible, value);
        }

        public ObservableCollection<JsonNodeViewModel> JsonNodes { get; } = new();

        public System.Windows.Input.ICommand DetachCommand { get; }
        public System.Windows.Input.ICommand CopyAsSqlCommand { get; }
        public System.Windows.Input.ICommand CopyAsJsonCommand { get; }
        public System.Windows.Input.ICommand CopyAsCsvCommand { get; }
        public System.Windows.Input.ICommand CopyNodeKeyCommand { get; }
        public System.Windows.Input.ICommand CopyNodeKeyAndValueCommand { get; }
        public System.Windows.Input.ICommand CopyNodeKeyAndSubtreeCommand { get; }

        public QueryResultViewModel()
        {
            DetachCommand = new RelayCommand(DetachResult);
            CopyAsSqlCommand = new RelayCommand<System.Collections.IList>(CopyAsSql);
            CopyAsJsonCommand = new RelayCommand<System.Collections.IList>(CopyAsJson);
            CopyAsCsvCommand = new RelayCommand<System.Collections.IList>(CopyAsCsv);
            CopyNodeKeyCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyOnly));
            CopyNodeKeyAndValueCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyAndValueJson));
            CopyNodeKeyAndSubtreeCommand = new RelayCommand<JsonNodeViewModel>(node => CopyJsonNodeWithMode(node, JsonCopyMode.KeyAndSubtreeJson));
        }

        private void DetachResult()
        {
            var window = new DbClient.Wpf.Views.DetachedResultWindow(this);
            window.Show();
        }

        private void CopyAsSql(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            string tableName = string.IsNullOrWhiteSpace(DataTable?.TableName) || DataTable.TableName == "Table" ? "temp_table" : DataTable.TableName;
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
                    sb.AppendLine($"INSERT INTO `{tableName}` ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});");
                }
            }
            if (sb.Length > 0)
            {
                System.Windows.Clipboard.SetText(sb.ToString());
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
            }
        }

        private async Task UpdateJsonDetailsAsync(DataRowView selectedRowView)
        {
            JsonNodes.Clear();
            if (selectedRowView == null) return;

            var row = selectedRowView.Row;
            var dt = row.Table;

            string tableName = string.IsNullOrWhiteSpace(dt.TableName) || dt.TableName == "Table" ? "Registro" : dt.TableName;
            var rootNode = new JsonNodeViewModel { Key = tableName, IsExpanded = true };

            foreach (DataColumn col in dt.Columns)
            {
                var colName = col.ColumnName;
                var val = row[colName];

                var fieldNode = new JsonNodeViewModel
                {
                    Key = colName,
                    Value = val == DBNull.Value ? null : val,
                    IsEditable = false
                };

                rootNode.Children.Add(fieldNode);
            }

            JsonNodes.Add(rootNode);

            if (!string.IsNullOrEmpty(JsonFilterText))
            {
                ApplyJsonFilter(JsonFilterText);
            }
            
            await Task.CompletedTask;
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
                bool selfMatches = node.Key.Contains(text, StringComparison.OrdinalIgnoreCase);
                node.IsVisible = selfMatches;
                SetSubtreeVisible(node, selfMatches);
                return selfMatches;
            }
            else
            {
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
            CopyJsonNodeWithMode(node, JsonCopyMode.KeyOnly);
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al copiar nodo JSON: {ex.Message}");
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
}
