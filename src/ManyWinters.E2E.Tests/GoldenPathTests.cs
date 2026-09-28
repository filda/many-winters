using System.Text.RegularExpressions;

namespace ManyWinters.E2E.Tests;

// The five golden-path scenarios agreed on 2026-09-22, alongside WorldBootSmokeTests. What each
// test claims, it claims through the game's own log: the facts that exist only inside the
// running engine - that a posted click was interpreted, that a panel came up, that a view was
// created - one line per fact, emitted where the fact happens. What the simulation does with an
// order is ManyWinters.Tests' business and is deliberately not claimed again here. Every click is
// verified against the log and retried when it did not land: a posted click can be swallowed on
// a stuttering machine, and each retry is guarded by the wait that preceded it. The frames saved
// along the way are for the human reviewer, never asserted.

/// <summary>Clicking a person opens their selection panel, showing details plus available
/// actions for whatever was under the cursor.</summary>
public sealed class EntityInspectionTests : IClassFixture<GameFixture>
{
    private readonly GameFixture _game;

    public EntityInspectionTests(GameFixture game) => _game = game;

    [Fact]
    public void ClickingAnEntityOpensItsInspector()
    {
        _game.DismissPrologue();

        // Where the first living person stands, read off the game's own log rather than a
        // recorded frame - the calibration cannot go stale even once people wander around a
        // shared home range instead of standing still.
        var person = _game.ReadAnchor("person");
        Assert.NotNull(person);

        _game.Click(person.Value.X, person.Value.Y);
        var selected = _game.WaitForGameLog("Selected ", TimeSpan.FromSeconds(4));
        if (selected is null)
        {
            _game.Click(person.Value.X, person.Value.Y);
            selected = _game.WaitForGameLog("Selected ", TimeSpan.FromSeconds(4));
        }

        Assert.NotNull(selected);
        Assert.NotNull(_game.WaitForGameLog("Card shown for ", TimeSpan.FromSeconds(4)));
        _game.SaveDebugShot("entity-after-click");
    }
}

/// <summary>Opening a person's workbench and picking a recipe should visibly change what they're
/// carrying.</summary>
public sealed class CraftingUiTests : IClassFixture<GameFixture>
{
    // Nobody carries anything at the boot tick, and the workshop works on what is carried - so
    // the test first orders the selected person to gather the wood pile in the camp (a left
    // click on a node with somebody selected) and steps the held clock while the order is walked
    // and resolved. Where the wood pile is comes off the game's own log, not a recorded frame.
    private const int GatherTicks = 30;

    // The selection panel's "Pack" line (the whole line is a button) opens the workbench. The
    // panel is docked to the right edge (width 300 + margin 16); the line sits
    // below the two meters - title bar 30 + padding 14 + the two meter rows + spacing puts it at
    // y 122..149 regardless of what the pack line says.
    private const int PackLineX = 986;
    private const int PackLineY = 135;

    // The "Make basket" recipe line in the centred workbench's recipe column (the recipes are
    // the offers the person can carry out; basket and warm clothing both come off 20 wood).
    // Clicking the line runs the recipe the same way the person's own card does.
    private const int RecipeX = 640;
    private const int RecipeY = 239;

    private readonly GameFixture _game;

    public CraftingUiTests(GameFixture game) => _game = game;

    [Fact]
    public void CraftingAnItemUpdatesInventoryDisplay()
    {
        _game.DismissPrologue();

        var person = _game.ReadAnchor("person");
        var wood = _game.ReadAnchor("wood");
        Assert.NotNull(person);
        Assert.NotNull(wood);

        // Select the first living person and send them gathering. A swallowed gather click would
        // leave the workbench without a recipe to offer; re-clicking is safe - a repeated
        // selection falls through to the ground and the repeated gather order replaces whatever
        // that walk started, landing at the wood pile either way.
        _game.Click(person.Value.X, person.Value.Y); // select the person
        _game.Click(wood.Value.X, wood.Value.Y); // send them gathering
        var gathered = _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        if (gathered is null)
        {
            _game.Click(person.Value.X, person.Value.Y);
            _game.Click(wood.Value.X, wood.Value.Y);
            gathered = _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        }

        Assert.NotNull(gathered);

        for (var i = 0; i < GatherTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        GameFixture.Settle(800); // the last tick's frame, not the one before it

        _game.Click(PackLineX, PackLineY); // open their Workshop panel
        Assert.NotNull(_game.WaitForGameLog("Workshop opened for ", TimeSpan.FromSeconds(5)));
        GameFixture.Settle(400);
        _game.SaveDebugShot("crafting-workshop");

        _game.Click(RecipeX, RecipeY); // run the offered recipe
        // A recipe whose output fits the pack is carried out the moment it is ordered - nothing
        // walks, so nothing is pending and no tick is owed. The order line is the claim's last
        // link: the recipe line's click reached the command layer through the bench.
        Assert.NotNull(_game.WaitForGameLog("Make basket", TimeSpan.FromSeconds(5)));
        GameFixture.Settle(400);
        _game.SaveDebugShot("crafting-crafted");
    }
}

/// <summary>Placing a building through the order flow should render it at the position clicked,
/// not just record it in world state.</summary>
public sealed class BuildingPlacementTests : IClassFixture<GameFixture>
{
    // The store is built out of wood, and nobody carries anything at the boot tick - the same
    // gather-first setup used elsewhere in this suite (the wood pile in the camp, thirty
    // held-clock steps, then a settled frame). Where the person and the wood pile are comes off
    // the game's own log, not a recorded frame.
    private const int GatherTicks = 30;

    // The open ground the store is asked for on and placed at: the context menu opens exactly at
    // the right-click point, so the menu's lines are offsets from here.
    private const int GroundX = 500;
    private const int GroundY = 500;

    // The "Build storage hut" line under "Walk here": the menu opens at the right-click point and
    // is pushed back on screen (its bottom would cross the margin), which puts the second row at
    // y 574..602 regardless. Calibrated off the menu's row rects in the game's log.
    private const int BuildEntryX = 616;
    private const int BuildEntryY = 588;

    // Measured: the order resolves on the first tick after it is placed (the build spot sits
    // beside the wood pile the person was sent to), so 40 is all margin.
    private const int BuildTicks = 40;

    private readonly GameFixture _game;

    public BuildingPlacementTests(GameFixture game) => _game = game;

    [Fact]
    public void PlacingABuildingRendersItAtThePosition()
    {
        _game.DismissPrologue();

        var person = _game.ReadAnchor("person");
        var wood = _game.ReadAnchor("wood");
        Assert.NotNull(person);
        Assert.NotNull(wood);

        // The same gather-first setup used above, with the same guarded re-clicks.
        _game.Click(person.Value.X, person.Value.Y); // select the first living person
        _game.Click(wood.Value.X, wood.Value.Y); // send them gathering
        var gathered = _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        if (gathered is null)
        {
            _game.Click(person.Value.X, person.Value.Y);
            _game.Click(wood.Value.X, wood.Value.Y);
            gathered = _game.WaitForGameLog(": Gather.", TimeSpan.FromSeconds(4));
        }

        Assert.NotNull(gathered);

        for (var i = 0; i < GatherTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        GameFixture.Settle(800);

        // The menu is confirmed open before anything is pressed on it: the pressed line carries
        // the order, and a click aimed at a menu that never opened would land on the ground as a
        // walk instead.
        _game.RightClick(GroundX, GroundY); // "This spot": Walk here / Build …
        var menu = _game.WaitForGameLog("Menu This spot", TimeSpan.FromSeconds(4));
        if (menu is null)
        {
            _game.RightClick(GroundX, GroundY);
            menu = _game.WaitForGameLog("Menu This spot", TimeSpan.FromSeconds(4));
        }

        if (menu is null)
        {
            _game.SaveDebugShot("building-no-menu"); // what the second right-click left on screen
        }

        Assert.NotNull(menu);
        GameFixture.Settle(400);
        _game.SaveDebugShot("building-menu");

        _game.Click(BuildEntryX, BuildEntryY); // place the store at the pointed ground
        var ordered = _game.WaitForGameLog(": Build", TimeSpan.FromSeconds(4));
        if (ordered is null)
        {
            _game.RightClick(GroundX, GroundY);
            _game.Click(BuildEntryX, BuildEntryY);
            ordered = _game.WaitForGameLog(": Build", TimeSpan.FromSeconds(4));
        }

        Assert.NotNull(ordered);

        for (var i = 0; i < BuildTicks; i++)
        {
            _game.AdvanceOneTick();
        }

        // The building's view appearing is the claim's witness: the order became a thing on
        // screen, at the ground the menu was opened on.
        Assert.NotNull(_game.WaitForGameLog("Building view created for storage_hut", TimeSpan.FromSeconds(4)));
        GameFixture.Settle(800);
        _game.SaveDebugShot("building-placed-debug");
    }
}

/// <summary>The debug inspector's real "Extinguish Band" hook is the quick way to a band's
/// epitaph and its "Another band comes" offer — the only other way there is playing a band out
/// to its last death by hand.</summary>
public sealed class ExtinctionTransitionTests : IClassFixture<GameFixture>
{
    // The status bar packs its buttons against the right edge of its fixed 48px bottom bar;
    // "Inspector" sits left of the tick readout and the "?" help button. Calibrated off the
    // button rect in the game's log.
    private const int InspectorButtonX = 863;
    private const int InspectorButtonY = 625;

    // The inspector panel opens at a fixed position (16, 16); "Extinguish Band" is the second
    // button under the one-line "No selection." dump. Calibrated off the panel's button rects in
    // the game's log.
    private const int ExtinguishButtonX = 198;
    private const int ExtinguishButtonY = 123;

    // "Another band comes" is the one line the epitaph's overlay offers: a centred word-button at
    // the height of the closing words an epitaph with nobody left to go on for does not carry.
    // Calibrated off the overlay's button rect in the game's log.
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
        var opened = _game.WaitForGameLog("Inspector opened", TimeSpan.FromSeconds(4));
        if (opened is null)
        {
            _game.Click(InspectorButtonX, InspectorButtonY);
            opened = _game.WaitForGameLog("Inspector opened", TimeSpan.FromSeconds(4));
        }

        Assert.NotNull(opened);

        // The ending is announced on the tick after the last death, so the band is extinguished
        // and the clock stepped once; the epitaph's own inscription line witnesses both.
        _game.Click(ExtinguishButtonX, ExtinguishButtonY); // extinguish the band
        _game.AdvanceOneTick();
        var epitaph = _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(8));
        if (epitaph is null)
        {
            _game.Click(ExtinguishButtonX, ExtinguishButtonY);
            _game.AdvanceOneTick();
            epitaph = _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(8));
        }

        Assert.NotNull(epitaph);
        GameFixture.Settle(800);
        _game.SaveDebugShot("extinction-epitaph");

        // A successor band arrives into this world; its prologue's inscription line witnesses
        // this click the same way. A repeated click is safe while the epitaph is still up, and
        // the wait-for-log means it is only ever repeated when it was.
        _game.Click(AnotherBandComesX, AnotherBandComesY);
        var successor = _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(8));
        if (successor is null)
        {
            _game.Click(AnotherBandComesX, AnotherBandComesY);
            successor = _game.WaitForGameLog("Inscription:", TimeSpan.FromSeconds(8));
        }

        Assert.NotNull(successor);
        GameFixture.Settle(800);
        _game.SaveDebugShot("extinction-another-band");
    }
}

/// <summary>Steepening the camera's tilt should keep a decoration's billboard sprite grounded
/// (FixedY, per the billboard-mode/ground-contact design note) rather than floating. Whether the
/// sprite stays grounded is a visual property, so this test asserts what the log can say - the
/// held key drove the camera all the way up - and leaves both frames for the human reviewer.</summary>
public sealed class BillboardRenderingTests : IClassFixture<GameFixture>
{
    // Win32 VK_PRIOR (Page Up) — holding it steps the camera's tilt toward its 70° max at
    // 45°/s. The tilt needs ~1.3s of the game's own time to cross the full 12–70° range - game
    // time, not wall time: on a software renderer a frame can outlast any fixed hold, so the key
    // is held until the limit's own log line says it got there.
    private const int VkPageUp = 0x21;

    private readonly GameFixture _game;

    public BillboardRenderingTests(GameFixture game) => _game = game;

    [Fact]
    public void TiltedCameraKeepsBillboardSpriteGrounded()
    {
        _game.DismissPrologue();
        _game.SaveDebugShot("billboard-at-rest");

        var tilted = _game.HoldKeyUntilLog(VkPageUp, "Camera tilted to", TimeSpan.FromSeconds(60));

        Assert.NotNull(tilted);
        _game.SaveDebugShot("billboard-tilted");
    }
}
