using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DbClient.Wpf.Views
{
    /// <summary>
    /// Lógica de interacción para ConnectionManagerView.xaml
    /// </summary>
    public partial class ConnectionManagerView : UserControl
    {
        public ConnectionManagerView()
        {
            InitializeComponent();
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.ConnectionManagerViewModel vm && vm.ConnectCommand?.CanExecute(null) == true)
            {
                vm.ConnectCommand.Execute(null);
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }
        }
    }
}
