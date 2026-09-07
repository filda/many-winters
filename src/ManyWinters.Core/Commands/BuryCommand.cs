using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record BuryCommand(Person BuryingPerson, Person Deceased) : ICommand
{
    private const float SkillGainPerBurial = 1f;
    private const int PracticesBeforeDiscovery = 5;

    // The practice curve is not linear any more (see Skills.Increase), so the threshold is
    // stated as the number of tries it stands for rather than as a level.
    private static readonly float DiscoveryThreshold = Skills.LevelAfter(PracticesBeforeDiscovery);

    private static readonly SkillTypeId BurialSkill = new("burial");

    public void Execute(WorldState world)
    {
        if (!BuryingPerson.IsAlive
            || Deceased.IsAlive
            || Deceased.IsBuried
            || !world.IsWithinReach(BuryingPerson.Position, Deceased.Position))
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
