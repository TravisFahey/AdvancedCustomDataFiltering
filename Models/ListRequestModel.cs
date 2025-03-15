namespace AdvancedCustomDataFiltering.Models
{
	public class ListRequestModel
	{
		public int Page { get; set; } = 1; // The page number for the data we're requesting
		public int PageSize { get; set; } = 10; // The number of items per page
		public string Search { get; set; }
		public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
	}
}
