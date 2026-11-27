using EINVWORLD.Helpers;
using Xunit;

namespace EINVWORLD.Tests
{
    /// <summary>PDF/Print/preview break description lines exactly where the invoice forms' enforceLineLength does.</summary>
    public class DescriptionWrapTests
    {
        [Fact]
        public void Wraps_at_the_last_space_like_the_form() =>
            Assert.Equal(
                "Lorem ipsum dolor sit amet, consectetur adipiscing\nelit. Nam cursus arcu nec metus semper, id porttitor\nsapien aliquam. Curabitur ut.",
                DescriptionWrap.Wrap("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nam cursus arcu nec metus semper, id porttitor sapien aliquam. Curabitur ut."));

        [Fact]
        public void Already_wrapped_text_is_unchanged() =>
            Assert.Equal("Line one\nLine two", DescriptionWrap.Wrap("Line one\r\nLine two"));

        [Fact]
        public void A_word_longer_than_a_line_is_hard_broken() =>
            Assert.Equal(new string('A', 56) + "\nAAAA", DescriptionWrap.Wrap(new string('A', 60)));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Empty_is_empty(string? text) => Assert.Equal(string.Empty, DescriptionWrap.Wrap(text));
    }
}
