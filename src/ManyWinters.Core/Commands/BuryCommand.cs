using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record BuryCommand(Person BuryingPerson, Person Deceased) : ICommand
{
    private const float SkillGainPerBurial = 1f;
    private const float DiscoveryThreshold = 5f;

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
        var mother = Deceased.MotherId is { } motherId ? world.People.FirstOrDefault(p => p.Id == motherId) : null;
        var father = Deceased.FatherId is { } fatherId ? world.People.FirstOrDefault(p => p.Id == fatherId) : null;

        world.AddGrave(new Grave
        {
            Id = world.NextGraveId,
            Position = Deceased.Position,
            IsMarked = isMarked,
            Name = isMarked ? Deceased.Name : null,
            AgeAtDeath = isMarked ? ageAtDeath : null,
            CauseOfDeath = isMarked ? Deceased.CauseOfDeath : null,
            MotherName = isMarked ? mother?.Name : null,
            FatherName = isMarked ? father?.Name : null,
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
