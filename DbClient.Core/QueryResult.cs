using System.Data;

namespace DbClient.Core
{
    /// <summary>
    /// Representa el resultado de la ejecución de una única consulta SQL dentro de un lote.
    /// Puede contener un conjunto de datos (tabla) o información sobre registros afectados.
    /// </summary>
    public class QueryResult
    {
        /// <summary>
        /// Datos resultantes para consultas tipo SELECT.
        /// Es nulo si la consulta no devuelve registros (ej. UPDATE, INSERT, DELETE).
        /// </summary>
        public DataTable DataTable { get; set; }

        /// <summary>
        /// Número de filas afectadas por la consulta.
        /// </summary>
        public int RecordsAffected { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado (ej. "25 filas afectadas").
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Indica si este resultado contiene una tabla de datos.
        /// </summary>
        public bool IsTable => DataTable != null;
    }
}
