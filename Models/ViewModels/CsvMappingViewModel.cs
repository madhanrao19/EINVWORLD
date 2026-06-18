// File: Models/ViewModels/CsvMappingViewModel.cs
namespace EINVWORLD.Models.ViewModels
{
    public class CsvMappingViewModel
    {
        public List<string> CsvHeaders { get; set; } = new();
        public Dictionary<string, string> FieldMap { get; set; } = new();

        public static readonly List<string> RequiredFields = new()
        {
            "InvoiceNo", "IssueDate", "Currency", "CustomerName", "CustomerTIN",
            "CustomerAddress", "ItemDescription", "Quantity", "UnitPrice"
        };
    }
}
