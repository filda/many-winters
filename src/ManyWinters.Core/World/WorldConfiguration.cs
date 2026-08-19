using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.World;

public sealed record WorldConfiguration(
    ResourceCatalog ResourceCatalog,
    SkillCatalog SkillCatalog,
    RecipeCatalog RecipeCatalog,
    BuildingCatalog BuildingCatalog,
    SeasonParameters SeasonParameters)
{
    public static WorldConfiguration Empty { get; } = new(
        new ResourceCatalog([]),
        new SkillCatalog([]),
        new RecipeCatalog([]),
        new BuildingCatalog([]),
        SeasonParameters.Default);
}
