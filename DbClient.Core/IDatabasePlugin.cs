using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DbClient.Core
{
    /// <summary>
    /// Contrato obligatorio para todas las implementaciones de plugins de bases de datos.
    /// Define métodos asíncronos para interactuar con la base de datos de manera desacoplada.
    /// </summary>
    public interface IDatabasePlugin
    {
        /// <summary>
        /// Nombre del plugin (ej. "MySQL", "PostgreSQL", etc.)
        /// </summary>
        string PluginName { get; }

        /// <summary>
        /// Establece la conexión con la base de datos usando la cadena de conexión provista.
        /// </summary>
        Task ConnectAsync(string connectionString);

        /// <summary>
        /// Cierra la conexión activa con la base de datos.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Obtiene la lista de bases de datos disponibles.
        /// </summary>
        Task<List<string>> GetDatabasesAsync();

        /// <summary>
        /// Obtiene la lista de tablas dentro de la base de datos especificada.
        /// </summary>
        Task<List<string>> GetTablesAsync(string databaseName);

        /// <summary>
        /// Obtiene la lista de nombres de columnas para la tabla especificada.
        /// </summary>
        Task<List<string>> GetColumnsAsync(string tableName);

        /// <summary>
        /// Ejecuta una consulta SQL y devuelve los resultados en un DataTable.
        /// </summary>
        Task<DataTable> ExecuteQueryAsync(string query);

        /// <summary>
        /// Ejecuta un script SQL que puede contener múltiples consultas separadas por punto y coma,
        /// y devuelve la lista de resultados individuales de cada una.
        /// </summary>
        Task<List<QueryResult>> ExecuteScriptAsync(string query);

        /// <summary>
        /// Obtiene el esquema completo de una tabla, incluyendo columnas y comentario/descripción.
        /// </summary>
        Task<TableSchema> GetTableSchemaAsync(string databaseName, string tableName);

        /// <summary>
        /// Obtiene los esquemas completos de múltiples tablas en una sola operación optimizada.
        /// </summary>
        Task<List<TableSchema>> GetTableSchemasAsync(string databaseName, List<string> tableNames);

        /// <summary>
        /// Aplica las modificaciones estructurales en la base de datos para la tabla provista.
        /// </summary>
        Task AlterTableAsync(string databaseName, string tableName, TableAlteration alteration);

        /// <summary>
        /// Elimina un índice de la tabla especificada.
        /// </summary>
        Task DropIndexAsync(string databaseName, string tableName, string indexName);

        /// <summary>
        /// Elimina una llave foránea de la tabla especificada.
        /// </summary>
        Task DropForeignKeyAsync(string databaseName, string tableName, string constraintName);
    }
}
