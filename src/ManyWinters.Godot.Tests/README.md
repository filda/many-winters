# ManyWinters.Godot.Tests

Tests for the presentation layer's own calculations - the parts whose signatures mention
`Color`, `Vector3` or a sprite extent, so they cannot move to Core (which never references
Godot) but are still ordinary functions worth pinning.

## The one rule

**Never construct or touch anything `Node`- or `Resource`-derived here.** `Node3D`, `Image`,
`Texture2D`, `Camera3D`, `Sprite3D`, `SurfaceTool`, `ResourceLoader`, `Godot.FileAccess` and
friends enter a native runtime that is not initialised outside the engine. That does not throw
a catchable exception - it aborts the whole test host, so one such test takes every other test
in this project down with it. Mocking them is not an escape either: a mock of a class is a
generated subclass, so its constructor still calls the real one.

What *is* safe: Godot's math value types (`Vector2`, `Vector3`, `Basis`, `Transform3D`,
`Color`, `Mathf`), which are plain managed structs.

Beware indirect reaches - a method can look pure and still call `ResourceLoader.Exists`
underneath. `ResourceNodeView.BaseTexturePathFor` and `BuildingView.ColorFor` both do.

See `docs/todo/godot-extraction.md` for what else is worth pulling out.
