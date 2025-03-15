namespace AdvancedCustomDataFiltering.Utilities
{
    public class CsvHelper
    {
        public static string MakeCsvSafe(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            // Escape double quotes by doubling them
            string escapedField = field.Replace("\"", "\"\"");

            // Enclose the field in double quotes if it contains a special character (comma, double quote, newline, carriage return)
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                escapedField = $"\"{escapedField}\"";
            }

            return escapedField;
        }
    }
}
