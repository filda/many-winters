namespace ManyWinters.Core.Continuity;

// What has become of a band's line - read off its living and dead (BandEnding.FateOf), never
// stored.
public enum BandFate
{
    // Men and women both still living: the line can go on.
    Living,

    // No living man is left; no child will be born to the women. "Spear side" is the old name
    // for the male line, "spindle side" for the female.
    SpearSideEnded,

    // No living woman is left.
    SpindleSideEnded,

    // Nobody is left at all.
    Ended,
}
