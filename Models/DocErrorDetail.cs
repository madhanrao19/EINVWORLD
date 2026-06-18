namespace eInvWorld.Models
{
    public class DocErrorDetail
    {
        public string PropertyName { get; set; } = null!;
        public string PropertyPath { get; set; } = null!;
        public string ErrorCode { get; set; } = null!;
        public string Error { get; set; } = null!;
        public string ErrorMs { get; set; } = null!;
        public string MetaData { get; set; } = null!;
        public List<InnerErrorMsg> InnerError { get; set; } = null!;
    }
}
