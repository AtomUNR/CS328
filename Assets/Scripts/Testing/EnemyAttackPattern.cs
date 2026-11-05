using UnityEngine;

public abstract class EnemyAttackPattern : MonoBehaviour
{
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