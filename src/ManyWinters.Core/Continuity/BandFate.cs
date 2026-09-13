namespace ManyWinters.Core.Continuity;

// What has become of a band's line - read off its living and dead (BandEnding.FateOf), never
// stored, so nothing has to be kept in step with the people it describes.
public enum BandFate
{
    // Men and women both still living: the line can go on.
    Living,

    // No living man is left. Women may be, but no child will be born to them. "Spear side" is
    // the old name for the male line, as "spindle side" is for the female one - both survive
    // in English from a time when a family was reckoned along either.
    SpearSideEnded,

    // No living woman is left.
    SpindleSideEnded,

    // Nobody is left at all.
    Ended,
}
