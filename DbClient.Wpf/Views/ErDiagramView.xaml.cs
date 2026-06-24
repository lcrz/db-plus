using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DbClient.Wpf.ViewModels;

namespace DbClient.Wpf.Views
{
    public partial class ErDiagramView : UserControl
    {
        // Dragging state variables
        private TableNodeViewModel _draggedNode;
        private Point _lastMousePosition;
        private bool _isDragging;

        // Panning state variables
        private bool _isPanning;
        private Point _panStartMousePosition;
        private double _panStartPanX;
        private double _panStartPanY;

        public ErDiagramView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ErDiagramViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
                oldVm.Tables.CollectionChanged -= OnTablesCollectionChanged;
            }

            if (e.NewValue is ErDiagramViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                newVm.Tables.CollectionChanged += OnTablesCollectionChanged;
            }

            UpdateOverlays();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ErDiagramViewModel.IsLoading) ||
                e.PropertyName == nameof(ErDiagramViewModel.ErrorMessage))
            {
                UpdateOverlays();
            }
        }

        private void OnTablesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateOverlays();
        }

        private void UpdateOverlays()
        {
            if (DataContext is ErDiagramViewModel vm)
            {
                loadingIndicator.Visibility = vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                errorOverlay.Visibility = !string.IsNullOrEmpty(vm.ErrorMessage) ? Visibility.Visible : Visibility.Collapsed;
                emptyOverlay.Visibility = (vm.Tables.Count == 0 && !vm.IsLoading && string.IsNullOrEmpty(vm.ErrorMessage))
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                loadingIndicator.Visibility = Visibility.Collapsed;
                errorOverlay.Visibility = Visibility.Collapsed;
                emptyOverlay.Visibility = Visibility.Visible;
            }
        }

        // --- Table Dragging Logic ---
        private void TableCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null && element.DataContext is TableNodeViewModel node)
            {
                _draggedNode = node;
                _lastMousePosition = e.GetPosition(diagramCanvas);
                _isDragging = true;
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void TableCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedNode != null)
            {
                var element = sender as FrameworkElement;
                if (element != null && element.IsMouseCaptured)
                {
                    Point currentPos = e.GetPosition(diagramCanvas);
                    double deltaX = currentPos.X - _lastMousePosition.X;
                    double deltaY = currentPos.Y - _lastMousePosition.Y;

                    // Update node coordinates
                    _draggedNode.X += deltaX;
                    _draggedNode.Y += deltaY;

                    _lastMousePosition = currentPos;
                    e.Handled = true;
                }
            }
        }

        private void TableCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                var element = sender as FrameworkElement;
                element?.ReleaseMouseCapture();
                _isDragging = false;
                _draggedNode = null;
                e.Handled = true;
            }
        }

        // --- Canvas Panning Logic (Left Click on empty space & Middle Click) ---
        private void DiagramContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                StartPanning(e);
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                // Only pan if we clicked directly on the background / empty space (and not on a card)
                if (e.OriginalSource == diagramContainer || e.OriginalSource == diagramCanvas)
                {
                    StartPanning(e);
                }
            }
        }

        private void StartPanning(MouseButtonEventArgs e)
        {
            if (DataContext is ErDiagramViewModel vm)
            {
                _isPanning = true;
                _panStartMousePosition = e.GetPosition(diagramContainer);
                _panStartPanX = vm.PanX;
                _panStartPanY = vm.PanY;
                diagramContainer.CaptureMouse();
                diagramContainer.Cursor = Cursors.Hand;
                e.Handled = true;
            }
        }

        private void DiagramContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && DataContext is ErDiagramViewModel vm)
            {
                Point currentPos = e.GetPosition(diagramContainer);
                double deltaX = currentPos.X - _panStartMousePosition.X;
                double deltaY = currentPos.Y - _panStartMousePosition.Y;

                vm.PanX = _panStartPanX + deltaX;
                vm.PanY = _panStartPanY + deltaY;
                e.Handled = true;
            }
        }

        private void DiagramContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                StopPanning();
                e.Handled = true;
            }
        }

        private void StopPanning()
        {
            if (_isPanning)
            {
                diagramContainer.ReleaseMouseCapture();
                diagramContainer.Cursor = Cursors.Arrow;
                _isPanning = false;
            }
        }

        // --- Canvas Zooming Logic (Ctrl + MouseWheel) ---
        private void DiagramContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && DataContext is ErDiagramViewModel vm)
            {
                double zoomDelta = e.Delta > 0 ? 0.1 : -0.1;
                vm.ZoomScale += zoomDelta;
                e.Handled = true;
            }
        }

        // --- PNG Export Logic ---
        private void ExportImage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ErDiagramViewModel vm)
            {
                // 1. Get visible tables only
                var visibleTables = vm.Tables.Where(t => t.IsVisible).ToList();
                if (visibleTables.Count == 0)
                {
                    MessageBox.Show("No hay tablas visibles para exportar en el diagrama.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Ask where to save
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Image PNG (*.png)|*.png",
                    FileName = $"{visibleTables.First().TableName}_ERD.png",
                    Title = "Exportar Diagrama ER como Imagen"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        // 3. Calculate bounding box of visible tables
                        double minX = visibleTables.Min(t => t.X);
                        double minY = visibleTables.Min(t => t.Y);
                        double maxX = visibleTables.Max(t => t.X + t.Width);
                        double maxY = visibleTables.Max(t => t.Y + t.Height);

                        // Width and height of the bounding box with 60px padding on all sides
                        double padding = 60;
                        double rawWidth = maxX - minX + (padding * 2);
                        double rawHeight = maxY - minY + (padding * 2);

                        // Limit size to prevent OutOfMemoryExceptions on huge canvases
                        int width = (int)Math.Min(12000, Math.Max(100, rawWidth));
                        int height = (int)Math.Min(12000, Math.Max(100, rawHeight));

                        // 4. Temporarily save scale and pan
                        double prevScale = vm.ZoomScale;
                        double prevPanX = vm.PanX;
                        double prevPanY = vm.PanY;

                        // 5. Force rendering size and offset so the canvas displays exactly the bounding box at 1:1 scale
                        vm.ZoomScale = 1.0;
                        vm.PanX = -minX + padding;
                        vm.PanY = -minY + padding;

                        // Force UI to update layout
                        diagramCanvas.Width = width;
                        diagramCanvas.Height = height;
                        diagramCanvas.UpdateLayout();
                        diagramContainer.UpdateLayout();

                        // 6. Render canvas contents into bitmap
                        RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(diagramCanvas);

                        // 7. Save to file
                        PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                        pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

                        using (Stream fs = File.Create(saveDialog.FileName))
                        {
                            pngEncoder.Save(fs);
                        }

                        // 8. Restore canvas size and properties
                        vm.ZoomScale = prevScale;
                        vm.PanX = prevPanX;
                        vm.PanY = prevPanY;
                        diagramCanvas.Width = 10000;
                        diagramCanvas.Height = 10000;
                        diagramCanvas.UpdateLayout();

                        MessageBox.Show("Diagrama exportado con éxito.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al exportar diagrama: {ex.Message}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
