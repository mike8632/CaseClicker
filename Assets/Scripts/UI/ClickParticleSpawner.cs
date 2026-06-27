using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns small falling + rotating icons inside bounded UI panels on coin/case click.
///
/// Setup:
///   1. Place this script on any persistent GameObject in the scene (e.g. a GameSystems object).
///   2. Assign Coin Spawn Area  → the RectTransform of the coin panel.
///      Assign Case Spawn Area  → the RectTransform of the case panel.
///      Add RectMask2D to each of those panels so icons are clipped inside.
///   3. Assign coinSprite and caseSprite.
///   4. Done — clicks are detected automatically via ClickerController events.
/// </summary>
public class ClickParticleSpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Spawn Areas")]
    [Tooltip("RectTransform of the coin click panel. Must have RectMask2D to clip icons.")]
    [SerializeField] private RectTransform coinSpawnArea;
    [Tooltip("RectTransform of the case click panel. Must have RectMask2D to clip icons.")]
    [SerializeField] private RectTransform caseSpawnArea;

    [Header("Icons")]
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite caseSprite;

    [Header("Limits  (shared across both areas)")]
    [SerializeField] private int   maxParticles = 22;
    [SerializeField] private float particleSize = 36f;

    [Header("Fall Speed  (units/sec)")]
    [SerializeField] private float minFallSpeed = 90f;
    [SerializeField] private float maxFallSpeed = 220f;

    [Header("Rotation Speed  (deg/sec)")]
    [SerializeField] private float minRotateSpeed = 60f;
    [SerializeField] private float maxRotateSpeed = 270f;

    // ── Internal particle data ────────────────────────────────────────────────
    private class Particle
    {
        public RectTransform rect;
        public float         speed;
        public float         rotSpeed;
        public float         destroyY;
    }

    private readonly List<Particle> _active = new List<Particle>();

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        if (ClickerController.Instance != null)
            Subscribe(ClickerController.Instance);
        else
            StartCoroutine(WaitForClicker());
    }

    private System.Collections.IEnumerator WaitForClicker()
    {
        while (ClickerController.Instance == null)
            yield return null;
        Subscribe(ClickerController.Instance);
    }

    private void Subscribe(ClickerController ctrl)
    {
        if (coinSprite != null && coinSpawnArea != null)
            ctrl.OnMoneyClicked.AddListener(OnMoneyClick);
        if (caseSprite != null && caseSpawnArea != null)
            ctrl.OnCaseClicked.AddListener(OnCaseClick);
    }

    private void OnDisable()
    {
        if (ClickerController.Instance != null)
        {
            ClickerController.Instance.OnMoneyClicked.RemoveListener(OnMoneyClick);
            ClickerController.Instance.OnCaseClicked.RemoveListener(OnCaseClick);
        }
    }

    private void OnMoneyClick(float _) => SpawnCoin();
    private void OnCaseClick(float _)  => SpawnCase();

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var p = _active[i];
            if (p.rect == null) { _active.RemoveAt(i); continue; }

            var pos = p.rect.anchoredPosition;
            pos.y -= p.speed * dt;
            p.rect.anchoredPosition = pos;

            p.rect.Rotate(0f, 0f, p.rotSpeed * dt, Space.Self);

            if (pos.y < p.destroyY)
            {
                Destroy(p.rect.gameObject);
                _active.RemoveAt(i);
            }
        }
    }

    // ── Public spawn API ──────────────────────────────────────────────────────
    public void SpawnCoin()
    {
        if (coinSprite != null && coinSpawnArea != null)
            SpawnIcon(coinSprite, coinSpawnArea);
    }

    public void SpawnCase()
    {
        if (caseSprite != null && caseSpawnArea != null)
            SpawnIcon(caseSprite, caseSpawnArea);
    }

    // ── Core spawn ────────────────────────────────────────────────────────────
    private void SpawnIcon(Sprite icon, RectTransform area)
    {
        if (PlayerPrefs.GetInt("settings.fallingClickIcons", 1) == 0) return;

        // Cull the oldest particle if at the limit
        while (_active.Count >= maxParticles)
        {
            var old = _active[0];
            _active.RemoveAt(0);
            if (old.rect != null) Destroy(old.rect.gameObject);
        }

        Rect r = area.rect;

        float halfSize = particleSize * 0.5f;
        float xMin     = r.xMin + halfSize;
        float xMax     = r.xMax - halfSize;
        float spawnY   = r.yMax - halfSize;
        float destroyY = r.yMin - particleSize;

        float x = (xMin < xMax) ? Random.Range(xMin, xMax) : 0f;

        var go = new GameObject("ClickParticle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(area, worldPositionStays: false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(particleSize, particleSize);
        rt.anchoredPosition = new Vector2(x, spawnY);

        var img = go.GetComponent<Image>();
        img.sprite         = icon;
        img.preserveAspect = true;
        img.raycastTarget  = false;

        float rotDir = Random.value > 0.5f ? 1f : -1f;

        _active.Add(new Particle
        {
            rect     = rt,
            speed    = Random.Range(minFallSpeed, maxFallSpeed),
            rotSpeed = Random.Range(minRotateSpeed, maxRotateSpeed) * rotDir,
            destroyY = destroyY
        });
    }
}
