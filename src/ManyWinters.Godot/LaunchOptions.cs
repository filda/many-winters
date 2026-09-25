using Godot;

namespace ManyWinters.Godot;

// The modes a rendered session can be asked for on its own command line — everything after "++"
// in the arguments (see OS.GetCmdlineUserArgs). Each names something the game itself offers, and
// none of them knows who asked or why; a session that wants several just lists several:
//
// - hold-clock: the simulation clock is held for the whole session, and the "advance one tick"
//   key (see Main._Input) steps it one tick at a time — the world ticked only when asked, the
//   way an inscription or a pause already holds it, just without a page of its own.
// - verbose: the log says what the player's input turned into — a selection, an order, a panel
//   opened — so a session can be followed from its log alone.
// - still: nothing on screen moves on real time. The person bob and walk playback hold at the
//   tick's own position and the status bar's live counters stay blank, so a frame captured
//   twice is the same frame — for a still screenshot, or for anything comparing frames.
internal static class LaunchOptions
{
    private static readonly string[] Args = OS.GetCmdlineUserArgs();

    public static readonly bool ClockHeld = Args.Contains("hold-clock");

    public static readonly bool Verbose = Args.Contains("verbose");

    public static readonly bool Still = Args.Contains("still");
}
