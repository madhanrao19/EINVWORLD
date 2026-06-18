namespace eInvWorld.Models.Document
{
    public class ValidationStep
    {
        public string name { get; set; } = null!;

        public string status { get; set; } = null!;

        public ValidationError? error { get; set; }
    }
}
