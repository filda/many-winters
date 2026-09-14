using System.Globalization;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
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
    // own name, and "(practised)" for the one they worked out for themselves (see
    // SkillDefinition.EfficientTechnique). The raw ids stay in the debug inspector (ForTechniques).
    // A line per skill, not one long comma-spliced sentence: the player is reading a list of what
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
    internal static string ForCarried(Inventory inventory, ItemCatalog items)
    {
        var carried = inventory.Counts
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .Select(entry => $"{items.Get(entry.Key).DisplayName} x{entry.Value}")
            .ToList();

        return carried.Count > 0 ? string.Join(", ", carried) : "nothing";
    }

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
            $"{ForParents(grave.MotherName, grave.FatherName)}" +
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
            $"{ForParents(grave.MotherName, grave.FatherName)}" +
            $"Known techniques: {techniques}";
    }

    internal static string ForParents(string? motherName, string? fatherName)
    {
        if (motherName is null && fatherName is null)
        {
            return string.Empty;
        }

        var parents = string.Join(" and ", new[] { motherName, fatherName }.Where(name => name is not null));
        return $"Child of {parents}\n";
    }
}
