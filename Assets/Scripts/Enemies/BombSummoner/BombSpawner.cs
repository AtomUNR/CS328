using System.Collections;
using UnityEngine;

public class BombSpawner : MonoBehaviour
{
    public Bomb bombPrefab;
    public float spawnY = 6f;           // just above the top edge of the arena
    public float minX = -4f, maxX = 4f;
    public float minExplodeY = -2f;     // lowest a bomb can fall before blowing
    public float maxExplodeY = 4f;      // highest it can blow up
    public int bombsPerWave = 10;
    public float timeBetweenBombs = 0.35f;
    public float timeBetweenWaves = 2.5f;

    public bool playOnStart = true;
    [Min(1)] public int totalBombs = 25;
    private BombRainPattern uiPattern;
    public float WaveElapsed { get; private set; }
    public int TotalSpawned { get; private set; }
    private int spawnedInWave;

    void Start()
    {
        if (playOnStart && uiPattern == null) StartCoroutine(Attack());
    }

    public void BeginUI(BombRainPattern pattern)
    {
        StopAttack();
        uiPattern = pattern;
        WaveElapsed = 0f;
        spawnedInWave = 0;
        TotalSpawned = 0;
    }

    public void TickUI(float deltaTime, float castDelay)
    {
        WaveElapsed += deltaTime;
        int count = Mathf.Max(1, bombsPerWave);
        float interval = Mathf.Max(0.02f, timeBetweenBombs);
        int limit = Mathf.Max(1, totalBombs);
        while (spawnedInWave < count && TotalSpawned < limit && WaveElapsed >= castDelay + spawnedInWave * interval)
        {
            Rect bounds = uiPattern.battleBox.rect;
            var bomb = Instantiate(bombPrefab, uiPattern.ProjectileRoot, false);
            Vector2 halfSize = ((RectTransform)bomb.transform).rect.size * 0.5f;
            bomb.BeginUI(uiPattern, new Vector2(Random.Range(bounds.xMin + halfSize.x, bounds.xMax - halfSize.x),
                bounds.yMax + halfSize.y), Random.Range(bounds.yMin * 0.65f, bounds.yMax * 0.65f));
            spawnedInWave++;
            TotalSpawned++;
        }
        float waveDuration = castDelay + count * interval + Mathf.Max(0.02f, timeBetweenWaves);
        if (WaveElapsed >= waveDuration && TotalSpawned < limit)
        {
            WaveElapsed %= waveDuration;
            spawnedInWave = 0;
        }
    }

    public void StopAttack() => StopAllCoroutines();

    void OnDisable() => StopAttack();

    IEnumerator Attack()
    {
        while (true)
        {
            for (int i = 0; i < bombsPerWave; i++)
            {
                    var pos = new Vector3(Random.Range(minX, maxX), spawnY, 0);
                    var b = Instantiate(bombPrefab, pos, Quaternion.identity);
                    b.explodeY = Random.Range(minExplodeY, maxExplodeY);
                yield return new WaitForSeconds(Mathf.Max(0.02f, timeBetweenBombs));
            }
            yield return new WaitForSeconds(Mathf.Max(0.02f, timeBetweenWaves));
        }
    }
}
