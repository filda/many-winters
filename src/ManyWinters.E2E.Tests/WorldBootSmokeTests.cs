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
        _game.AssertMatchesBaseline("world-boot");
    }
}
