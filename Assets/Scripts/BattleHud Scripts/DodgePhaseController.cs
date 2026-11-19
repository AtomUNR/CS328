using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DodgePhaseController : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] private GameObject dodgePanel;
    [SerializeField] private RectTransform arenaArea;
    [SerializeField] private RectTransform essence;

    [Header("Enemy animation")]
    [SerializeField] private Image enemyImage;

    [Header("Movement")]
    [SerializeField, Min(1f)] private float moveSpeed = 300f;

    [Header("Damage protection")]
    [SerializeField, Min(0.1f)]
    private float invulnerabilityDuration = 0.75f;

    public event Action PhaseFinished;

    public Image EnemyImage => enemyImage;

    private EnemyAttackData currentAttack;
    private CharacterStats playerStats;
    private EnemyAttackPattern activePattern;

    private Image essenceImage;
    private Color normalEssenceColor;
    private Sprite restingSprite;

    private float elapsed;
    private float invulnerabilityRemaining;
    private bool isRunning;

    private void Awake()
    {
        if (dodgePanel != null)
            dodgePanel.SetActive(false);
    }

    public bool BeginPhase(
        EnemyAttackData attack,
        Sprite normalSprite,
        CharacterStats stats)
    {
        if (isRunning)
            return false;

        if (attack == null || stats == null ||
            dodgePanel == null || arenaArea == null ||
            essence == null)
        {
            Debug.LogError(
                "Assign the attack, player stats, and arena references.",
                this
            );
            return false;
        }

        if (essence.parent != arenaArea)
        {
            Debug.LogError(
                "Essence must be a direct child of ArenaArea.",
                this
            );
            return false;
        }

        if (attack.PatternPrefab == null ||
            attack.PatternPrefab.GetComponent<EnemyAttackPattern>()
                == null ||
            attack.PatternPrefab.GetComponent<RectTransform>() == null)
        {
            Debug.LogError(
                "The attack needs a pattern prefab with a " +
                "RectTransform and an EnemyAttackPattern on its root.",
                attack
            );
            return false;
        }

        currentAttack = attack;
        playerStats = stats;
        restingSprite = normalSprite;

        elapsed = 0f;
        invulnerabilityRemaining = 0f;

        dodgePanel.SetActive(true);
        Canvas.ForceUpdateCanvases();

        essence.anchorMin = new Vector2(0.5f, 0.5f);
        essence.anchorMax = new Vector2(0.5f, 0.5f);
        essence.pivot = new Vector2(0.5f, 0.5f);
        essence.anchoredPosition = Vector2.zero;

        essenceImage = essence.GetComponent<Image>();

        if (essenceImage != null)
            normalEssenceColor = essenceImage.color;

        GameObject patternObject = Instantiate(
            attack.PatternPrefab,
            arenaArea,
            false
        );

        RectTransform patternRect =
            patternObject.GetComponent<RectTransform>();

        patternRect.anchorMin = Vector2.zero;
        patternRect.anchorMax = Vector2.one;
        patternRect.pivot = new Vector2(0.5f, 0.5f);
        patternRect.offsetMin = Vector2.zero;
        patternRect.offsetMax = Vector2.zero;
        patternRect.localScale = Vector3.one;
        patternRect.localRotation = Quaternion.identity;

        patternObject.SetActive(true);

        activePattern =
            patternObject.GetComponent<EnemyAttackPattern>();

        activePattern.Initialize(this, arenaArea);

        // Draw the essence above projectiles.
        essence.SetAsLastSibling();

        isRunning = true;
        UpdateEnemyAnimation();
        return true;
    }

    private void Update()
    {
        if (!isRunning)
            return;

        if (playerStats == null)
        {
            FinishPhase();
            return;
        }

        if (playerStats.CurrentHP <= 0)
        {
            FinishPhase();
            return;
        }

        float deltaTime = Time.deltaTime;
        float previousElapsed = elapsed;

        Vector2 essenceBefore = essence.anchoredPosition;
        MoveEssence(deltaTime);
        Vector2 essenceAfter = essence.anchoredPosition;

        invulnerabilityRemaining = Mathf.Max(
            0f,
            invulnerabilityRemaining - deltaTime
        );

        elapsed += deltaTime;

        float activeStart = currentAttack.WindUpDuration;
        float activeEnd =
            activeStart + currentAttack.ActiveDuration;

        // Only simulate the portion of this frame inside the
        // active attack window.
        float overlapStart = Mathf.Max(previousElapsed, activeStart);
        float overlapEnd = Mathf.Min(elapsed, activeEnd);
        float activeDelta = Mathf.Max(0f, overlapEnd - overlapStart);

        if (activePattern != null && activeDelta > 0f)
        {
            float startFraction = deltaTime > 0f
                ? (overlapStart - previousElapsed) / deltaTime
                : 0f;

            float endFraction = deltaTime > 0f
                ? (overlapEnd - previousElapsed) / deltaTime
                : 1f;

            float essenceRadius = Mathf.Min(
                essence.rect.width,
                essence.rect.height
            ) * 0.5f;

            activePattern.Tick(
                activeDelta,
                Vector2.Lerp(
                    essenceBefore, essenceAfter, startFraction),
                Vector2.Lerp(
                    essenceBefore, essenceAfter, endFraction),
                essenceRadius
            );
        }

        if (playerStats.CurrentHP <= 0)
        {
            FinishPhase();
            return;
        }

        // Clear all hazards when recovery begins.
        if (elapsed >= activeEnd)
            ClearPattern();

        UpdateEssenceFlash();
        UpdateEnemyAnimation();

        if (elapsed >= activeEnd + currentAttack.RecoveryDuration)
            FinishPhase();
    }

    private void MoveEssence(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 direction = Vector2.zero;

        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed)
                direction.x -= 1f;

            if (keyboard.rightArrowKey.isPressed)
                direction.x += 1f;

            if (keyboard.downArrowKey.isPressed)
                direction.y -= 1f;

            if (keyboard.upArrowKey.isPressed)
                direction.y += 1f;
        }

        direction = Vector2.ClampMagnitude(direction, 1f);

        Vector2 position = essence.anchoredPosition;
        position += direction * moveSpeed * deltaTime;

        float halfWidth = Mathf.Max(
            0f,
            (arenaArea.rect.width - essence.rect.width) * 0.5f
        );

        float halfHeight = Mathf.Max(
            0f,
            (arenaArea.rect.height - essence.rect.height) * 0.5f
        );

        position.x = Mathf.Clamp(position.x, -halfWidth, halfWidth);
        position.y = Mathf.Clamp(position.y, -halfHeight, halfHeight);

        essence.anchoredPosition = position;
    }

    public bool TryTakeHit()
    {
        if (!isRunning || playerStats == null ||
            currentAttack == null || playerStats.CurrentHP <= 0 ||
            invulnerabilityRemaining > 0f)
        {
            return false;
        }

        float activeEnd =
            currentAttack.WindUpDuration + currentAttack.ActiveDuration;

        if (elapsed < currentAttack.WindUpDuration ||
            elapsed > activeEnd)
        {
            return false;
        }

        playerStats.TakeDamage(currentAttack.DamagePerHit);

        invulnerabilityRemaining =
            Mathf.Max(0.1f, invulnerabilityDuration);

        return true;
    }

    private void UpdateEssenceFlash()
    {
        if (essenceImage == null)
            return;

        Color color = normalEssenceColor;

        if (invulnerabilityRemaining > 0f)
        {
            bool dim = Mathf.FloorToInt(elapsed * 12f) % 2 == 0;
            color.a = dim ? normalEssenceColor.a * 0.25f
                          : normalEssenceColor.a;
        }

        essenceImage.color = color;
    }

    private void UpdateEnemyAnimation()
    {
        if (enemyImage == null || currentAttack == null)
            return;

        // A synchronized pattern owns the sprite until ClearPattern restores it.
        if (activePattern != null && activePattern.ControlsEnemyAnimation)
            return;

        float activeEnd =
            currentAttack.WindUpDuration + currentAttack.ActiveDuration;

        if (elapsed >= activeEnd || currentAttack.FrameCount == 0)
        {
            SetEnemySprite(restingSprite);
            return;
        }

        int frame = Mathf.FloorToInt(
            elapsed * Mathf.Max(1f, currentAttack.FramesPerSecond)
        );

        if (currentAttack.LoopAnimation)
            frame %= currentAttack.FrameCount;
        else
            frame = Mathf.Min(frame, currentAttack.FrameCount - 1);

        Sprite sprite = currentAttack.GetAnimationFrame(frame);
        SetEnemySprite(sprite != null ? sprite : restingSprite);
    }

    private void SetEnemySprite(Sprite sprite)
    {
        if (enemyImage == null)
            return;

        enemyImage.sprite = sprite;
        enemyImage.enabled = sprite != null;
    }

    private void ClearPattern()
    {
        if (activePattern == null)
            return;

        activePattern.ClearProjectiles();
        activePattern.gameObject.SetActive(false);
        Destroy(activePattern.gameObject);
        activePattern = null;
    }

    private void FinishPhase()
    {
        CancelPhase();
        PhaseFinished?.Invoke();
    }

    public void CancelPhase()
    {
        bool wasRunning = isRunning;
        isRunning = false;

        ClearPattern();

        if (wasRunning)
        {
            SetEnemySprite(restingSprite);

            if (essenceImage != null)
                essenceImage.color = normalEssenceColor;
        }

        currentAttack = null;
        playerStats = null;

        if (dodgePanel != null)
            dodgePanel.SetActive(false);
    }

    private void OnDisable()
    {
        CancelPhase();
    }
}