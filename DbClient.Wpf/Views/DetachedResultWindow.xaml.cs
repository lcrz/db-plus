using System.Windows;
using DbClient.Wpf.ViewModels;

namespace DbClient.Wpf.Views
{
    public partial class DetachedResultWindow : Window
    {
        public DetachedResultWindow(QueryResultViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
