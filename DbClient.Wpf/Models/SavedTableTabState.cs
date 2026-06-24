namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Modelo que representa el estado persistente de una pestaña de datos de tabla.
    /// </summary>
    public class SavedTableTabState
    {
        public string TableName { get; set; }
        public bool IsActive { get; set; }
    }
}
