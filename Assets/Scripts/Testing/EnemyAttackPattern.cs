using UnityEngine;

public abstract class EnemyAttackPattern : MonoBehaviour
{
    // Patterns may synchronize enemy poses to their own hazard timing.
    public virtual bool ControlsEnemyAnimation => false;

    public abstract void Initialize(
        DodgePhaseController controller,
        RectTransform arenaArea);

    public abstract void Tick(
        float deltaTime,
        Vector2 essenceBefore,
        Vector2 essenceAfter,
        float essenceRadius);

    public abstract void ClearProjectiles();
}