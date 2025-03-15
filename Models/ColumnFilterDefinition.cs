namespace AdvancedCustomDataFiltering.Models
{
    public class ColumnFilterDefinition
    {
        public string? Column {  get; set; }
        public string? AndOr { get; set; }
        public string? Value { get; set; }
        public DateTime? DateTimeValue { get; set; }
        public string? Operator { get; set; } // Optional, based on your filtering needs
    }
}
