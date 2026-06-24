using System;
using System.Collections.Generic;

namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Modelo que representa el estado visual guardado de una conexión de servidor al desconectar/salir.
    /// </summary>
    public class ServerViewState
    {
        public string SelectedDatabase { get; set; } = string.Empty;
        public string TableSearchText { get; set; } = string.Empty;
        public List<string> ExpandedTables { get; set; } = new List<string>();
    }
}
