using EINVWORLD.Models.Public;

namespace EINVWORLD.Helpers
{
    public class ResourceSeoChecklistItem
    {
        public string Label { get; set; } = "";
        public bool Passed { get; set; }
    }

    public class ResourceSeoScoreResult
    {
        /// <summary>0-100, rounded percentage of checklist items passing.</summary>
        public int Score { get; set; }
        public List<ResourceSeoChecklistItem> Checklist { get; set; } = new();
    }

    /// <summary>
    /// Single source of truth for the SEO/GEO readiness score shown on the Manage Resources list
    /// (score chip) and the Create/Edit form (live gauge + checklist). The Create/Edit page mirrors
    /// this exact checklist in a small vanilla-JS function (see einvworld-resources.js) so the score
    /// updates live on keystroke without a round trip — keep the two in sync if this list changes.
    /// </summary>
    public static class ResourceSeoScorer
    {
        public static ResourceSeoScoreResult Compute(ResourceItem resource)
        {
            var checklist = new List<ResourceSeoChecklistItem>
            {
                new() { Label = "Slug set", Passed = !string.IsNullOrWhiteSpace(resource.Slug) },
                new() { Label = "Meta title ≤ 60 chars", Passed = !string.IsNullOrWhiteSpace(resource.MetaTitle) && resource.MetaTitle.Length <= 60 },
                new() { Label = "Meta description ≤ 160 chars", Passed = !string.IsNullOrWhiteSpace(resource.MetaDescription) && resource.MetaDescription.Length <= 160 },
                new() { Label = "Focus keyword set", Passed = !string.IsNullOrWhiteSpace(resource.FocusKeyword) },
                new() { Label = "Canonical URL set", Passed = !string.IsNullOrWhiteSpace(resource.CanonicalUrl) },
                new() { Label = "Image alt text set", Passed = !string.IsNullOrWhiteSpace(resource.ImageAlt) },
                new() { Label = "Author set", Passed = !string.IsNullOrWhiteSpace(resource.Author) },
                new() { Label = "AI summary (TL;DR) set", Passed = !string.IsNullOrWhiteSpace(resource.Tldr) },
                new() { Label = "Schema type set", Passed = true }, // SchemaType is a non-nullable enum; always has a value.
            };

            if (resource.SchemaType == ResourceSchemaType.FAQ)
            {
                checklist.Add(new ResourceSeoChecklistItem
                {
                    Label = "FAQ has 2+ questions",
                    Passed = resource.FaqItems.Count(q => !string.IsNullOrWhiteSpace(q.Question) && !string.IsNullOrWhiteSpace(q.Answer)) >= 2
                });
            }

            var passed = checklist.Count(c => c.Passed);
            var score = checklist.Count == 0 ? 0 : (int)Math.Round(passed * 100.0 / checklist.Count);

            return new ResourceSeoScoreResult { Score = score, Checklist = checklist };
        }

        /// <summary>Bootstrap/einv-badge semantic tier for a score: success (>=80), warning (50-79), danger (&lt;50).</summary>
        public static string Tier(int score) => score >= 80 ? "success" : score >= 50 ? "warning" : "danger";
    }
}
