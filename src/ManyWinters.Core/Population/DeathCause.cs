namespace ManyWinters.Core.Population;

public enum DeathCause
{
    Hunger,
    OldAge,

    // A hunter's roll came off (HuntCommand): only an Animal dies this way today, but the value
    // itself carries no such restriction - see docs/todo/fauna-plan.md phase 3.
    Hunted,
}
