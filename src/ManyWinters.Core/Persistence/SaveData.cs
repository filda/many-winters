using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
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
    IReadOnlyList<AffectionSaveData> Affections,
    IReadOnlyList<WordSaveData> Vocabulary,
    // Both added in the same version bump as Animal/HomeRange itself
    // (docs/todo/fauna-plan.md, phase 1a).
    IReadOnlyList<AnimalSaveData> Animals,
    IReadOnlyList<HomeRangeSaveData> HomeRanges);

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
    IReadOnlyList<BeliefSaveData> Beliefs,
    IReadOnlyList<ItemStackSaveData> Inventory,
    IReadOnlyList<AssemblySaveData> WorkedThings,
    long BirthTick,
    long? DeathTick,
    DeathCause? CauseOfDeath,
    bool IsBuried,
    // Guid.Empty is Person.Unknown, the only Person outside People and Forebears.
    Guid MotherId,
    Guid FatherId,
    // Stored, not re-derived from the id: MapLoader pins the starting band's sex, and a pinned
    // sex has to survive a reload.
    Sex Sex,
    // Set per band rather than per rules, so it has to survive a reload.
    float Curiosity);

// One bond per pair, not per direction - Affections is symmetric; which id is A is storage
// order.
public sealed record AffectionSaveData(Guid PersonA, Guid PersonB, float Value);

// One word a band coined for one shape of thing. The signature is the shape (AssemblyPattern),
// not the object, so the word covers every later thing built that way.
public sealed record WordSaveData(string PatternSignature, string Word);

public sealed record SkillLevelSaveData(SkillTypeId Type, float Level);

// What one person takes one property of one substance to be. Saved rather than re-derived: the
// whole point of a belief is that it need not match the world.
public sealed record BeliefSaveData(MaterialId Material, MaterialProperty Property, float Value, float Confidence);

public sealed record ItemStackSaveData(ItemKindId Kind, int Count);

// One worked object out of the instance tier, mirroring Assembly's two cases as two nullable
// blocks - the same shape GrowthSaveData uses for "only some entities have one", and the reason
// a whole bound object cannot fall out of a save silently: a case nobody wrote a block for will
// not round-trip at all rather than round-tripping as half of itself.
public sealed record AssemblySaveData(PartSaveData? Part, JointSaveData? Joint);

public sealed record PartSaveData(MaterialId Material, FormId Form, float Quality, float Volume);

public sealed record JointSaveData(float Strength, float Weight, AssemblySaveData Left, AssemblySaveData Right);

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
    IReadOnlyList<ItemStackSaveData>? Storage,
    // One worked object lying on the ground. Last, and nullable, so a save written before made
    // things could be put down still reads.
    AssemblySaveData? Made = null,
    // The worked things on a store's shelves, beside the counted stock in Storage. Same reason
    // a person's inventory needs two lists: a count is no truth at all about two axes of
    // different quality.
    IReadOnlyList<AssemblySaveData>? StorageWorkedThings = null);

public sealed record GraveSaveData(
    Guid Id,
    double PositionX,
    double PositionY,
    bool IsMarked,
    string? Name,
    Sex? Sex,
    int? AgeAtDeath,
    DeathCause? CauseOfDeath,
    string? MotherName,
    string? FatherName,
    IReadOnlyList<TechniqueId> KnownTechniques);

public sealed record ExplorationCellSaveData(int X, int Y);

// The second kind of Creature (docs/todo/fauna-plan.md, phase 1a). No name, no beliefs, no
// curiosity - what Animal itself doesn't have. MaxHunger isn't saved for the same reason a
// Person's isn't (PersonSaveData): it is always redrawn from the id.
public sealed record AnimalSaveData(
    Guid Id,
    SpeciesId Species,
    double PositionX,
    double PositionY,
    bool IsAlive,
    float Hunger,
    float Fatigue,
    IReadOnlyList<SkillLevelSaveData> Skills,
    IReadOnlyList<TechniqueId> KnownTechniques,
    long BirthTick,
    long? DeathTick,
    DeathCause? CauseOfDeath,
    Sex Sex,
    Guid HomeRangeId,
    // Null for one spawned as an adult (every animal in phase 1a - MapLoader's starting herds);
    // set once a fawn is born (phase 1b).
    Guid? MotherId,
    // Null for a male and for a female not currently carrying (Animal.PregnantSinceTick).
    long? PregnantSinceTick = null,
    // What a dead animal's carcass still holds (docs/todo/fauna-plan.md, phase 3) - null for one
    // still alive, since a living animal never carries anything (SpeciesDefinition.CanCarry is
    // false for every animal today). Last and nullable so a save written before butchering
    // existed still reads.
    IReadOnlyList<ItemStackSaveData>? Inventory = null);

public sealed record HomeRangeSaveData(Guid Id, double AnchorX, double AnchorY, float Radius, float DriftMetresPerSeason);
