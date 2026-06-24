using System.Windows;
using DbClient.Core;
using DbClient.Plugins.MySql;
using DbClient.Wpf.Services;
using DbClient.Wpf.ViewModels;

namespace DbClient.Wpf
{
    /// <summary>
    /// Lógica de interacción para App.xaml.
    /// Configura el contenedor DI, registra servicios, almacenamiento encriptado, modelos de vista y vistas.
    /// </summary>
    public partial class App : Application
    {
        private SimpleDependencyContainer _container;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Suscribirse a excepciones no controladas
            DispatcherUnhandledException += (sender, args) =>
            {
                LogUnhandledException(args.Exception, "DispatcherUnhandledException");
                args.Handled = true;
            };

            System.AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is System.Exception ex)
                {
                    LogUnhandledException(ex, "AppDomain.UnhandledException");
                }
            };

            // Inicializar el contenedor de Inyección de Dependencias básico
            _container = new SimpleDependencyContainer();

            // Registrar servicios de núcleo y plugins
            _container.RegisterSingleton<IDatabasePlugin, MySqlPlugin>();
            
            // Registrar el servicio de persistencia encriptada
            var connectionStorage = new ConnectionStorageService();
            _container.RegisterSingleton<ConnectionStorageService>(connectionStorage);
            _container.RegisterSingleton<AppDataStorageService>(new AppDataStorageService(connectionStorage));

            // Registrar servicio de IA (Ollama)
            _container.RegisterSingleton<IOllamaService, OllamaService>();

            // Registrar los elementos del patrón MVVM
            _container.RegisterTransient<ConnectionManagerViewModel, ConnectionManagerViewModel>();
            _container.RegisterTransient<SqlEditorViewModel, SqlEditorViewModel>();
            _container.RegisterTransient<MainWindowViewModel, MainWindowViewModel>();
            _container.RegisterTransient<MainWindow, MainWindow>();

            // Resolver la vista principal con todas sus dependencias inyectadas
            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.Show();
        }

        private void LogUnhandledException(System.Exception exception, string source)
        {
            try
            {
                string crashPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{System.DateTime.Now}] Source: {source}");
                
                System.Exception current = exception;
                int depth = 0;
                while (current != null)
                {
                    sb.AppendLine($"Exception [{depth}]: {current.GetType().FullName}");
                    sb.AppendLine($"Message [{depth}]: {current.Message}");
                    sb.AppendLine($"StackTrace [{depth}]:\n{current.StackTrace}");
                    sb.AppendLine(new string('-', 40));
                    current = current.InnerException;
                    depth++;
                }
                sb.AppendLine();
                
                System.IO.File.AppendAllText(crashPath, sb.ToString());
                MessageBox.Show($"Ha ocurrido un error inesperado:\n{exception.Message}\n\nEl detalle se ha guardado en:\n{crashPath}", "Error Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Evitar fallas en cascada al registrar el error
            }
        }
    }
}
