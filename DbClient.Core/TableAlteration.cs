using System.Collections.Generic;

namespace DbClient.Core
{
    public class TableAlteration
    {
        public string OriginalDescription { get; set; }
        public string NewDescription { get; set; }
        public List<ColumnAlteration> ColumnAlterations { get; set; } = new();
    }
}
