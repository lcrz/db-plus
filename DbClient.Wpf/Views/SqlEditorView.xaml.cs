using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using DbClient.Wpf.ViewModels;
using DbClient.Wpf.Services;

namespace DbClient.Wpf.Views
{
    /// <summary>
    /// Lógica de interacción para SqlEditorView.xaml.
    /// Integra el control AvalonEdit con el ViewModel de múltiples pestañas,
    /// implementa drag-and-drop para reordenar pestañas y gestiona el historial de deshacer/rehacer por pestaña.
    /// </summary>
    public partial class SqlEditorView : UserControl
    {
        private CompletionWindow _completionWindow;
        private readonly Dictionary<SqlQueryTabViewModel, TextDocument> _documents = new();
        private Point _dragStartPoint;

        public SqlEditorView()
        {
            InitializeComponent();
            // Load custom dark theme SQL highlighting
            try
            {
                var uri = new Uri("pack://application:,,,/DbClient.Wpf;component/Resources/SQL-Dark.xshd");
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using (var reader = new System.Xml.XmlTextReader(streamInfo.Stream))
                    {
                        textEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
                    }
                }
                else
                {
                    // Fallback to default SQL highlighting
                    textEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("SQL");
                }
            }
            catch
            {
                // In case of any error, fallback to default highlighting
                textEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("SQL");
            }

            textEditor.TextArea.TextEntered += TextArea_TextEntered;
            textEditor.TextArea.TextEntering += TextArea_TextEntering;
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SqlEditorViewModel oldVm)
            {
                oldVm.Tabs.CollectionChanged -= Tabs_CollectionChanged;
            }
            if (e.NewValue is SqlEditorViewModel vm)
            {
                vm.Tabs.CollectionChanged += Tabs_CollectionChanged;
                if (vm.SelectedTab != null)
                {
                    SwitchToTabDocument(vm.SelectedTab);
                }
            }
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove ||
                e.Action == NotifyCollectionChangedAction.Replace)
            {
                if (e.OldItems != null)
                {
                    foreach (SqlQueryTabViewModel oldTab in e.OldItems)
                    {
                        _documents.Remove(oldTab);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _documents.Clear();
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabControl != null && e.Source == tabControl && DataContext is SqlEditorViewModel vm && vm.SelectedTab != null)
            {
                SwitchToTabDocument(vm.SelectedTab);
            }
        }

        private void SwitchToTabDocument(SqlQueryTabViewModel tab)
        {
            if (tab == null || textEditor == null) return;

            if (!_documents.TryGetValue(tab, out var doc))
            {
                doc = new TextDocument(tab.QueryText ?? string.Empty);
                _documents[tab] = doc;
                
                doc.TextChanged += (s, ev) =>
                {
                    if (tab.QueryText != doc.Text)
                    {
                        tab.QueryText = doc.Text;
                    }
                };

                tab.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(SqlQueryTabViewModel.QueryText))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (doc.Text != tab.QueryText)
                            {
                                doc.Text = tab.QueryText ?? string.Empty;
                            }
                        });
                    }
                };
            }

            if (textEditor.Document != doc)
            {
                textEditor.Document = doc;
            }
        }

        private void textEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                ExecuteQuery();
                e.Handled = true;
                return;
            }

            if (_completionWindow != null)
            {
                if (e.Key == Key.Enter)
                {
                    _completionWindow.Close();
                    _completionWindow = null;
                }
            }
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuery();
        }

        private void ExecuteQuery()
        {
            if (DataContext is SqlEditorViewModel vm && vm.SelectedTab != null && textEditor != null)
            {
                var tabVm = vm.SelectedTab;
                tabVm.QueryText = textEditor.Text;
                
                string queryToExecute = textEditor.SelectionLength > 0 
                    ? textEditor.SelectedText 
                    : textEditor.Text;

                if (tabVm.ExecuteQueryCommand.CanExecute(queryToExecute))
                {
                    tabVm.ExecuteQueryCommand.Execute(queryToExecute);
                }
            }
        }

        // --- Drag and Drop Tab Reordering ---
        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is DependencyObject depObj)
            {
                var tabItem = FindVisualParent<TabItem>(depObj);
                if (tabItem != null && tabItem.DataContext is SqlQueryTabViewModel)
                {
                    _dragStartPoint = e.GetPosition(null);
                }
            }
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (e.Source is DependencyObject depObj)
                    {
                        var tabItem = FindVisualParent<TabItem>(depObj);
                        if (tabItem != null && tabItem.DataContext is SqlQueryTabViewModel)
                        {
                            DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(TabItem)) is TabItem sourceTabItem)
            {
                if (e.Source is DependencyObject depObj)
                {
                    var targetTabItem = FindVisualParent<TabItem>(depObj);
                    if (targetTabItem != null && !Equals(sourceTabItem, targetTabItem))
                    {
                        if (DataContext is SqlEditorViewModel vm && 
                            sourceTabItem.DataContext is SqlQueryTabViewModel sourceTab && 
                            targetTabItem.DataContext is SqlQueryTabViewModel targetTab)
                        {
                            int sourceIndex = vm.Tabs.IndexOf(sourceTab);
                            int targetIndex = vm.Tabs.IndexOf(targetTab);

                            if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                            {
                                vm.Tabs.Move(sourceIndex, targetIndex);
                                vm.SaveTabsState();
                            }
                        }
                    }
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent)
            {
                return parent;
            }
            return FindVisualParent<T>(parentObject);
        }

        // --- SQL Autocomplete ---
        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                {
                    // Cerrar el autocompletado en caracteres no alfanuméricos como espacio o puntuación sin insertar la sugerencia.
                    _completionWindow.Close();
                    _completionWindow = null;
                }
            }
        }

        private async void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0)
            {
                char lastChar = e.Text[0];
                if (char.IsLetterOrDigit(lastChar) || lastChar == '.' || lastChar == '_')
                {
                    await ShowCompletionWindowAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task ShowCompletionWindowAsync()
        {
            if (textEditor?.TextArea == null || _completionWindow != null) return;

            int caretOffset = textEditor.TextArea.Caret.Offset;
            int startOffset = caretOffset;
            string alias = null;

            // Buscar si hay una palabra antes del cursor (excluyendo el punto si lo acabamos de escribir)
            while (startOffset > 0)
            {
                char c = textEditor.Document.GetCharAt(startOffset - 1);
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    startOffset--;
                }
                else
                {
                    break;
                }
            }

            // Si hay un punto justo antes del segmento que estamos escribiendo, detectamos el alias
            if (startOffset > 1 && textEditor.Document.GetCharAt(startOffset - 1) == '.')
            {
                int aliasEnd = startOffset - 2;
                int aliasStart = aliasEnd;
                while (aliasStart >= 0)
                {
                    char c = textEditor.Document.GetCharAt(aliasStart);
                    if (char.IsLetterOrDigit(c) || c == '_')
                    {
                        aliasStart--;
                    }
                    else
                    {
                        break;
                    }
                }
                int len = (aliasEnd + 1) - (aliasStart + 1);
                if (len > 0)
                {
                    alias = textEditor.Document.GetText(aliasStart + 1, len);
                }
            }

            List<string> columns = null;
            string resolvedTable = null;
            if (!string.IsNullOrEmpty(alias) && DataContext is SqlEditorViewModel vm)
            {
                var aliases = ParseTableAliases(textEditor.Text);
                if (aliases.TryGetValue(alias, out resolvedTable))
                {
                    columns = await vm.GetColumnsForTableAsync(resolvedTable);
                }
            }

            // Volver a verificar el estado por si cambió durante el await
            if (textEditor?.TextArea == null || _completionWindow != null) return;

            _completionWindow = new CompletionWindow(textEditor.TextArea);
            _completionWindow.StartOffset = startOffset;
            IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

            if (columns != null && columns.Count > 0)
            {
                // Si encontramos columnas para el alias/tabla, mostrar solo las columnas
                foreach (var col in columns)
                {
                    data.Add(new SqlCompletionData(col, $"Columna de la tabla '{resolvedTable}'"));
                }
            }
            else
            {
                // Si no hay alias/punto, mostrar autocompletado normal de palabras clave y tablas
                string[] keywords = { 
                    "SELECT", "FROM", "WHERE", "INSERT", "INTO", "UPDATE", "DELETE", 
                    "JOIN", "LEFT", "RIGHT", "INNER", "ON", "GROUP BY", "ORDER BY", 
                    "HAVING", "LIMIT", "AND", "OR", "NOT", "CREATE", "TABLE", 
                    "DROP", "ALTER", "AS", "COUNT", "SUM", "AVG", "MIN", "MAX" 
                };

                foreach (var keyword in keywords)
                {
                    data.Add(new SqlCompletionData(keyword, "Palabra clave de SQL estándar"));
                }

                if (DataContext is SqlEditorViewModel vmNormal && vmNormal.Tables != null)
                {
                    foreach (var table in vmNormal.Tables)
                    {
                        data.Add(new SqlCompletionData(table, "Tabla de la base de datos activa"));
                    }
                }
            }

            if (data.Count > 0)
            {
                _completionWindow.Show();
                _completionWindow.Closed += delegate {
                    _completionWindow = null;
                };
            }
        }

        private Dictionary<string, string> ParseTableAliases(string sql)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(sql)) return aliases;

            var regex = new System.Text.RegularExpressions.Regex(
                @"(?i)\b(?:FROM|JOIN)\s+([\w\.\`\[\]]+)(?:\s+(?:AS\s+)?(\w+))?", 
                System.Text.RegularExpressions.RegexOptions.Compiled);
            var matches = regex.Matches(sql);

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Groups[1].Success)
                {
                    string rawTable = m.Groups[1].Value;
                    string cleanTable = rawTable.Replace("`", "").Replace("[", "").Replace("]", "");
                    int dotIdx = cleanTable.LastIndexOf('.');
                    if (dotIdx >= 0)
                    {
                        cleanTable = cleanTable.Substring(dotIdx + 1);
                    }

                    string tableAlias = m.Groups[2].Success ? m.Groups[2].Value : cleanTable;

                    if (!string.IsNullOrEmpty(tableAlias))
                    {
                        aliases[tableAlias] = cleanTable;
                    }
                    
                    if (!aliases.ContainsKey(cleanTable))
                    {
                        aliases[cleanTable] = cleanTable;
                    }
                }
            }

            return aliases;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (menuItemFormatSelection != null && textEditor != null)
            {
                menuItemFormatSelection.IsEnabled = textEditor.SelectionLength > 0;
            }
        }

        private void FormatSelection_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor != null && textEditor.SelectionLength > 0)
            {
                try
                {
                    string selectedText = textEditor.SelectedText;
                    var formattingManager = new PoorMansTSqlFormatterLib.SqlFormattingManager();
                    string formatted = formattingManager.Format(selectedText);
                    textEditor.Document.Replace(textEditor.SelectionStart, textEditor.SelectionLength, formatted);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al formatear selección: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FormatAll_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor != null)
            {
                try
                {
                    string text = textEditor.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var formattingManager = new PoorMansTSqlFormatterLib.SqlFormattingManager();
                        string formatted = formattingManager.Format(text);
                        textEditor.Document.Replace(0, textEditor.Document.TextLength, formatted);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al formatear SQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (sender is TreeView treeView && treeView.SelectedItem is ViewModels.JsonNodeViewModel selectedNode)
                {
                    if (treeView.DataContext is ViewModels.QueryResultViewModel vm)
                    {
                        vm.CopyJsonNodeToClipboard(selectedNode);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
