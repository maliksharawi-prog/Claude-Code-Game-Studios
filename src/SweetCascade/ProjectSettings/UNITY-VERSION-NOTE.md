# UNITY-VERSION-NOTE.md

`ProjectVersion.txt` in this folder is a **placeholder pin**, authored
file-based (no Unity editor available in this environment) per story-001.

```
m_EditorVersion: 6000.3.0f1
m_EditorVersionWithRevision: 6000.3.0f1 (PENDING_EDITOR_PASS)
```

- `6000.3.0f1` is a representative patch under the project's pinned engine
  version, "Unity 6.3 LTS (6000.3.x)" (ADR-001;
  `docs/engine-reference/unity/VERSION.md`). The exact installed `fN` patch
  is unknown until a human opens the project in a real, installed Unity 6.3.x
  LTS editor.
- `(PENDING_EDITOR_PASS)` is a deliberately-invalid placeholder revision hash
  (real Unity revision hashes are opaque hex strings) so nobody mistakes this
  file for editor-verified state.

**Manual editor-checklist item (story-001):** open the project once in the
real installed Unity 6.3 LTS editor. Unity will overwrite this file with the
true `m_EditorVersion` and `m_EditorVersionWithRevision`. Commit the
corrected file — do not hand-edit it to "fix" the revision hash.
