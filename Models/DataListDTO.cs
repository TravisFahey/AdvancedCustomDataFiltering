namespace AdvancedCustomDataFiltering.Models
{
	public class DataListDTO
	{
		public IEnumerable<Person> Items { get; set; }
		public int ItemTotalCount { get; set; } = 0;
	}
}
