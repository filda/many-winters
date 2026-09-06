using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record BuryCommand(PersonId BuryingPersonId, PersonId DeceasedPersonId) : ICommand
{
    private const float SkillGainPerBurial = 1f;
    private const float DiscoveryThreshold = 5f;

    private static readonly SkillTypeId BurialSkill = new("burial");

    public void Execute(WorldState world)
    {
        var buryingPerson = world.People.FirstOrDefault(p => p.Id == BuryingPersonId && p.IsAlive);
        var deceased = world.People.FirstOrDefault(p => p.Id == DeceasedPersonId && !p.IsAlive && !p.IsBuried);
        if (buryingPerson is null
            || deceased is null
            || !world.IsWithinReach(buryingPerson.Position, deceased.Position))
        {
            return;
        }

        var skillDefinition = world.Configuration.SkillCatalog.Get(BurialSkill);
        var technique = skillDefinition.EfficientTechnique;
        var isMarked = buryingPerson.KnownTechniques.Contains(technique);

        var deathTick = deceased.DeathTick ?? world.Clock.CurrentTick;
        var ageAtDeath = (int)world.AgeInYearsAt(deceased, deathTick);
        var mother = deceased.MotherId is { } motherId ? world.People.FirstOrDefault(p => p.Id == motherId) : null;
        var father = deceased.FatherId is { } fatherId ? world.People.FirstOrDefault(p => p.Id == fatherId) : null;

        world.AddGrave(new Grave
        {
            Id = world.NextGraveId,
            Position = deceased.Position,
            IsMarked = isMarked,
            Name = isMarked ? deceased.Name : null,
            AgeAtDeath = isMarked ? ageAtDeath : null,
            CauseOfDeath = isMarked ? deceased.CauseOfDeath : null,
            MotherName = isMarked ? mother?.Name : null,
            FatherName = isMarked ? father?.Name : null,
            KnownTechniques = isMarked ? deceased.KnownTechniques.ToList() : [],
        });

        deceased.IsBuried = true;

        buryingPerson.Skills.Increase(BurialSkill, SkillGainPerBurial);
        if (buryingPerson.Skills.Get(BurialSkill) >= DiscoveryThreshold)
        {
            buryingPerson.KnownTechniques.Add(technique);
        }
    }
}
