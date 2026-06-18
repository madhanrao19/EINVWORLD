namespace eInvWorld.Models.Document
{
    public class DocumentSummary
    {
        public string uuid { get; set; } = null!;
        public string submissionUid { get; set; } = null!;
        public string longId { get; set; } = null!;
        public string internalId { get; set; } = null!;
        public string typeName { get; set; } = null!;
        public string typeVersionName { get; set; } = null!;
        public string issuerTin { get; set; } = null!;
        public string issuerName { get; set; } = null!;
        public string receiverTin { get; set; } = null!;
        public string receiverId { get; set; } = null!;
        public string receiverIdType { get; set; } = null!;
        public string receiverName { get; set; } = null!;
        public DateTime? dateTimeIssued { get; set; }
        public DateTime? dateTimeReceived { get; set; }
        public DateTime? dateTimeValidated { get; set; }
        public Decimal? totalExcludingTax { get; set; }
        public Decimal? totalSales { get; set; }
        public Decimal? totalDiscount { get; set; }
        public Decimal? totalNetAmount { get; set; }
        public Decimal? netAmount { get; set; }
        public Decimal? totalPayableAmount { get; set; }
        public Decimal? total { get; set; }
        public Decimal? totalOriginalDiscount { get; set; }
        public Decimal? totalOriginalSales { get; set; }
        public Decimal? netOriginalAmount { get; set; }
        public Decimal? totalOriginal { get; set; }
        public string status { get; set; } = null!;
        public DateTime? cancelDateTime { get; set; }
        public DateTime? rejectRequestDateTime { get; set; }
        public string documentStatusReason { get; set; } = null!;
        public string rejectedByUserId { get; set; } = null!; // reject by
        public string createdByUserId { get; set; } = null!;
        public string document { get; set; } = null!;
        public string supplierTIN { get; set; } = null!;
        public string supplierName { get; set; } = null!;
        public string submissionChannel { get; set; } = null!;
        public string intermediaryName { get; set; } = null!;
        public string intermediaryTIN { get; set; } = null!;
        public string buyerName { get; set; } = null!;
        public string buyerTIN { get; set; } = null!;

        // New properties for statuses
        public string lhdnStatus { get; set; } = null!; // Holds LHDN status (Valid, Invalid, etc.)
        public string internalStatus { get; set; } = null!; // Holds internal status (Draft, Request Reject, etc.)
    }
}
