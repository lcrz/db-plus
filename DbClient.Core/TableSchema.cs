using System.Collections.Generic;

namespace DbClient.Core
{
    public class TableSchema
    {
        public string TableName { get; set; }
        public string Description { get; set; }
        public List<ColumnSchema> Columns { get; set; } = new();
        public List<IndexSchema> Indexes { get; set; } = new();
        public List<ForeignKeySchema> ForeignKeys { get; set; } = new();
    }
}
