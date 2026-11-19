using UnityEngine;

public enum TowerSound { MenuMove, Confirm, Cancel, InventoryOpen, InventoryClose, Heal, CannotUse }

// One per gameplay scene. Music can be added separately later.
[DisallowMultipleComponent]
public sealed class BattleTowerAudio : MonoBehaviour
{
    public static BattleTowerAudio Instance { get; private set; }
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField] private AudioClip menuMove;
    [SerializeField] private AudioClip confirm;
    [SerializeField] private AudioClip cancel;
    [SerializeField] private AudioClip inventoryOpen;
    [SerializeField] private AudioClip inventoryClose;
    [SerializeField] private AudioClip heal;
    [SerializeField] private AudioClip cannotUse;
    private AudioSource source;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Only one BattleTowerAudio is needed per scene.", this);
            enabled = false;
            return;
        }
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    public static void Play(TowerSound sound)
    {
        if (Instance == null || !Instance.isActiveAndEnabled || Instance.source == null) return;
        AudioClip clip = Instance.ClipFor(sound);
        if (clip != null) Instance.source.PlayOneShot(clip, Instance.volume);
    }

    private AudioClip ClipFor(TowerSound sound)
    {
        switch (sound)
        {
            case TowerSound.MenuMove: return menuMove;
            case TowerSound.Confirm: return confirm;
            case TowerSound.Cancel: return cancel;
            case TowerSound.InventoryOpen: return inventoryOpen;
            case TowerSound.InventoryClose: return inventoryClose;
            case TowerSound.Heal: return heal;
            case TowerSound.CannotUse: return cannotUse;
            default: return null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
