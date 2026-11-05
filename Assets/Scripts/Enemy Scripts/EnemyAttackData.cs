using UnityEngine;

[CreateAssetMenu(
    fileName = "NewEnemyAttack",
    menuName = "Battle Tower/Enemy Attack")]
public class EnemyAttackData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string attackName = "New Attack";

    [Header("Enemy animation")]
    [Tooltip("Frames displayed on the battle HUD's enemy image.")]
    [SerializeField] private Sprite[] animationFrames;

    [SerializeField, Min(1f)]
    private float framesPerSecond = 8f;

    [SerializeField]
    private bool loopAnimation = true;

    [Header("Attack timing")]
    [Tooltip("Warning time before damaging projectiles begin.")]
    [SerializeField, Min(0f)]
    private float windUpDuration = 0.75f;

    [Tooltip("How long the projectile pattern runs.")]
    [SerializeField, Min(0.1f)]
    private float activeDuration = 6f;

    [Tooltip("Pause after projectiles are cleared.")]
    [SerializeField, Min(0f)]
    private float recoveryDuration = 0.5f;

    [Header("Damage")]
    [SerializeField, Min(1)]
    private int damagePerHit = 5;

    [Header("Attack pattern")]
    [Tooltip("The prefab spawned inside the dodge arena for this attack.")]
    [SerializeField] private GameObject patternPrefab;

    public string AttackName => attackName;
    public int FrameCount =>
        animationFrames == null ? 0 : animationFrames.Length;

    public float FramesPerSecond => framesPerSecond;
    public bool LoopAnimation => loopAnimation;

    public float WindUpDuration => windUpDuration;
    public float ActiveDuration => activeDuration;
    public float RecoveryDuration => recoveryDuration;

    public int DamagePerHit => damagePerHit;
    public GameObject PatternPrefab => patternPrefab;

    public Sprite GetAnimationFrame(int index)
    {
        if (animationFrames == null ||
            index < 0 ||
            index >= animationFrames.Length)
        {
            return null;
        }

        return animationFrames[index];
    }
}