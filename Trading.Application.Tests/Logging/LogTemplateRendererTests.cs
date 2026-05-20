using FluentAssertions;
using Trading.Strategies.Adapters;
using Xunit;

namespace Trading.Application.Tests.Logging
{
    public class LogTemplateRendererTests
    {
        // ===== Render =====

        [Fact]
        public void Render_WithMatchingArgs_ReplacesPlaceholders()
        {
            var result = LogTemplateRenderer.Render(
                "Order {OrderId} filled at {Price}",
                new object[] { "ABC123", 50000m });

            result.Should().Be("Order ABC123 filled at 50000");
        }

        [Fact]
        public void Render_WithFewerArgsThanPlaceholders_DoesNotThrow_ReturnsBestEffort()
        {
            string result = null!;
            var act = () => { result = LogTemplateRenderer.Render(
                "Order {OrderId} filled at {Price}", new object[] { "ABC123" }); };

            act.Should().NotThrow();
            result.Should().Contain("ABC123");
            result.Should().Contain("{Price}"); // placeholder sin arg se conserva
        }

        [Fact]
        public void Render_WithMoreArgsThanPlaceholders_DoesNotThrow_IgnoresExtras()
        {
            string result = null!;
            var act = () => { result = LogTemplateRenderer.Render(
                "Order {OrderId}", new object[] { "ABC123", "extra" }); };

            act.Should().NotThrow();
            result.Should().Be("Order ABC123");
        }

        [Fact]
        public void Render_WithNoPlaceholders_ReturnsTemplateUnchanged()
        {
            var result = LogTemplateRenderer.Render("No placeholders here", new object[] { "arg1" });

            result.Should().Be("No placeholders here");
        }

        [Fact]
        public void Render_WithEmptyArgs_ReturnsTemplateUnchanged()
        {
            var result = LogTemplateRenderer.Render("Template {Foo}", System.Array.Empty<object>());

            result.Should().Be("Template {Foo}");
        }

        // ===== ExtractProperties =====

        [Fact]
        public void ExtractProperties_WithMatchingArgs_ReturnsPairs()
        {
            var props = LogTemplateRenderer.ExtractProperties(
                "Order {OrderId} at {Price}", new object[] { "ABC123", 50000m });

            props.Should().HaveCount(2);
            props[0].Key.Should().Be("OrderId");
            props[0].Value.Should().Be("ABC123");
            props[1].Key.Should().Be("Price");
            props[1].Value.Should().Be(50000m);
        }

        [Fact]
        public void ExtractProperties_WithNoPlaceholders_ReturnsEmpty()
        {
            var props = LogTemplateRenderer.ExtractProperties("No placeholders", new object[] { "arg1" });

            props.Should().BeEmpty();
        }

        [Fact]
        public void ExtractProperties_WithMismatchedCounts_ReturnsBestEffortPairs_DoesNotThrow()
        {
            System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, object?>> props = null!;
            var act = () => { props = LogTemplateRenderer.ExtractProperties(
                "Order {OrderId} at {Price} for {Symbol}", new object[] { "ABC123" }); };

            act.Should().NotThrow();
            props.Should().HaveCount(1);
            props[0].Key.Should().Be("OrderId");
        }
    }
}
