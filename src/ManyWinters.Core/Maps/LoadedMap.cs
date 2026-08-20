using ManyWinters.Core.World;

namespace ManyWinters.Core.Maps;

public sealed record LoadedMap(WorldState World, Position CampCenter);
