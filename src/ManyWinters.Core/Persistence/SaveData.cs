using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public sealed record SaveData(
    int Version,
    long Tick,
    int NextPersonId,
    IReadOnlyList<PersonSaveData> People,
    int NextResourceNodeId,
    IReadOnlyList<ResourceNodeSaveData> ResourceNodes,
    int NextBuildingId,
    IReadOnlyList<BuildingSaveData> Buildings);

public sealed record PersonSaveData(
    int Id,
    string Name,
    float PositionX,
    float PositionY,
    bool IsAlive,
    float Hunger,
    float Fatigue,
    IReadOnlyList<SkillLevelSaveData> Skills,
    IReadOnlyList<TechniqueId> KnownTechniques,
    IReadOnlyList<ItemStackSaveData> Inventory);

public sealed record SkillLevelSaveData(SkillTypeId Type, float Level);

public sealed record ItemStackSaveData(ItemKindId Kind, int Count);

public sealed record ResourceNodeSaveData(
    int Id,
    ResourceKindId Kind,
    float PositionX,
    float PositionY,
    float RemainingAmount);

public sealed record BuildingSaveData(
    int Id,
    BuildingKindId Kind,
    float PositionX,
    float PositionY,
    float Condition);
