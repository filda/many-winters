using System.Globalization;
using System.Text.RegularExpressions;

namespace ManyWinters.E2E.Tests;

/// <summary>Golden path 1 of 6 (see AGENTS.md e2e design discussion, 2026-09-22): there is no
/// main menu — the game boots straight into a generated world — so this is the smoke test
/// everything else in this suite builds on: the world renders after "Main ready.".</summary>
public sealed class WorldBootSmokeTests : IClassFixture<GameFixture>
{
    private readonly GameFixture _game;

    public WorldBootSmokeTests(GameFixture game) => _game = game;

    [Fact]
    public void WorldRendersOnBoot()
    {
        // The prologue going down witnesses that it was up; one tick is then stepped so the
        // session's render report happens with the world on screen. The claim is the report's
        // own number: geometry reached the renderer, which a stuck boot or a black frame cannot
        // fake. What the world is made of is the simulation's business (ManyWinters.Tests).
        _game.DismissPrologue();
        _game.AdvanceOneTick();

        // Checked before "Draw:", not after: both are printed by the same tick
        // (SimulationLoop.TickOnce), "Animals:" first, and WaitForGameLog only ever looks
        // forward - waiting for "Draw:" first would consume past "Animals:" and this could never
        // match it. The shipped map's starting deer herds (docs/todo/fauna-plan.md, phase 2b)
        // exist from world creation, well before any tick; this is simply the first point in the
        // log a test can witness them from, without clicking anything.
        var animals = _game.WaitForGameLog("Animals:", TimeSpan.FromSeconds(10));
        Assert.NotNull(animals);
        var count = Regex.Match(animals, @"Animals: (\d+)");
        Assert.True(count.Success, $"Unrecognised animal count: {animals}");
        Assert.True(int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture) > 0, $"No animals at boot: {animals}");

        var report = _game.WaitForGameLog("Draw:", TimeSpan.FromSeconds(10));
        Assert.NotNull(report);

        var objects = Regex.Match(report, @"Draw: (\d+) objects");
        Assert.True(objects.Success, $"Unrecognised render report: {report}");
        Assert.True(int.Parse(objects.Groups[1].Value, CultureInfo.InvariantCulture) > 0, $"Nothing reached the renderer: {report}");
    }
}
