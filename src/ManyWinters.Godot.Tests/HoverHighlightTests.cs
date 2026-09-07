using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class HoverHighlightTests
{
    [Fact]
    public void TheHoverTintIsAFixedBlendTowardTheHighlightColour()
    {
        // Black blended 80% of the way to the highlight (1, 0.85, 0.15) - asserted exactly,
        // because both the colour and how far it blends decide whether hover reads at all.
        var tinted = HoverHighlight.TintFor(new Color(0f, 0f, 0f));

        Assert.Equal(0.8f, tinted.R, 5);
        Assert.Equal(0.68f, tinted.G, 5);
        Assert.Equal(0.12f, tinted.B, 5);
    }

    [Fact]
    public void TheBlendStopsShortOfTheHighlightColourItself()
    {
        // Blending all the way would throw the sprite's own colour away, so every hovered
        // thing would look identical. Some of the original has to survive.
        var tinted = HoverHighlight.TintFor(new Color(0f, 0f, 1f));

        Assert.True(tinted.B > 0.15f, "the original blue should still show through");
        Assert.True(tinted.B < 1f, "but it should have moved toward the highlight");
    }

    [Fact]
    public void ADarkSpriteBrightensRatherThanStayingPutTheWayAMultiplyWouldLeaveIt()
    {
        // The reason this is a lerp and not a Modulate multiply (see HoverHighlight's own
        // comment): this art is mostly near-black crosshatch ink, and multiplying near-zero by
        // anything stays near zero. A blend has no such blind spot.
        var ink = new Color(0.02f, 0.02f, 0.02f);

        var tinted = HoverHighlight.TintFor(ink);

        Assert.True(tinted.R > ink.R + 0.5f);
    }

    [Fact]
    public void AlphaBlendsWithEverythingElse()
    {
        // Pinned because it is easy to assume otherwise: alpha is lerped like the three colour
        // channels, so a partly transparent sprite comes back closer to opaque. Nothing hovers
        // a faded sprite today (SpritePixelHit refuses one), which is why it does not show.
        Assert.Equal(0.8f, HoverHighlight.TintFor(new Color(0f, 0f, 0f, 0f)).A, 5);
        Assert.Equal(1f, HoverHighlight.TintFor(new Color(0f, 0f, 0f)).A, 5);
    }

    [Fact]
    public void HoverMakesThingsBiggerNotSmaller()
    {
        // A size bump is the part of the feedback that always shows, whatever the sprite's own
        // colour - shrinking instead would read as the thing moving away.
        Assert.True(HoverHighlight.ScaleFactor > 1f);
    }
}
