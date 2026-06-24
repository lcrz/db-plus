namespace DbClient.Core
{
    public class IndexSchema
    {
        public string Name { get; set; }
        public string Columns { get; set; }
        public bool IsUnique { get; set; }
        public string Type { get; set; }
    }
}
