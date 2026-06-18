using System;
using System.Text;

namespace eInvWorld.Models.Document
{
    public class SearchDocumentInput
    {
        public string uuid { get; set; } = null!;
        public DateTime? submissionDateFrom { get; set; }
        public DateTime? submissionDateTo { get; set; }
        public int? pageSize { get; set; }
        public int? pageNo { get; set; }
        public DateTime? issueDateFrom { get; set; }
        public DateTime? issueDateTo { get; set; }
        public string invoiceDirection { get; set; } = null!;  // Sent, Received
        public string status { get; set; } = null!;
        public string documentType { get; set; } = null!;


        // ✅ Issuer and Receiver Filters
        public string issuerTin { get; set; } = null!;  // Supplier TIN
        public string receiverTin { get; set; } = null!;  // Buyer TIN
        public string receiverId { get; set; } = null!; // Receiver ID (NRIC, Passport, etc.)
        public string receiverIdType { get; set; } = null!; // Receiver ID Type (NRIC, BRN, Passport, etc.)

        public string searchQuery { get; set; } = null!;  // uuid,buyerTIN,supplierTIN,buyerName,supplierName,internalID,total

        public string sortBy { get; set; } = null!; // Sorting column (e.g., InvoiceNo, IssueDate, TotalAmount)
        public string sortOrder { get; set; } = null!; // "asc" or "desc"


        // Add properties for status
        public string lhdnStatus { get; set; } = null!; // Holds LHDN status (Valid, Invalid, etc.)
        public string internalStatus { get; set; } = null!; // Holds internal status (Draft, Request Reject, etc.)

        public string GetQueryString()
        {
            var queryParams = new StringBuilder("?");

            if (!string.IsNullOrEmpty(uuid))
                queryParams.Append($"uuid={uuid}&");

            if (submissionDateFrom.HasValue)
                queryParams.Append($"submissionDateFrom={submissionDateFrom.Value:yyyy-MM-ddTHH:mm:ssZ}&");

            if (submissionDateTo.HasValue)
                queryParams.Append($"submissionDateTo={submissionDateTo.Value:yyyy-MM-ddTHH:mm:ssZ}&");

            if (issueDateFrom.HasValue)
                queryParams.Append($"issueDateFrom={issueDateFrom.Value:yyyy-MM-ddTHH:mm:ssZ}&");

            if (issueDateTo.HasValue)
                queryParams.Append($"issueDateTo={issueDateTo.Value:yyyy-MM-ddTHH:mm:ssZ}&");

            if (pageSize.HasValue)
                queryParams.Append($"pageSize={pageSize}&");

            if (pageNo.HasValue)
                queryParams.Append($"pageNo={pageNo}&");

            if (!string.IsNullOrEmpty(invoiceDirection))
                queryParams.Append($"invoiceDirection={invoiceDirection}&");

            if (!string.IsNullOrEmpty(status))
                queryParams.Append($"status={status}&");

            if (!string.IsNullOrEmpty(lhdnStatus))
                queryParams.Append($"lhdnStatus={lhdnStatus}&");

            if (!string.IsNullOrEmpty(internalStatus))
                queryParams.Append($"internalStatus={internalStatus}&");

            if (!string.IsNullOrEmpty(documentType))
                queryParams.Append($"documentType={documentType}&");

            if (!string.IsNullOrEmpty(issuerTin))
                queryParams.Append($"issuerTin={issuerTin}&");

            if (!string.IsNullOrEmpty(receiverTin))
                queryParams.Append($"receiverTin={receiverTin}&");

            if (!string.IsNullOrEmpty(receiverId))
                queryParams.Append($"receiverId={receiverId}&");

            if (!string.IsNullOrEmpty(receiverIdType))
                queryParams.Append($"receiverIdType={receiverIdType}&");

            if (!string.IsNullOrEmpty(searchQuery))
                queryParams.Append($"searchQuery={searchQuery}&");
            
            if (!string.IsNullOrEmpty(sortBy))
                queryParams.Append($"sortBy={sortBy}&");

            if (!string.IsNullOrEmpty(sortOrder))
                queryParams.Append($"sortOrder={sortOrder}&");

            if (queryParams.Length > 1)
                queryParams.Length--; // Remove trailing "&"

            return queryParams.ToString();
        }
    }
}
