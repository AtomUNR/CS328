# COLD METAL

**COLD METAL** is a 2D, top-down pixel-art RPG set in a dark, snowy industrial office facility under attack by an invading robotic army. The protagonist is an office worker; the current character art is a placeholder. Exploration and separate turn-based battle screens are planned, with keyboard-only controls.

## Team development versions

All contributors should use the exact same Unity Editor version and shared Unity package lockfile.

| Component | Version / setting | Verification |
| --- | --- | --- |
| Unity Editor | **6000.6.0f1** | Reported from Unity Hub; confirm after uploading `ProjectSettings/ProjectVersion.txt` |
| Unity project template | **Universal 2D (URP)** | Selected when creating the project |
| Input System | **1.20.1** | User reports successful package update; verify in `Packages/manifest.json` |
| Active Input Handling | **Input System Package (New)** | Confirmed in Unity Player settings |
| Asset serialization | **Force Text** | **To verify** in Edit > Project Settings > Editor |
| Version control | Git + GitHub Desktop | Repository `AtomUNR/CS328` |

**Important:** The Unity project itself has not yet been uploaded to GitHub. Versions above reflect the local setup reported during initialization; they cannot be independently validated until the project files are committed.

## Game structure (planned)

- `MainMenu`: keyboard-operated menu.
- `BreakRoom`: first playable area and tutorial.
- Exploration areas: connected office rooms and industrial facility floors.
- `BattleScene`: reusable separate combat screen for robots and bosses.

A scene per individual room or enemy is not required.

## Clone / open the game

1. Install Unity Hub, GitHub Desktop, and **Unity Editor 6000.6.0f1**.
2. Clone `AtomUNR/CS328` through GitHub Desktop.
3. Once the Unity project files are committed, open the cloned folder in Unity Hub via **Add project from disk** (the folder containing `Assets/`, `Packages/`, and `ProjectSettings/`).
4. Unity will restore its dependencies from the committed `Packages/manifest.json` and `Packages/packages-lock.json`.

## What to commit

- `Assets/`, including accompanying `.meta` files
- `Packages/manifest.json` and `Packages/packages-lock.json`
- `ProjectSettings/`
- Documentation and source code

Never commit generated `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, or `Builds/`. The root `.gitignore` excludes these.

## Team workflow

1. Pull latest `main` before starting work.
2. Coordinate edits to scenes and prefabs to avoid conflicts.
3. Commit descriptive changes and push your work.
4. Use feature branches and pull requests for review when appropriate.
5. Verify that teammates can open and run the project before adding significant content.

## Next milestone

Upload the newly created **COLD METAL** Unity project, verify editor/package versions directly from committed files, then implement the keyboard-driven main menu and its transition to the break room.
