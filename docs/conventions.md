# Project Conventions

These are general engineering conventions carried over from other projects, kept deliberately tech-stack-agnostic. Anything specific to Godot/C# belongs in the technical plan or in its own document instead.

## Language

- Application UI text must be in English.
- Source code, identifiers, and code comments must be in English.
- Repository documentation must be in English.

## Engineering

- A bugfix is not complete without a test that would fail before the fix and pass after it.
- Prefer small, extracted, pure/testable helpers for logic that is otherwise hard to test through UI or engine setup code.
- Standardize a small set of entry-point commands for building, running, and checking the project, so the same commands work the same way regardless of which tool sits behind them.
- Pin toolchain and dependency versions explicitly rather than floating on "latest", especially where a version mismatch causes hard-to-diagnose breakage.
- Keep experimental/prototype code clearly separated from production code, and don't hold prototypes to the same testing bar as production code.
- Treat mutation-testing (or equivalent) survivors as real gaps to close, not noise. An explicit, documented exclusion list is fine; a silently declining score is not.

## Consistency

- **Same control, same shape.** When the same kind of control or interaction appears in more than one place, its order, labels, defaults, and behavior must match everywhere it appears.
- **Same word for the same thing.** Pick one term for a concept and use it consistently; don't let synonyms drift in over time.
- **Look for the slot before adding the field.** Before extending a shared data model, search for whether the concept already has a place to live. Adding a duplicate at a different level forces every reader to learn two slots.
- **Parallel patterns over premature abstraction.** When a second instance wants the same shape as an existing one, it's fine to copy-adapt rather than factor a generic abstraction immediately. Factor once a third instance appears — early abstraction tends to constrain the second instance's shape, and that constraint costs more than the duplication.
- **Catch drift after family-wide changes.** After a change touches one member of a family of similar things (screens, entities, systems), spot-check the other members for the same surface and confirm they still match.
