namespace eInvWorld.Models.Document
{
    public class ValidationResults
    {
        public string status { get; set; } = null!;
        public List<ValidationStep> validationSteps { get; set; } = new();
    }
}
