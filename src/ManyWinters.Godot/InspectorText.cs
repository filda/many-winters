using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;

namespace ManyWinters.Godot;

// The inspector's own prose - the one place simulation state is rendered into English that a
// player reads. Kept apart from the panel that shows it, so the wording stays a plain function
// of the state with no widget in the way.
internal static class InspectorText
{
    internal static string ForTask(Person person) => person.Tasks.Current switch
    {
        MoveTask move => $"Walking to {move.Destination}",
        GatherTask gather => $"Gathering {gather.Target.Kind}",
        _ => "Idle",
    };

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
