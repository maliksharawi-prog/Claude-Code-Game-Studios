# Packages/manifest.json — provenance & editor-pass note

Authored file-based, without a Unity editor, per story-001's Implementation
Notes ("File-based (no editor GUI required)"). This file is a **seed**, not
authoritative — see `docs/engine-reference/unity/VERSION.md`: LLM training
reliably covers only ~Unity 6.0/6.1, and exact package versions compatible
with the project's pinned 6000.3.x LTS patch are not independently verified
here.

## The four approved packages (control-manifest.md "Approved Libraries")

| Package | Pinned in manifest.json | Source of the version generation cited in-repo |
|---|---|---|
| Input System | `com.unity.inputsystem` @ `1.11.2` | `docs/engine-reference/unity/PLUGINS.md` cites `@1.11` |
| Addressables | `com.unity.addressables` @ `2.2.2` | `docs/engine-reference/unity/PLUGINS.md` cites `@2.0` generation |
| URP (rendering) | `com.unity.render-pipelines.universal` @ `17.0.3` | `docs/engine-reference/unity/modules/rendering.md` cites `@17.0` |
| Test Framework | `com.unity.test-framework` @ `1.4.5` | Best-effort; not independently cited in `docs/engine-reference/unity/` |

**UI Toolkit is intentionally NOT a manifest.json entry.**
`docs/engine-reference/unity/PLUGINS.md` lists it as `Package: Built-in` for
Unity 6 — it ships as an engine module, not a discrete UPM dependency. Story
001's own Implementation Notes flag this exact ambiguity ("UI Toolkit
(`com.unity.ui`/module as applicable in 6.3)"). If the installed 6.3 editor
resolves it differently (e.g. requires an explicit `com.unity.modules.
uielements` entry to be listed), update this manifest and this note during
the editor pass and record the resolved shape here.

## What the editor pass must do (story-001 Manual editor-checklist item 1)

Open `src/SweetCascade/` once in the real, installed Unity 6.3 LTS editor.
Unity will resolve every version above (upgrading/downgrading as needed for
the installed patch), generate `.meta` files, and write
`Packages/packages-lock.json` — **that generated file is the authoritative
version record**, not this manifest's starting pins. Commit both
`Packages/manifest.json` (if the editor changed any version) and the new
`Packages/packages-lock.json`.

## Explicitly NOT added (control-manifest.md "no speculative dependency")

Cinemachine, DOTS/Entities, Newtonsoft.Json, any networking package, or any
other package outside the four-package approved list above.
