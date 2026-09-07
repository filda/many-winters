# ManyWinters.Godot.Tests

Tests for the presentation layer's own calculations - the parts whose signatures mention
`Color`, `Vector3` or a sprite extent, so they cannot move to Core (which never references
Godot) but are still ordinary functions worth pinning.

## The one rule

**Never construct or touch anything `Node`- or `Resource`-derived here.** `Node3D`, `Image`,
`Texture2D`, `Camera3D`, `Sprite3D`, `SurfaceTool`, `ResourceLoader`, `Godot.FileAccess` and
friends enter a native runtime that is not initialised outside the engine. That does not throw
a catchable exception - it aborts the whole test host, so one such test takes every other test
in this project down with it.

Watch for indirect reaches: a method can look perfectly pure and still call
`ResourceLoader.Exists` underneath.

`docs/development.md`, "Testing the presentation layer", has the rest - what *is* safe, why
mocking cannot get around this, and where an extracted calculation belongs.
