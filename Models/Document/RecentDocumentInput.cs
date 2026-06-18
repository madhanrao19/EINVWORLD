using eInvWorld.Models.JsonModels;

namespace eInvWorld.Models.Document
{
    public class RecentDocumentInput
    {
        public int? pageNo { get; set; }
        public int? pageSize { get; set; }
        public DateTime? submissionDateFrom { get; set; }
        public DateTime? submissionDateTo { get; set; }
        public DateTime? issueDateFrom { get; set; }
        public DateTime? issueDateTo { get; set; }
        public string direction { get; set; } = null!;
        public string status { get; set; } = null!;
        public string documentType { get; set; } = null!;
        public string receiverId { get; set; } = null!;
        public string receiverIdType { get; set; } = null!;
        public string receiverTin { get; set; } = null!;
        public string issuerTin { get; set; } = null!;

        //public string getQueryString()
        //{
        //    string queryString = "";
        //    if (this.pageNo.HasValue)
        //        queryString = queryString + "pageNo=" + this.pageNo.Value.ToString() + "&";
        //    if (this.pageSize.HasValue)
        //        queryString = queryString + "pageNo=" + this.pageSize.Value.ToString() + "&";
        //    if (this.submissionDateFrom.HasValue)
        //    {
        //        string jsonDateTime = JsonDateTimeUtil.getJsonDateTime(this.submissionDateFrom.Value);
        //        queryString = queryString + "submissionDateFrom=" + jsonDateTime + "&";
        //    }
        //    if (this.submissionDateTo.HasValue)
        //    {
        //        string jsonDateTime = JsonDateTimeUtil.getJsonDateTime(this.submissionDateTo.Value);
        //        queryString = queryString + "submissionDateTo=" + jsonDateTime + "&";
        //    }
        //    if (this.issueDateFrom.HasValue)
        //    {
        //        string jsonDateTime = JsonDateTimeUtil.getJsonDateTime(this.issueDateFrom.Value);
        //        queryString = queryString + "issueDateFrom=" + jsonDateTime + "&";
        //    }
        //    if (this.issueDateTo.HasValue)
        //    {
        //        string jsonDateTime = JsonDateTimeUtil.getJsonDateTime(this.issueDateTo.Value);
        //        queryString = queryString + "issueDateTo=" + jsonDateTime + "&";
        //    }
        //    if (!string.IsNullOrEmpty(this.direction))
        //        queryString = queryString + "direction=" + this.direction + "&";
        //    if (!string.IsNullOrEmpty(this.status))
        //        queryString = queryString + "status=" + this.status + "&";
        //    if (!string.IsNullOrEmpty(this.documentType))
        //        queryString = queryString + "documentType=" + this.documentType + "&";
        //    if (!string.IsNullOrEmpty(this.receiverId))
        //        queryString = queryString + "receiverId=" + this.receiverId + "&";
        //    if (!string.IsNullOrEmpty(this.receiverIdType))
        //        queryString = queryString + "receiverIdType=" + this.receiverIdType + "&";
        //    if (!string.IsNullOrEmpty(this.receiverTin))
        //        queryString = queryString + "receiverTin=" + this.receiverTin + "&";
        //    if (!string.IsNullOrEmpty(this.issuerTin))
        //        queryString = queryString + "issuerTin=" + this.issuerTin + "&";
        //    if (queryString.Length > 0)
        //        queryString = queryString.Substring(0, queryString.Length - 1);
        //    return queryString;
        //}
    }
}
