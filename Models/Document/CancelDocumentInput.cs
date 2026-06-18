namespace eInvWorld.Models.Document
{
   public class CancelDocumentInput
    {
        public string Status { get; set; } = "Rejected";
        public string Reason { get; set; }
        public string DocType { get; set; } = "Invoice";

        // Constructor
        public CancelDocumentInput(string reason)
        {
            Reason = reason;
        }

        // Static method to get predefined list of reasons
        public static List<string> GetReasons()
        {
            return new List<string>
            {
                "Wrong supplier details",
                "Wrong buyer details",
                "Wrong invoice details",
                "Others"
            };
        }
    }
}
