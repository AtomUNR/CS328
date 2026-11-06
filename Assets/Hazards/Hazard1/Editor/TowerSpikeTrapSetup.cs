#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TowerSpikeTrapSetup : EditorWindow
{
    private Transform interior;
    private CharacterStats player;
    private Collider2D playerCollider;
    private MonoBehaviour movement;
    private string deathScene = "Game Over";

    [MenuItem("Tools/Battle Tower/Set Up Interior Spikes")]
    private static void Open() { GetWindow<TowerSpikeTrapSetup>("Interior Spikes"); }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Choose your scene's TowerInteriorMap and player components. This updates the three existing trap areas without rebuilding the map. Player references remain scene-instance overrides.", MessageType.Info);
        interior = (Transform)EditorGUILayout.ObjectField("Interior Map", interior, typeof(Transform), true);
        player = (CharacterStats)EditorGUILayout.ObjectField("Player Stats", player, typeof(CharacterStats), true);
        playerCollider = (Collider2D)EditorGUILayout.ObjectField("Player Body Collider", playerCollider, typeof(Collider2D), true);
        movement = (MonoBehaviour)EditorGUILayout.ObjectField("Player Movement", movement, typeof(MonoBehaviour), true);
        deathScene = EditorGUILayout.TextField("Death Scene Name", deathScene);
        EditorGUILayout.HelpBox("Assign the player's physical collider, not an interaction trigger. Death Scene Name must match your existing death scene. Leave blank only if you will wire On Player Defeated manually.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
            interior == null || player == null || playerCollider == null || movement == null))
        {
            if (GUILayout.Button("Set Up Three Spike Traps")) Setup();
        }
    }

    private void Setup()
    {
        if (EditorUtility.IsPersistent(interior) || EditorUtility.IsPersistent(player) ||
            !interior.gameObject.scene.IsValid() || !player.gameObject.scene.IsValid())
        { Debug.LogError("Select scene objects from the Hierarchy, not prefab assets."); return; }
        if (playerCollider.GetComponentInParent<CharacterStats>() != player ||
            movement.GetComponentInParent<CharacterStats>() != player || playerCollider.isTrigger)
        { Debug.LogError("Assign the same player's stats, movement component, and non-trigger physical collider."); return; }

        Transform hazards = interior.Find("SpikeTraps");
        string[] names = { "SpikeTrap_Lower", "SpikeTrap_Middle", "SpikeTrap_Upper" };
        if (hazards == null) { Debug.LogError("This map needs the SpikeTraps group created by TowerInteriorBuilder."); return; }
        foreach (string trapName in names)
            if (hazards.Find(trapName) == null || hazards.Find(trapName).GetComponent<BoxCollider2D>() == null)
            { Debug.LogError("Missing trap area: " + trapName); return; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up tower spikes");
        for (int i = 0; i < names.Length; i++)
        {
            GameObject trapObject = hazards.Find(names[i]).gameObject;
            TowerSpikeTrap trap = trapObject.GetComponent<TowerSpikeTrap>();
            if (trap == null) trap = Undo.AddComponent<TowerSpikeTrap>(trapObject);
            Undo.RecordObject(trap, "Configure spike trap");
            trap.playerStats = player;
            trap.playerCollider = playerCollider;
            trap.playerMovement = movement;
            trap.damage = 3;
            trap.safeDuration = 2f;
            trap.warningDuration = 0.7f;
            trap.raisedDuration = 1f;
            trap.startDelay = i * 1.25f;
            trap.deathSceneName = deathScene;
            trap.enabled = true;
            BoxCollider2D area = trapObject.GetComponent<BoxCollider2D>();
            Undo.RecordObject(area, "Enable spike trigger");
            area.enabled = true;
            area.isTrigger = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trap);
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);
        Debug.Log("Three traps configured. Save the scene. Visuals appear in Play mode. Test safe, warning, raised, stationary damage, battle pause, and death.", interior);
    }
}
#endif
