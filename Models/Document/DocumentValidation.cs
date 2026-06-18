namespace eInvWorld.Models.Document
{
    public class DocumentValidation
    {
        public string uuid { get; set; } = null!;

        public string submissionUid { get; set; } = null!;

        public string longId { get; set; } = null!;

        public string typeName { get; set; } = null!;

        public string typeVersionName { get; set; } = null!;

        public string issuerTin { get; set; } = null!;

        public string issuerName { get; set; } = null!;

        public string receiverId { get; set; } = null!;

        public string receiverName { get; set; } = null!;

        public DateTime? dateTimeIssued { get; set; }

        public DateTime? dateTimeReceived { get; set; }

        public DateTime? dateTimeValidated { get; set; }

        public Decimal? totalSales { get; set; }

        public Decimal? totalDiscount { get; set; }

        public Decimal? netAmount { get; set; }

        public Decimal? total { get; set; }

        public string status { get; set; } = null!;

        public string createdByUserId { get; set; } = null!;

        public string documentStatusReason { get; set; } = null!;

        public DateTime? cancelDateTime { get; set; }

        public DateTime? rejectRequestDateTime { get; set; }

        public ValidationResults validationResults { get; set; } = new();
    }
}
