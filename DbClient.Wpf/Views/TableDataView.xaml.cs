using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DbClient.Wpf.Views
{
    /// <summary>
    /// Lógica de interacción para TableDataView.xaml
    /// </summary>
    public partial class TableDataView : UserControl
    {
        public TableDataView()
        {
            InitializeComponent();
            DataContextChanged += TableDataView_DataContextChanged;
            Unloaded += TableDataView_Unloaded;
            Loaded += TableDataView_Loaded;
            myDataGrid.AddHandler(Button.ClickEvent, new RoutedEventHandler(DataGrid_ButtonClick));
        }

        private void TableDataView_Loaded(object sender, RoutedEventArgs e)
        {
            // Cuando la vista se instancia (puede ocurrir después de que los datos ya cargaron),
            // forzar el reset del ItemsSource para que el DataGrid genere las columnas.
            ForceDataGridRefresh();
        }

        private void TableDataView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.TableDataViewModel vm)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
            }
        }

        private void TableDataView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.TableDataViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is ViewModels.TableDataViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                // Al cambiar de pestaña, el DataContext cambia. Si ya hay datos, refrescar.
                ForceDataGridRefresh();
            }
        }

        /// <summary>
        /// Fuerza el reset del ItemsSource del DataGrid para que se regeneren las columnas automáticas.
        /// Usa BeginInvoke con prioridad Render para asegurar que el DataGrid esté completamente
        /// inicializado antes de asignar los datos.
        /// </summary>
        private void ForceDataGridRefresh()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
            {
                if (DataContext is ViewModels.TableDataViewModel vm && vm.QueryResults != null)
                {
                    myDataGrid.Columns.Clear();
                    myDataGrid.AutoGenerateColumns = true;
                    myDataGrid.ItemsSource = null;
                    myDataGrid.ItemsSource = vm.QueryResults.DefaultView;
                }
            });
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.TableDataViewModel.QueryResults))
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
                {
                    myDataGrid.Columns.Clear();
                    if (DataContext is ViewModels.TableDataViewModel vm && vm.QueryResults != null)
                    {
                        myDataGrid.AutoGenerateColumns = true;
                        myDataGrid.ItemsSource = null;
                        myDataGrid.ItemsSource = vm.QueryResults.DefaultView;
                    }
                });
            }
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (DataContext is ViewModels.TableDataViewModel vm)
            {
                var colInfo = vm.GetColumnInfo(e.PropertyName);
                if (colInfo != null)
                {
                    var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
                    
                    if (colInfo.IsPrimaryKey)
                    {
                        var keyText = new TextBlock 
                        { 
                            Text = "🔑 ", 
                            Foreground = System.Windows.Media.Brushes.Gold, 
                            VerticalAlignment = VerticalAlignment.Center 
                        };
                        headerStack.Children.Add(keyText);
                    }
                    
                    var nameText = new TextBlock 
                    { 
                        Text = colInfo.Field, 
                        FontWeight = FontWeights.Bold, 
                        VerticalAlignment = VerticalAlignment.Center 
                    };
                    headerStack.Children.Add(nameText);

                    var typeText = new TextBlock 
                    { 
                        Text = $" {colInfo.Type}", 
                        Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush"), 
                        FontSize = 10,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    headerStack.Children.Add(typeText);

                    e.Column.Header = headerStack;
                }

                // Si la columna es clave foránea, cambiar por una de tipo plantilla (TemplateColumn)
                // para mostrar el botón/icono de enlace al lado del texto.
                if (vm.TryGetForeignKeyAssociation(e.PropertyName, out string refTable, out string refColumn))
                {
                    var templateCol = new DataGridTemplateColumn
                    {
                        Header = e.Column.Header,
                        SortMemberPath = e.PropertyName
                    };

                    string escapedRefTable = System.Security.SecurityElement.Escape(refTable);

                    string cellXaml = $@"
                        <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""*""/>
                                    <ColumnDefinition Width=""Auto""/>
                                </Grid.ColumnDefinitions>
                                <TextBlock Text=""{{Binding [{e.PropertyName}]}}"" VerticalAlignment=""Center"" Margin=""2,0""/>
                                <Button Grid.Column=""1"" 
                                        Content=""🔗"" 
                                        ToolTip=""Ir a la tabla relacionada ({escapedRefTable})""
                                        Tag=""{e.PropertyName}""
                                        Background=""Transparent"" 
                                        BorderThickness=""0"" 
                                        Foreground=""#4CAF50""
                                        Cursor=""Hand""
                                        Padding=""4,0""
                                        VerticalAlignment=""Center""/>
                            </Grid>
                        </DataTemplate>";

                    string editingXaml = $@"
                        <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                            <TextBox Text=""{{Binding [{e.PropertyName}], UpdateSourceTrigger=LostFocus}}"" 
                                     BorderThickness=""0"" 
                                     Padding=""0"" 
                                     VerticalAlignment=""Center""/>
                        </DataTemplate>";

                    templateCol.CellTemplate = (DataTemplate)XamlReader.Parse(cellXaml);
                    templateCol.CellEditingTemplate = (DataTemplate)XamlReader.Parse(editingXaml);

                    e.Column = templateCol;
                }
            }
        }

        private void DataGrid_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is string colName)
            {
                if (btn.DataContext is System.Data.DataRowView rowView && DataContext is ViewModels.TableDataViewModel vm)
                {
                    vm.NavigateToForeignKey(rowView, colName);
                }
            }
        }

        private void TreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (sender is TreeView treeView && treeView.SelectedItem is ViewModels.JsonNodeViewModel selectedNode)
                {
                    if (DataContext is ViewModels.TableDataViewModel vm)
                    {
                        vm.CopyJsonNodeToClipboard(selectedNode);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
