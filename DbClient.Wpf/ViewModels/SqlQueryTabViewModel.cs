using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.ObjectModel;
using DbClient.Core;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que representa una pestaña individual de consulta SQL.
    /// Contiene su propio texto de consulta, resultados de ejecución y estado de carga.
    /// </summary>
    public class SqlQueryTabViewModel : BaseViewModel
    {
        private readonly IDatabasePlugin _databasePlugin;

        private Guid _id = Guid.NewGuid();
        private string _title = "Nueva Consulta";
        private string _queryText = "";
        private string _executionTimeMessage;
        private string _statusMessage = "Listo.";
        private bool _isBusy;

        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string QueryText
        {
            get => _queryText;
            set => SetProperty(ref _queryText, value);
        }

        private ObservableCollection<QueryResultViewModel> _queryResultsList = new();
        public ObservableCollection<QueryResultViewModel> QueryResultsList
        {
            get => _queryResultsList;
            set => SetProperty(ref _queryResultsList, value);
        }

        private QueryResultViewModel _selectedQueryResult;
        public QueryResultViewModel SelectedQueryResult
        {
            get => _selectedQueryResult;
            set => SetProperty(ref _selectedQueryResult, value);
        }

        public string ExecutionTimeMessage
        {
            get => _executionTimeMessage;
            set => SetProperty(ref _executionTimeMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public ICommand ExecuteQueryCommand { get; }

        public SqlQueryTabViewModel(IDatabasePlugin databasePlugin, string title = "Nueva Consulta", string queryText = "")
        {
            _databasePlugin = databasePlugin ?? throw new ArgumentNullException(nameof(databasePlugin));
            Title = title;
            QueryText = queryText;

            ExecuteQueryCommand = new RelayCommand<string>(async (query) => await ExecuteQueryAsync(query), (query) => !IsBusy && !string.IsNullOrWhiteSpace(query ?? QueryText));
        }

        public async Task ExecuteQueryAsync(string specificQuery = null)
        {
            IsBusy = true;
            StatusMessage = "Ejecutando consulta SQL...";
            ExecutionTimeMessage = string.Empty;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string queryToExecute = !string.IsNullOrWhiteSpace(specificQuery) ? specificQuery : QueryText;
                var results = await _databasePlugin.ExecuteScriptAsync(queryToExecute);
                stopwatch.Stop();

                QueryResultsList.Clear();
                int selectCount = 0;
                int nonSelectCount = 0;
                int totalRows = 0;

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    string title;
                    if (r.IsTable)
                    {
                        selectCount++;
                        title = $"Resultado {selectCount}";
                        totalRows += r.DataTable.Rows.Count;
                    }
                    else
                    {
                        nonSelectCount++;
                        title = $"Mensaje {nonSelectCount}";
                    }

                    QueryResultsList.Add(new QueryResultViewModel
                    {
                        Title = title,
                        DataTable = r.DataTable,
                        Message = r.Message,
                        ExecutionTimeMessage = $"Ejecutado en {stopwatch.Elapsed.TotalMilliseconds:N2} ms."
                    });
                }

                if (QueryResultsList.Count > 0)
                {
                    SelectedQueryResult = QueryResultsList[0];
                }

                ErrorMessage = null;
                HasError = false;

                if (selectCount > 0 && nonSelectCount > 0)
                {
                    ExecutionTimeMessage = $"Ejecutado correctamente en {stopwatch.Elapsed.TotalMilliseconds:N2} ms. Tablas: {selectCount} (registros: {totalRows}), Mensajes: {nonSelectCount}.";
                }
                else if (selectCount > 0)
                {
                    ExecutionTimeMessage = $"Ejecutado correctamente en {stopwatch.Elapsed.TotalMilliseconds:N2} ms. Registros: {totalRows}.";
                }
                else
                {
                    ExecutionTimeMessage = $"Ejecutado correctamente en {stopwatch.Elapsed.TotalMilliseconds:N2} ms.";
                }

                StatusMessage = "Listo.";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                QueryResultsList.Clear();
                ErrorMessage = ex.Message;
                HasError = true;
                StatusMessage = $"Error al ejecutar consulta: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
