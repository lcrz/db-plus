using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using DbClient.Core;
using DbClient.Wpf.Models;
using DbClient.Wpf.Services;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel principal del editor SQL que gestiona múltiples pestañas de consulta
    /// y delega la ejecución de consultas a la pestaña activa seleccionada.
    /// </summary>
    public class SqlEditorViewModel : BaseViewModel
    {
        private readonly IDatabasePlugin _databasePlugin;
        private readonly AppDataStorageService _appDataStorageService;
        private readonly IOllamaService _ollamaService;
        private readonly ConnectionStorageService _connectionStorageService;
        private readonly Dictionary<string, List<string>> _columnsCache = new(StringComparer.OrdinalIgnoreCase);

        private string _activeConnectionId;
        private string _activeDatabaseName;
        private bool _isLoadingTabs;

        public ObservableCollection<SqlQueryTabViewModel> Tabs { get; } = new();

        private SqlQueryTabViewModel _selectedTab;
        public SqlQueryTabViewModel SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    SaveTabsState();
                }
            }
        }

        private List<string> _tables = new();
        public List<string> Tables
        {
            get => _tables;
            set
            {
                if (SetProperty(ref _tables, value))
                {
                    _columnsCache.Clear();
                }
            }
        }

        public ICommand NewTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand CloseAllTabsCommand { get; }
        public ICommand CloseOtherTabsCommand { get; }
        public ICommand CloseTabsToTheRightCommand { get; }

        private string _aiPrompt;
        public string AiPrompt
        {
            get => _aiPrompt;
            set => SetProperty(ref _aiPrompt, value);
        }

        private string _aiGeneratedSql;
        public string AiGeneratedSql
        {
            get => _aiGeneratedSql;
            set => SetProperty(ref _aiGeneratedSql, value);
        }

        private bool _isGeneratingSql;
        public bool IsGeneratingSql
        {
            get => _isGeneratingSql;
            set => SetProperty(ref _isGeneratingSql, value);
        }

        private bool _isSaveQueryDialogOpen;
        public bool IsSaveQueryDialogOpen
        {
            get => _isSaveQueryDialogOpen;
            set => SetProperty(ref _isSaveQueryDialogOpen, value);
        }

        private string _newQueryName;
        public string NewQueryName
        {
            get => _newQueryName;
            set => SetProperty(ref _newQueryName, value);
        }

        public ICommand GenerateSqlCommand { get; }
        public ICommand InsertSqlToEditorCommand { get; }
        public ICommand ShowSaveQueryDialogCommand { get; }
        public ICommand CloseSaveQueryDialogCommand { get; }
        public ICommand SaveActiveQueryCommand { get; }

        public event Action QuerySaved;

        public SqlEditorViewModel(
            IDatabasePlugin databasePlugin, 
            AppDataStorageService appDataStorageService, 
            IOllamaService ollamaService,
            ConnectionStorageService connectionStorageService)
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            _appDataStorageService = appDataStorageService ?? throw new ArgumentNullException(nameof(appDataStorageService));
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _connectionStorageService = connectionStorageService ?? throw new ArgumentNullException(nameof(connectionStorageService));

            NewTabCommand = new RelayCommand(NewTab);
            CloseTabCommand = new RelayCommand<SqlQueryTabViewModel>(CloseTab);
            CloseAllTabsCommand = new RelayCommand(CloseAllTabs);
            CloseOtherTabsCommand = new RelayCommand<SqlQueryTabViewModel>(CloseOtherTabs);
            CloseTabsToTheRightCommand = new RelayCommand<SqlQueryTabViewModel>(CloseTabsToTheRight);

            GenerateSqlCommand = new RelayCommand(async () => await GenerateSqlAsync(), () => !IsGeneratingSql && !string.IsNullOrWhiteSpace(AiPrompt));
            InsertSqlToEditorCommand = new RelayCommand(InsertSqlToEditor, () => !string.IsNullOrWhiteSpace(AiGeneratedSql) && SelectedTab != null);

            ShowSaveQueryDialogCommand = new RelayCommand(ShowSaveQueryDialog, () => SelectedTab != null);
            CloseSaveQueryDialogCommand = new RelayCommand(CloseSaveQueryDialog);
            SaveActiveQueryCommand = new RelayCommand(SaveActiveQuery, () => SelectedTab != null && !string.IsNullOrWhiteSpace(NewQueryName));
        }

        private void ShowSaveQueryDialog()
        {
            if (SelectedTab == null) return;
            
            // Si el título empieza por "Consulta " (que es el nombre genérico predeterminado),
            // sugerimos un campo vacío para obligar al usuario a elegir un nombre.
            // De lo contrario, sugerimos el título actual de la pestaña.
            if (SelectedTab.Title != null && SelectedTab.Title.StartsWith("Consulta "))
            {
                NewQueryName = string.Empty;
            }
            else
            {
                NewQueryName = SelectedTab.Title;
            }
            
            IsSaveQueryDialogOpen = true;
        }

        private void CloseSaveQueryDialog()
        {
            IsSaveQueryDialogOpen = false;
        }

        private void SaveActiveQuery()
        {
            if (SelectedTab == null || string.IsNullOrWhiteSpace(NewQueryName)) return;
            try
            {
                var cleanName = NewQueryName.Trim();
                _connectionStorageService.SaveQuery(cleanName, SelectedTab.QueryText);
                
                // Actualizar el título de la pestaña actual al nombre bajo el cual se guardó la consulta
                SelectedTab.Title = cleanName;
                
                IsSaveQueryDialogOpen = false;
                QuerySaved?.Invoke();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar la consulta: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void AddNewTab(string title, string sqlText)
        {
            var newTab = new SqlQueryTabViewModel(_databasePlugin, title, sqlText);
            Tabs.Add(newTab);
            SelectedTab = newTab;
            SaveTabsState();
        }

        public void LoadTabs(string connectionId, string databaseName)
        {
            _isLoadingTabs = true;
            try
            {
                _activeConnectionId = connectionId;
                _activeDatabaseName = databaseName;

                Tabs.Clear();
                var saved = _appDataStorageService.LoadTabsForConnection(connectionId, databaseName);
                if (saved != null && saved.Count > 0)
                {
                    SqlQueryTabViewModel activeTab = null;
                    foreach (var s in saved)
                    {
                        var tab = new SqlQueryTabViewModel(_databasePlugin, s.Title, s.QueryText)
                        {
                            Id = s.Id
                        };
                        Tabs.Add(tab);
                        if (s.IsActive)
                        {
                            activeTab = tab;
                        }
                    }
                    SelectedTab = activeTab ?? Tabs.FirstOrDefault();
                }
                else
                {
                    NewTab();
                }
            }
            finally
            {
                _isLoadingTabs = false;
            }
        }

        public void SaveTabsState()
        {
            if (_isLoadingTabs)
                return;

            if (string.IsNullOrEmpty(_activeConnectionId) || string.IsNullOrEmpty(_activeDatabaseName))
                return;

            var list = Tabs.Select(t => new SavedTabState
            {
                Id = t.Id,
                Title = t.Title,
                QueryText = t.QueryText,
                IsActive = t == SelectedTab
            }).ToList();

            _appDataStorageService.SaveTabsForConnection(_activeConnectionId, _activeDatabaseName, list);
        }

        private void NewTab()
        {
            int count = Tabs.Count + 1;
            while (Tabs.Any(t => t.Title == $"Consulta {count}"))
            {
                count++;
            }

            var newTab = new SqlQueryTabViewModel(_databasePlugin, $"Consulta {count}", "");
            Tabs.Add(newTab);
            SelectedTab = newTab;
            SaveTabsState();
        }

        private void CloseTab(SqlQueryTabViewModel tab)
        {
            if (tab == null) return;
            int index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            if (SelectedTab == tab || SelectedTab == null)
            {
                if (Tabs.Count > 0)
                {
                    SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
                }
                else
                {
                    NewTab();
                }
            }
            else
            {
                SaveTabsState();
            }
        }

        private void CloseAllTabs()
        {
            Tabs.Clear();
            NewTab();
        }

        private void CloseOtherTabs(SqlQueryTabViewModel tab)
        {
            if (tab == null) return;
            var toRemove = Tabs.Where(t => t != tab).ToList();
            foreach (var t in toRemove)
            {
                Tabs.Remove(t);
            }
            SelectedTab = tab;
            SaveTabsState();
        }

        private void CloseTabsToTheRight(SqlQueryTabViewModel tab)
        {
            if (tab == null) return;
            int idx = Tabs.IndexOf(tab);
            if (idx < 0) return;

            var toRemove = Tabs.Skip(idx + 1).ToList();
            foreach (var t in toRemove)
            {
                Tabs.Remove(t);
            }
            SelectedTab = tab;
            SaveTabsState();
        }

        public async Task<List<string>> GetColumnsForTableAsync(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return new List<string>();

            if (_columnsCache.TryGetValue(tableName, out var cached))
            {
                return cached;
            }

            try
            {
                var cols = await _databasePlugin.GetColumnsAsync(tableName);
                if (cols != null)
                {
                    _columnsCache[tableName] = cols;
                    return cols;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener columnas para {tableName}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error al obtener columnas para {tableName}: {ex.Message}");
            }

            return new List<string>();
        }

        private async Task<Dictionary<string, string>> GetTableDescriptionsAsync()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Inicializar con descripciones vacías para todas las tablas conocidas
            if (Tables != null)
            {
                foreach (var table in Tables)
                {
                    dict[table] = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(_activeDatabaseName)) return dict;

            try
            {
                var safeDb = _activeDatabaseName.Replace("'", "''");
                var query = $"SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{safeDb}';";
                var dt = await _databasePlugin.ExecuteQueryAsync(query);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        var tableName = row["TABLE_NAME"]?.ToString();
                        var comment = row["TABLE_COMMENT"]?.ToString();
                        if (!string.IsNullOrEmpty(tableName))
                        {
                            dict[tableName] = comment ?? string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener descripciones de tablas: {ex.Message}");
            }

            return dict;
        }

        private async Task<List<ForeignKeyInfo>> GetForeignKeysAsync()
        {
            var list = new List<ForeignKeyInfo>();
            if (string.IsNullOrEmpty(_activeDatabaseName)) return list;

            try
            {
                var safeDb = _activeDatabaseName.Replace("'", "''");
                var sql = $@"
                    SELECT 
                        TABLE_NAME,
                        COLUMN_NAME, 
                        REFERENCED_TABLE_NAME, 
                        REFERENCED_COLUMN_NAME 
                    FROM 
                        information_schema.KEY_COLUMN_USAGE 
                    WHERE 
                        TABLE_SCHEMA = '{safeDb}' 
                        AND REFERENCED_TABLE_NAME IS NOT NULL;";
                var dt = await _databasePlugin.ExecuteQueryAsync(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new ForeignKeyInfo
                        {
                            TableName = row["TABLE_NAME"]?.ToString() ?? string.Empty,
                            ColumnName = row["COLUMN_NAME"]?.ToString() ?? string.Empty,
                            ReferencedTable = row["REFERENCED_TABLE_NAME"]?.ToString() ?? string.Empty,
                            ReferencedColumn = row["REFERENCED_COLUMN_NAME"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener claves foráneas de la base de datos: {ex.Message}");
            }
            return list;
        }

        private async Task GenerateSqlAsync()
        {
            if (string.IsNullOrWhiteSpace(AiPrompt)) return;

            IsGeneratingSql = true;
            AiGeneratedSql = "Generando consulta...";

            try
            {
                // Paso 1: Obtener tablas y sus descripciones
                var tableDescriptions = await GetTableDescriptionsAsync();

                var tablesWithDesc = new List<string>();
                var tablesWithoutDesc = new List<string>();

                foreach (var table in Tables)
                {
                    var desc = tableDescriptions.TryGetValue(table, out var d) ? d : string.Empty;
                    if (string.IsNullOrWhiteSpace(desc))
                    {
                        tablesWithoutDesc.Add(table);
                    }
                    else
                    {
                        tablesWithDesc.Add($"- Tabla: '{table}' - Descripción: {desc}");
                    }
                }

                var sbPrompt1 = new StringBuilder();
                sbPrompt1.AppendLine("Lista de tablas disponibles en la base de datos:");
                if (tablesWithDesc.Count > 0)
                {
                    sbPrompt1.AppendLine("Tablas con descripción:");
                    foreach (var twd in tablesWithDesc)
                    {
                        sbPrompt1.AppendLine(twd);
                    }
                    sbPrompt1.AppendLine();
                }
                if (tablesWithoutDesc.Count > 0)
                {
                    sbPrompt1.AppendLine("Otras tablas disponibles (sin descripción):");
                    sbPrompt1.AppendLine(string.Join(", ", tablesWithoutDesc));
                    sbPrompt1.AppendLine();
                }

                var systemPrompt1 = "Eres un asistente experto en bases de datos. Tu tarea es analizar la solicitud del usuario y determinar cuáles de las tablas proporcionadas son relevantes para construir la consulta SQL necesaria. Debes responder únicamente con una lista de los nombres exactos de las tablas relevantes, separados por comas. No agregues explicaciones, no uses markdown, no saludes, ni agregues ningún otro texto.";
                var userPrompt1 = $"{sbPrompt1.ToString()}\n\nSolicitud del usuario: \"{AiPrompt}\"\n\nTablas relevantes:";

                Console.WriteLine("=== PASO 1: ENVIANDO SOLICITUD DE TABLAS RELEVANTES ===");
                Console.WriteLine(userPrompt1);
                Console.WriteLine("=====================================================");

                var response1 = await _ollamaService.GenerateResponseAsync(systemPrompt1, userPrompt1);

                Console.WriteLine("=== PASO 1: RESPUESTA DE TABLAS RELEVANTES ===");
                Console.WriteLine(response1 != null ? response1.Text : string.Empty);
                Console.WriteLine("=============================================");

                var relevantTables = new List<string>();
                if (response1 != null && !string.IsNullOrWhiteSpace(response1.Text))
                {
                    var rawNames = response1.Text.Split(new[] { ',', '\n', ';', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var rawName in rawNames)
                    {
                        var cleaned = rawName.Trim().Trim('`', '\'', '"', ' ', '[', ']');
                        if (string.IsNullOrEmpty(cleaned)) continue;

                        var match = Tables.FirstOrDefault(t => string.Equals(t, cleaned, StringComparison.OrdinalIgnoreCase));
                        if (match != null && !relevantTables.Contains(match))
                        {
                            relevantTables.Add(match);
                        }
                    }
                }

                // Fallback heurístico si no se detectaron tablas
                if (relevantTables.Count == 0)
                {
                    Console.WriteLine("=== AVISO: El modelo no retornó tablas válidas. Aplicando fallback heurístico ===");
                    var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "el", "la", "los", "las", "un", "una", "unos", "unas",
                        "y", "o", "e", "u", "en", "de", "del", "al", "con", "para", "por", "sin",
                        "su", "sus", "es", "son", "fue", "eran", "ser", "estar",
                        "cual", "cuales", "este", "esta", "estos", "estas", "aquel", "aquella",
                        "mas", "menos", "como", "que", "quien", "quienes", "donde", "cuando",
                        "the", "a", "an", "of", "in", "on", "at", "for", "with", "by", "to", "from",
                        "is", "are", "was", "were", "be", "been", "which", "who", "whom", "this", "that", "these", "those",
                        "most", "more", "less", "how", "what", "where", "when", "why"
                    };

                    var promptWords = (AiPrompt ?? string.Empty).ToLowerInvariant()
                        .Split(new[] { ' ', ',', '.', ';', '(', ')', '[', ']', '_', '-', '\'', '"', '?', '¿', '!', '¡' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => !stopWords.Contains(w))
                        .ToArray();

                    foreach (var table in Tables)
                    {
                        if (IsTableRelevant(table, promptWords))
                        {
                            relevantTables.Add(table);
                        }
                    }

                    // Fallback extremo si sigue vacío: incluir todas las tablas
                    if (relevantTables.Count == 0)
                    {
                        relevantTables.AddRange(Tables);
                    }
                }

                // Paso 2: Obtener campos y llaves foráneas para las tablas relevantes
                var fks = await GetForeignKeysAsync();
                var sbSchema = new StringBuilder();
                sbSchema.AppendLine($"Base de datos activa: {_activeDatabaseName}\n");
                sbSchema.AppendLine("Estructura de tablas relevantes para la consulta:");

                foreach (var table in relevantTables)
                {
                    var schema = await _databasePlugin.GetTableSchemaAsync(_activeDatabaseName, table);
                    sbSchema.AppendLine($"\n- Tabla '{table}':");
                    if (!string.IsNullOrWhiteSpace(schema.Description))
                    {
                        sbSchema.AppendLine($"  Descripción: {schema.Description}");
                    }

                    sbSchema.AppendLine("  Columnas / Campos:");
                    foreach (var col in schema.Columns)
                    {
                        var pkStr = col.IsPrimaryKey ? " (Llave Primaria)" : "";
                        var nullStr = col.IsNullable ? " NULL" : " NOT NULL";
                        var defaultStr = col.DefaultValue != null ? $" DEFAULT '{col.DefaultValue}'" : "";
                        sbSchema.AppendLine($"    - {col.Name} ({col.Type}){pkStr}{nullStr}{defaultStr}");
                    }

                    var tableFks = fks.Where(fk => string.Equals(fk.TableName, table, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (tableFks.Count > 0)
                    {
                        sbSchema.AppendLine("  Llaves Foráneas (Relaciones):");
                        foreach (var fk in tableFks)
                        {
                            sbSchema.AppendLine($"    - {fk.ColumnName} relaciona con la tabla '{fk.ReferencedTable}' campo '{fk.ReferencedColumn}'");
                        }
                    }
                }

                Console.WriteLine("=== PASO 2: CONTEXTO ENVIADO A OLLAMA ===");
                Console.WriteLine(sbSchema.ToString());
                Console.WriteLine("=========================================");

                var sqlResponse = await _ollamaService.GenerateSqlAsync(AiPrompt, sbSchema.ToString());

                if (sqlResponse == null || string.IsNullOrWhiteSpace(sqlResponse.Text))
                {
                    AiGeneratedSql = "-- No se pudo generar una consulta.";
                    return;
                }

                var sql = sqlResponse.Text;

                // Validación de seguridad (C#)
                var upperSql = sql.Trim().ToUpperInvariant();
                if (!upperSql.StartsWith("SELECT"))
                {
                    AiGeneratedSql = "-- ALERTA DE SEGURIDAD: La consulta generada no es un SELECT válido.\n-- Consulta original devuelta por la IA:\n-- " + sql.Replace("\n", "\n-- ");
                    return;
                }

                var destructiveWords = new[] { "UPDATE", "DELETE", "DROP", "INSERT", "ALTER", "TRUNCATE", "REPLACE" };
                var hasDestructive = destructiveWords.Any(w => Regex.IsMatch(upperSql, $@"\b{w}\b"));
                if (hasDestructive)
                {
                    AiGeneratedSql = "-- ALERTA DE SEGURIDAD: La consulta generada contiene palabras clave destructivas prohibidas.\n-- Consulta original devuelta por la IA:\n-- " + sql.Replace("\n", "\n-- ");
                    return;
                }

                // Generar estadísticas discretas como comentario SQL
                var stats = new StringBuilder();
                stats.Append("-- [IA: ");
                
                // Paso 1
                if (response1 != null)
                {
                    stats.Append($"Paso 1: {response1.DurationSeconds:F1}s");
                    if (response1.InputTokens.HasValue || response1.OutputTokens.HasValue)
                    {
                        stats.Append(" (");
                        if (response1.InputTokens.HasValue) stats.Append($"In: {response1.InputTokens}t");
                        if (response1.OutputTokens.HasValue) stats.Append($", Out: {response1.OutputTokens}t");
                        stats.Append(")");
                    }
                }

                stats.Append(" | ");

                // Paso 2
                stats.Append($"Paso 2: {sqlResponse.DurationSeconds:F1}s");
                if (sqlResponse.InputTokens.HasValue || sqlResponse.OutputTokens.HasValue)
                {
                    stats.Append(" (");
                    if (sqlResponse.InputTokens.HasValue) stats.Append($"In: {sqlResponse.InputTokens}t");
                    if (sqlResponse.OutputTokens.HasValue) stats.Append($", Out: {sqlResponse.OutputTokens}t");
                    stats.Append(")");
                }

                // Total
                double totalDuration = (response1?.DurationSeconds ?? 0) + sqlResponse.DurationSeconds;
                stats.Append($" | Total: {totalDuration:F1}s]");

                AiGeneratedSql = $"{sql}\n{stats.ToString()}";
            }
            catch (Exception ex)
            {
                AiGeneratedSql = $"-- Error al contactar con la IA: {ex.Message}";
            }
            finally
            {
                IsGeneratingSql = false;
            }
        }

        private bool IsTableRelevant(string tableName, string[] promptWords)
        {
            var tableLower = tableName.ToLowerInvariant();
            
            // 1. Coincidencia directa
            if (promptWords.Any(word => {
                // Evitar coincidencia accidental de "venta" con "inventario" (in-venta-rio)
                if ((word == "venta" || word == "ventas" || word == "vendido") && tableLower.Contains("inventario")) return false;
                return tableLower.Contains(word) || word.Contains(tableLower);
            }))
            {
                return true;
            }

            // 2. Coincidencia singular/plural
            foreach (var word in promptWords)
            {
                var w = word;
                if (w.EndsWith("s") && w.Length > 2) w = w.Substring(0, w.Length - 1);
                if (w.EndsWith("es") && w.Length > 3) w = w.Substring(0, w.Length - 2);

                var t = tableLower;
                if (t.EndsWith("s") && t.Length > 2) t = t.Substring(0, t.Length - 1);
                if (t.EndsWith("es") && t.Length > 3) t = t.Substring(0, t.Length - 2);

                // Evitar coincidencia accidental de "venta" con "inventario" (in-venta-rio)
                if ((w == "venta" || w == "ventas" || w == "vendido") && t.Contains("inventario")) continue;

                if (t.Contains(w) || w.Contains(t))
                {
                    return true;
                }
            }

            // 3. Mapeo semántico de sinónimos comunes en bases de datos
            var synonymGroups = new Dictionary<string[], string[]>
            {
                // Ventas / Facturas
                { 
                    new[] { "venta", "ventas", "vendido", "vendidos", "factura", "facturas", "boleta", "boletas", "invoice", "sale", "sales" },
                    new[] { "venta", "factura", "boleta", "sale", "invoice", "det_fac", "fac_", "orden_venta" } 
                },
                // Compras / Proveedores
                { 
                    new[] { "compra", "compras", "comprado", "comprados", "adquisicion", "proveedor", "proveedores", "purchase", "provider" }, 
                    new[] { "compra", "proveedor", "purchase", "provider", "orden_compra" } 
                },
                // Productos / Artículos
                { 
                    new[] { "producto", "productos", "articulo", "articulos", "item", "items", "product" }, 
                    new[] { "producto", "articulo", "item", "product", "prod" } 
                },
                // Clientes
                { 
                    new[] { "cliente", "clientes", "client", "customer", "customers" }, 
                    new[] { "cliente", "client", "customer", "cli" } 
                },
                // Empleados / Usuarios
                { 
                    new[] { "empleado", "empleados", "usuario", "usuarios", "vendedor", "vendedores", "user", "employee" }, 
                    new[] { "empleado", "usuario", "vendedor", "user", "employee", "member" } 
                }
            };

            foreach (var group in synonymGroups)
            {
                if (group.Key.Any(keyword => promptWords.Contains(keyword)))
                {
                    if (group.Value.Any(root => {
                        // Evitar coincidencia accidental de "venta" con "inventario" (in-venta-rio)
                        if (root == "venta" && tableLower.Contains("inventario")) return false;
                        return tableLower.Contains(root);
                    }))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void InsertSqlToEditor()
        {
            if (SelectedTab != null && !string.IsNullOrWhiteSpace(AiGeneratedSql))
            {
                // Si la consulta no es un mensaje de error o advertencia
                if (AiGeneratedSql.StartsWith("-- ALERTA") || AiGeneratedSql.StartsWith("-- Error"))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(SelectedTab.QueryText))
                {
                    SelectedTab.QueryText += "\n\n";
                }
                SelectedTab.QueryText += AiGeneratedSql;
            }
        }
    }

    public class ForeignKeyInfo
    {
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }
}
