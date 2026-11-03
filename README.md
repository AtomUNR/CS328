# Battle Tower

**Battle Tower** is a 2D pixel-art game. The project is in active development and currently includes a keyboard-operated main menu, its scenes, input actions, and supporting scripts.

## Current project

- `Assets/Scenes/Main Menu.unity` — title screen with Begin, Settings, and Quit options.
- `Assets/Scenes/Battle Tower.unity` — the game scene opened from the menu.
- `Assets/Scripts/` — menu and scene behavior.
- `Assets/Input Settings/Battle Tower Controls.inputactions` — keyboard controls for the menu.

The build scene list starts with the main menu, followed by the Battle Tower scene.

## Controls

| Key | Action |
| --- | --- |
| Arrow keys | Navigate menus |
| Z | Select / confirm |
| X | Back / cancel |
| C | Inventory (planned) |

## Team Unity setup

Use the same Unity Editor version and committed package lockfile so everyone opens the project with matching dependencies.

| Component | Version / setting |
| --- | --- |
| Unity Editor | **6000.6.0f1** |
| Render pipeline | Universal Render Pipeline **17.6.0** |
| Input System | **1.20.1** |
| Active Input Handling | Input System Package (New) |
| Asset serialization | Force Text |

## Clone and open the game

1. Install Unity Hub and Unity Editor **6000.6.0f1**.
2. Clone `AtomUNR/CS328` using GitHub Desktop.
3. In Unity Hub, choose **Add project from disk** and select the cloned folder containing `Assets/`, `Packages/`, and `ProjectSettings/`.
4. Allow Unity to restore packages from `Packages/manifest.json` and `Packages/packages-lock.json`.

## What to commit

Commit `Assets/` (including Unity `.meta` files), `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/`, source code, and documentation.

Do not commit generated `Library/`, `Temp/`, `Logs/`, `Obj/`, or build folders. The root `.gitignore` excludes these and generated IDE files.

## Team workflow

1. Pull the latest `main` before starting work.
2. Use a feature branch for a change and open a pull request when it is ready for review.
3. Coordinate edits to scenes and prefabs to reduce merge conflicts.
4. Commit descriptive changes and verify the project opens in Unity after pulling.

## Next milestone

Continue the gameplay loop and add the planned inventory control.
