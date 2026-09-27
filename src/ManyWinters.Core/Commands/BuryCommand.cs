using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record BuryCommand(Person BuryingPerson, Person Deceased) : ICommand
{
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
        // A decayed corpse is unmarked whatever the gravedigger knows: the person who could have
        // been recognized is gone, only bones are left, and the technique they dug the grave
        // with does not bring an identity back.
        var isMarked = BuryingPerson.KnownTechniques.Contains(technique) && !world.IsDecayed(Deceased);

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

        BuryingPerson.Skills.Increase(BurialSkill, world.Configuration.Rules.SkillGainPerBurial);
        if (BuryingPerson.Skills.Get(BurialSkill) >= Skills.LevelAfter(world.Configuration.Rules.PracticesBeforeDiscovery))
        {
            BuryingPerson.KnownTechniques.Add(technique);
        }
    }
}
