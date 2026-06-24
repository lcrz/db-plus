using System.Data;

namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que representa un único tab de resultado para una consulta SQL ejecutada.
    /// </summary>
    public class QueryResultViewModel : BaseViewModel
    {
        private string _title;
        private DataTable _dataTable;
        private string _message;

        /// <summary>
        /// Título de la pestaña de resultado (ej. "Resultado 1", "Mensaje 1").
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Conjunto de datos para consultas SELECT.
        /// </summary>
        public DataTable DataTable
        {
            get => _dataTable;
            set
            {
                if (SetProperty(ref _dataTable, value))
                {
                    OnPropertyChanged(nameof(IsTable));
                }
            }
        }

        /// <summary>
        /// Mensaje informativo para consultas DML o de estado.
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Indica si este resultado contiene un conjunto de datos (tabla) para mostrar en un DataGrid.
        /// </summary>
        public bool IsTable => DataTable != null;
    }
}
