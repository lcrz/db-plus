using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DbClient.Wpf.ViewModels;
using DbClient.Wpf.Models;

namespace DbClient.Wpf
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml.
    /// Recibe MainWindowViewModel mediante Inyección de Dependencias.
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _dragStartPoint;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SaveViewState();
                vm.SqlEditor.SaveTabsState();
                vm.SaveTableTabsState();
            }
            base.OnClosing(e);
        }

        // --- Drag and Drop Tab Reordering for Table Data Tabs ---
        private void TableDataTabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is DependencyObject depObj)
            {
                var tabItem = FindVisualParent<TabItem>(depObj);
                if (tabItem != null && tabItem.DataContext is TableDataViewModel)
                {
                    _dragStartPoint = e.GetPosition(null);
                }
            }
        }

        private void TableDataTabControl_PreviewMouseMove(object sender, MouseEventArgs e)
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
                        if (tabItem != null && tabItem.DataContext is TableDataViewModel)
                        {
                            DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private void TableDataTabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(TabItem)) is TabItem sourceTabItem)
            {
                if (e.Source is DependencyObject depObj)
                {
                    var targetTabItem = FindVisualParent<TabItem>(depObj);
                    if (targetTabItem != null && !Equals(sourceTabItem, targetTabItem))
                    {
                        if (DataContext is MainWindowViewModel vm && 
                            sourceTabItem.DataContext is TableDataViewModel sourceTab && 
                            targetTabItem.DataContext is TableDataViewModel targetTab)
                        {
                            int sourceIndex = vm.TableDataTabs.IndexOf(sourceTab);
                            int targetIndex = vm.TableDataTabs.IndexOf(targetTab);

                            if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                            {
                                vm.TableDataTabs.Move(sourceIndex, targetIndex);
                                vm.SaveTableTabsState();
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

        private void QuickSearchOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Grid overlayGrid && overlayGrid.Visibility == Visibility.Visible)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    quickSearchTextBox.Focus();
                    Keyboard.Focus(quickSearchTextBox);
                }));
            }
        }

        private void QuickSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (quickSearchListBox.SelectedIndex < quickSearchListBox.Items.Count - 1)
                {
                    quickSearchListBox.SelectedIndex++;
                    quickSearchListBox.ScrollIntoView(quickSearchListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (quickSearchListBox.SelectedIndex > 0)
                {
                    quickSearchListBox.SelectedIndex--;
                    quickSearchListBox.ScrollIntoView(quickSearchListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (quickSearchListBox.SelectedItem is QuickSearchItem selectedItem)
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.ExecuteQuickSearchItemCommand.Execute(selectedItem);
                    }
                }
                e.Handled = true;
            }
        }

        private void QuickSearchListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (quickSearchListBox.SelectedItem is QuickSearchItem selectedItem)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.ExecuteQuickSearchItemCommand.Execute(selectedItem);
                }
            }
        }
    }
}