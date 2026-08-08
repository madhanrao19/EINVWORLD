using EINVWORLD.Helpers;
using EINVWORLD.Models.Public;
using Xunit;

namespace EINVWORLD.Tests
{
    public class ResourceSeoScorerTests
    {
        private static ResourceItem EmptyResource() => new ResourceItem { Slug = "" };

        private static ResourceItem FullyOptimizedArticle() => new ResourceItem
        {
            Slug = "how-to-e-invoice",
            MetaTitle = "How to E-Invoice in Malaysia",
            MetaDescription = "A complete guide to e-invoicing compliance under LHDN MyInvois.",
            FocusKeyword = "e-invoice malaysia",
            CanonicalUrl = "https://einvworld.com/resources/article/how-to-e-invoice",
            ImageAlt = "E-invoice compliance diagram",
            Author = "EinvWorld Team",
            Tldr = "E-invoicing in Malaysia requires LHDN MyInvois integration for B2B/B2G/B2C transactions.",
            SchemaType = ResourceSchemaType.Article
        };

        [Fact]
        public void Compute_EmptyResource_ScoresBelow50()
        {
            var result = ResourceSeoScorer.Compute(EmptyResource());

            Assert.True(result.Score < 50);
            Assert.Equal("danger", ResourceSeoScorer.Tier(result.Score));
        }

        [Fact]
        public void Compute_FullyOptimizedArticle_Scores100()
        {
            var result = ResourceSeoScorer.Compute(FullyOptimizedArticle());

            Assert.Equal(100, result.Score);
            Assert.Equal("success", ResourceSeoScorer.Tier(result.Score));
            Assert.All(result.Checklist, c => Assert.True(c.Passed, c.Label));
        }

        [Theory]
        [InlineData(79, "warning")]
        [InlineData(80, "success")]
        [InlineData(49, "danger")]
        [InlineData(50, "warning")]
        public void Tier_BoundaryValues_MatchExpectedTier(int score, string expectedTier)
        {
            Assert.Equal(expectedTier, ResourceSeoScorer.Tier(score));
        }

        [Fact]
        public void Compute_MetaTitleOver60Chars_FailsThatCheck()
        {
            var resource = FullyOptimizedArticle();
            resource.MetaTitle = new string('a', 61);

            var result = ResourceSeoScorer.Compute(resource);

            Assert.Contains(result.Checklist, c => c.Label == "Meta title ≤ 60 chars" && !c.Passed);
        }

        [Fact]
        public void Compute_MetaDescriptionOver160Chars_FailsThatCheck()
        {
            var resource = FullyOptimizedArticle();
            resource.MetaDescription = new string('a', 161);

            var result = ResourceSeoScorer.Compute(resource);

            Assert.Contains(result.Checklist, c => c.Label == "Meta description ≤ 160 chars" && !c.Passed);
        }

        [Fact]
        public void Compute_ArticleSchemaType_DoesNotRequireFaqChecklistItem()
        {
            var resource = FullyOptimizedArticle();
            resource.SchemaType = ResourceSchemaType.Article;

            var result = ResourceSeoScorer.Compute(resource);

            Assert.DoesNotContain(result.Checklist, c => c.Label.Contains("FAQ"));
        }

        [Fact]
        public void Compute_FaqSchemaTypeWithFewerThanTwoQuestions_FailsFaqCheck()
        {
            var resource = FullyOptimizedArticle();
            resource.SchemaType = ResourceSchemaType.FAQ;
            resource.FaqItems = new List<ResourceFaqItem>
            {
                new() { Question = "What is e-invoicing?", Answer = "A digital invoice format." }
            };

            var result = ResourceSeoScorer.Compute(resource);

            Assert.Contains(result.Checklist, c => c.Label == "FAQ has 2+ questions" && !c.Passed);
        }

        [Fact]
        public void Compute_FaqSchemaTypeWithTwoOrMoreQuestions_PassesFaqCheck()
        {
            var resource = FullyOptimizedArticle();
            resource.SchemaType = ResourceSchemaType.FAQ;
            resource.FaqItems = new List<ResourceFaqItem>
            {
                new() { Question = "What is e-invoicing?", Answer = "A digital invoice format." },
                new() { Question = "Who must comply?", Answer = "All businesses above the threshold." }
            };

            var result = ResourceSeoScorer.Compute(resource);

            Assert.Contains(result.Checklist, c => c.Label == "FAQ has 2+ questions" && c.Passed);
            Assert.Equal(100, result.Score);
        }

        [Fact]
        public void Compute_FaqItemWithBlankAnswer_DoesNotCountTowardTwo()
        {
            var resource = FullyOptimizedArticle();
            resource.SchemaType = ResourceSchemaType.FAQ;
            resource.FaqItems = new List<ResourceFaqItem>
            {
                new() { Question = "What is e-invoicing?", Answer = "A digital invoice format." },
                new() { Question = "Who must comply?", Answer = "" } // blank answer — should not count
            };

            var result = ResourceSeoScorer.Compute(resource);

            Assert.Contains(result.Checklist, c => c.Label == "FAQ has 2+ questions" && !c.Passed);
        }

        [Fact]
        public void FaqItems_RoundTripsThroughJsonColumn()
        {
            var resource = new ResourceItem();
            resource.FaqItems = new List<ResourceFaqItem>
            {
                new() { Question = "Q1", Answer = "A1" },
                new() { Question = "Q2", Answer = "A2" }
            };

            Assert.False(string.IsNullOrWhiteSpace(resource.FaqItemsJson));

            var reloaded = new ResourceItem { FaqItemsJson = resource.FaqItemsJson };
            Assert.Equal(2, reloaded.FaqItems.Count);
            Assert.Equal("Q1", reloaded.FaqItems[0].Question);
        }

        [Fact]
        public void FaqItems_EmptyList_ClearsJsonColumn()
        {
            var resource = new ResourceItem { FaqItemsJson = "[{\"Question\":\"Q\",\"Answer\":\"A\"}]" };

            resource.FaqItems = new List<ResourceFaqItem>();

            Assert.Null(resource.FaqItemsJson);
        }

        [Fact]
        public void FaqItems_MalformedJson_ReturnsEmptyListInsteadOfThrowing()
        {
            var resource = new ResourceItem { FaqItemsJson = "{not valid json" };

            var items = resource.FaqItems;

            Assert.Empty(items);
        }
    }
}
