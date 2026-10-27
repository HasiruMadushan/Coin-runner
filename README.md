# Coin Runner

Lab Sheet 02: a Unity 2D platformer. Run with ← → or A / D, jump with Space, collect coins, avoid spikes and gaps, and reach the flag.

## Files
- `Assets/Scripts/`: `PlayerMovement.cs`, `PlayerInteractions.cs`, `CameraFollow.cs`, `GameManager.cs`. This is the lab code, plus extra challenges 1, 3 and 4.
- `Assets/Editor/CoinRunnerBuilder.cs`: builds the scene for lab Parts 1–9 (the Tilemap level, Player, coins, spikes, goal, UI and wiring).

## Rebuild the project
1. In Unity Hub, create a new **Universal 2D** project with Unity 6000.6.6f1. 6000.3 LTS also works.
2. Copy `Assets/Scripts` and `Assets/Editor` from this repo into the new project's `Assets` folder.
3. Go to **Window → TextMeshPro → Import TMP Essential Resources**.
4. Go to **Edit → Project Settings → Player → Active Input Handling** and set it to **Both**. Let Unity restart.
5. Choose **Coin Runner → Build Level**, open `Assets/Scenes/SampleScene`, and press **Play**.
