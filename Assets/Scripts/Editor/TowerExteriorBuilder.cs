#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put this file in Assets/Editor. No component needs to be attached.
// Coordinates below match the full, uncropped Mossy tower courtyard entrance image.
public sealed class TowerExteriorBuilder : EditorWindow
{
    private Sprite background;
    private float mapWidth = 20f;
    private Transform player;

    [MenuItem("Tools/Battle Tower/Build Tower Exterior")]
    private static void Open()
    {
        GetWindow<TowerExteriorBuilder>("Tower Exterior Builder");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Tower exterior with collisions", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the full, uncropped tower exterior sprite. Creates a new prefab and scene instance at the world origin. " +
            "Existing backgrounds are not changed. Disable the old background and its colliders first.", MessageType.Info);
        background = (Sprite)EditorGUILayout.ObjectField("Background Sprite", background, typeof(Sprite), false);
        mapWidth = EditorGUILayout.FloatField("Map Width (world units)", mapWidth);
        player = (Transform)EditorGUILayout.ObjectField("Existing Player (optional)", player, typeof(Transform), true);
        EditorGUILayout.HelpBox(
            "Width 20 gives a 20 x 30 map for the supplied 2:3 artwork. The optional player is moved to the spawn marker. " +
            "Markers do not spawn enemies or start battles. The entrance stays blocked until you connect victory logic.", MessageType.None);
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
        string path = EditorUtility.SaveFilePanelInProject("Save exterior prefab", "TowerExteriorMap", "prefab", "Choose a prefab location.");
        if (string.IsNullOrEmpty(path)) return;
        // Never overwrite a previous map, even if the same name was chosen.
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        GameObject root = new GameObject("TowerExteriorMap");
        Undo.RegisterCreatedObjectUndo(root, "Build tower exterior");
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
        // Image coordinates: (0,0) = top left, (1,1) = bottom right.
        Box(walls, "BottomBoundary", 0f, 0.99f, 1f, 1.03f, mapWidth, height);
        Box(walls, "LeftOuterBoundary", -0.03f, 0f, 0.01f, 1f, mapWidth, height);
        Box(walls, "RightOuterBoundary", 0.99f, 0f, 1.03f, 1f, mapWidth, height);
        // Solid upper tower, leaving a short doorway pocket open beneath it.
        Box(walls, "TowerUpperWall", 0f, -0.03f, 1f, 0.23f, mapWidth, height);
        Poly(walls, "TowerBaseLeft", mapWidth, height, new Vector2[] {
            new Vector2(0f,0f), new Vector2(.435f,0f), new Vector2(.435f,.295f),
            new Vector2(.36f,.285f), new Vector2(.23f,.265f), new Vector2(.12f,.235f), new Vector2(0f,.235f) });
        Poly(walls, "TowerBaseRight", mapWidth, height, new Vector2[] {
            new Vector2(.585f,0f), new Vector2(1f,0f), new Vector2(1f,.235f),
            new Vector2(.85f,.25f), new Vector2(.73f,.275f), new Vector2(.585f,.295f) });
        Poly(walls, "LeftForest", mapWidth, height, new Vector2[] {
            new Vector2(0f,0f), new Vector2(.13f,0f), new Vector2(.13f,.25f),
            new Vector2(.065f,.35f), new Vector2(.07f,.50f), new Vector2(.14f,.60f),
            new Vector2(.22f,.73f), new Vector2(.29f,.84f), new Vector2(.32f,1f), new Vector2(0f,1f) });
        Poly(walls, "RightForest", mapWidth, height, new Vector2[] {
            new Vector2(1f,0f), new Vector2(.88f,0f), new Vector2(.88f,.25f),
            new Vector2(.96f,.38f), new Vector2(.94f,.50f), new Vector2(.88f,.62f),
            new Vector2(.79f,.74f), new Vector2(.71f,.86f), new Vector2(.68f,1f), new Vector2(1f,1f) });
        // Approximate ground footprints, deliberately simpler than decorative silhouettes.
        Box(walls, "UpperLeftRuin", .09f,.308f,.31f,.352f,mapWidth,height);
        Box(walls, "UpperRightRuin", .80f,.334f,.94f,.383f,mapWidth,height);
        Box(walls, "ArchLeftFoot", .10f,.433f,.16f,.501f,mapWidth,height);
        Box(walls, "ArchRightFoot", .225f,.437f,.31f,.506f,mapWidth,height);
        Box(walls, "MiddleRightRuin", .69f,.505f,.86f,.547f,mapWidth,height);
        Box(walls, "LowerLeftRuin", .12f,.642f,.285f,.685f,mapWidth,height);
        Box(walls, "LowerRightRuin", .70f,.636f,.86f,.683f,mapWidth,height);
        Box(walls, "LeftPathEdging", .342f,.51f,.375f,.99f,mapWidth,height);
        Box(walls, "RightPathEdging", .635f,.55f,.672f,.99f,mapWidth,height);

        Transform gates = Child(root.transform, "Gates");
        Box(gates, "EntranceBlocker", .425f,.278f,.595f,.300f,mapWidth,height);
        Transform points = Child(root.transform, "PlacementMarkers");
        Marker(points, "PlayerSpawn", .51f,.93f,mapWidth,height);
        Marker(points, "GuardPosition", .51f,.325f,mapWidth,height);
        Marker(points, "EntranceDestinationMarker", .51f,.26f,mapWidth,height);

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
                  "Save your scene and test the collisions with your actual player collider.", root);
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
