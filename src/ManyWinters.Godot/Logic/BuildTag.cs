using System.Globalization;

namespace ManyWinters.Godot.Logic;

// The one line printed at startup that says which build is actually running (see Main._Ready).
// It used to be a hand-written string bumped whenever someone remembered, which is exactly the
// question it exists to answer - a stale tag doesn't just fail to help, it actively asserts the
// wrong build. The stamp is therefore derived from the running assembly's own file time; all
// that is left here is rendering it, including the case where there is no file to ask.
internal static class BuildTag
{
    // Said out loud rather than left blank or filled with "now": a build tag nobody can read
    // is a smaller problem than one that looks like a real answer and isn't.
    private const string Unknown = "unknown";

    // Second precision, UTC, sortable: rebuilds during a session are minutes apart at most, and
    // the point is comparing this line against when the build actually ran - which is the same
    // comparison whichever machine or timezone reads the log back.
    private const string Format = "yyyy-MM-dd HH:mm:ss'Z'";

    public static string For(DateTimeOffset? buildTimeUtc) =>
        buildTimeUtc is { } time
            ? time.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture)
            : Unknown;
}
