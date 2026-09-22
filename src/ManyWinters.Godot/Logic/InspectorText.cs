using System.Globalization;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;

namespace ManyWinters.Godot.Logic;

// The inspector's prose - the one place simulation state is rendered into English for the
// player. Kept apart from the panel so the wording is a plain function of the state.
internal static class InspectorText
{
    internal static string ForTask(Person person) => person.Tasks.Current switch
    {
        MoveTask move => $"Walking to {move.Destination}",
        GatherTask gather => $"Gathering {gather.Target.Kind}",
        FollowTask follow => $"Keeping up with {follow.Target.Name}",
        _ => "Idle",
    };

    // The same thing in the player's words rather than the debugger's: what someone is doing,
    // never where. A destination in raw coordinates is a fact about the simulation, and a card
    // about a person is not the place to read one off.
    internal static string ForWork(Person person, ResourceCatalog resources) => person.Tasks.Current switch
    {
        MoveTask => "Walking",
        GatherTask gather => $"Gathering {resources.Get(gather.Target.Kind).DisplayName.ToLowerInvariant()}",
        FollowTask follow => $"Keeping up with {follow.Target.Name}",
        // "Idle" is a scheduler's word for a person standing in a field.
        _ => "At rest",
    };

    // How a person is introduced: age first, then sex, both in lower case so the phrase reads as
    // a description rather than a heading. Beside the name on the selection card, under it on
    // the band's roster.
    internal static string ForAgeAndSex(string age, Sex sex) => $"{age}, {sex}".ToLowerInvariant();

    // The three lists the selection panel and the debug inspector both show. Each reads "none"
    // or "empty" when there is nothing rather than leaving a bare label, and each is sorted, so
    // a person's card does not reshuffle itself between refreshes.
    internal static string ForTechniques(IReadOnlyCollection<TechniqueId> techniques) =>
        techniques.Count > 0
            ? string.Join(", ", techniques.Select(technique => technique.Value).OrderBy(name => name, StringComparer.Ordinal))
            : "none";

    // Invariant, not the machine's locale: the UI is English (docs/conventions.md), so a level
    // reads "2.5" on a Czech Windows too.
    internal static string ForSkills(Skills skills) =>
        skills.Levels.Count > 0
            ? string.Join(", ", skills.Levels.OrderBy(level => level.Key.Value, StringComparer.Ordinal).Select(level => $"{level.Key}: {level.Value.ToString("0.#", CultureInfo.InvariantCulture)}"))
            : "none";

    internal static string ForInventory(Inventory inventory) =>
        inventory.Counts.Count > 0
            ? string.Join(", ", inventory.Counts.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal).Select(entry => $"{entry.Key} x{entry.Value}"))
            : "empty";

    // What a person knows, named the way the player met it rather than by technique id: a skill's
    // own name, and "(practised)" for the one they worked out for themselves. The raw ids stay in
    // the debug inspector. A line per skill, not one long comma-spliced sentence: the player is
    // reading a list of what
    // somebody can do, and a list reads as a list. Empty when they know nothing at all, which the
    // caller words for itself - "nothing yet" and "nothing" are different things to say.
    internal static IReadOnlyList<string> ForKnowledge(IReadOnlyCollection<TechniqueId> techniques, SkillCatalog skills) =>
        skills.Definitions
            .Select(skill => (skill.DisplayName, Base: techniques.Contains(skill.BaseTechnique), Practised: techniques.Contains(skill.EfficientTechnique)))
            .Where(entry => entry.Base || entry.Practised)
            .OrderBy(entry => entry.DisplayName, StringComparer.Ordinal)
            .Select(entry => entry.Practised ? $"{entry.DisplayName} (practised)" : entry.DisplayName)
            .ToList();

    // How someone died, in one sentence. The corpse is in front of the player, so the card says
    // it; the grave repeats it, because the body will not always be there to ask.
    internal static string ForDeath(int ageAtDeath, DeathCause? cause)
    {
        var of = cause switch
        {
            DeathCause.Hunger => " of hunger",
            DeathCause.OldAge => " of old age",
            _ => string.Empty,
        };

        return $"Died at {ageAtDeath} winter{(ageAtDeath == 1 ? "" : "s")}{of}.";
    }

    // What they are carrying, by the items' own names. "Nothing" rather than "empty": this sits
    // under a weight the player has just read, not beside a container.
    //
    // Raw stock first and counted, then the worked things one by one - which is what the two
    // tiers are: a count is the whole truth about twelve grass, and no truth at all about two
    // cords of different quality.
    internal static string ForCarried(Inventory inventory, WorldState world)
    {
        var carried = inventory.Counts
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .Select(entry => $"{world.Configuration.ItemCatalog.Get(entry.Key).DisplayName} x{entry.Value}")
            .Concat(inventory.Assemblies
                .Select(assembly => ForWorkedThing(assembly, world))
                .OrderBy(name => name, StringComparer.Ordinal))
            .ToList();

        return carried.Count > 0 ? string.Join(", ", carried) : "nothing";
    }

    // The band's own word for this kind of thing, if they have coined one. A word earned at the
    // moment of discovery outranks anything generated here - which is the point of letting
    // people name what they make rather than recognising it for them.
    internal static string ForWorkedThing(Assembly assembly, WorldState world) =>
        world.Vocabulary.WordFor(assembly)
        ?? ForWorkedThing(assembly, world.Configuration.MaterialCatalog, world.Configuration.FormCatalog);

    // What a thing is made of, for anything nobody has a word for: the substance and the shape
    // for a single piece ("grass cord"), and what was tied to what for a bound one ("lashed
    // stone wedge and wood stick"). Section 8's fallback naming - a description, never a claim
    // about what the thing is for.
    internal static string ForWorkedThing(Assembly assembly, MaterialCatalog materials, FormCatalog forms) => assembly switch
    {
        Assembly.Part part => ForPiece(part, materials, forms),
        Assembly.Joined joined =>
            $"lashed {ForWorkedThing(joined.Left, materials, forms)} and {ForWorkedThing(joined.Right, materials, forms)}",
        _ => Unnameable,
    };

    private const string Unnameable = "something made";

    private static string ForPiece(Assembly.Part part, MaterialCatalog materials, FormCatalog forms)
    {
        var material = materials.Find(part.Material)?.DisplayName;
        var form = forms.Find(part.Form)?.DisplayName;

        return (material, form) switch
        {
            (null, null) => Unnameable,
            (null, not null) => Lowered(form),
            (not null, null) => Lowered(material),
            _ => $"{Lowered(material)} {Lowered(form)}",
        };
    }

    private static string Lowered(string displayName) => displayName.ToLowerInvariant();

    // The grave as the player finds it: what the stone says, and nothing the stone could not say.
    // ForGrave keeps the id and the coordinates for the debug inspector.
    internal static string ForGraveRecord(Grave grave, SkillCatalog skills)
    {
        if (!grave.IsMarked)
        {
            return "An unmarked grave. No record survives of who lies here.";
        }

        var knew = ForKnowledge(grave.KnownTechniques, skills);

        return
            $"{grave.Name}. {ForDeath(grave.AgeAtDeath ?? 0, grave.CauseOfDeath)}\n" +
            $"{ForParents(grave.Sex, grave.MotherName, grave.FatherName)}" +
            $"Knew: {(knew.Count > 0 ? string.Join(", ", knew) : "nothing")}";
    }

    internal static string ForGrave(Grave grave)
    {
        if (!grave.IsMarked)
        {
            return $"{grave.Id}\nPosition: {grave.Position}\nUnmarked grave - no record survives.";
        }

        var techniques = grave.KnownTechniques.Count > 0
            ? string.Join(", ", grave.KnownTechniques)
            : "none";
        var causeText = grave.CauseOfDeath switch
        {
            DeathCause.Hunger => " of hunger",
            DeathCause.OldAge => " of old age",
            _ => string.Empty,
        };
        return
            $"{grave.Id}\n" +
            $"Position: {grave.Position}\n" +
            $"{grave.Name}, died at age {grave.AgeAtDeath} winter{(grave.AgeAtDeath == 1 ? "" : "s")}{causeText}\n" +
            $"{ForParents(grave.Sex, grave.MotherName, grave.FatherName)}" +
            $"Known techniques: {techniques}";
    }

    internal static string ForParents(Sex? sex, string? motherName, string? fatherName)
    {
        if (motherName is null && fatherName is null)
        {
            return string.Empty;
        }

        var childWord = sex switch
        {
            Sex.Male => "Son",
            Sex.Female => "Daughter",
            _ => "Child",
        };

        var parents = string.Join(" and ", new[] { motherName, fatherName }.Where(name => name is not null));
        return $"{childWord} of {parents}\n";
    }
}
