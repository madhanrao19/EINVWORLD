namespace eInvWorld.Models.JsonModels
{
    public class Documents
    {

        public string Format { get; set; } = "JSON";
        public string DocumentHash { get; set; } = null!;
        public string CodeNumber { get; set; } = null!;
        public string Document { get; set; } = null!;

        public Documents(string format, string documentHash, string codeNumber, string document)
        {
            Format = format;  //JSON
            DocumentHash = documentHash;   // Uses SHA256 to create the hash
            CodeNumber = codeNumber;  //invoice number
            Document = document;  // Encode THE json to Base64 format
        }

        public Documents()
        {
        }

        public Dictionary<string, object> ToMap()
        {
            var map = new Dictionary<string, object>
            {
                { "format", Format },
                { "documentHash", DocumentHash },
                { "codeNumber", CodeNumber },
                { "document", Document }
            };

            return map;
        }
    }
}
