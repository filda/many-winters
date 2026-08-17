using ManyWinters.Core.Persistence;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

var world = new WorldState();
var ava = world.AddPerson("Ava", new Position(0, 0));
var bran = world.AddPerson("Bran", new Position(1, 0));
ava.Tasks.Enqueue(new IdleTask());
bran.Tasks.Enqueue(new IdleTask());

world.Clock.Advance(10);

Console.WriteLine($"Tick {world.Clock.CurrentTick}: {world.People.Count} people alive.");
foreach (var person in world.People)
{
    Console.WriteLine($"  {person.Id} {person.Name} at {person.Position}");
}

var savePath = Path.Combine(Path.GetTempPath(), "manywinters-demo-save.json");
SaveGameService.Save(world, savePath);
Console.WriteLine($"Saved to {savePath}");

var restored = SaveGameService.Load(savePath);
Console.WriteLine($"Loaded back: tick {restored.Clock.CurrentTick}, {restored.People.Count} people.");
