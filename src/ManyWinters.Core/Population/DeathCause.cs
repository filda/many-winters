namespace ManyWinters.Core.Population;

public enum DeathCause
{
    Hunger,
    OldAge,

    // A hunter's roll came off: only an Animal dies this way today, but the value itself carries
    // no such restriction.
    Hunted,
}
