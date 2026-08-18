using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Persistence;

public sealed record SaveData(
    int Version,
    long Tick,
    int NextPersonId,
    IReadOnlyList<PersonSaveData> People,
    int NextResourceNodeId,
    IReadOnlyList<ResourceNodeSaveData> ResourceNodes);

public sealed record PersonSaveData(
    int Id,
    string Name,
    float PositionX,
    float PositionY,
    bool IsAlive,
    float Hunger,
    float Fatigue,
    float GatheringSkill,
    IReadOnlyList<Technique> KnownTechniques);

public sealed record ResourceNodeSaveData(
    int Id,
    ResourceKind Kind,
    float PositionX,
    float PositionY,
    float RemainingAmount);
