using System.Collections.Generic;
using System.Linq;
//using EINVWORLD.Models.JsonModels;

namespace EINVWORLD.Models
{
    public class DocumentSubmission
    {
        public Documents[] Documents { get; set; }

        public DocumentSubmission(Documents[] documents)
        {
            Documents = documents;
        }

        public Dictionary<string, object> ToMap()
        {
            return new Dictionary<string, object>
            {
                { "documents", Documents.Select(d => d.ToMap()).ToArray() }
            };
        }
    }
}
