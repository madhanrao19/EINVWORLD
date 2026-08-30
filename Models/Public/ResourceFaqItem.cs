namespace EINVWORLD.Models.Public
{
    /// <summary>One Q&amp;A pair for a resource whose SchemaType is FAQ. Serialized as JSON into
    /// ResourceItem.FaqItemsJson — see ResourceItem.FaqItems for the typed accessor.</summary>
    public class ResourceFaqItem
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }
}
