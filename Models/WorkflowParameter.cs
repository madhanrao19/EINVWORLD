namespace eInvWorld.Models
{
    public class WorkflowParameter
    {
        public int id { get; set; }
        public string parameter { get; set; } = null!;
        public int value { get; set; }
        public DateTime? activeFrom { get; set; }
        public DateTime? activeTo { get; set; }
    }
}
