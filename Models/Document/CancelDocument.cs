using Newtonsoft.Json;

namespace eInvWorld.Models.Document
{
    public class CancelDocument
    {
        public string status { get; set; } = null!;
        public string reason { get; set; } = null!;
        
        //[JsonIgnore]
        //public string docType { get; set; }
    }
}
