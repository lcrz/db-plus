using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DbClient.Wpf.Models;

namespace DbClient.Wpf.Services
{
    /// <summary>
    /// Servicio encargado de gestionar el almacenamiento local de las conexiones y configuraciones.
    /// Utiliza cifrado DPAPI de Windows para proteger las credenciales guardadas.
    /// </summary>
    public class ConnectionStorageService
    {
        private readonly string _settingsFilePath;
        private readonly string _defaultStorageFolder;
        private AppSettings _currentSettings;

        public AppSettings CurrentSettings => _currentSettings;

        public ConnectionStorageService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appData, "DbClient");
            Directory.CreateDirectory(appFolder);

            _settingsFilePath = Path.Combine(appFolder, "appsettings.json");
            _defaultStorageFolder = Path.Combine(appFolder, "Connections");

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json);
                }
                catch
                {
                    // Manejo silencioso de error si el archivo de configuración está corrupto
                }
            }

            if (_currentSettings == null)
            {
                _currentSettings = new AppSettings
                {
                    ConnectionsStorageFolder = _defaultStorageFolder
                };
                SaveSettings();
            }

            // Asegurar que exista la carpeta configurada
            try
            {
                Directory.CreateDirectory(_currentSettings.ConnectionsStorageFolder);
            }
            catch
            {
                // Revertir a la ruta por defecto si el directorio seleccionado no es válido
                _currentSettings.ConnectionsStorageFolder = _defaultStorageFolder;
                Directory.CreateDirectory(_defaultStorageFolder);
            }
        }

        public void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al guardar la configuración: {ex.Message}", ex);
            }
        }

        public void UpdateStorageFolder(string newFolder)
        {
            if (string.IsNullOrWhiteSpace(newFolder))
                throw new ArgumentException("La ruta de la carpeta no puede estar vacía.");

            // Validar que se pueda escribir en el directorio
            Directory.CreateDirectory(newFolder);

            string oldFolder = _currentSettings.ConnectionsStorageFolder;
            if (!string.Equals(Path.GetFullPath(oldFolder), Path.GetFullPath(newFolder), StringComparison.OrdinalIgnoreCase))
            {
                MigrateFiles(oldFolder, newFolder);
            }

            _currentSettings.ConnectionsStorageFolder = newFolder;
            SaveSettings();
        }

        private void MigrateFiles(string oldFolder, string newFolder)
        {
            string[] filesToMigrate = { "connections.json", "appdata.json", "tabletabs.json" };
            foreach (var fileName in filesToMigrate)
            {
                string sourcePath = Path.Combine(oldFolder, fileName);

                // Fallback para appdata.json y tabletabs.json si no existen en oldFolder pero están en el root original
                if (!File.Exists(sourcePath) && (fileName == "appdata.json" || fileName == "tabletabs.json"))
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string oldRootFolder = Path.Combine(appData, "DbClient");
                    sourcePath = Path.Combine(oldRootFolder, fileName);
                }

                if (File.Exists(sourcePath))
                {
                    try
                    {
                        string targetPath = Path.Combine(newFolder, fileName);
                        if (!File.Exists(targetPath))
                        {
                            File.Copy(sourcePath, targetPath, overwrite: false);
                            try { File.Delete(sourcePath); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al migrar {fileName} a {newFolder}: {ex.Message}");
                    }
                }
            }

            // Migrar la carpeta Queries si existe
            string oldQueriesDir = Path.Combine(oldFolder, "Queries");
            string newQueriesDir = Path.Combine(newFolder, "Queries");
            if (Directory.Exists(oldQueriesDir))
            {
                try
                {
                    Directory.CreateDirectory(newQueriesDir);
                    foreach (var file in Directory.GetFiles(oldQueriesDir, "*.sql"))
                    {
                        string targetPath = Path.Combine(newQueriesDir, Path.GetFileName(file));
                        if (!File.Exists(targetPath))
                        {
                            File.Copy(file, targetPath, overwrite: false);
                            try { File.Delete(file); } catch { }
                        }
                    }
                    try { Directory.Delete(oldQueriesDir); } catch { }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al migrar carpeta de consultas: {ex.Message}");
                }
            }
        }

        private string GetConnectionsFilePath()
        {
            return Path.Combine(_currentSettings.ConnectionsStorageFolder, "connections.json");
        }

        /// <summary>
        /// Lee y descifra las conexiones guardadas en el archivo local.
        /// </summary>
        public List<ConnectionDetails> LoadConnections()
        {
            string filePath = GetConnectionsFilePath();
            if (!File.Exists(filePath))
            {
                return new List<ConnectionDetails>();
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(filePath);

                // Descifrado usando la API de Protección de Datos de Windows (DPAPI)
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decryptedBytes);

                return JsonSerializer.Deserialize<List<ConnectionDetails>>(json) ?? new List<ConnectionDetails>();
            }
            catch (CryptographicException)
            {
                // Captura el caso donde cambie de máquina/usuario Windows o el archivo no esté encriptado
                return new List<ConnectionDetails>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al leer el archivo de conexiones: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cifra y guarda la lista de conexiones en el directorio configurado.
        /// </summary>
        public void SaveConnections(List<ConnectionDetails> connections)
        {
            if (connections == null) throw new ArgumentNullException(nameof(connections));

            string filePath = GetConnectionsFilePath();
            string directory = Path.GetDirectoryName(filePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                string json = JsonSerializer.Serialize(connections);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);

                // Cifrar usando DPAPI con alcance de Usuario Actual (solo el usuario de Windows actual puede descifrar)
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(filePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error al cifrar y guardar las conexiones: {ex.Message}", ex);
            }
        }

        private string GetQueriesDirectoryPath()
        {
            var path = Path.Combine(_currentSettings.ConnectionsStorageFolder, "Queries");
            Directory.CreateDirectory(path);
            return path;
        }

        public IEnumerable<string> GetSavedQueryNames()
        {
            var dir = GetQueriesDirectoryPath();
            var files = Directory.GetFiles(dir, "*.sql");
            var names = new List<string>();
            foreach (var file in files)
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }
            return names;
        }

        public string LoadSavedQuery(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la consulta no puede estar vacío.");
            var filePath = Path.Combine(GetQueriesDirectoryPath(), $"{name}.sql");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"No se encontró el archivo de consulta: {filePath}");
            }
            return File.ReadAllText(filePath, Encoding.UTF8);
        }

        public void SaveQuery(string name, string sqlText)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la consulta no puede estar vacío.");
            
            // Sanear nombre de archivo
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            
            var filePath = Path.Combine(GetQueriesDirectoryPath(), $"{name}.sql");
            File.WriteAllText(filePath, sqlText ?? string.Empty, Encoding.UTF8);
        }

        public void DeleteSavedQuery(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la consulta no puede estar vacío.");
            var filePath = Path.Combine(GetQueriesDirectoryPath(), $"{name}.sql");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
