using System.Windows;

namespace DbClient.Wpf.Views
{
    /// <summary>
    /// Lógica de interacción para SqlPreviewWindow.xaml
    /// </summary>
    public partial class SqlPreviewWindow : Window
    {
        public SqlPreviewWindow(string sql)
        {
            InitializeComponent();
            // Apply custom dark theme highlighting to the read‑only editor
            try
            {
                var uri = new Uri("pack://application:,,,/DbClient.Wpf;component/Resources/SQL-Dark.xshd");
                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using (var reader = new System.Xml.XmlTextReader(streamInfo.Stream))
                    {
                        sqlEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
                    }
                }
                else
                {
                    sqlEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("SQL");
                }
            }
            catch
            {
                sqlEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("SQL");
            }
            sqlEditor.Text = sql;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(sqlEditor.Text);
            MessageBox.Show("Código SQL copiado al portapapeles con éxito.", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
