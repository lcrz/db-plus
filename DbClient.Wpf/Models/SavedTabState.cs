using System;

namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Modelo que representa el estado persistente de una pestaña de consulta SQL.
    /// </summary>
    public class SavedTabState
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string QueryText { get; set; }
        public bool IsActive { get; set; }
    }
}
