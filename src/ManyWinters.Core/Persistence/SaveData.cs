using ManyWinters.Core.Construction;
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
    IReadOnlyList<ResourceNodeSaveData> ResourceNodes,
    IReadOnlyList<BuildingSaveData> Buildings,
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
    // Guid.Empty is Person.Unknown - the only id a Person can carry without being in People
    // or Forebears (see Person.Unknown).
    Guid MotherId,
    Guid FatherId,
    // Stored rather than re-derived from the id on load: it usually would come out the same
    // (see Person.Sex), but MapLoader pins it for the starting band, and a pinned sex has to
    // survive a reload.
    Sex Sex);

// One bond, written once per pair rather than once per direction - Affections is symmetric,
// and which of the two ids is A is whatever order it happened to store them in.
public sealed record AffectionSaveData(Guid PersonA, Guid PersonB, float Value);

public sealed record SkillLevelSaveData(SkillTypeId Type, float Level);

public sealed record ItemStackSaveData(ItemKindId Kind, int Count);

public sealed record ResourceNodeSaveData(
    Guid Id,
    ResourceKindId Kind,
    double PositionX,
    double PositionY,
    float RemainingAmount,
    float MaxAmount);

public sealed record BuildingSaveData(
    Guid Id,
    BuildingKindId Kind,
    double PositionX,
    double PositionY,
    float Condition,
    IReadOnlyList<ItemStackSaveData> Inventory);

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
