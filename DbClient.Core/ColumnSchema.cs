namespace DbClient.Core
{
    public class ColumnSchema
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public string DefaultValue { get; set; }
        public string Extra { get; set; }
    }
}
