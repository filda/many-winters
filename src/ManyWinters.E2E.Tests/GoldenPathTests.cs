namespace ManyWinters.E2E.Tests;

// The five golden-path scenarios agreed on 2026-09-22, alongside WorldBootSmokeTests. Each click
// target is grounded in a real UI hook (see the comment on each test), but where that hook's
// exact pixel offset can't be derived without running the game, it's a named constant flagged
// "TODO(calibrate)" rather than a guess dressed up as a fact — take a screenshot after the click
// before it, read the real offset off it, and replace the placeholder.

/// <summary>Clicking a person opens their selection panel — <c>SelectionController</c> handles
/// the click and shows details plus available actions for whatever was under the cursor.</summary>
public sealed class EntityInspectionTests : IClassFixture<GameFixture>
{
    private readonly GameFixture _game;

    public EntityInspectionTests(GameFixture game) => _game = game;

    [Fact]
    public void ClickingAnEntityOpensItsInspector()
    {
        // Main._Ready seats FreeCameraRig on the band's own camp position (campX/campZ), so the
        // window's centre is where a person or camp entity sits right after boot — no coordinate
        // to calibrate for this one specifically.
        var size = _game.WindowSize();
        _game.Click(size.Width / 2, size.Height / 2);
        _game.AssertMatchesBaseline("entity-selected");
    }
}

/// <summary>Opening a person's workbench (<c>SelectionController.WorkshopRequested</c> →
/// <c>WorkshopController</c>/<c>WorkshopPanel</c>) and picking a recipe should visibly change
/// what they're carrying.</summary>
public sealed class CraftingUiTests : IClassFixture<GameFixture>
{
    // TODO(calibrate): the selection panel's "Workshop" action button, once a person is selected.
    private const int WorkshopButtonX = 400;
    private const int WorkshopButtonY = 300;

    // TODO(calibrate): WorkshopPanel's "Make" button (WorkshopIcons.Make(), WorkshopPanel.cs).
    private const int MakeButtonX = 640;
    private const int MakeButtonY = 500;

    private readonly GameFixture _game;

    public CraftingUiTests(GameFixture game) => _game = game;

    [Fact]
    public void CraftingAnItemUpdatesInventoryDisplay()
    {
        var size = _game.WindowSize();
        _game.Click(size.Width / 2, size.Height / 2); // select the person at camp
        _game.Click(WorkshopButtonX, WorkshopButtonY); // open their Workshop panel
        _game.AssertMatchesBaseline("workshop-open");

        _game.Click(MakeButtonX, MakeButtonY); // attempt the offered recipe
        _game.AssertMatchesBaseline("workshop-crafted");
    }
}

/// <summary>Placing a building through the order flow (<c>OrderCoordinator</c>) should render it
/// at the position clicked, not just record it in world state.</summary>
public sealed class BuildingPlacementTests : IClassFixture<GameFixture>
{
    // TODO(calibrate): the context menu's "Build" entry that appears on a right-click
    // (see Ui/ContextMenu.cs), and the ground point to place at afterwards.
    private const int BuildMenuEntryX = 400;
    private const int BuildMenuEntryY = 300;
    private const int PlacementGroundX = 500;
    private const int PlacementGroundY = 400;

    private readonly GameFixture _game;

    public BuildingPlacementTests(GameFixture game) => _game = game;

    [Fact]
    public void PlacingABuildingRendersItAtThePosition()
    {
        _game.Click(BuildMenuEntryX, BuildMenuEntryY);
        _game.Click(PlacementGroundX, PlacementGroundY);
        _game.AssertMatchesBaseline("building-placed");
    }
}

/// <summary>The debug inspector's real "Extinguish Band" hook (Ui/InspectorPanel.cs) is the
/// quick way to a band's epitaph and its "Another band comes" offer — the only other way there
/// is playing a band out to its last death by hand.</summary>
public sealed class ExtinctionTransitionTests : IClassFixture<GameFixture>
{
    // TODO(calibrate): StatusBar is a fixed 48px bottom bar with buttons packed against its
    // right edge (Band, Inspector, then the 28px "?" Help button); InspectorPanel's panel opens
    // at a fixed Position (16, 16) with "Spawn Person" then "Extinguish Band" stacked under it.
    // These offsets need a real run to nail down precisely.
    private const int InspectorButtonX = -80; // relative to window width, see below
    private const int InspectorButtonY = -24; // relative to window height
    private const int ExtinguishButtonX = 40;
    private const int ExtinguishButtonY = 100;

    // TODO(calibrate): whatever button EndingAnnouncements/InscriptionOverlay shows for
    // "Another band comes" once the epitaph is up.
    private const int AnotherBandComesX = 640;
    private const int AnotherBandComesY = 400;

    private readonly GameFixture _game;

    public ExtinctionTransitionTests(GameFixture game) => _game = game;

    [Fact]
    public void LastDeathShowsEndScreenThenAnotherBandComes()
    {
        var size = _game.WindowSize();
        _game.Click(size.Width + InspectorButtonX, size.Height + InspectorButtonY); // StatusBar "Inspector"
        _game.Click(ExtinguishButtonX, ExtinguishButtonY); // InspectorPanel "Extinguish Band"
        _game.AssertMatchesBaseline("epitaph");

        _game.Click(AnotherBandComesX, AnotherBandComesY);
        _game.AssertMatchesBaseline("another-band-comes");
    }
}

/// <summary>Steepening the camera's tilt should keep a decoration's billboard sprite grounded
/// (FixedY, per the billboard-mode/ground-contact design note) rather than floating.</summary>
public sealed class BillboardRenderingTests : IClassFixture<GameFixture>
{
    // Win32 VK_PRIOR (Page Up) — FreeCameraRig.HandleInput reads Key.Pageup while held and steps
    // _tiltDegrees toward MaxTiltDegrees (70°) at TiltSpeedDegreesPerSecond (45°/s); holding it
    // for a couple of seconds covers the full 12–70° range regardless of where it started.
    private const int VkPageUp = 0x21;

    private readonly GameFixture _game;

    public BillboardRenderingTests(GameFixture game) => _game = game;

    [Fact]
    public void TiltedCameraKeepsBillboardSpriteGrounded()
    {
        _game.KeyPress(VkPageUp, TimeSpan.FromSeconds(2));
        _game.AssertMatchesBaseline("camera-tilted-billboard-grounded");
    }
}
