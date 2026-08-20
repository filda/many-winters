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
    IReadOnlyList<BuildingSaveData> Buildings,
    int NextGraveId,
    IReadOnlyList<GraveSaveData> Graves);

public sealed record PersonSaveData(
    int Id,
    string Name,
    double PositionX,
    double PositionY,
    bool IsAlive,
    float Hunger,
    float Fatigue,
    IReadOnlyList<SkillLevelSaveData> Skills,
    IReadOnlyList<TechniqueId> KnownTechniques,
    IReadOnlyList<ItemStackSaveData> Inventory,
    long BirthTick,
    long? DeathTick,
    bool IsBuried);

public sealed record SkillLevelSaveData(SkillTypeId Type, float Level);

public sealed record ItemStackSaveData(ItemKindId Kind, int Count);

public sealed record ResourceNodeSaveData(
    int Id,
    ResourceKindId Kind,
    double PositionX,
    double PositionY,
    float RemainingAmount,
    float MaxAmount);

public sealed record BuildingSaveData(
    int Id,
    BuildingKindId Kind,
    double PositionX,
    double PositionY,
    float Condition,
    IReadOnlyList<ItemStackSaveData> Inventory);

public sealed record GraveSaveData(
    int Id,
    double PositionX,
    double PositionY,
    bool IsMarked,
    string? Name,
    int? AgeAtDeath,
    IReadOnlyList<TechniqueId> KnownTechniques);
