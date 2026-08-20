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
            || WorldState.Distance(buryingPerson.Position, deceased.Position) > WorldState.MaxInteractionDistance)
        {
            return;
        }

        var skillDefinition = world.SkillCatalog.Get(BurialSkill);
        var technique = skillDefinition.EfficientTechnique;
        var isMarked = buryingPerson.KnownTechniques.Contains(technique);

        var deathTick = deceased.DeathTick ?? world.Clock.CurrentTick;
        var ageAtDeath = (int)((deathTick - deceased.BirthTick) / WorldState.TicksPerYear);

        world.AddGrave(
            deceased.Position,
            isMarked,
            name: isMarked ? deceased.Name : null,
            ageAtDeath: isMarked ? ageAtDeath : null,
            knownTechniques: isMarked ? deceased.KnownTechniques.ToList() : []);

        deceased.IsBuried = true;

        buryingPerson.Skills.Increase(BurialSkill, SkillGainPerBurial);
        if (buryingPerson.Skills.Get(BurialSkill) >= DiscoveryThreshold)
        {
            buryingPerson.KnownTechniques.Add(technique);
        }
    }
}
