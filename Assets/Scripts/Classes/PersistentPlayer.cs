
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
 
/// <summary>
/// Added at runtime by SceneTransition (nothing to attach by hand).
/// Keeps the existing Player alive for exactly one hop to the next scene so HP, XP, money
/// and inventory survive. On arrival it:
///   - disables/destroys any second Player placed in the new scene,
///   - points BattleInteraction.player and CameraFollow.target at the carried Player.
/// If any other scene is loaded later (Game Over, Main Menu, retry) the carried Player is
/// destroyed, so a retry starts fresh with the scene's own Player.
/// </summary>
public class PersistentPlayer : MonoBehaviour
{
    private Player player;
    private string expectedScene;
 
    public static void Keep(Player target, string nextScene)
    {
        var pp = target.GetComponent<PersistentPlayer>();
        if (pp == null) pp = target.gameObject.AddComponent<PersistentPlayer>();
        pp.player = target;
        pp.expectedScene = nextScene;
        DontDestroyOnLoad(target.transform.root.gameObject);
        SceneManager.sceneLoaded -= pp.OnSceneLoaded;
        SceneManager.sceneLoaded += pp.OnSceneLoaded;
    }
 
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
 
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (player == null) return;
 
        if (expectedScene == null || scene.name != expectedScene)
        {
            // Some other load (game over, menu, retry): don't carry the Player any further.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Destroy(player.transform.root.gameObject);
            return;
        }
        expectedScene = null;   // arrival handled; any later load not via SceneTransition ends the hop
 
        foreach (Player other in FindObjectsByType<Player>())
        {
            if (other == player) continue;
            other.gameObject.SetActive(false);   // hidden now so nothing else finds it
            Destroy(other.gameObject);
        }
 
        foreach (BattleInteraction bi in FindObjectsByType<BattleInteraction>(FindObjectsInactive.Include))
            SetField(bi, "player", player);
        foreach (CameraFollow cf in FindObjectsByType<CameraFollow>(FindObjectsInactive.Include))
            SetField(cf, "target", player.transform);
    }
 
    // Their fields are private [SerializeField]s; this avoids editing their scripts.
    private static void SetField(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null && f.FieldType.IsAssignableFrom(value.GetType())) f.SetValue(target, value);
        else Debug.LogWarning("PersistentPlayer: couldn't set '" + field + "' on " + target.GetType().Name + " (field renamed?).");
    }
}