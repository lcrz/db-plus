using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DbClient.Wpf.Models;

namespace DbClient.Wpf.Services
{
    /// <summary>
    /// Servicio encargado de gestionar la persistencia de datos dinámicos de la aplicación,
    /// como las pestañas abiertas y el código SQL de cada ventana, en appdata.json.
    /// </summary>
    public class AppDataStorageService
    {
        private readonly ConnectionStorageService _connectionStorage;

        private string AppDataFilePath => Path.Combine(_connectionStorage.CurrentSettings.ConnectionsStorageFolder, "appdata.json");
        private string TableTabsFilePath => Path.Combine(_connectionStorage.CurrentSettings.ConnectionsStorageFolder, "tabletabs.json");
        private string ViewStateFilePath => Path.Combine(_connectionStorage.CurrentSettings.ConnectionsStorageFolder, "viewstate.json");

        public AppDataStorageService(ConnectionStorageService connectionStorage)
        {
            _connectionStorage = connectionStorage ?? throw new ArgumentNullException(nameof(connectionStorage));
            MigrateFromOldRootLocation();
        }

        private void MigrateFromOldRootLocation()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string oldRootFolder = Path.Combine(appData, "DbClient");
                string targetFolder = _connectionStorage.CurrentSettings.ConnectionsStorageFolder;

                if (string.IsNullOrEmpty(targetFolder))
                    return;

                Directory.CreateDirectory(targetFolder);

                string oldAppData = Path.Combine(oldRootFolder, "appdata.json");
                string oldTableTabs = Path.Combine(oldRootFolder, "tabletabs.json");

                string targetAppData = Path.Combine(targetFolder, "appdata.json");
                string targetTableTabs = Path.Combine(targetFolder, "tabletabs.json");

                if (File.Exists(oldAppData) && !File.Exists(targetAppData))
                {
                    File.Copy(oldAppData, targetAppData, overwrite: false);
                    try { File.Delete(oldAppData); } catch { }
                }
                if (File.Exists(oldTableTabs) && !File.Exists(targetTableTabs))
                {
                    File.Copy(oldTableTabs, targetTableTabs, overwrite: false);
                    try { File.Delete(oldTableTabs); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error migrando archivos desde el root: {ex.Message}");
            }
        }

        private Dictionary<string, List<SavedTabState>> LoadAllSavedTabs()
        {
            string filePath = AppDataFilePath;
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, List<SavedTabState>>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, List<SavedTabState>>>(json) 
                       ?? new Dictionary<string, List<SavedTabState>>();
            }
            catch
            {
                return new Dictionary<string, List<SavedTabState>>();
            }
        }

        private void SaveAllSavedTabs(Dictionary<string, List<SavedTabState>> savedTabs)
        {
            try
            {
                string json = JsonSerializer.Serialize(savedTabs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AppDataFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar appdata.json: {ex.Message}");
            }
        }

        public List<SavedTabState> LoadTabsForConnection(string connectionId, string databaseName)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(databaseName))
                return new List<SavedTabState>();

            string key = $"{connectionId}_{databaseName}";
            var all = LoadAllSavedTabs();
            if (all.TryGetValue(key, out var tabs))
            {
                return tabs;
            }
            return new List<SavedTabState>();
        }

        public void SaveTabsForConnection(string connectionId, string databaseName, List<SavedTabState> tabs)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(databaseName))
                return;

            string key = $"{connectionId}_{databaseName}";
            var all = LoadAllSavedTabs();
            all[key] = tabs;
            SaveAllSavedTabs(all);
        }

        private Dictionary<string, List<SavedTableTabState>> LoadAllSavedTableTabs()
        {
            string filePath = TableTabsFilePath;
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, List<SavedTableTabState>>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, List<SavedTableTabState>>>(json) 
                       ?? new Dictionary<string, List<SavedTableTabState>>();
            }
            catch
            {
                return new Dictionary<string, List<SavedTableTabState>>();
            }
        }

        private void SaveAllSavedTableTabs(Dictionary<string, List<SavedTableTabState>> savedTabs)
        {
            try
            {
                string json = JsonSerializer.Serialize(savedTabs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TableTabsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar tabletabs.json: {ex.Message}");
            }
        }

        public List<SavedTableTabState> LoadTableTabsForConnection(string connectionId, string databaseName)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(databaseName))
                return new List<SavedTableTabState>();

            string key = $"{connectionId}_{databaseName}";
            var all = LoadAllSavedTableTabs();
            if (all.TryGetValue(key, out var tabs))
            {
                return tabs;
            }
            return new List<SavedTableTabState>();
        }

        public void SaveTableTabsForConnection(string connectionId, string databaseName, List<SavedTableTabState> tabs)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(databaseName))
                return;

            string key = $"{connectionId}_{databaseName}";
            var all = LoadAllSavedTableTabs();
            all[key] = tabs;
            SaveAllSavedTableTabs(all);
        }

        private Dictionary<string, ServerViewState> LoadAllViewStates()
        {
            string filePath = ViewStateFilePath;
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, ServerViewState>();
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, ServerViewState>>(json) 
                       ?? new Dictionary<string, ServerViewState>();
            }
            catch
            {
                return new Dictionary<string, ServerViewState>();
            }
        }

        private void SaveAllViewStates(Dictionary<string, ServerViewState> viewStates)
        {
            try
            {
                string json = JsonSerializer.Serialize(viewStates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ViewStateFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al guardar viewstate.json: {ex.Message}");
            }
        }

        public ServerViewState LoadViewStateForConnection(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
                return new ServerViewState();

            var all = LoadAllViewStates();
            if (all.TryGetValue(connectionId, out var state))
            {
                return state;
            }
            return new ServerViewState();
        }

        public void SaveViewStateForConnection(string connectionId, ServerViewState state)
        {
            if (string.IsNullOrEmpty(connectionId))
                return;

            var all = LoadAllViewStates();
            all[connectionId] = state;
            SaveAllViewStates(all);
        }
    }
}
