namespace DbClient.Wpf.ViewModels
{
    /// <summary>
    /// ViewModel que representa un campo/columna de una tabla para su edición visual.
    /// Contiene propiedades de seguimiento para determinar qué columnas fueron modificadas, añadidas o eliminadas.
    /// </summary>
    public class ColumnStructureViewModel : BaseViewModel
    {
        private string _field;
        private string _type = "varchar(50)";
        private bool _isNullable = true;
        private bool _isPrimaryKey;
        private string _defaultVal;
        private string _extra;

        public string Field
        {
            get => _field;
            set => SetProperty(ref _field, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public bool IsNullable
        {
            get => _isNullable;
            set => SetProperty(ref _isNullable, value);
        }

        public bool IsPrimaryKey
        {
            get => _isPrimaryKey;
            set => SetProperty(ref _isPrimaryKey, value);
        }

        public string DefaultVal
        {
            get => _defaultVal;
            set => SetProperty(ref _defaultVal, value);
        }

        public string Extra
        {
            get => _extra;
            set => SetProperty(ref _extra, value);
        }

        // Propiedades de seguimiento para generar la consulta ALTER TABLE
        public string OriginalField { get; set; }
        public string OriginalType { get; set; }
        public bool OriginalIsNullable { get; set; }
        public string OriginalDefaultVal { get; set; }
        public bool IsNew { get; set; }
        public bool IsDeleted { get; set; }
    }
}
