using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DbClient.Core;
using DbClient.Wpf.Models;
using DbClient.Wpf.Services;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que gestiona la lógica de la pantalla de administración de conexiones.
    /// Maneja el CRUD, pruebas de conexión, e interactúa con el ConnectionStorageService.
    /// </summary>
    public class ConnectionManagerViewModel : BaseViewModel
    {
        private readonly IDatabasePlugin _databasePlugin;
        private readonly ConnectionStorageService _storageService;

        // Campos del formulario
        private string _connectionName = "Nueva Conexión MySQL";
        private string _server = "127.0.0.1";
        private string _port = "3306";
        private string _username = "root";
        private string _password = "";
        private string _databaseName = "";

        // Selección e Historial
        private ConnectionDetails _selectedConnection;
        private ObservableCollection<ConnectionDetails> _savedConnections = new();

        // Parámetros/Configuración de ruta
        private string _storageFolderPath;

        // Configuración de Ollama IA
        private string _ollamaEndpointUrl;
        private string _ollamaModelName;
        private string _ollamaSystemPrompt;
        private string _aiProvider;
        private string _antigravityCliPath;

        // Opciones de Copiado
        private JsonCopyMode _selectedJsonCopyMode = JsonCopyMode.KeyOnly;
        public JsonCopyMode SelectedJsonCopyMode
        {
            get => _selectedJsonCopyMode;
            set => SetProperty(ref _selectedJsonCopyMode, value);
        }

        public List<JsonCopyModeOption> AvailableCopyModes { get; } = new()
        {
            new JsonCopyModeOption(JsonCopyMode.KeyOnly, "Copiar solo key"),
            new JsonCopyModeOption(JsonCopyMode.KeyAndValueJson, "Copiar key + valor (JSON)"),
            new JsonCopyModeOption(JsonCopyMode.KeyAndSubtreeJson, "Copiar key + sub-nodos (JSON)")
        };

        // Estado y Feedback
        private string _feedbackMessage;
        private bool _isSuccessFeedback;
        private bool _isBusy;

        /// <summary>
        /// Evento disparado cuando se logra establecer una conexión con éxito para entrar al editor SQL.
        /// </summary>
        public event Action<ConnectionDetails> ConnectionEstablished;

        public string ConnectionName
        {
            get => _connectionName;
            set => SetProperty(ref _connectionName, value);
        }

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }

        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string DatabaseName
        {
            get => _databaseName;
            set => SetProperty(ref _databaseName, value);
        }

        public ConnectionDetails SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (SetProperty(ref _selectedConnection, value))
                {
                    LoadSelectedConnectionDetails();
                }
            }
        }

        public ObservableCollection<ConnectionDetails> SavedConnections
        {
            get => _savedConnections;
            set => SetProperty(ref _savedConnections, value);
        }

        public string StorageFolderPath
        {
            get => _storageFolderPath;
            set => SetProperty(ref _storageFolderPath, value);
        }

        public string OllamaEndpointUrl
        {
            get => _ollamaEndpointUrl;
            set => SetProperty(ref _ollamaEndpointUrl, value);
        }

        public string OllamaModelName
        {
            get => _ollamaModelName;
            set => SetProperty(ref _ollamaModelName, value);
        }

        public string OllamaSystemPrompt
        {
            get => _ollamaSystemPrompt;
            set => SetProperty(ref _ollamaSystemPrompt, value);
        }

        public string AiProvider
        {
            get => _aiProvider;
            set => SetProperty(ref _aiProvider, value);
        }

        public string AntigravityCliPath
        {
            get => _antigravityCliPath;
            set => SetProperty(ref _antigravityCliPath, value);
        }

        public string FeedbackMessage
        {
            get => _feedbackMessage;
            set => SetProperty(ref _feedbackMessage, value);
        }

        public bool IsSuccessFeedback
        {
            get => _isSuccessFeedback;
            set => SetProperty(ref _isSuccessFeedback, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // Comandos del formulario
        public ICommand TestConnectionCommand { get; }
        public ICommand SaveConnectionCommand { get; }
        public ICommand DeleteConnectionCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand ClearFormCommand { get; }

        // Comandos de configuración
        public ICommand BrowseFolderCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand SaveAiSettingsCommand { get; }
        public ICommand ResetAiPromptCommand { get; }
        public ICommand SaveOptionsCommand { get; }

        public ConnectionManagerViewModel(IDatabasePlugin databasePlugin, ConnectionStorageService storageService)
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            StorageFolderPath = _storageService.CurrentSettings.ConnectionsStorageFolder;

            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => !IsBusy);
            SaveConnectionCommand = new RelayCommand(SaveConnection, () => !IsBusy && !string.IsNullOrWhiteSpace(ConnectionName));
            DeleteConnectionCommand = new RelayCommand(DeleteConnection, () => !IsBusy && SelectedConnection != null);
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsBusy);
            ClearFormCommand = new RelayCommand(ClearForm);

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            SaveAiSettingsCommand = new RelayCommand(SaveAiSettings);
            ResetAiPromptCommand = new RelayCommand(ResetAiPrompt);
            SaveOptionsCommand = new RelayCommand(SaveOptions);

            OllamaEndpointUrl = _storageService.CurrentSettings.OllamaEndpointUrl;
            OllamaModelName = _storageService.CurrentSettings.OllamaModelName;
            OllamaSystemPrompt = _storageService.CurrentSettings.OllamaSystemPrompt;
            AiProvider = _storageService.CurrentSettings.AiProvider;
            AntigravityCliPath = _storageService.CurrentSettings.AntigravityCliPath;

            if (Enum.TryParse<JsonCopyMode>(_storageService.CurrentSettings.JsonCopyMode, out var copyMode))
            {
                SelectedJsonCopyMode = copyMode;
            }
            else
            {
                SelectedJsonCopyMode = JsonCopyMode.KeyOnly;
            }

            LoadSavedConnections();
        }

        private void LoadSavedConnections()
        {
            try
            {
                var list = _storageService.LoadConnections();
                SavedConnections.Clear();
                foreach (var conn in list)
                {
                    SavedConnections.Add(conn);
                }
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al cargar conexiones locales: {ex.Message}", false);
            }
        }

        private void LoadSelectedConnectionDetails()
        {
            if (SelectedConnection != null)
            {
                ConnectionName = SelectedConnection.Name;
                Server = SelectedConnection.Server;
                Port = SelectedConnection.Port;
                Username = SelectedConnection.Username;
                Password = SelectedConnection.Password;
                DatabaseName = SelectedConnection.DatabaseName;
                ShowFeedback($"Perfil '{SelectedConnection.Name}' cargado.", true);
            }
        }

        private void ClearForm()
        {
            SelectedConnection = null;
            ConnectionName = "Nueva Conexión MySQL";
            Server = "127.0.0.1";
            Port = "3306";
            Username = "root";
            Password = "";
            DatabaseName = "";
            FeedbackMessage = string.Empty;
        }

        private string BuildConnectionString()
        {
            var parts = new System.Collections.Generic.List<string>
            {
                $"Server={Server}",
                $"Port={Port}",
                $"User ID={Username}",
                $"Password={Password}",
                "Convert Zero Datetime=True"
            };
            if (!string.IsNullOrWhiteSpace(DatabaseName))
            {
                parts.Add($"Database={DatabaseName}");
            }
            return string.Join(";", parts);
        }

        private ConnectionDetails GetFormDetails()
        {
            return new ConnectionDetails
            {
                Id = SelectedConnection?.Id ?? Guid.NewGuid(),
                Name = ConnectionName,
                Server = Server,
                Port = Port,
                Username = Username,
                Password = Password,
                DatabaseName = DatabaseName
            };
        }

        private async Task TestConnectionAsync()
        {
            IsBusy = true;
            ShowFeedback("Probando conexión...", true);

            try
            {
                string connStr = BuildConnectionString();
                await _databasePlugin.ConnectAsync(connStr);
                await _databasePlugin.DisconnectAsync();
                ShowFeedback("¡Prueba de conexión exitosa!", true);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error en la prueba: {ex.Message}", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveConnection()
        {
            IsBusy = true;
            try
            {
                var details = GetFormDetails();
                var list = _storageService.LoadConnections();

                var existing = list.FirstOrDefault(c => c.Id == details.Id);
                if (existing != null)
                {
                    existing.Name = details.Name;
                    existing.Server = details.Server;
                    existing.Port = details.Port;
                    existing.Username = details.Username;
                    existing.Password = details.Password;
                    existing.DatabaseName = details.DatabaseName;
                }
                else
                {
                    list.Add(details);
                }

                _storageService.SaveConnections(list);
                LoadSavedConnections();

                SelectedConnection = SavedConnections.FirstOrDefault(c => c.Id == details.Id);
                ShowFeedback("Conexión guardada con éxito cifrada en archivo local.", true);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al guardar: {ex.Message}", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void DeleteConnection()
        {
            if (SelectedConnection == null) return;

            IsBusy = true;
            try
            {
                var list = _storageService.LoadConnections();
                var existing = list.FirstOrDefault(c => c.Id == SelectedConnection.Id);
                if (existing != null)
                {
                    list.Remove(existing);
                    _storageService.SaveConnections(list);
                    ShowFeedback($"Conexión '{SelectedConnection.Name}' eliminada.", true);
                    ClearForm();
                    LoadSavedConnections();
                }
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al eliminar: {ex.Message}", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConnectAsync()
        {
            IsBusy = true;
            ShowFeedback("Estableciendo conexión...", true);

            try
            {
                string connStr = BuildConnectionString();
                await _databasePlugin.ConnectAsync(connStr);
                
                var details = GetFormDetails();
                ShowFeedback("Conexión lista. Abriendo área de trabajo...", true);
                
                ConnectionEstablished?.Invoke(details);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al conectar: {ex.Message}", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Seleccionar Carpeta para Conexiones",
                InitialDirectory = StorageFolderPath
            };

            if (dialog.ShowDialog() == true)
            {
                StorageFolderPath = dialog.FolderName;
            }
        }

        private void SaveSettings()
        {
            if (string.IsNullOrWhiteSpace(StorageFolderPath))
            {
                ShowFeedback("La ruta no puede ser nula.", false);
                return;
            }

            try
            {
                _storageService.UpdateStorageFolder(StorageFolderPath);
                ShowFeedback("Ruta de almacenamiento guardada con éxito.", true);
                LoadSavedConnections();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al actualizar la carpeta: {ex.Message}", false);
            }
        }

        private void SaveAiSettings()
        {
            try
            {
                _storageService.CurrentSettings.OllamaEndpointUrl = OllamaEndpointUrl;
                _storageService.CurrentSettings.OllamaModelName = OllamaModelName;
                _storageService.CurrentSettings.OllamaSystemPrompt = OllamaSystemPrompt;
                _storageService.CurrentSettings.AiProvider = AiProvider;
                _storageService.CurrentSettings.AntigravityCliPath = AntigravityCliPath;
                
                _storageService.SaveSettings();
                ShowFeedback("Configuración de IA guardada correctamente.", true);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al guardar configuración de Ollama: {ex.Message}", false);
            }
        }

        private void ResetAiPrompt()
        {
            OllamaSystemPrompt = "Eres un asistente experto en bases de datos MySQL. Tu única tarea es traducir las solicitudes en lenguaje natural del usuario a una consulta SQL válida (únicamente sentencias SELECT) basándose en el esquema de tablas proporcionado.\nIMPORTANTE: El usuario te pedirá cosas como 'Muéstrame las facturas' o 'Busca los usuarios'. No intentes acceder a ninguna base de datos real ni respondas indicando que no puedes acceder. Tu única labor es escribir la consulta SQL SELECT correspondiente que la aplicación cliente ejecutará.\nREGLA DE SEGURIDAD ABSOLUTA: Solo puedes generar sentencias SELECT. Tienes estrictamente prohibido generar sentencias UPDATE, INSERT, DELETE, DROP, TRUNCATE, ALTER o cualquier otra instrucción que modifique datos o estructuras. Si el usuario pide modificar datos, debes responder con un comentario SQL indicando que no tienes los permisos para esa acción.\nREGLA DE NOMBRES EXACTOS: Debes utilizar únicamente los nombres EXACTOS de las tablas y columnas provistos en el esquema. No asumas ni inventes nombres. Si en el esquema una tabla es 'factura' en singular o 'cliente' en singular, debes escribirla exactamente en singular en el SQL, sin pluralizarla a 'facturas' o 'clientes' (o viceversa).\nREGLA DE SINTAXIS CRÍTICA: Al usar múltiples JOINs, asegúrate de que el orden de las tablas unidas sea lógico. Nunca hagas referencia al alias de una tabla en la cláusula ON de un JOIN si esa tabla aún no ha sido declarada en el FROM o en un JOIN anterior. Por ejemplo, en lugar de 'FROM a JOIN b ON a.id = c.a_id JOIN c ON ...', escribe 'FROM a JOIN c ON a.id = c.a_id JOIN b ON ...' (declarando c antes de usar su alias).\nDevuelve únicamente el código SQL limpio, sin bloques de código Markdown (```sql), sin explicaciones adicionales ni saludos.";
            ShowFeedback("System Prompt restablecido. Haz clic en Guardar para aplicar.", true);
        }

        private void SaveOptions()
        {
            try
            {
                _storageService.CurrentSettings.JsonCopyMode = SelectedJsonCopyMode.ToString();
                _storageService.SaveSettings();
                ShowFeedback("Opciones de copiado guardadas correctamente.", true);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Error al guardar opciones: {ex.Message}", false);
            }
        }

        private void ShowFeedback(string message, bool isSuccess)
        {
            FeedbackMessage = message;
            IsSuccessFeedback = isSuccess;
        }
    }
}
