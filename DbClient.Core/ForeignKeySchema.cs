namespace DbClient.Core
{
    public class ForeignKeySchema
    {
        public string Name { get; set; }
        public string Columns { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumns { get; set; }
        public string OnUpdate { get; set; }
        public string OnDelete { get; set; }
    }
}
