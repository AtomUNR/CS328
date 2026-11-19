using UnityEngine;

public class Shard : MonoBehaviour
{
    Vector2 velocity;
    public float lifetime = 4f;
    public AudioClip hitSfx;            // Shard_Hit.wav
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    private BombRainPattern uiPattern;
    private RectTransform uiRect;
    private bool consumed;
    private float age;

    public void BeginUI(BombRainPattern pattern, Vector2 position)
    {
        uiPattern = pattern;
        uiRect = (RectTransform)transform;
        uiRect.anchoredPosition = position;
    }

    void Awake()
    {
        if (hitSfx == null) hitSfx = Resources.Load<AudioClip>("BombSummoner/Shard_Hit");
    }

    public void Launch(Vector2 dir, float speed)
    {
        velocity = dir.normalized * speed;
        transform.rotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        if (uiPattern == null) Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!consumed && uiPattern == null) transform.position += (Vector3)(velocity * Time.deltaTime);
    }

    public void TickUI(float deltaTime, Vector2 essenceBefore, Vector2 essenceAfter, float essenceRadius)
    {
        if (consumed) return;
        age += deltaTime;
        Vector2 start = uiRect.anchoredPosition;
        Vector2 end = start + velocity * deltaTime;
        uiRect.anchoredPosition = end;
        float radius = Mathf.Min(uiRect.rect.width, uiRect.rect.height) * 0.5f;
        if (uiPattern.TryHit(start - essenceBefore, end - essenceAfter, radius + essenceRadius)) Hit();
        if (age >= lifetime || !uiPattern.battleBox.rect.Contains(end))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    void Hit()
    {
        consumed = true;
        if (uiPattern != null) uiPattern.PlaySound(hitSfx, sfxVolume);
        else if (hitSfx != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(hitSfx, pos, sfxVolume);
        }
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!consumed && uiPattern == null && other.CompareTag("Player"))
        {
            other.GetComponentInParent<CharacterStats>()?.TakeDamage(5);
            Hit();
        }
    }
}
