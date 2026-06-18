namespace eInvWorld.Models
{
    public class InnerErrorMsg
    {
        public string PropertyName { get; set; } = null!;

        public string PropertyPath { get; set; } = null!;

        public string ErrorCode { get; set; } = null!;

        public string Error { get; set; } = null!;

        public string ErrorMs { get; set; } = null!;

        public string MetaData { get; set; } = null!;

        public object InnerError { get; set; } = null!;
    }
}

