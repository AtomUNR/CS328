#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put this file in Assets/Editor. No component needs to be attached.
// Coordinates below match the full, uncropped Wizard King chamber image.
public sealed class TowerInteriorBuilder : EditorWindow
{
    private Sprite background;
    private float mapWidth = 20f;
    private Transform player;

    [MenuItem("Tools/Battle Tower/Build Tower Interior")]
    private static void Open()
    {
        GetWindow<TowerInteriorBuilder>("Tower Interior Builder");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tower interior with collisions", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the full, uncropped tower interior sprite. Creates a new prefab and scene instance at the world position (40, 0, 0). " +
            "Existing backgrounds are not changed. Disable the old background and its colliders first.", MessageType.Info);
        background = (Sprite)EditorGUILayout.ObjectField("Background Sprite", background, typeof(Sprite), false);
        mapWidth = EditorGUILayout.FloatField("Map Width (world units)", mapWidth);
        player = (Transform)EditorGUILayout.ObjectField("Existing Player (optional)", player, typeof(Transform), true);
        EditorGUILayout.HelpBox(
            "Width 20 gives a 20 x 30 map for the supplied 2:3 artwork. The optional player is moved to the spawn marker. " +
            "Markers do not spawn enemies or start battles. The entrance blocker prevents walking out. Spike areas are inactive triggers until hazard scripts are added.", MessageType.None);
        bool invalidPlayer = player != null && (EditorUtility.IsPersistent(player) || !player.gameObject.scene.IsValid());
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || background == null ||
                                           !float.IsFinite(mapWidth) || mapWidth < 5f || invalidPlayer))
        {
            if (GUILayout.Button("Build Map Prefab")) Build();
        }
    }

    private void Build()
    {
        if (background.rect.width <= 0 || background.bounds.size.x <= 0) return;
        float height = mapWidth * background.rect.height / background.rect.width;
        string path = EditorUtility.SaveFilePanelInProject("Save interior prefab", "TowerInteriorMap", "prefab", "Choose a prefab location.");
        if (string.IsNullOrEmpty(path)) return;
        // Never overwrite a previous map, even if the same name was chosen.
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        GameObject root = new GameObject("TowerInteriorMap");
        root.transform.position = new Vector3(40f, 0f, 0f);
        Undo.RegisterCreatedObjectUndo(root, "Build tower interior");
        Transform visual = Child(root.transform, "Background");
        SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = background;
        renderer.sortingOrder = -100;
        // Unlit material keeps the background visible without requiring a 2D light.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.ChangeExtension(path, null) + "_Unlit.mat");
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
            renderer.sharedMaterial = material;
        }
        float scale = mapWidth / background.bounds.size.x;
        visual.localScale = new Vector3(scale, scale, 1f);
        Vector3 center = background.bounds.center;
        visual.localPosition = new Vector3(-center.x * scale, -center.y * scale, 0f);

        Transform walls = Child(root.transform, "Collisions");
        // Coordinates trace the walkable floor of the FULL 1024 x 1536 chamber image.
        // Ivory lines on the floor are decoration, not walls.
        Box(walls, "NorthWall", 0f, 0f, 1f, .17f, mapWidth, height);
        Poly(walls, "WestWall", mapWidth, height, new Vector2[] {
            new Vector2(0f,0f), new Vector2(.17f,0f),
            new Vector2(.17f,.17f), new Vector2(.10f,.24f),
            new Vector2(.10f,.30f), new Vector2(.20f,.36f),
            new Vector2(.20f,.55f), new Vector2(.16f,.60f),
            new Vector2(.16f,.68f), new Vector2(.25f,.76f),
            new Vector2(.41f,.82f), new Vector2(.41f,1.02f), new Vector2(0f,1.02f) });
        Poly(walls, "EastWall", mapWidth, height, new Vector2[] {
            new Vector2(1f,0f), new Vector2(.83f,0f),
            new Vector2(.83f,.17f), new Vector2(.90f,.24f),
            new Vector2(.90f,.30f), new Vector2(.80f,.36f),
            new Vector2(.80f,.55f), new Vector2(.84f,.60f),
            new Vector2(.84f,.68f), new Vector2(.75f,.76f),
            new Vector2(.56f,.82f), new Vector2(.56f,1.02f), new Vector2(1f,1.02f) });

        Transform gates = Child(root.transform, "Gates");
        Box(gates, "EntranceBlocker", .40f,.965f,.57f,.99f,mapWidth,height);
        Transform points = Child(root.transform, "PlacementMarkers");
        Marker(points, "PlayerSpawn", .485f,.91f,mapWidth,height);
        Marker(points, "BossPosition", .50f,.245f,mapWidth,height);
        Marker(points, "BossApproach", .50f,.37f,mapWidth,height);

        Transform hazards = Child(root.transform, "SpikeTraps");
        Box(hazards, "SpikeTrap_Lower", .32f,.67f,.51f,.71f,mapWidth,height);
        Box(hazards, "SpikeTrap_Middle", .47f,.555f,.67f,.596f,mapWidth,height);
        Box(hazards, "SpikeTrap_Upper", .32f,.439f,.51f,.478f,mapWidth,height);
        foreach (BoxCollider2D area in hazards.GetComponentsInChildren<BoxCollider2D>())
        {
            area.isTrigger = true;
            // Setup only: enable via the future spike trap controller when spikes are raised.
            area.enabled = false;
            Child(area.transform, "SpikeVisual");
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction, out bool success);
        if (!success)
        {
            Debug.LogError("Prefab save failed. The built map remains in the scene for inspection.");
            return;
        }
        if (player != null)
        {
            Undo.RecordObject(player, "Move player to map spawn");
            Vector3 spawn = points.Find("PlayerSpawn").position;
            player.position = new Vector3(spawn.x, spawn.y, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Created " + path + ". Assign Background to CameraFollow's Map Background field. " +
                  "For testing, move the player to PlayerSpawn and switch the camera map reference. " +
                  "Spike areas do not deal damage yet. Save your scene and test wall collisions.", root);
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Vector2 Point(float x, float y, float width, float height)
    {
        return new Vector2((x - .5f) * width, (.5f - y) * height);
    }

    private static void Marker(Transform parent, string name, float x, float y, float width, float height)
    {
        Child(parent, name).localPosition = Point(x, y, width, height);
    }

    private static void Box(Transform parent, string name, float left, float top, float right, float bottom,
                            float width, float height)
    {
        Transform child = Child(parent, name);
        child.localPosition = Point((left + right) * .5f, (top + bottom) * .5f, width, height);
        BoxCollider2D collider = child.gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2((right - left) * width, (bottom - top) * height);
        collider.isTrigger = false;
    }

    private static void Poly(Transform parent, string name, float width, float height, Vector2[] normalized)
    {
        PolygonCollider2D collider = Child(parent, name).gameObject.AddComponent<PolygonCollider2D>();
        Vector2[] vertices = new Vector2[normalized.Length];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = Point(normalized[i].x, normalized[i].y, width, height);
        collider.pathCount = 1;
        collider.SetPath(0, vertices);
        collider.isTrigger = false;
    }
}
#endif
