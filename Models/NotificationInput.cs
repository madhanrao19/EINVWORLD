namespace eInvWorld.Models
{
    public class NotificationInput
    {
        public DateTime? dateFrom { get; set; }

        public DateTime? dateTo { get; set; }

        public string type { get; set; } = null!;

        public string language { get; set; } = null!;

        public string status { get; set; } = null!;

        public string channel { get; set; } = null!;

        public int? pageNo { get; set; }

        public int? pageSize { get; set; }

        public string getQueryString()
        {
            string queryString = "";
            //if (this.dateFrom.HasValue)
            //{
            //    string jsonDateWithHhmm = JsonDateTimeUtil.getJsonDate_WithHHmm(this.dateFrom.Value);
            //    queryString = queryString + "dateFrom=" + jsonDateWithHhmm + "&";
            //}
            //if (this.dateTo.HasValue)
            //{
            //    string jsonDateWithHhmm = JsonDateTimeUtil.getJsonDate_WithHHmm(this.dateTo.Value);
            //    queryString = queryString + "dateTo=" + jsonDateWithHhmm + "&";
            //}
            if (!string.IsNullOrEmpty(this.type))
                queryString = queryString + "type=" + this.type + "&";
            if (!string.IsNullOrEmpty(this.language))
                queryString = queryString + "language=" + this.language + "&";
            if (!string.IsNullOrEmpty(this.status))
                queryString = queryString + "status=" + this.status + "&";
            if (!string.IsNullOrEmpty(this.channel))
                queryString = queryString + "channel=" + this.channel + "&";
            if (this.pageNo.HasValue)
                queryString = queryString + "pageNo=" + this.pageNo.Value.ToString() + "&";
            if (this.pageSize.HasValue)
                queryString = queryString + "pageSize=" + this.pageSize.Value.ToString() + "&";
            if (queryString.Length > 0)
                queryString = queryString.Substring(0, queryString.Length - 1);
            return queryString;
        }
    }
}
