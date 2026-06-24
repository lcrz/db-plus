using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace DbClient.Wpf.Helpers
{
    public static class DataGridExtensions
    {
        public static readonly DependencyProperty BindableSourceProperty =
            DependencyProperty.RegisterAttached("BindableSource", typeof(DataTable), typeof(DataGridExtensions), new PropertyMetadata(null, OnBindableSourceChanged));

        public static DataTable GetBindableSource(DependencyObject obj)
        {
            return (DataTable)obj.GetValue(BindableSourceProperty);
        }

        public static void SetBindableSource(DependencyObject obj, DataTable value)
        {
            obj.SetValue(BindableSourceProperty, value);
        }

        private static void OnBindableSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                var dataTable = e.NewValue as DataTable;

                // Hook Loaded event to guarantee DataGrid is in the visual tree
                RoutedEventHandler loadedHandler = null;
                loadedHandler = (s, args) =>
                {
                    dataGrid.Loaded -= loadedHandler;
                    RefreshDataGrid(dataGrid, dataTable);
                };
                
                if (dataGrid.IsLoaded)
                {
                    RefreshDataGrid(dataGrid, dataTable);
                }
                else
                {
                    dataGrid.Loaded += loadedHandler;
                }
            }
        }

        private static void RefreshDataGrid(DataGrid dataGrid, DataTable dataTable)
        {
            dataGrid.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new System.Action(() =>
            {
                dataGrid.Columns.Clear();
                dataGrid.AutoGenerateColumns = true;
                dataGrid.ItemsSource = null;

                if (dataTable != null)
                {
                    dataGrid.ItemsSource = dataTable.DefaultView;
                }
            }));
        }
    }
}
