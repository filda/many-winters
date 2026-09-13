using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// Writes the inscription shown when a band arrives (see BandArrival for the facts) - the
// opening page of the chronicle whose last page Epitaph writes, in the same voice and drawn
// the same way: every sentence with more than one wording is picked by a seed, here the
// eldest member's own, so the same band always arrives with the same words.
public static class Prologue
{
    private const uint SaltStride = 0x9E3779B9;

    public static Inscription Write(BandArrival arrival)
    {
        var draw = new PhraseDraw(arrival);
        var band = arrival.BandName;

        var title = draw.Pick(
            $"{band} come to the land.",
            $"The coming of {band}.",
            $"Here begin {band}.");

        var lines = new List<string>
        {
            HeadcountLine(arrival, draw),
            EldestLine(arrival, draw),
            arrival.KnowsAnything
                ? draw.Pick(
                    "They brought a little knowledge with them, and would need more.",
                    "Some of them knew a little; none of them knew enough.")
                : draw.Pick(
                    "They knew nothing of this land, and nothing of how to live in it.",
                    "None of them knew how to feed themselves here."),
            draw.Pick(
                "They will learn only what somebody teaches them.",
                "What they do not learn, they will not live to pass on.",
                "Their graves, if any are dug, will say the rest."),
        };

        return new Inscription(title, lines);
    }

    private static string HeadcountLine(BandArrival arrival, PhraseDraw draw)
    {
        var count = NumberWords.Of(arrival.People);
        var season = arrival.Season.ToString().ToLowerInvariant();
        var groups = Groups(arrival);
        return draw.Pick(
            $"In the {season}, {count} came to this land: {groups}.",
            $"{Capitalize(count)} of them came to this land in the {season}: {groups}.",
            $"They came in the {season}, {count} in all: {groups}.");
    }

    // "six men, six women and three children" - a group nobody is in is left out rather than
    // written as "no children", so a band of grown men reads as one.
    private static string Groups(BandArrival arrival)
    {
        var parts = new List<string>();
        Add(arrival.Men, "man", "men");
        Add(arrival.Women, "woman", "women");
        Add(arrival.Children, "child", "children");
        return parts.Count == 1
            ? parts[0]
            : $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}";

        void Add(int count, string singular, string plural)
        {
            if (count > 0)
            {
                parts.Add($"{NumberWords.Of(count)} {(count == 1 ? singular : plural)}");
            }
        }
    }

    private static string EldestLine(BandArrival arrival, PhraseDraw draw)
    {
        var eldest = arrival.Eldest;
        var his = eldest.Sex == Sex.Male ? "his" : "her";
        var winters = arrival.EldestWinters switch
        {
            0 => "had not yet seen a winter",
            1 => "had seen one winter",
            _ => $"had seen {NumberWords.Of(arrival.EldestWinters)} winters",
        };
        return draw.Pick(
            $"{eldest.Name}, who {winters}, was the eldest among them, and the band took {his} name.",
            $"The eldest among them was {eldest.Name}, who {winters}; they were {his} people.");
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private sealed class PhraseDraw(BandArrival arrival)
    {
        private readonly uint _seed = unchecked((uint)arrival.Eldest.Id.Seed ^ (uint)arrival.ArrivalTick);
        private uint _salt;

        public string Pick(params string[] variants)
        {
            var mixed = unchecked(_seed + (++_salt * SaltStride));
            var index = (uint)SeedHash.Avalanche(mixed) % (uint)variants.Length;
            return variants[index];
        }
    }
}
