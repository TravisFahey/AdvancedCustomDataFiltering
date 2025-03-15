namespace AdvancedCustomDataFiltering.Models
{
	public class FilterDefinition
	{
		public string Column { get; set; }
		public string? Value { get; set; }
		public string? Operator { get; set; } // Optional, based on your filtering needs
	}
}
