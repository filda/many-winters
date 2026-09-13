using System.Globalization;

namespace ManyWinters.Godot.Logic;

// The one line printed at startup saying which build is running (see Main._Ready). Derived
// from the running assembly's file time, never hand-written: a stale tag actively asserts the
// wrong build. Only the rendering lives here, including the case where there is no file to ask.
internal static class BuildTag
{
    // Said out loud rather than left blank or filled with "now": a tag that looks like a real
    // answer and isn't is the worse problem.
    private const string Unknown = "unknown";

    // Second precision, UTC, sortable: rebuilds in a session are minutes apart, and the line is
    // compared against when the build ran, whichever machine or timezone reads the log.
    private const string Format = "yyyy-MM-dd HH:mm:ss'Z'";

    public static string For(DateTimeOffset? buildTimeUtc) =>
        buildTimeUtc is { } time
            ? time.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture)
            : Unknown;
}
