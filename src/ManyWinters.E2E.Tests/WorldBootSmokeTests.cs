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

        var report = _game.WaitForGameLog("Draw:", TimeSpan.FromSeconds(10));
        Assert.NotNull(report);

        var objects = Regex.Match(report, @"Draw: (\d+) objects");
        Assert.True(objects.Success, $"Unrecognised render report: {report}");
        Assert.True(int.Parse(objects.Groups[1].Value, CultureInfo.InvariantCulture) > 0, $"Nothing reached the renderer: {report}");
    }
}
