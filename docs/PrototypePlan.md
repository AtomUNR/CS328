# CS328 — Playable Prototype Plan

## Concept
A 3D cooperative multiplayer horror game exploring sound, memory, and survival. Players communicate, explore threatening spaces, investigate audio clues, and solve objectives while avoiding dangerous encounters. Working title is not finalized.

## First playable milestone (single-player development slice)
A small first-person test environment in which a player can:
- Walk and look around with keyboard and mouse.
- Sprint and crouch.
- Interact with an object (such as a door or an audio recording).
- Hear an audio clue with spatial audio and see basic interaction feedback.
- Restart or exit the prototype.

No art polish or networking is required for this first milestone.

## Later milestones
1. Cooperative session creation and joining, networked movement and interaction.
2. Sound and memory mechanics: audio clues and corresponding puzzles.
3. Sound-reactive enemy behavior, survival objectives, and win/lose states.
4. Playtesting, accessibility, performance, and content polish.

## Unity project setup
1. Install Unity Hub and choose a stable supported Unity Editor version as a team.
2. Create a **Universal 3D (URP)** project using Unity Hub. Prefer placing the entire Unity project at the root of this repository, containing `Assets/`, `Packages/`, and `ProjectSettings/` (avoid unnecessary nesting).
3. In **Edit > Project Settings > Editor**, select **Force Text** asset serialization. Keep `.meta` files.
4. Verify the existing Unity `.gitignore` before committing; avoid committing `Library/` and other generated files.
5. Save a basic scene such as `Assets/Scenes/Prototype.unity`.
6. Commit `Assets/`, `Packages/`, `ProjectSettings/`, and any newly created tracked configuration on `main` using GitHub Desktop.

## Initial scene (suggested)
- A floor, four walls, a light source, and a first-person player.
- One interactable test object.
- A distinctive spatial audio source or recorded clue.
- No external paid assets required.

## Decisions to make during implementation
- Exact Unity Editor version (commit `ProjectSettings/ProjectVersion.txt`).
- Multiplayer framework (evaluate Netcode for GameObjects once movement prototype works).
- Final game title and narrative.
- How sound and memory clues influence puzzles and enemies.

## Team safety
- Coordinate ownership of shared scenes and prefabs.
- Pull before work and commit small changes with descriptive messages.
- Use Git LFS where large binary assets justify it.
- Test merges and gameplay frequently.

This plan documents intended work; it does not replace a Unity-generated project.
