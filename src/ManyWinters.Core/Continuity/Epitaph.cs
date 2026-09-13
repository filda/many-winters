using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// Writes the inscription shown when a band's line ends (see BandEnding for the facts, BandFate
// for which ending). Written in the voice of a chronicle rather than a scoreboard - "Nine
// winters Liska's people endured", not "Survived: 9 winters" - because the woodcut world in
// front of it would make a scoreboard look like a debugger.
//
// Every sentence with more than one way of saying it is drawn from a short list, and the draw
// is seeded from the death that ended the line, the way every other per-entity variation in
// this game is (see SeedHash): the same ending reads the same on every reload and in every
// retelling, and a different ending reads differently. The facts themselves are never varied,
// only their wording.
public static class Epitaph
{
    // Spread the two inputs apart before the draw (see PhraseDraw) - a plain sum of a small
    // seed and a small tick would make neighbouring endings pick neighbouring phrases.
    private const uint SaltStride = 0x9E3779B9;

    public static Inscription Write(BandEnding ending)
    {
        var draw = new PhraseDraw(ending);
        return ending.Fate switch
        {
            BandFate.SpearSideEnded => WriteLineEnding(ending, draw, Sex.Male),
            BandFate.SpindleSideEnded => WriteLineEnding(ending, draw, Sex.Female),
            BandFate.Ended => WriteEnding(ending, draw),
            _ => throw new ArgumentException("A living band has no epitaph.", nameof(ending)),
        };
    }

    // No man (or no woman) is left, so no child will be born - but the survivors live on, and
    // a line closed this way is not what a chronicle calls the end of a people.
    private static Inscription WriteLineEnding(BandEnding ending, PhraseDraw draw, Sex sexThatEnded)
    {
        var band = ending.BandName;
        var side = sexThatEnded == Sex.Male ? "spear" : "spindle";
        var lost = sexThatEnded == Sex.Male ? "man" : "woman";

        var title = draw.Pick(
            $"The {side} side of {band} is ended.",
            $"No {lost} is left to {band}.");

        var lines = new List<string>
        {
            draw.Pick(
                $"No {lost} remains among {band}.",
                $"The last {lost} of {band} is dead."),
        };

        if (ending.LastToDie is { } last)
        {
            lines.Add($"In the {SeasonWord(ending.SeasonOfEnding)}, {last.Name} {Died(last, draw)}.");
        }

        lines.Add(SurvivorsLine(ending.Survivors, sexThatEnded));
        lines.Add(draw.Pick(
            $"The line has ended on the {side} side.",
            $"On the {side} side the line is ended."));

        return new Inscription(title, lines);
    }

    private static string SurvivorsLine(int survivors, Sex sexThatEnded)
    {
        if (sexThatEnded == Sex.Male)
        {
            return survivors == 1
                ? "One woman is left; no child will be born to her."
                : $"{Capitalize(NumberWords.Of(survivors))} women are left. No child will be born to them.";
        }

        return survivors == 1
            ? "One man is left; he will father no child."
            : $"{Capitalize(NumberWords.Of(survivors))} men are left. None of them will father a child.";
    }

    // Nobody is left. LastToDie is never null here: the band had people, and all of them died.
    private static Inscription WriteEnding(BandEnding ending, PhraseDraw draw)
    {
        var band = ending.BandName;
        var last = ending.LastToDie!;

        var title = draw.Pick(
            $"{band} are no more.",
            $"Here ends the line of {band}.",
            $"The last of {band}.");

        var lines = new List<string> { WintersLine(ending, draw) };

        if (ending.WintersKeptAfterwards > 0)
        {
            lines.Add(KeptAfterwardsLine(ending, draw));
        }

        lines.Add(LastDeathLine(last, ending.SeasonOfEnding, draw));
        lines.Add(BornLine(ending.Born, draw));
        lines.Add(GravesLine(ending));
        lines.Add(UnburiedLine(ending, last));
        lines.Add(ClosingLine(ending, draw));

        return new Inscription(title, lines);
    }

    // Names go into the ground only with a burial; a band nobody buried has nowhere its names
    // could have gone, so that wording is kept for one that at least dug graves.
    private static string ClosingLine(BandEnding ending, PhraseDraw draw)
    {
        if (ending.Graves == 0)
        {
            return "Nobody is left who could say who they were.";
        }

        return ending.MarkedGraves == 0
            ? draw.Pick(
                "Nobody is left who could say who they were.",
                "Their names went into the ground with them.")
            : draw.Pick(
                "This much the graves remember.",
                "So much the stones still tell.");
    }

    private static string WintersLine(BandEnding ending, PhraseDraw draw)
    {
        var band = ending.BandName;
        var winters = ending.WintersSeen;
        var count = NumberWords.Of(winters);
        return winters switch
        {
            0 => draw.Pick(
                $"{band} did not live to see a winter.",
                $"Not one winter did {band} see.",
                $"{band} were gone before the first snow."),
            1 => draw.Pick(
                $"One winter {band} saw, and it was their last.",
                $"{band} lived to see a single winter.",
                $"One winter came to {band}; a second did not."),
            _ => draw.Pick(
                $"{Capitalize(count)} winters {band} endured.",
                $"For {count} winters {band} kept their fire.",
                $"{Capitalize(count)} winters came and went over {band}."),
        };
    }

    private static string KeptAfterwardsLine(BandEnding ending, PhraseDraw draw)
    {
        var count = NumberWords.Of(ending.WintersKeptAfterwards);
        var winters = ending.WintersKeptAfterwards == 1 ? "winter" : "winters";
        var (lost, kept) = ending.SideThatEndedFirst == BandFate.SpearSideEnded ? ("man", "women") : ("woman", "men");
        return draw.Pick(
            $"For {count} {winters} after the last {lost} died, the {kept} kept the fire.",
            $"The last {lost} died {count} {winters} before the last of the {kept}.");
    }

    private static string LastDeathLine(Person last, Season season, PhraseDraw draw)
    {
        var died = Died(last, draw);
        var seasonWord = SeasonWord(season);
        return draw.Pick(
            $"In the {seasonWord}, {last.Name}, the last of them, {died}.",
            $"{last.Name} was the last of them. In the {seasonWord} {Pronoun(last, "he", "she")} {died}.",
            $"The last of them was {last.Name}, who {died} in the {seasonWord}.");
    }

    private static string BornLine(int born, PhraseDraw draw)
    {
        var count = NumberWords.Of(born);
        return born switch
        {
            0 => draw.Pick(
                "No child was ever born to them.",
                "They bore no children."),
            1 => draw.Pick(
                "One child was born to them.",
                "A single child was born among them."),
            _ => draw.Pick(
                $"{Capitalize(count)} children were born to them.",
                $"They bore {count} children."),
        };
    }

    // Numbers only, no variation: this is the line a later band will check against the ground.
    private static string GravesLine(BandEnding ending)
    {
        var graves = ending.Graves;
        var marked = ending.MarkedGraves;
        if (graves == 0)
        {
            return "None of them was laid in the ground.";
        }

        var lie = graves == 1 ? "One lies in the ground" : $"{Capitalize(NumberWords.Of(graves))} lie in the ground";
        if (marked == 0)
        {
            return $"{lie}, and no grave bears a name.";
        }

        if (marked == graves)
        {
            return graves == 1 ? $"{lie}, and the grave bears a name." : $"{lie}, and every grave bears a name.";
        }

        return marked == 1
            ? $"{lie}; one of the graves bears a name."
            : $"{lie}; {NumberWords.Of(marked)} of the graves bear a name.";
    }

    private static string UnburiedLine(BandEnding ending, Person last)
    {
        if (ending.Unburied == 1)
        {
            return $"{last.Name} lies where {Pronoun(last, "he", "she")} fell.";
        }

        return $"{Capitalize(NumberWords.Of(ending.Unburied))} lie unburied where they fell, {last.Name} among them.";
    }

    // Each of these has to read both at the end of a sentence and before "in the winter", so
    // none of them ends in a clause of its own.
    private static string Died(Person person, PhraseDraw draw) =>
        person.CauseOfDeath switch
        {
            DeathCause.OldAge => draw.Pick(
                "died old",
                "died full of years",
                "died of nothing but years"),
            _ => draw.Pick(
                "starved",
                "died hungry",
                "died with nothing to eat"),
        };

    private static string Pronoun(Person person, string male, string female) => person.Sex == Sex.Male ? male : female;

    private static string SeasonWord(Season season) => season.ToString().ToLowerInvariant();

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    // One draw per slot, each from its own salt, so changing the wording of one sentence never
    // reshuffles the others: the n-th Pick of an inscription always looks at the n-th salt.
    private sealed class PhraseDraw(BandEnding ending)
    {
        private readonly uint _seed = unchecked((uint)(ending.LastToDie?.Id.Seed ?? 0) ^ (uint)ending.EndingTick);
        private uint _salt;

        public string Pick(params string[] variants)
        {
            var mixed = unchecked(_seed + (++_salt * SaltStride));
            var index = (uint)SeedHash.Avalanche(mixed) % (uint)variants.Length;
            return variants[index];
        }
    }
}
