using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Population;

// What one person takes a substance to be like - which is not the same thing as what it is.
//
// The first knowledge in the game that can be *wrong* rather than merely absent (see
// docs/materials-and-crafting-architecture.md section 7). A technique is known or unknown;
// "cord needs something stiff" is a mistaken belief that sends somebody reaching for the wrong
// material. Nothing distorts a belief yet - everyone who learns one learns it true - but the
// shape is the one distortion will need, so adding it later is a new writer rather than a
// rewrite of every reader.
public sealed class Beliefs
{
    // How much handling it takes before what somebody has noticed is firm enough to act on.
    // Below it they have an inkling, not knowledge, and AsBelieved leaves the property blank.
    private const float FirmEnoughToAct = 1f;

    private readonly Dictionary<(MaterialId Material, MaterialProperty Property), Belief> _held = new();

    public IReadOnlyDictionary<(MaterialId Material, MaterialProperty Property), Belief> Held => _held;

    // Noticing the same thing again makes it firmer, up to certainty. The value is overwritten
    // rather than averaged: the last handling is the freshest evidence, and when a distorted
    // account arrives it should be able to talk somebody round.
    public void Learn(MaterialId material, MaterialProperty property, float value, float confidenceGained)
    {
        var key = (material, property);
        var confidence = _held.TryGetValue(key, out var existing) ? existing.Confidence : 0f;

        _held[key] = new Belief(value, Math.Clamp(confidence + confidenceGained, 0f, 1f));
    }

    // Everything there is to notice about one substance at once - what having it in your hands
    // teaches, whether that is carrying it about or working it.
    public void LearnAll(MaterialDefinition material, float confidenceGained)
    {
        Learn(material.Id, MaterialProperty.Density, material.Density, confidenceGained);
        Learn(material.Id, MaterialProperty.Hardness, material.Hardness, confidenceGained);
        Learn(material.Id, MaterialProperty.Toughness, material.Toughness, confidenceGained);
        Learn(material.Id, MaterialProperty.Flexibility, material.Flexibility, confidenceGained);
        Learn(material.Id, MaterialProperty.Elasticity, material.Elasticity, confidenceGained);
        Learn(material.Id, MaterialProperty.Fibrousness, material.Fibrousness, confidenceGained);
    }

    // Restoring a saved belief, which is not the same as noticing it again (see Skills.Restore).
    public void Restore(MaterialId material, MaterialProperty property, float value, float confidence) =>
        _held[(material, property)] = new Belief(value, confidence);

    // Firm enough that this person would act on it, and so firm enough to pass on as fact.
    public bool IsFirm(MaterialId material, MaterialProperty property) =>
        ConfidenceIn(material, property) >= FirmEnoughToAct;

    public float ConfidenceIn(MaterialId material, MaterialProperty property) =>
        _held.TryGetValue((material, property), out var belief) ? belief.Confidence : 0f;

    public bool HoldsAnythingAbout(MaterialId material) =>
        _held.Any(entry => entry.Key.Material == material && entry.Value.Confidence >= FirmEnoughToAct);

    // The substance as this person takes it to be - the same shape as the real definition, so
    // everything that reads a material (MaterialAffordances, MaterialWords) can be pointed at
    // somebody's understanding of it without knowing that beliefs exist. What they have no firm
    // belief about reads as zero: not "the same as the truth", but "nothing they know of".
    public MaterialDefinition AsBelieved(MaterialDefinition actual) => actual with
    {
        Density = Of(actual.Id, MaterialProperty.Density),
        Hardness = Of(actual.Id, MaterialProperty.Hardness),
        Toughness = Of(actual.Id, MaterialProperty.Toughness),
        Flexibility = Of(actual.Id, MaterialProperty.Flexibility),
        Elasticity = Of(actual.Id, MaterialProperty.Elasticity),
        Fibrousness = Of(actual.Id, MaterialProperty.Fibrousness),
    };

    private float Of(MaterialId material, MaterialProperty property) =>
        _held.TryGetValue((material, property), out var belief) && belief.Confidence >= FirmEnoughToAct
            ? belief.Value
            : 0f;
}

// What somebody takes one property to be, and how sure they are of it. Confidence is what makes
// a belief something that can be shaken; today only handling raises it.
public readonly record struct Belief(float Value, float Confidence);
