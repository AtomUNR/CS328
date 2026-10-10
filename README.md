# CS328 — Unity Game Project

Team Unity game repository.

## Getting started
1. Install Unity Hub and GitHub Desktop.
2. Clone this repository in GitHub Desktop.
3. Once the Unity project files are committed, use Unity Hub > Add project from disk and select the directory containing `Assets/`, `Packages/`, and `ProjectSettings/`.
4. Use the Unity Editor version specified by `ProjectSettings/ProjectVersion.txt`.

## Files that should be committed
- `Assets/` (including `.meta` files)
- `Packages/` (including `manifest.json` and `packages-lock.json`)
- `ProjectSettings/`

The `.gitignore` excludes Unity-generated caches and editor files. Do not commit the `Library/` directory.

## Collaboration
1. Pull the latest `main` before starting.
2. Create a feature branch, for example `feature/player-movement`.
3. Save Unity scenes and assets, then commit and push your work.
4. Open a pull request for review before merging into `main`.
5. Coordinate edits to the same scenes and prefabs to minimize merge conflicts.

In Unity, configure Edit > Project Settings > Editor > Asset Serialization > Force Text, and use Visible Meta Files if your Unity version provides that setting. Consider Git LFS for large binary assets.

## Pending project upload
The Unity project folders and settings have not yet been verified in this repository. Add your existing local Unity project files to the repository root; do not create an empty replacement project just to populate these folders.

Update this README with gameplay, multiplayer setup, build instructions, and controls as the game develops.
