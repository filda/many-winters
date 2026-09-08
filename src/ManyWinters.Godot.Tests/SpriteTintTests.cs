using Godot;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class SpriteTintTests
{
    [Fact]
    public void ModulateUndoesTheRecolourableBaseSoTheAskedForColourComesOutOfTheShader()
    {
        // The person sprite is drawn in a near-white base tint so it can be recoloured; the
        // modulate has to divide that base back out, or every requested colour renders darker
        // than it was asked for. Feeding the base itself back in must therefore give plain
        // white - no tint at all.
        var neutral = new Color(0.82f, 0.80f, 0.78f);

        var modulate = SpriteTint.ModulateFor(neutral);

        Assert.Equal(1f, modulate.R, 5);
        Assert.Equal(1f, modulate.G, 5);
        Assert.Equal(1f, modulate.B, 5);
    }

    [Fact]
    public void ModulateScalesEachChannelByItsOwnShareOfTheBase()
    {
        var modulate = SpriteTint.ModulateFor(new Color(0.41f, 0.40f, 0.39f));

        // Exactly half of the base on every channel, so exactly half the modulate.
        Assert.Equal(0.5f, modulate.R, 5);
        Assert.Equal(0.5f, modulate.G, 5);
        Assert.Equal(0.5f, modulate.B, 5);
    }
}
