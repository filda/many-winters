using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record BuryCommand(Person BuryingPerson, Person Deceased) : ICommand
{
    private const float SkillGainPerBurial = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // Stated in tries, not as a level: the practice curve is not linear (see Skills.Increase).
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    private static readonly SkillTypeId BurialSkill = new("burial");

    public ActionBlocker Blocker(WorldState world)
    {
        if (!BuryingPerson.IsAlive)
        {
            return ActionBlocker.ActorIsDead;
        }

        if (Deceased.IsAlive)
        {
            return ActionBlocker.TargetIsAlive;
        }

        if (Deceased.IsBuried)
        {
            return ActionBlocker.AlreadyBuried;
        }

        return world.IsWithinReach(BuryingPerson.Position, Deceased.Position)
            ? ActionBlocker.None
            : ActionBlocker.TooFar;
    }

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(BurialSkill);
        var technique = skillDefinition.EfficientTechnique;
        var isMarked = BuryingPerson.KnownTechniques.Contains(technique);

        var deathTick = Deceased.DeathTick ?? world.Clock.CurrentTick;
        var ageAtDeath = (int)world.AgeInYearsAt(Deceased, deathTick);

        world.AddGrave(new Grave
        {
            Position = Deceased.Position,
            IsMarked = isMarked,
            Name = isMarked ? Deceased.Name : null,
            Sex = isMarked ? Deceased.Sex : null,
            AgeAtDeath = isMarked ? ageAtDeath : null,
            CauseOfDeath = isMarked ? Deceased.CauseOfDeath : null,
            MotherName = isMarked ? Deceased.Mother.Name : null,
            FatherName = isMarked ? Deceased.Father.Name : null,
            KnownTechniques = isMarked ? Deceased.KnownTechniques.ToList() : [],
        });

        Deceased.IsBuried = true;

        BuryingPerson.Skills.Increase(BurialSkill, SkillGainPerBurial);
        if (BuryingPerson.Skills.Get(BurialSkill) >= DiscoveryThreshold)
        {
            BuryingPerson.KnownTechniques.Add(technique);
        }
    }
}
