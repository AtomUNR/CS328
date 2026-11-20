#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FlamethrowerBuildCheck
{
    private const string BuildResult = "Temp/FlamethrowerBuild.txt";
    private const string TestResult = "Temp/FlamethrowerCheck.txt";
    private static BattleManager manager;
    private static DodgePhaseController dodge;
    private static Monster enemy;
    private static Player player;
    private static FlamethrowerSweepPattern pattern;
    private static int stage;
    private static int initialHP;
    private static float started;
    private static float forwardAngle;
    private static int errors;

    static FlamethrowerBuildCheck()
    {
        EditorApplication.playModeStateChanged += OnPlay;
        if (!File.Exists(BuildResult) && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += Build;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void TextLines(SerializedObject so, string field, params string[] lines)
    {
        var array = so.FindProperty(field);
        array.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++) array.GetArrayElementAtIndex(i).stringValue = lines[i];
    }

    private static void Build()
    {
        try
        {
            const string artPath = "Assets/Art/Enemies/Flamethrower/Flamethrower.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(artPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 272;
            var metadata = new SpriteMetaData[8];
            for (int i = 0; i < metadata.Length; i++)
                metadata[i] = new SpriteMetaData { name = "Flamethrower_" + i, rect = new Rect(i * 272, 0, 272, 272),
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f };
            importer.spritesheet = metadata;
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Check(frames.Length == 8, "Character sheet must contain eight frames");
            Sprite[] flames = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Enemies/Boss/Fireball_Animation_Spritesheet_4x2.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var root = new GameObject("FlamethrowerSweepPattern", typeof(RectTransform), typeof(AudioSource), typeof(FlamethrowerSweepPattern));
            root.GetComponent<AudioSource>().playOnAwake = false;
            var attackPattern = root.GetComponent<FlamethrowerSweepPattern>();
            attackPattern.characterFrames = frames;
            attackPattern.fireFrames = flames;
            attackPattern.warningIcons = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/FX/Spear/WarningIcon_00.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/FX/Spear/WarningIcon_01.png")
            };
            attackPattern.warningChevron = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/FX/Spear/WarningChevron.png");
            attackPattern.warningSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Combat/Spear_Warning.wav");
            attackPattern.flamethrowerLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Combat/Flamethrower_Loop.wav");
            Check(attackPattern.flamethrowerLoop != null, "Flamethrower sound is missing");
            GameObject attackPrefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Attacks/FlamethrowerSweepPattern.prefab");
            UnityEngine.Object.DestroyImmediate(root);

            const string attackPath = "Assets/Data/Enemies/Flamethrower/FireSweep.asset";
            EnemyAttackData attack = AssetDatabase.LoadAssetAtPath<EnemyAttackData>(attackPath);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<EnemyAttackData>();
                AssetDatabase.CreateAsset(attack, attackPath);
            }
            var attackSo = new SerializedObject(attack);
            attackSo.FindProperty("attackName").stringValue = "Flamethrower Sweep";
            attackSo.FindProperty("patternPrefab").objectReferenceValue = attackPrefab;
            var animation = attackSo.FindProperty("animationFrames");
            animation.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) animation.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            attackSo.FindProperty("framesPerSecond").floatValue = 8;
            attackSo.FindProperty("loopAnimation").boolValue = false;
            attackSo.FindProperty("windUpDuration").floatValue = 0.5f;
            attackSo.FindProperty("activeDuration").floatValue = 10f;
            attackSo.FindProperty("recoveryDuration").floatValue = 0.5f;
            attackSo.FindProperty("damagePerHit").intValue = 4;
            attackSo.ApplyModifiedPropertiesWithoutUndo();

            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/First Level.unity");
            if (!scene.isLoaded || (SceneManager.sceneCount == 1 && !scene.isDirty))
                scene = EditorSceneManager.OpenScene("Assets/Scenes/First Level.unity", OpenSceneMode.Single);
            var allActors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Monster>(true)).ToArray();
            Monster bomb = allActors.Single(m => m.DisplayName == "Bomb Summoner");
            Monster flameEnemy = allActors.FirstOrDefault(m => m.DisplayName == "Flamethrower");
            if (flameEnemy == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(bomb.gameObject);
                clone.name = "Flamethrower";
                if (clone.scene != scene) SceneManager.MoveGameObjectToScene(clone, scene);
                flameEnemy = clone.GetComponent<Monster>();
            }
            var enemySo = new SerializedObject(flameEnemy);
            enemySo.FindProperty("monsterName").stringValue = "Flamethrower";
            enemySo.FindProperty("battleSprite").objectReferenceValue = frames[0];
            var attacks = enemySo.FindProperty("attacks");
            attacks.arraySize = 1;
            attacks.GetArrayElementAtIndex(0).objectReferenceValue = attack;
            TextLines(enemySo, "normalDialogue", "* The flamethrower opens the fuel valve.", "* A hiss warns of the next sweep.");
            TextLines(enemySo, "lowHealthDialogue", "* The flame sputters, but the tank is still hot.");
            TextLines(enemySo, "defeatedDialogue", "* The fuel valve snaps shut. The fire dies away.");
            enemySo.ApplyModifiedPropertiesWithoutUndo();
            var stats = new SerializedObject(flameEnemy.GetComponent<CharacterStats>());
            stats.FindProperty("maxHP").intValue = 20;
            stats.ApplyModifiedPropertiesWithoutUndo();
            flameEnemy.GetComponent<SpriteRenderer>().sprite = frames[0];
            Transform map = scene.GetRootGameObjects().Single(g => g.name == "TowerInteriorMap").transform;
            flameEnemy.transform.position = new Vector3(2f * map.position.x - bomb.transform.position.x, bomb.transform.position.y, bomb.transform.position.z);
            flameEnemy.transform.localScale = bomb.transform.localScale * 0.8f;
            BattleInteraction interaction = flameEnemy.GetComponent<BattleInteraction>();
            var bombInteraction = new SerializedObject(bomb.GetComponent<BattleInteraction>());
            UnityEngine.Object scenePlayer = bombInteraction.FindProperty("player").objectReferenceValue;
            UnityEngine.Object sceneManager = bombInteraction.FindProperty("battleManager").objectReferenceValue;
            Set(interaction, "monster", flameEnemy);
            Set(interaction, "player", null);
            Set(interaction, "battleManager", null);
            PrefabUtility.SaveAsPrefabAssetAndConnect(flameEnemy.gameObject, "Assets/Prefabs/Enemies/Flamethrower.prefab", InteractionMode.AutomatedAction);
            Set(interaction, "player", scenePlayer);
            Set(interaction, "battleManager", sceneManager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
            BossBarrier gate = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BossBarrier>(true)).Single();
            if (!gate.requiredEnemies.Contains(flameEnemy))
                gate.requiredEnemies = gate.requiredEnemies.Concat(new[] { flameEnemy }).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene), "Saving First Level failed");
            AssetDatabase.SaveAssets();
            File.WriteAllText(BuildResult, "PASS: eight-frame flamethrower enemy placed opposite bomb summoner, 20 HP, warned two-way fire stream, loop sound, original battle flow, four-enemy boss gate.");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            File.WriteAllText(BuildResult, error.ToString());
            Debug.LogException(error);
        }
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(BuildResult) || File.Exists(TestResult)) return;
        SceneManager.sceneLoaded += OnLevel;
        SceneManager.LoadScene("First Level");
    }

    private static void OnLevel(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "First Level") return;
        SceneManager.sceneLoaded -= OnLevel;
        try
        {
            var actors = UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
            enemy = actors.Single(m => m.DisplayName == "Flamethrower");
            Monster bomb = actors.Single(m => m.DisplayName == "Bomb Summoner");
            BossBarrier gate = UnityEngine.Object.FindFirstObjectByType<BossBarrier>();
            float center = gate.blocker.transform.parent.parent.position.x;
            Check(Mathf.Abs(enemy.transform.position.x + bomb.transform.position.x - 2f * center) < 0.02f, "Enemy is not opposite the bomb summoner");
            Check(enemy.Stats.MaxHP == 20 && gate.requiredEnemies.Length == 4 && gate.requiredEnemies.Contains(enemy), "Health or boss gate assignment failed");
            manager = gate.battleManager;
            dodge = UnityEngine.Object.FindFirstObjectByType<DodgePhaseController>();
            player = UnityEngine.Object.FindFirstObjectByType<Player>();
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            typeof(DodgePhaseController).GetField("moveSpeed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dodge, 0f);
            initialHP = player.Stats.CurrentHP;
            started = Time.time;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static object Field(string name) => typeof(FlamethrowerSweepPattern).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pattern);
    private static float CurrentAngle() => (float)typeof(FlamethrowerSweepPattern).GetMethod("Angle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pattern, new[] { Field("timer") });

    private static void Tick()
    {
        try
        {
            float elapsed = Time.time - started;
            if (stage == 0 && elapsed > 0.3f)
            {
                Check(manager.StartBattle(player, enemy), "Original battle manager could not start flamethrower encounter");
                typeof(BattleManager).GetMethod("EnemyAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                pattern = UnityEngine.Object.FindFirstObjectByType<FlamethrowerSweepPattern>();
                Check(pattern != null, "Flamethrower pattern was not spawned");
                started = Time.time;
                stage = 1;
            }
            else if (stage == 1 && elapsed > 0.9f)
            {
                Check(Field("phase").ToString() == "Warning" && player.Stats.CurrentHP == initialHP, "Warning deals damage or is missing");
                Check(!pattern.GetComponent<AudioSource>().isPlaying, "Flame roar starts before ignition");
                ScreenCapture.CaptureScreenshot("C:/Users/red2a/.codex/visualizations/2026/11/19/01a1f5a6-3fef-7742-9663-e99058e60b31/FlamethrowerWarning.png");
                stage = 2;
            }
            else if (stage == 2 && elapsed > 2.1f)
            {
                Check(Field("phase").ToString() == "Sweep", "Fire sweep did not start");
                Check(pattern.GetComponent<AudioSource>().isPlaying, "Flamethrower roar is silent");
                forwardAngle = CurrentAngle();
                stage = 3;
            }
            else if (stage == 3 && elapsed > 3.3f)
            {
                Check(CurrentAngle() > forwardAngle, "First sweep is not left to right");
                Check(player.Stats.CurrentHP < initialHP, "Continuous stream does not damage player");
                ScreenCapture.CaptureScreenshot("C:/Users/red2a/.codex/visualizations/2026/11/19/01a1f5a6-3fef-7742-9663-e99058e60b31/FlamethrowerSweep.png");
                stage = 4;
            }
            else if (stage == 4 && elapsed > 5.5f)
            {
                Check(Field("phase").ToString() == "Warning" && (int)Field("sweepIndex") == 1, "Reverse sweep lacks a warning");
                Check(!pattern.GetComponent<AudioSource>().isPlaying, "Flame roar continues through warning");
                stage = 5;
            }
            else if (stage == 5 && elapsed > 6.6f)
            {
                forwardAngle = CurrentAngle();
                stage = 6;
            }
            else if (stage == 6 && elapsed > 7.5f)
            {
                Check(CurrentAngle() < forwardAngle && (int)Field("sweepIndex") == 1, "Second sweep is not right to left");
                stage = 7;
            }
            else if (stage == 7 && elapsed > 11.2f)
            {
                string state = typeof(BattleManager).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager).ToString();
                Check(state == "ChoosingCommand" && UnityEngine.Object.FindFirstObjectByType<FlamethrowerSweepPattern>() == null, "Attack did not finish and clean up");
                typeof(BattleManager).GetMethod("EnemyAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                stage = 8;
            }
            else if (stage == 8 && elapsed > 11.5f)
            {
                manager.EndBattle();
                stage = 9;
            }
            else if (stage == 9 && elapsed > 11.8f)
            {
                Check(UnityEngine.Object.FindFirstObjectByType<FlamethrowerSweepPattern>() == null && errors == 0, "Cancellation cleanup or runtime errors failed: " + errors);
                Finish("PASS: eight-frame enemy opposite Bomb Summoner, 20 HP, four-enemy boss gate, original encounter flow, non-damaging warnings before both sweeps, left/right then right/left motion, actual stream damage, looping roar only during fire, automatic completion, cancellation cleanup, zero runtime errors.");
            }
            if (elapsed > 25f) throw new Exception("Validation timeout at stage " + stage);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Finish(string message)
    {
        File.WriteAllText(TestResult, message);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.isPlaying = false;
    }
}
#endif
