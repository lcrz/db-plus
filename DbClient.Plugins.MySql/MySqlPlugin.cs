using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DbClient.Core;
using MySqlConnector;

using System.Threading;

namespace DbClient.Plugins.MySql
{
    /// <summary>
    /// Implementación concreta de IDatabasePlugin para bases de datos MySQL utilizando MySqlConnector.
    /// Esta clase está aislada dentro de su propio proyecto de biblioteca para cumplir con las reglas de arquitectura.
    /// </summary>
    public class MySqlPlugin : IDatabasePlugin
    {
        private MySqlConnection _connection;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string PluginName => "MySQL";

        public async Task ConnectAsync(string connectionString)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_connection != null)
                {
                    await DisconnectInternalAsync();
                }

                _connection = new MySqlConnection(connectionString);
                await _connection.OpenAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                await DisconnectInternalAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task DisconnectInternalAsync()
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await _connection.CloseAsync();
                }
                await _connection.DisposeAsync();
                _connection = null;
            }
        }

        public async Task<List<string>> GetDatabasesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                var databases = new List<string>();

                using (var command = new MySqlCommand("SHOW DATABASES;", _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
                return databases;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<string>> GetTablesAsync(string databaseName)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();

                // Cambiar de base de datos activa si es diferente
                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                var tables = new List<string>();
                using (var command = new MySqlCommand("SHOW TABLES;", _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
                return tables;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<string>> GetColumnsAsync(string tableName)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                var columns = new List<string>();

                // Query para obtener las columnas de la tabla indicada
                using (var command = new MySqlCommand($"SHOW COLUMNS FROM `{tableName}`;", _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // El nombre del campo está en la primera columna del resultado de SHOW COLUMNS
                        columns.Add(reader.GetString(0));
                    }
                }
                return columns;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<DataTable> ExecuteQueryAsync(string query)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                var dataTable = new DataTable();

                using (var command = new MySqlCommand(query, _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Carga los datos leídos del flujo asíncrono
                    dataTable.Load(reader);
                }
                return dataTable;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<QueryResult>> ExecuteScriptAsync(string query)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                var results = new List<QueryResult>();

                using (var command = new MySqlCommand(query, _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    do
                    {
                        if (reader.IsClosed)
                            break;

                        var result = new QueryResult();
                        if (reader.FieldCount > 0)
                        {
                            var dataTable = new DataTable();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string colName = reader.GetName(i);
                                if (string.IsNullOrEmpty(colName)) colName = $"Column{i}";

                                string uniqueColName = colName;
                                int suffix = 1;
                                while (dataTable.Columns.Contains(uniqueColName))
                                {
                                    uniqueColName = $"{colName}{suffix}";
                                    suffix++;
                                }

                                Type colType = reader.GetFieldType(i) ?? typeof(object);
                                dataTable.Columns.Add(uniqueColName, colType);
                            }

                            while (await reader.ReadAsync())
                            {
                                var row = dataTable.NewRow();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[i] = reader.GetValue(i);
                                }
                                dataTable.Rows.Add(row);
                            }

                            result.DataTable = dataTable;
                            result.RecordsAffected = dataTable.Rows.Count;
                        }
                        else
                        {
                            result.RecordsAffected = reader.RecordsAffected;
                            result.Message = $"Consulta ejecutada correctamente. Filas afectadas: {reader.RecordsAffected}.";
                        }
                        results.Add(result);

                    } while (await reader.NextResultAsync());
                }
                return results;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<TableSchema> GetTableSchemaAsync(string databaseName, string tableName)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();

                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                var schema = new TableSchema
                {
                    TableName = tableName,
                    Columns = new List<ColumnSchema>()
                };

                // 1. Get Table Description/Comment
                string commentQuery = "SELECT TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tbl LIMIT 1;";
                using (var cmd = new MySqlCommand(commentQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    cmd.Parameters.AddWithValue("@tbl", tableName);
                    var result = await cmd.ExecuteScalarAsync();
                    schema.Description = result != null && result != DBNull.Value ? result.ToString() : string.Empty;
                }

                // 2. Get Columns
                string columnsQuery = $"SHOW COLUMNS FROM `{tableName}`;";
                using (var cmd = new MySqlCommand(columnsQuery, _connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var col = new ColumnSchema
                        {
                            Name = reader.GetString(reader.GetOrdinal("Field")),
                            Type = reader.GetString(reader.GetOrdinal("Type")),
                            IsNullable = string.Equals(reader.GetString(reader.GetOrdinal("Null")), "YES", StringComparison.OrdinalIgnoreCase),
                            IsPrimaryKey = string.Equals(reader.GetString(reader.GetOrdinal("Key")), "PRI", StringComparison.OrdinalIgnoreCase),
                            DefaultValue = reader.IsDBNull(reader.GetOrdinal("Default")) ? null : reader.GetValue(reader.GetOrdinal("Default")).ToString(),
                            Extra = reader.IsDBNull(reader.GetOrdinal("Extra")) ? string.Empty : reader.GetString(reader.GetOrdinal("Extra"))
                        };
                        schema.Columns.Add(col);
                    }
                }

                // 3. Get Indexes
                schema.Indexes = new List<IndexSchema>();
                var indexMap = new Dictionary<string, IndexSchema>();
                using (var cmd = new MySqlCommand($"SHOW INDEX FROM `{tableName}`;", _connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string keyName = reader.GetString(reader.GetOrdinal("Key_name"));
                        string columnName = reader.GetString(reader.GetOrdinal("Column_name"));
                        bool nonUnique = reader.GetInt32(reader.GetOrdinal("Non_unique")) != 0;
                        string indexType = reader.GetString(reader.GetOrdinal("Index_type"));

                        if (indexMap.TryGetValue(keyName, out var existingIndex))
                        {
                            existingIndex.Columns += $", {columnName}";
                        }
                        else
                        {
                            var idx = new IndexSchema
                            {
                                Name = keyName,
                                Columns = columnName,
                                IsUnique = !nonUnique,
                                Type = indexType
                            };
                            indexMap[keyName] = idx;
                            schema.Indexes.Add(idx);
                        }
                    }
                }

                // 4. Get Foreign Keys
                schema.ForeignKeys = new List<ForeignKeySchema>();
                var fkMap = new Dictionary<string, ForeignKeySchema>();
                string fkQuery = @"
                    SELECT 
                        k.CONSTRAINT_NAME,
                        k.COLUMN_NAME,
                        k.REFERENCED_TABLE_NAME,
                        k.REFERENCED_COLUMN_NAME,
                        r.UPDATE_RULE,
                        r.DELETE_RULE
                    FROM 
                        INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
                    JOIN 
                        INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS r 
                        ON k.CONSTRAINT_NAME = r.CONSTRAINT_NAME 
                        AND k.CONSTRAINT_SCHEMA = r.CONSTRAINT_SCHEMA
                    WHERE 
                        k.TABLE_SCHEMA = @db 
                        AND k.TABLE_NAME = @tbl
                        AND k.REFERENCED_TABLE_NAME IS NOT NULL
                    ORDER BY
                        k.CONSTRAINT_NAME, k.ORDINAL_POSITION;";

                using (var cmd = new MySqlCommand(fkQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    cmd.Parameters.AddWithValue("@tbl", tableName);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string constraintName = reader.GetString(reader.GetOrdinal("CONSTRAINT_NAME"));
                            string columnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
                            string refTableName = reader.GetString(reader.GetOrdinal("REFERENCED_TABLE_NAME"));
                            string refColumnName = reader.GetString(reader.GetOrdinal("REFERENCED_COLUMN_NAME"));
                            string updateRule = reader.GetString(reader.GetOrdinal("UPDATE_RULE"));
                            string deleteRule = reader.GetString(reader.GetOrdinal("DELETE_RULE"));

                            if (fkMap.TryGetValue(constraintName, out var existingFk))
                            {
                                existingFk.Columns += $", {columnName}";
                                existingFk.ReferencedColumns += $", {refColumnName}";
                            }
                            else
                            {
                                var fk = new ForeignKeySchema
                                {
                                    Name = constraintName,
                                    Columns = columnName,
                                    ReferencedTable = refTableName,
                                    ReferencedColumns = refColumnName,
                                    OnUpdate = updateRule,
                                    OnDelete = deleteRule
                                };
                                fkMap[constraintName] = fk;
                                schema.ForeignKeys.Add(fk);
                            }
                        }
                    }
                }

                return schema;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<TableSchema>> GetTableSchemasAsync(string databaseName, List<string> tableNames)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();

                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                if (tableNames == null || tableNames.Count == 0)
                {
                    return new List<TableSchema>();
                }

                var targetTableSet = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
                var schemasMap = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);

                foreach (var tableName in tableNames)
                {
                    schemasMap[tableName] = new TableSchema
                    {
                        TableName = tableName,
                        Description = string.Empty,
                        Columns = new List<ColumnSchema>(),
                        Indexes = new List<IndexSchema>(),
                        ForeignKeys = new List<ForeignKeySchema>()
                    };
                }

                // 1. Get Table Descriptions
                string commentQuery = "SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @db;";
                using (var cmd = new MySqlCommand(commentQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string tableName = reader.GetString(0);
                            if (schemasMap.TryGetValue(tableName, out var schema))
                            {
                                schema.Description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            }
                        }
                    }
                }

                // 2. Get Columns
                string columnsQuery = @"
                    SELECT 
                        TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY, COLUMN_DEFAULT, EXTRA 
                    FROM 
                        INFORMATION_SCHEMA.COLUMNS 
                    WHERE 
                        TABLE_SCHEMA = @db
                    ORDER BY 
                        TABLE_NAME, ORDINAL_POSITION;";
                using (var cmd = new MySqlCommand(columnsQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string tableName = reader.GetString(0);
                            if (schemasMap.TryGetValue(tableName, out var schema))
                            {
                                var col = new ColumnSchema
                                {
                                    Name = reader.GetString(1),
                                    Type = reader.GetString(2),
                                    IsNullable = string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase),
                                    IsPrimaryKey = string.Equals(reader.GetString(4), "PRI", StringComparison.OrdinalIgnoreCase),
                                    DefaultValue = reader.IsDBNull(5) ? null : reader.GetValue(5).ToString(),
                                    Extra = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                                };
                                schema.Columns.Add(col);
                            }
                        }
                    }
                }

                // 3. Get Indexes
                var indexLookup = new Dictionary<(string, string), IndexSchema>();
                string indexQuery = @"
                    SELECT 
                        TABLE_NAME, INDEX_NAME, COLUMN_NAME, NON_UNIQUE, INDEX_TYPE
                    FROM 
                        INFORMATION_SCHEMA.STATISTICS
                    WHERE 
                        TABLE_SCHEMA = @db
                    ORDER BY 
                        TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;";
                using (var cmd = new MySqlCommand(indexQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string tableName = reader.GetString(0);
                            if (schemasMap.TryGetValue(tableName, out var schema))
                            {
                                string indexName = reader.GetString(1);
                                string columnName = reader.GetString(2);
                                bool nonUnique = reader.GetInt32(3) != 0;
                                string indexType = reader.GetString(4);

                                var lookupKey = (tableName, indexName);
                                if (indexLookup.TryGetValue(lookupKey, out var existingIndex))
                                {
                                    existingIndex.Columns += $", {columnName}";
                                }
                                else
                                {
                                    var idx = new IndexSchema
                                    {
                                        Name = indexName,
                                        Columns = columnName,
                                        IsUnique = !nonUnique,
                                        Type = indexType
                                    };
                                    indexLookup[lookupKey] = idx;
                                    schema.Indexes.Add(idx);
                                }
                            }
                        }
                    }
                }

                // 4. Get Foreign Keys
                var fkLookup = new Dictionary<(string, string), ForeignKeySchema>();
                string fkQuery = @"
                    SELECT 
                        k.TABLE_NAME,
                        k.CONSTRAINT_NAME,
                        k.COLUMN_NAME,
                        k.REFERENCED_TABLE_NAME,
                        k.REFERENCED_COLUMN_NAME,
                        r.UPDATE_RULE,
                        r.DELETE_RULE
                    FROM 
                        INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
                    JOIN 
                        INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS r 
                        ON k.CONSTRAINT_NAME = r.CONSTRAINT_NAME 
                        AND k.CONSTRAINT_SCHEMA = r.CONSTRAINT_SCHEMA
                    WHERE 
                        k.CONSTRAINT_SCHEMA = @db 
                        AND k.REFERENCED_TABLE_NAME IS NOT NULL
                    ORDER BY
                        k.TABLE_NAME, k.CONSTRAINT_NAME, k.ORDINAL_POSITION;";
                using (var cmd = new MySqlCommand(fkQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@db", databaseName);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string tableName = reader.GetString(0);
                            string refTableName = reader.GetString(3);
                            if (schemasMap.TryGetValue(tableName, out var schema) && targetTableSet.Contains(refTableName))
                            {
                                string constraintName = reader.GetString(1);
                                string columnName = reader.GetString(2);
                                string refColumnName = reader.GetString(4);
                                string updateRule = reader.GetString(5);
                                string deleteRule = reader.GetString(6);

                                var lookupKey = (tableName, constraintName);
                                if (fkLookup.TryGetValue(lookupKey, out var existingFk))
                                {
                                    existingFk.Columns += $", {columnName}";
                                    existingFk.ReferencedColumns += $", {refColumnName}";
                                }
                                else
                                {
                                    var fk = new ForeignKeySchema
                                    {
                                        Name = constraintName,
                                        Columns = columnName,
                                        ReferencedTable = refTableName,
                                        ReferencedColumns = refColumnName,
                                        OnUpdate = updateRule,
                                        OnDelete = deleteRule
                                    };
                                    fkLookup[lookupKey] = fk;
                                    schema.ForeignKeys.Add(fk);
                                }
                            }
                        }
                    }
                }

                var resultList = new List<TableSchema>();
                foreach (var tableName in tableNames)
                {
                    if (schemasMap.TryGetValue(tableName, out var schema))
                    {
                        resultList.Add(schema);
                    }
                }
                return resultList;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AlterTableAsync(string databaseName, string tableName, TableAlteration alteration)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();

                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                var ddlStatements = new List<string>();

                // 1. Process column alterations
                foreach (var alt in alteration.ColumnAlterations)
                {
                    if (alt.Type == AlterationType.Drop)
                    {
                        ddlStatements.Add($"ALTER TABLE `{databaseName}`.`{tableName}` DROP COLUMN `{alt.OriginalName}`;");
                    }
                    else if (alt.Type == AlterationType.Add)
                    {
                        ddlStatements.Add($"ALTER TABLE `{databaseName}`.`{tableName}` ADD COLUMN `{alt.Column.Name}` {alt.Column.Type} {(alt.Column.IsNullable ? "NULL" : "NOT NULL")}{FormatDefaultValue(alt.Column.DefaultValue, alt.Column.Type)};");
                    }
                    else if (alt.Type == AlterationType.Modify)
                    {
                        bool nameChanged = alt.Column.Name != alt.OriginalName;
                        if (nameChanged)
                        {
                            ddlStatements.Add($"ALTER TABLE `{databaseName}`.`{tableName}` CHANGE COLUMN `{alt.OriginalName}` `{alt.Column.Name}` {alt.Column.Type} {(alt.Column.IsNullable ? "NULL" : "NOT NULL")}{FormatDefaultValue(alt.Column.DefaultValue, alt.Column.Type)};");
                        }
                        else
                        {
                            ddlStatements.Add($"ALTER TABLE `{databaseName}`.`{tableName}` MODIFY COLUMN `{alt.Column.Name}` {alt.Column.Type} {(alt.Column.IsNullable ? "NULL" : "NOT NULL")}{FormatDefaultValue(alt.Column.DefaultValue, alt.Column.Type)};");
                        }
                    }
                }

                // 2. Process table comment alteration
                if (alteration.OriginalDescription != alteration.NewDescription)
                {
                    string safeComment = alteration.NewDescription?.Replace("'", "''") ?? string.Empty;
                    ddlStatements.Add($"ALTER TABLE `{databaseName}`.`{tableName}` COMMENT = '{safeComment}';");
                }

                // Execute DDL statements sequentially
                foreach (var sql in ddlStatements)
                {
                    using (var cmd = new MySqlCommand(sql, _connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DropIndexAsync(string databaseName, string tableName, string indexName)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                string sql;
                if (string.Equals(indexName, "PRIMARY", StringComparison.OrdinalIgnoreCase))
                {
                    sql = $"ALTER TABLE `{databaseName}`.`{tableName}` DROP PRIMARY KEY;";
                }
                else
                {
                    sql = $"ALTER TABLE `{databaseName}`.`{tableName}` DROP INDEX `{indexName}`;";
                }

                using (var cmd = new MySqlCommand(sql, _connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DropForeignKeyAsync(string databaseName, string tableName, string constraintName)
        {
            await _semaphore.WaitAsync();
            try
            {
                EnsureConnected();
                if (!string.Equals(_connection.Database, databaseName, StringComparison.OrdinalIgnoreCase))
                {
                    await _connection.ChangeDatabaseAsync(databaseName);
                }

                string sql = $"ALTER TABLE `{databaseName}`.`{tableName}` DROP FOREIGN KEY `{constraintName}`;";
                using (var cmd = new MySqlCommand(sql, _connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string FormatDefaultValue(string defaultValue, string type)
        {
            if (string.IsNullOrWhiteSpace(defaultValue))
                return "";

            string trimmed = defaultValue.Trim();
            if (string.Equals(trimmed, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return " DEFAULT NULL";
            }

            if (string.Equals(trimmed, "CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "NOW()", StringComparison.OrdinalIgnoreCase))
            {
                return $" DEFAULT {trimmed}";
            }

            // If it is already quoted (e.g. starting and ending with '), don't add quotes
            if (trimmed.StartsWith("'") && trimmed.EndsWith("'"))
            {
                return $" DEFAULT {trimmed}";
            }

            // Otherwise, check if it's a numeric value (int, double, decimal, float)
            if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return $" DEFAULT {trimmed}";
            }

            // For any other value, escape single quotes and wrap in single quotes
            string escaped = trimmed.Replace("'", "''");
            return $" DEFAULT '{escaped}'";
        }

        private void EnsureConnected()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("No se ha establecido una conexión activa con el servidor MySQL.");
            }
        }
    }
}
