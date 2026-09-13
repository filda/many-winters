namespace ManyWinters.Godot.Logic;

// The line under the pause panel's title (see PausePanel) - who is here to come back to,
// counted the same three ways Prologue and Epitaph already break a band down by (adults of
// each sex, everyone younger as a child), but in plain digits rather than drawn prose: this is
// a status readout, glanced at while paused, not a chronicle entry. A group nobody is in is
// left out rather than written as "0 children", same reasoning as Prologue's own Groups.
internal static class PopulationSummary
{
    public static string Of(int people, int men, int women, int children)
    {
        var groups = new List<string>();
        Add(groups, men, "man", "men");
        Add(groups, women, "woman", "women");
        Add(groups, children, "child", "children");

        var breakdown = groups.Count == 1
            ? groups[0]
            : $"{string.Join(", ", groups.Take(groups.Count - 1))} and {groups[^1]}";

        var noun = people == 1 ? "person" : "people";
        return $"{people} {noun}: {breakdown}.";
    }

    private static void Add(List<string> groups, int count, string singular, string plural)
    {
        if (count > 0)
        {
            groups.Add($"{count} {(count == 1 ? singular : plural)}");
        }
    }
}
