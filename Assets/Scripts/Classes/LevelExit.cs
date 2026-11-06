using UnityEngine;

/// <summary>Loads another scene when the Player walks into this trigger.</summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "First Floor";
    [SerializeField, Min(0f)] private float fadeDuration = 0.6f;
    private bool used;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || other.GetComponentInParent<Player>() == null) return;
        used = true;
        SceneTransition.Load(sceneToLoad, fadeDuration);
    }
}
