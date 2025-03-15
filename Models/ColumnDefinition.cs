using AdvancedCustomDataFiltering.Models;

namespace AdvancedCustomDataFiltering.Models
{
    public class ColumnDefinition
    {
        public string Header { get; set; }
        public Func<Person, object> DataBinding { get; set; }
        public string PropertyName { get; set; }
        public bool IsVisible { get; set; }
        public string? SortDirection { get; set; } = null;
        public bool Filtered { get; set; } = false;
        public List<ColumnFilterDefinition>? Filters { get; set; } = new List<ColumnFilterDefinition>();
        public Type DataType { get; set; }
    }
}
