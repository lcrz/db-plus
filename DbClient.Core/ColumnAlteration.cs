namespace DbClient.Core
{
    public enum AlterationType
    {
        Add,
        Drop,
        Modify
    }

    public class ColumnAlteration
    {
        public AlterationType Type { get; set; }
        public string OriginalName { get; set; }
        public ColumnSchema Column { get; set; }
    }
}
