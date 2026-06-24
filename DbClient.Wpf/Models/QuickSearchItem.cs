namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Representa un elemento en los resultados de la búsqueda rápida (Ctrl + P).
    /// </summary>
    public class QuickSearchItem
    {
        public string Name { get; set; }
        public string Type { get; set; } // "Table" o "Query"
        public string DisplayType => Type == "Table" ? "📊 Tabla" : "📄 Consulta Guardada";
        public object Data { get; set; } // DatabaseTreeItem para tablas, string para consultas
    }
}
