using Godot;
using ManyWinters.Core.Population;

namespace ManyWinters.Godot.Logic;

// How a person is drawn, paper-doll style: a body, a garment over it and a hairstyle on top, each
// an independent seeded pick, the garment and hair recoloured at runtime, so body x clothing x
// hair x colours come from a handful of images. Worked out once from the person's seed, so the
// figure walking about the world and the portrait on their page are the same person.
internal readonly record struct PersonLook(string Body, string Clothing, Color ClothingColor, string Hair, Color HairColor)
{
    // Dead keeps this person's own body/clothing/hair, drained of colour, rather than a shared
    // generic corpse. Modulate can only multiply, not desaturate, but a muted grey darkens the
    // multi-toned body enough to read as lifeless without a shader.
    internal static readonly Color DeadTint = new(0.5f, 0.5f, 0.52f);

    private const string People = "res://Content/people/";

    // Each lying-down layer is the standing one laid on its side (generate_sprites.py's
    // _lay_down) under the same name plus "_dead", so the same pick gives the same hairstyle and
    // garment either way.
    private const string LyingDownSuffix = "_dead";

    // A body of the person's own sex, and among those the seed's pick - one each so far, so a new
    // body drawn for either sex is one more entry here.
    private static readonly string[] MaleBodies = ["person_body_male"];
    private static readonly string[] FemaleBodies = ["person_body_female"];
    private static readonly string[] Garments = ["clothing_robe", "clothing_tunic", "clothing_cloak"];
    private static readonly string[] Hairstyles = ["hair_short", "hair_long", "hair_tied"];

    private static readonly Color[] HairColors =
    [
        new(0.22f, 0.16f, 0.11f),
        new(0.32f, 0.22f, 0.14f),
        new(0.45f, 0.40f, 0.34f),
    ];

    private static readonly Color[] ClothingColors =
    [
        new(0.34f, 0.24f, 0.16f),
        new(0.33f, 0.36f, 0.42f),
        new(0.47f, 0.27f, 0.15f),
        new(0.40f, 0.36f, 0.20f),
    ];

    // The salts are the ones the world sprite has always drawn with, so nobody's clothes or hair
    // change. The body has its own, distinct from the hairstyle/clothing picks, so it is not
    // correlated with them.
    internal static PersonLook For(int seed, Sex sex, bool lyingDown)
    {
        var suffix = lyingDown ? LyingDownSuffix : string.Empty;
        var bodies = sex == Sex.Male ? MaleBodies : FemaleBodies;
        return new PersonLook(
            Path(bodies[EntityVisualVariation.IndexFor(seed, salt: 4, bodies.Length)], suffix),
            Path(Garments[EntityVisualVariation.IndexFor(seed, salt: 5, Garments.Length)], suffix),
            ClothingColors[EntityVisualVariation.IndexFor(seed, salt: 6, ClothingColors.Length)],
            Path(Hairstyles[EntityVisualVariation.IndexFor(seed, salt: 7, Hairstyles.Length)], suffix),
            HairColors[EntityVisualVariation.IndexFor(seed, salt: 8, HairColors.Length)]);
    }

    private static string Path(string name, string suffix) => $"{People}{name}{suffix}.png";
}
