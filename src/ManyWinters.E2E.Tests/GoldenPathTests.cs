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
        _game.DismissPrologue();

        // A person standing in the camp, at the deterministic boot tick (the band is seeded and
        // the world stands at that tick with the clock held). Calibrated off
        // a recorded frame - the world's centre is open ground between people at this tick, not a
        // person, so the click targets a body, not the middle of the camp. A posted click can be
        // swallowed on a stuttering machine, so the selection is verified against the game's log
        // ("Selected …") and the click retried; a repeated click that misses re-selects nobody
        // and orders nothing.
        _game.Click(760, 345);
        if (!_game.WaitForGameLog("Selected ", TimeSpan.FromSeconds(2)))
        {
            _game.Click(760, 345);
            _game.WaitForGameLog("Selected ", TimeSpan.FromSeconds(2));
        }

        _game.SaveDebugShot("entity-after-click");
        _game.AssertMatchesBaseline("entity-selected");
    }
}

/// <summary>Opening a person's workbench (<c>SelectionController.WorkshopRequested</c> →
/// <c>WorkshopController</c>/<c>WorkshopPanel</c>) and picking a recipe should visibly change
/// what they're carrying.</summary>
public sealed class CraftingUiTests : IClassFixture<GameFixture>
{
    // Nobody carries anything at the deterministic boot tick, and the workshop works on what is
    // carried (WorkshopActions.Carried) - so the test first orders the selected person to gather
    // the wood pile in the camp (a left click on a node with somebody selected, see
    // WorldInputController.OnResourceNodeClicked) and steps the frozen clock until the pack line
    // shows the haul: Wood x20 from tick 18 on, unchanged through the last step.
    private const int GatherTargetX = 575;
    private const int GatherTargetY = 437;
    private const int GatherTicks = 30;

    // The selection panel's "Pack" line (the whole line is a button) opens the workbench. The
    // panel is docked to the right edge (SelectionPanel.Width 300 + Margin 16); the line sits
    // below the two meters - title bar 30 + padding 14 + the two meter rows + spacing puts it at
    // y 122..149 regardless of what the pack line says.
    private const int PackLineX = 986;
    private const int PackLineY = 135;

    // The "Make basket" recipe line in the centred workbench's recipe column (the recipes are
    // the offers the person can actually carry out; basket and warm clothing both come off 20
    // wood). Clicking the line runs the recipe the same way the person's own card does.
    private const int RecipeX = 640;
    private const int RecipeY = 239;

    private readonly GameFixture _game;

    public CraftingUiTests(GameFixture game) => _game = game;

    [Fact]
    public void CraftingAnItemUpdatesInventoryDisplay()
    {
        _game.DismissPrologue();

        // Select the person calibrated for EntityInspection and send them gathering, verified
        // against the game's log ("Order by …: Gather."): a swallowed click would leave the pack
        // empty and the workbench without a recipe. Re-clicking is safe - a repeated selection
        // falls through to the ground and the repeated gather order replaces whatever that walk
        // started, landing at the wood pile either way.
        _game.Click(760, 345); // select the person
        _game.Click(GatherTargetX, GatherTargetY); // send them gathering
        if (!_game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4)))
        {
            _game.Click(760, 345);
            _game.Click(GatherTargetX, GatherTargetY);
            _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        }

        for (var i = 0; i < GatherTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        GameFixture.Settle(800); // the last tick's frame, not the one before it

        _game.Click(PackLineX, PackLineY); // open their Workshop panel
        GameFixture.Settle(400);
        _game.SaveDebugShot("crafting-workshop");
        _game.AssertMatchesBaseline("workshop-open");

        _game.Click(RecipeX, RecipeY); // run the offered recipe
        GameFixture.Settle(400);
        _game.SaveDebugShot("crafting-crafted");
        _game.AssertMatchesBaseline("workshop-crafted");
    }
}

/// <summary>Placing a building through the order flow (<c>OrderCoordinator</c>) should render it
/// at the position clicked, not just record it in world state.</summary>
public sealed class BuildingPlacementTests : IClassFixture<GameFixture>
{
    // The store is built out of wood, and nobody carries anything at the boot tick - the same
    // gather-first setup as CraftingUiTests (the wood pile in the camp, thirty frozen-clock
    // steps, then a settled frame).
    private const int GatherTargetX = 575;
    private const int GatherTargetY = 437;
    private const int GatherTicks = 30;

    // The open ground the store is asked for on and placed at: the context menu opens exactly at
    // the right-click point (ContextMenu.Open), so the menu's lines are offsets from here.
    private const int GroundX = 500;
    private const int GroundY = 500;

    // The "Build storage hut" line under "Walk here": the menu opens at the right-click point and
    // is pushed back on screen (its bottom would cross the margin), which puts the second row at
    // y 574..602 regardless. Calibrated off the menu's row rects in the game's log.
    private const int BuildEntryX = 616;
    private const int BuildEntryY = 588;

    // TODO(calibrate): how many frozen-clock steps the walk there plus the build itself take.
    // Measured: the order resolves on the first tick after it is placed (the build spot sits
    // beside the wood pile the person was sent to), so 40 is all margin.
    private const int BuildTicks = 40;

    private readonly GameFixture _game;

    public BuildingPlacementTests(GameFixture game) => _game = game;

    [Fact]
    public void PlacingABuildingRendersItAtThePosition()
    {
        _game.DismissPrologue();

        // The same gather-first setup as CraftingUiTests, with the same guarded re-clicks.
        _game.Click(760, 345); // select the person calibrated for EntityInspection
        _game.Click(GatherTargetX, GatherTargetY); // send them gathering
        if (!_game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4)))
        {
            _game.Click(760, 345);
            _game.Click(GatherTargetX, GatherTargetY);
            _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        }

        for (var i = 0; i < GatherTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        GameFixture.Settle(800);

        // The menu is confirmed open ("Menu This spot …") before anything is pressed on it: the
        // pressed line carries the order, and a click aimed at a menu that never opened would
        // land on the ground as a walk instead.
        _game.RightClick(GroundX, GroundY); // "This spot": Walk here / Build …
        if (!_game.WaitForGameLog("Menu This spot", TimeSpan.FromSeconds(4)))
        {
            _game.RightClick(GroundX, GroundY);
            _game.WaitForGameLog("Menu This spot", TimeSpan.FromSeconds(4));
        }

        GameFixture.Settle(400);
        _game.SaveDebugShot("building-menu");

        _game.Click(BuildEntryX, BuildEntryY); // place the store at the pointed ground
        if (!_game.WaitForGameLog(": Build", TimeSpan.FromSeconds(4)))
        {
            _game.Click(BuildEntryX, BuildEntryY);
            _game.WaitForGameLog(": Build", TimeSpan.FromSeconds(4));
        }

        for (var i = 0; i < BuildTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        GameFixture.Settle(800);
        _game.SaveDebugShot("building-placed-debug");
        _game.AssertMatchesBaseline("building-placed");
    }
}

/// <summary>The debug inspector's real "Extinguish Band" hook (Ui/InspectorPanel.cs) is the
/// quick way to a band's epitaph and its "Another band comes" offer — the only other way there
/// is playing a band out to its last death by hand.</summary>
public sealed class ExtinctionTransitionTests : IClassFixture<GameFixture>
{
    // StatusBar packs its buttons against the right edge of its fixed 48px bottom bar; "Inspector"
    // sits left of the tick readout and the "?" help button. Calibrated off the button rect in the
    // game's log (Ui/StatusBar.cs).
    private const int InspectorButtonX = 863;
    private const int InspectorButtonY = 625;

    // InspectorPanel opens at a fixed Position (16, 16); "Extinguish Band" is the second button
    // under the one-line "No selection." dump. Calibrated off the panel's button rects in the
    // game's log (Ui/InspectorPanel.cs).
    private const int ExtinguishButtonX = 198;
    private const int ExtinguishButtonY = 123;

    // "Another band comes" is the one line the epitaph's overlay offers (InscriptionOverlay): a
    // centred word-button at the height of the closing words an epitaph with nobody left to go on
    // for does not carry. Calibrated off the overlay's button rect in the game's log.
    private const int AnotherBandComesX = 575;
    private const int AnotherBandComesY = 380;

    private readonly GameFixture _game;

    public ExtinctionTransitionTests(GameFixture game) => _game = game;

    [Fact]
    public void LastDeathShowsEndScreenThenAnotherBandComes()
    {
        _game.DismissPrologue();

        // The inspector is a toggle, so it is confirmed open against the game's log before
        // anything is pressed inside it - a retry that could not tell "open" from "closed" would
        // flip it shut and send the next click to the ground.
        _game.Click(InspectorButtonX, InspectorButtonY); // open the debug inspector
        if (!_game.WaitForGameLog("Inspector opened", TimeSpan.FromSeconds(2)))
        {
            _game.Click(InspectorButtonX, InspectorButtonY);
            _game.WaitForGameLog("Inspector opened", TimeSpan.FromSeconds(2));
        }

        // The ending is announced on the tick after the last death, so the band is extinguished
        // and the clock stepped once; the epitaph's own inscription line witnesses the click.
        _game.Click(ExtinguishButtonX, ExtinguishButtonY); // extinguish the band
        _game.AdvanceOneTick();
        if (!_game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(4)))
        {
            _game.Click(ExtinguishButtonX, ExtinguishButtonY);
            _game.AdvanceOneTick();
            _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(4));
        }

        GameFixture.Settle(800);
        _game.SaveDebugShot("extinction-epitaph");
        _game.AssertMatchesBaseline("epitaph");

        // A successor band arrives into this world; its prologue's inscription line witnesses
        // this click the same way. A repeated click is safe while the epitaph is still up, and
        // the wait-for-log means it is only ever repeated when it was.
        _game.Click(AnotherBandComesX, AnotherBandComesY);
        if (!_game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(4)))
        {
            _game.Click(AnotherBandComesX, AnotherBandComesY);
            _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(4));
        }

        GameFixture.Settle(800);
        _game.SaveDebugShot("extinction-another-band");
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
        _game.DismissPrologue();
        _game.KeyPress(VkPageUp, TimeSpan.FromSeconds(2));
        _game.AssertMatchesBaseline("camera-tilted-billboard-grounded");
    }
}
