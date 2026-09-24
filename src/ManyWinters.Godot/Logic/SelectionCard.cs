using Godot;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One measure of a person drawn as a bar that empties as things get worse, never fills: a full
// bar is a person with nothing wrong. No numbers - the player reads a length and a colour, and
// the exact figures stay in the debug inspector.
internal readonly record struct MeterReading(string Label, float Value, float Max, Color Fill)
{
    // Clamped, because a bar that overruns its box reads as a bug rather than as an overfull
    // pack; a zero maximum (nothing worth measuring against) shows empty rather than dividing.
    public float Fraction => Max > 0f ? Math.Clamp(Value / Max, 0f, 1f) : 0f;
}

// Everything the selection panel says about the person who is selected, worked out in one place
// so the panel is left with nothing but drawing. The debug inspector keeps its own raw dump
// (positions, ids, raw needs); this is the player's view of the same person.
internal sealed record SelectionCard(
    string Name,
    string Beside,
    string Parents,
    string Death,
    string Task,
    IReadOnlyList<MeterReading> Meters,
    string Carried,
    string KnowledgeLabel,
    IReadOnlyList<string> Knowledge,
    PersonLook Look,
    bool IsAlive,
    MeterReading? Fatigue)
{
    // Nothing in the rules caps fatigue yet; the same hundred hunger is measured against, so the
    // bar has something to fill towards until the simulation says what tired is.
    private const float FatigueScale = 100f;

    // A belly with nothing wrong with it, someone who has begun looking for food of their own
    // accord, and someone near the end of it. The bar walks from the first to the last as hunger
    // rises, so the colour changes at the moment the person's own behaviour does.
    // Deep enough to read on the paper these are drawn on.
    private static readonly Color Fed = new(0.33f, 0.45f, 0.24f);
    private static readonly Color Hungry = new(0.76f, 0.58f, 0.16f);
    private static readonly Color Starving = new(0.60f, 0.18f, 0.14f);

    // A load is nobody's alarm, so it stays the colour of the ink around it.
    private static readonly Color Load = new(0.38f, 0.31f, 0.21f);

    internal static SelectionCard For(WorldState world, Person person)
    {
        var rules = world.Configuration.Rules;

        var carrying = new MeterReading("Carrying", person.Inventory.TotalWeight(world.Configuration.ItemCatalog), world.MaxCarryWeightFor(person), Load);

        return new SelectionCard(
            person.Name,
            person.IsAlive ? InspectorText.ForAgeAndSex(world.AgeInYears(person), rules.MaxLifespanYears, person.Sex) : "deceased",
            InspectorText.ForParents(person.Sex, NameOrNull(person.Mother), NameOrNull(person.Father)).TrimEnd('\n'),
            // The one sentence a body can still tell the player. The living have no death to report.
            person.IsAlive
                ? string.Empty
                : InspectorText.ForDeath((int)world.AgeInYearsAt(person, person.DeathTick ?? world.Clock.CurrentTick), person.CauseOfDeath),
            // Nobody dead is doing anything, and "At rest" under a corpse reads as a joke.
            person.IsAlive ? InspectorText.ForWork(person, world.Configuration.ResourceCatalog) : string.Empty,
            // Hunger stops mattering once someone is dead. What is on the body still does,
            // because it can be taken.
            person.IsAlive
                ?
                [
                    FedFor(person, rules.HungerSeekFoodThreshold),
                    carrying,
                ]
                : [carrying],
            InspectorText.ForCarried(person.Inventory, world),
            person.IsAlive ? "Knows" : "Knew",
            InspectorText.ForKnowledge(person.KnownTechniques, world.Configuration.SkillCatalog),
            // Standing even for the dead: a portrait is of who they were, not of the body on the
            // ground - the page drains it of colour instead.
            PersonLook.For(person.Id.Seed, person.Sex, lyingDown: false),
            person.IsAlive,
            // The person's page only, not the summary card: nothing in the simulation moves
            // fatigue yet, and the narrow strip down the edge has no room for a bar that says
            // nothing. It fills as they tire, the way the load bar fills as the pack does. The dead
            // are past tiring.
            person.IsAlive ? new MeterReading("Fatigue", person.Needs.Fatigue, FatigueScale, Load) : null);
    }

    // How full they are, not how hungry: the bar drains as hunger rises. The band's roster draws
    // the same bar under every name, so the reading is built here for both.
    internal static MeterReading FedFor(Person person, float seekFoodThreshold) =>
        new("Fed", person.MaxHunger - person.Needs.Hunger, person.MaxHunger, HungerFill(person, seekFoodThreshold));

    // Green while the belly is its own business; yellow the moment hunger sends the person off to
    // look for food by themselves, then deepening to red the rest of the way to the hunger that
    // kills them.
    internal static Color HungerFill(Person person, float seekFoodThreshold)
    {
        if (person.Needs.Hunger < seekFoodThreshold)
        {
            return Fed;
        }

        var remaining = person.MaxHunger - seekFoodThreshold;
        var travelled = remaining > 0f ? Math.Clamp((person.Needs.Hunger - seekFoodThreshold) / remaining, 0f, 1f) : 1f;
        return Hungry.Lerp(Starving, travelled);
    }

    // Person.Mother and Father are never null - an unremembered parent is Person.Unknown, and a
    // card should say nothing rather than name them "Unknown".
    private static string? NameOrNull(Person parent) => ReferenceEquals(parent, Person.Unknown) ? null : parent.Name;
}
