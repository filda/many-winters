using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public sealed record SaveData(
    int Version,
    long Tick,
    IReadOnlyList<PersonSaveData> People,
    IReadOnlyList<PersonSaveData> Forebears,
    IReadOnlyList<EntitySaveData> Entities,
    IReadOnlyList<GraveSaveData> Graves,
    IReadOnlyList<ExplorationCellSaveData> ExploredCells,
    IReadOnlyList<AffectionSaveData> Affections);

public sealed record PersonSaveData(
    Guid Id,
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
    DeathCause? CauseOfDeath,
    bool IsBuried,
    // Guid.Empty is Person.Unknown, the only Person outside People and Forebears.
    Guid MotherId,
    Guid FatherId,
    // Stored, not re-derived from the id: MapLoader pins the starting band's sex, and a pinned
    // sex has to survive a reload (see Person.Sex).
    Sex Sex);

// One bond per pair, not per direction - Affections is symmetric; which id is A is storage
// order.
public sealed record AffectionSaveData(Guid PersonA, Guid PersonB, float Value);

public sealed record SkillLevelSaveData(SkillTypeId Type, float Level);

public sealed record ItemStackSaveData(ItemKindId Kind, int Count);

// Nested rather than flattened onto EntitySaveData: only a Growable entity has one, and its
// fields (IsAlive, DeathTick, CauseOfDeath, ColdStress) previously fell out of ResourceNodeSaveData
// silently on every save - keeping them together as one nullable block makes that omission
// impossible to repeat by accident.
public sealed record GrowthSaveData(
    float RemainingAmount,
    float MaxAmount,
    bool IsAlive,
    long? DeathTick,
    ResourceDeathCause? CauseOfDeath,
    float ColdStress);

public sealed record EntitySaveData(
    Guid Id,
    EntityKindId Kind,
    EntityCategory Category,
    double PositionX,
    double PositionY,
    GrowthSaveData? Growth,
    int? StaticAmount,
    float? Condition,
    IReadOnlyList<ItemStackSaveData>? Storage);

public sealed record GraveSaveData(
    Guid Id,
    double PositionX,
    double PositionY,
    bool IsMarked,
    string? Name,
    int? AgeAtDeath,
    DeathCause? CauseOfDeath,
    string? MotherName,
    string? FatherName,
    IReadOnlyList<TechniqueId> KnownTechniques);

public sealed record ExplorationCellSaveData(int X, int Y);
