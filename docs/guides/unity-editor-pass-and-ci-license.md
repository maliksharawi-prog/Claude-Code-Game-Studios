# Founder Guide — First Unity Editor Pass + CI License Setup

*Written 2026-07-18 for Sprint 1 close-out. Two independent tasks: (A–D) the
human editor pass that materializes the Unity project, and (E–F) the license
secrets that let GitHub CI execute the 124 authored tests. Checklist source:
`production/qa/qa-plan-sprint-01-2026-07-18.md`.*

---

## Part A — Install Unity (one-time, ~30–60 min mostly download)

1. Download **Unity Hub** from <https://unity.com/download> and install it.
2. Open the Hub and **sign in** with a Unity ID (create one free at the prompt —
   remember this email + password; CI needs them later).
3. Hub → **Installs → Install Editor** → pick the latest **Unity 6.3 LTS
   patch** (version starts with `6000.3.`). Our project pins the 6000.3.x
   line, any patch is fine.
4. On the modules screen tick **Android Build Support** (accept the nested
   OpenJDK + Android SDK/NDK boxes). On a Mac also tick **iOS Build Support**.
   (Skippable today — tests run without them — but you'll want them soon.)
5. Personal license: the Hub activates it automatically when you sign in
   (check Hub → Settings/Preferences → **Licenses** if unsure).

## Part B — Open the project (first import)

1. Clone the repo locally if you haven't, and check out the working branch:
   ```
   git clone https://github.com/maliksharawi-prog/Claude-Code-Game-Studios.git
   cd Claude-Code-Game-Studios
   git checkout claude/candy-crush-puzzle-game-ayp620
   git pull
   ```
2. Hub → **Projects → Add → Add project from disk** → select the
   **`src/SweetCascade`** folder (NOT the repo root).
3. Click the project. If the Hub warns the project was made with `6000.3.0f1`
   and offers your installed patch — **accept**. (Our pin is a placeholder;
   Unity will stamp the true version, which is the point of this pass.)
4. First open takes a while (package resolve + import). If a dialog asks to
   **enable the new Input System backend and restart** — click **Yes**.
5. If the Package Manager reports a version conflict for any of the four seed
   packages (Addressables / Input System / URP / Test Framework), open
   **Window → Package Manager** and update the offender to the version
   recommended for your 6.3 patch. The seed `manifest.json` is explicitly
   non-authoritative (see `Packages/README.md`).

## Part C — The checklist (in-editor, ~15 min)

Do these in order; jot anything unexpected.

1. **Console clean** — open **Window → General → Console**: there must be
   **zero red errors**. (Yellow warnings: note them, don't block.)
2. **Create the URP pipeline asset** — in the Project window create folder
   `Assets/Settings`, then right-click it →
   **Create → Rendering → URP Asset (with Universal Renderer)** → name it
   `SweetCascadeURP`. (Two files appear: the asset + its Renderer.)
3. **Assign it** —
   - **Edit → Project Settings → Graphics** → set **Default Render Pipeline**
     to `SweetCascadeURP`.
   - **Edit → Project Settings → Quality** → for every quality level, set
     **Render Pipeline Asset** to `SweetCascadeURP`.
4. **Confirm Render Graph** — in Project Settings → Graphics (URP section):
   there should be **no enabled "Compatibility Mode"** anywhere (Unity 6.3
   removed it; the toggle is absent or locked off). Nothing to change — just
   confirm you never see a "Compatibility Mode (Render Graph disabled)"
   checkbox ticked.
5. **Player settings** — **Edit → Project Settings → Player**:
   - Resolution & Presentation → **Default Orientation = Portrait**.
   - Other Settings → Configuration → **Active Input Handling =
     Input System Package (New)**.
6. **Assembly check** — click each of the 5 `.asmdef` files
   (`Assets/Domain`, `Assets/Game`, `Assets/UI`, `Assets/Editor`,
   `Assets/Tests/EditMode`, `Assets/Tests/PlayMode`) once in the Inspector:
   `SweetCascade.Domain` must show **No Engine References ✔** and an empty
   references list. (If the project compiled with no errors, the graph is
   already proven — this is a visual confirm.)
7. **Run the tests — the big one.** **Window → General → Test Runner**:
   - **EditMode** tab → **Run All** → expect **all green**
     (122 domain tests + conventions example). This is the first execution
     ever — if anything is red, screenshot it and send it to me as-is.
   - **PlayMode** tab → **Run All** → expect the smoke example green.
8. **Editor stub** — confirm the menu **Sweet Cascade → Level Manifest →
   Regenerate** exists (don't run it — it's a stub).
9. **Save** — **File → Save Project**, then quit Unity.

## Part D — Commit the materialized files

1. `git status` from the repo root. You should see: many new `*.meta` files,
   `Packages/packages-lock.json`, rewritten `ProjectSettings/*.asset`,
   `ProjectVersion.txt` (the `PENDING_EDITOR_PASS` marker gone), and
   `Assets/Settings/` with the URP assets. You should **NOT** see `Library/`,
   `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` — those are
   gitignored (commit `7a1be99`). If any appear, stop and tell Claude.
2. Commit and push (any message works; suggested):
   ```
   git add -A
   git commit -m "feat: first Unity 6.3 editor pass — meta files, package lock, URP asset (E01-001/002/006)"
   git push
   ```
3. Tell Claude the EditMode/PlayMode results — Claude writes the evidence doc
   (`production/qa/evidence/editor-pass-sprint-01.md`) and flips story
   statuses from the checklist outcome.

## Part E — CI license secrets (~10 min, one-time)

game-ci needs three GitHub secrets to run Unity headlessly. Personal license
flow (free):

1. **Generate the activation file (.alf)** on the machine where you installed
   Unity — run in a terminal (adjust the version folder to your installed
   patch):
   - **Windows (PowerShell):**
     ```
     & "C:\Program Files\Unity\Hub\Editor\6000.3.XfY\Editor\Unity.exe" -batchmode -createManualActivationFile -quit
     ```
   - **macOS:**
     ```
     /Applications/Unity/Hub/Editor/6000.3.XfY/Unity.app/Contents/MacOS/Unity -batchmode -createManualActivationFile -quit
     ```
   A file like `Unity_v6000.3.XfY.alf` appears in the folder you ran from.
2. **Convert it to a license (.ulf)** — go to
   <https://license.unity3d.com/manual>, sign in with the same Unity ID,
   upload the `.alf`, choose **Personal Edition** (and the "company revenue
   below the threshold" option), download the resulting `Unity_....ulf`.
3. **Add the three GitHub secrets** — on GitHub:
   repo **maliksharawi-prog/Claude-Code-Game-Studios → Settings →
   Secrets and variables → Actions → New repository secret**, create:
   | Name | Value |
   |---|---|
   | `UNITY_LICENSE` | the **entire text contents** of the `.ulf` file (open it in a text editor, select all, paste) |
   | `UNITY_EMAIL` | your Unity ID email |
   | `UNITY_PASSWORD` | your Unity ID password |
   All three are required for modern editors under game-ci v4.

## Part F — First CI run

The workflow (`.github/workflows/tests.yml`) triggers on **pushes to `main`
and PRs targeting `main`** — a push to the working branch alone does not run
it. Once Parts D + E are done, tell Claude — Claude will open the PR from
`claude/candy-crush-puzzle-game-ayp620` to `main` (with your go-ahead), watch
the run, and on the first green Edit+Play result close out E01-004 and flip
the In-Review stories to Complete via `/story-done`.

**Order note:** do the editor pass (B–D) before generating the `.alf` (E),
so the license matches the editor version CI will resolve from the committed
`ProjectVersion.txt`.
