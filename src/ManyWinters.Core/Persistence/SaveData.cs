namespace ManyWinters.Core.Persistence;

public sealed record SaveData(
    int Version,
    long Tick,
    int NextPersonId,
    IReadOnlyList<PersonSaveData> People);

public sealed record PersonSaveData(
    int Id,
    string Name,
    float PositionX,
    float PositionY,
    float Hunger,
    float Fatigue);
