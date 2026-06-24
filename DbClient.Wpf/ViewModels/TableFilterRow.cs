namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// Representa una fila de filtrado visual.
    /// Contiene el operador lógico, la columna a filtrar, el comparador y el valor de entrada.
    /// </summary>
    public class TableFilterRow : BaseViewModel
    {
        private string _logicOperator = "AND";
        private string _columnName;
        private string _operator = "equals";
        private string _value = string.Empty;
        private bool _isLogicOperatorVisible = true;

        public string LogicOperator
        {
            get => _logicOperator;
            set => SetProperty(ref _logicOperator, value);
        }

        public string ColumnName
        {
            get => _columnName;
            set => SetProperty(ref _columnName, value);
        }

        public string Operator
        {
            get => _operator;
            set => SetProperty(ref _operator, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public bool IsLogicOperatorVisible
        {
            get => _isLogicOperatorVisible;
            set => SetProperty(ref _isLogicOperatorVisible, value);
        }
    }
}
