using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Bomb : MonoBehaviour
{
    [Header("Prefabs / sprites")]
    public Shard shardPrefab;
    public Sprite[] fallFrames;         // Bomb_Fall_0, Bomb_Fall_1
    public Sprite flashWhite;           // Bomb_Flash_White
    public Sprite flashRed;             // Bomb_Flash_Red

    [Header("Sound")]
    public AudioClip flashSfx;          // Bomb_Flash.wav
    public AudioClip explodeSfx;        // Bomb_Explode.wav
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("Tuning")]
    public float fallSpeed = 4f;
    public float fuseFlicker = 0.12f;
    public float flashTime = 0.25f;     // "about to blow" pause
    public int shardCount = 5;          // 5 = star
    public float shardSpeed = 6f;

    [HideInInspector] public float explodeY;

    bool exploding;
    SpriteRenderer sr;
    float flickerT;
    int frame;
    Image uiImage;
    RectTransform uiRect;
    BombRainPattern uiPattern;
    float uiFlashElapsed;

    public void BeginUI(BombRainPattern pattern, Vector2 position, float detonationY)
    {
        uiPattern = pattern;
        uiRect = (RectTransform)transform;
        uiRect.anchoredPosition = position;
        explodeY = detonationY;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        uiImage = GetComponent<Image>();
        if (flashSfx == null) flashSfx = Resources.Load<AudioClip>("BombSummoner/Bomb_Flash");
        if (explodeSfx == null) explodeSfx = Resources.Load<AudioClip>("BombSummoner/Bomb_Explode");
    }

    void Update()
    {
        if (uiPattern != null || exploding) return;

        // fuse flicker
        flickerT += Time.deltaTime;
        if (flickerT >= fuseFlicker && fallFrames != null && fallFrames.Length > 1)
        {
            flickerT = 0;
            frame = 1 - frame;
            SetSprite(fallFrames[frame]);
        }

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
        if (transform.position.y <= explodeY) StartCoroutine(Explode());
    }

    public void TickUI(float deltaTime)
    {
        if (!exploding)
        {
            flickerT += deltaTime;
            if (fallFrames != null && fallFrames.Length > 0)
                SetSprite(fallFrames[Mathf.FloorToInt(flickerT / Mathf.Max(0.01f, fuseFlicker)) % fallFrames.Length]);
            uiRect.anchoredPosition += Vector2.down * fallSpeed * deltaTime;
            if (uiRect.anchoredPosition.y > explodeY) return;
            exploding = true;
            Play(flashSfx);
        }
        uiFlashElapsed += deltaTime;
        SetSprite(Mathf.FloorToInt(uiFlashElapsed * 6f / Mathf.Max(0.01f, flashTime)) % 2 == 0 ? flashWhite : flashRed);
        if (uiFlashElapsed >= flashTime) SpawnShards();
    }

    IEnumerator Explode()
    {
        exploding = true;
        Play(flashSfx);

        // Undertale-style flash before it goes off
        for (int i = 0; i < 3; i++)
        {
            SetSprite(flashWhite); yield return new WaitForSeconds(flashTime / 6f);
            SetSprite(flashRed);   yield return new WaitForSeconds(flashTime / 6f);
        }

        SpawnShards();
    }

    void SpawnShards()
    {
        Play(explodeSfx);

        // Shrapnel: evenly spaced, one point aimed straight up
        float step = 360f / Mathf.Max(1, shardCount);
        for (int i = 0; i < shardCount; i++)
        {
            float angle = 90f + step * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                      Mathf.Sin(angle * Mathf.Deg2Rad));
            Shard s;
            if (uiPattern != null)
            {
                s = Instantiate(shardPrefab, uiPattern.ProjectileRoot, false);
                s.BeginUI(uiPattern, uiRect.anchoredPosition);
            }
            else s = Instantiate(shardPrefab, transform.position, Quaternion.identity);
            s.Launch(dir, shardSpeed);
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void SetSprite(Sprite sprite)
    {
        if (uiImage != null) uiImage.sprite = sprite;
        else if (sr != null) sr.sprite = sprite;
    }

    // Plays at the camera so 2D games don't get quiet 3D-distance falloff
    void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (uiPattern != null)
        {
            uiPattern.PlaySound(clip, sfxVolume);
            return;
        }
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, sfxVolume);
    }
}
