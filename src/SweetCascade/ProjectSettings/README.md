# ProjectSettings/ — E01-001 file-based scaffold notes

## ProjectVersion.txt

See `UNITY-VERSION-NOTE.md` in this folder.

## ProjectSettings.asset

`ProjectSettings.asset` in this folder is a **deliberately minimal seed**,
not a full Unity-generated file. A real Unity-authored `ProjectSettings.asset`
runs to many hundreds of fields; Unity tolerates a partial file (missing
fields resolve to engine defaults) and will rewrite it in full the first time
the editor opens and saves the project — this is long-standing, version-stable
Unity YAML-upgrade behavior, not a 6.3-specific risk.

The seed encodes only the story-001 acceptance-criteria fields that are
safely file-based, stable, pre-cutoff API surface:

- `defaultScreenOrientation: 0` (Portrait) plus
  `allowedAutorotateToPortrait: 1` with the other three `allowedAutorotateTo*`
  flags at `0` — portrait-locked, matching technical-preferences.md's
  "Portrait orientation, one-handed play."
- `activeInputHandler: 1` — Input System Package (new), never legacy or
  "Both."

**After the first editor open, re-verify these three settings in Player
Settings** before committing the editor-materialized file, in case the
installed 6.3 patch's own defaults or upgrade path touched them.

Not encoded here (needs the real installed editor + its platform modules,
not a text edit): the iOS/Android active-build-target selection.

## GraphicsSettings.asset / QualitySettings.asset — deliberately NOT authored here

Story-001's Implementation Notes ask for these to "reference the URP asset,"
but the same story's Manual editor-checklist requires the URP pipeline asset
itself to be *created* via the Unity editor
(`Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)`) and then
*assigned* in Graphics/Quality — both explicitly listed there as
editor-GUI-only steps. There is no real asset/GUID yet for these files to
reference, and the Render Graph settings schema is a HIGH-RISK, post-cutoff
surface per `docs/engine-reference/unity/VERSION.md`. Hand-fabricating
`GraphicsSettings.asset`/`QualitySettings.asset` YAML against a 6.3 schema
this container cannot verify — to point at an asset that does not exist yet —
was judged higher-risk than leaving both files to the editor pass. This
decision has been folded into story-001's Manual editor-checklist (see the
story file's Status section); flag it to the reviewing programmer/lead if a
different call is wanted.
