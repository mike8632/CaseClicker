using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Shows floating gain text for money/case clicks.
/// Attach to a UI object and assign roots + text prefab in Inspector.
/// </summary>
public class ClickerFloatingTextUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private RectTransform moneyRoot;
    [SerializeField] private RectTransform caseRoot;

    [Header("Prefab")]
    [SerializeField] private Text floatingTextPrefab;

    [Header("Formats")]
    [SerializeField] private string moneyFormat = "+${0:F2}";
    [SerializeField] private string caseFormat = "+{0:F2}%";

    [Header("Animation")]
    [SerializeField] private float lifeTime = 0.8f;
    [SerializeField] private float liftDistance = 28f;
    [SerializeField] private float stackSpacing = 22f;

    [Header("Spawn")]
    [SerializeField] private bool spawnAtMousePosition = true;

    [Header("Colors")]
    [SerializeField] private Color moneyColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color caseColor = new Color(0.4f, 1f, 1f, 1f);

    private readonly List<FloatingEntry> moneyEntries = new List<FloatingEntry>();
    private readonly List<FloatingEntry> caseEntries = new List<FloatingEntry>();

    private Coroutine initRoutine;

    private class FloatingEntry
    {
        public Text text;
        public float elapsed;
        public Vector2 basePosition;
        public Color baseColor;
    }

    private void OnEnable()
    {
        if (initRoutine != null) StopCoroutine(initRoutine);
        initRoutine = StartCoroutine(WaitThenSubscribe());
    }

    private void OnDisable()
    {
        if (initRoutine != null)
        {
            StopCoroutine(initRoutine);
            initRoutine = null;
        }

        if (ClickerController.Instance != null)
        {
            ClickerController.Instance.OnMoneyClicked.RemoveListener(OnMoneyClicked);
            ClickerController.Instance.OnCaseClicked.RemoveListener(OnCaseClicked);
        }
    }

    private IEnumerator WaitThenSubscribe()
    {
        while (ClickerController.Instance == null)
            yield return null;

        ClickerController.Instance.OnMoneyClicked.AddListener(OnMoneyClicked);
        ClickerController.Instance.OnCaseClicked.AddListener(OnCaseClicked);
        initRoutine = null;
    }

    private void Update()
    {
        UpdateEntries(moneyEntries);
        UpdateEntries(caseEntries);
    }

    private void OnMoneyClicked(float amount)
    {
        SpawnEntry(moneyEntries, moneyRoot, string.Format(moneyFormat, amount), moneyColor);
    }

    private void OnCaseClicked(float amount)
    {
        SpawnEntry(caseEntries, caseRoot, string.Format(caseFormat, amount), caseColor);
    }

    private void SpawnEntry(List<FloatingEntry> list, RectTransform root, string textValue, Color color)
    {
        if (floatingTextPrefab == null || root == null) return;

        var txt = Instantiate(floatingTextPrefab, root);
        txt.text = textValue;
        txt.color = color;

        // Important: floating texts should never block button clicks
        txt.raycastTarget = false;
        var cg = txt.GetComponent<CanvasGroup>();
        if (cg == null) cg = txt.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var rt = txt.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 basePos = Vector2.zero;

        if (spawnAtMousePosition)
        {
            var canvas = root.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            Vector2 pointerPos = GetPointerScreenPosition();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointerPos, cam, out basePos);
        }

        // stack so multiple recent texts do not fully overlap
        basePos.y += list.Count * stackSpacing;

        rt.anchoredPosition = basePos;

        list.Add(new FloatingEntry
        {
            text = txt,
            elapsed = 0f,
            basePosition = basePos,
            baseColor = color
        });
    }

    private Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private void UpdateEntries(List<FloatingEntry> list)
    {
        if (list.Count == 0) return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e.text == null)
            {
                list.RemoveAt(i);
                continue;
            }

            e.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(e.elapsed / lifeTime);

            var rt = e.text.rectTransform;
            rt.anchoredPosition = new Vector2(e.basePosition.x, e.basePosition.y + (liftDistance * t));

            var c = e.baseColor;
            c.a = 1f - t;
            e.text.color = c;

            if (e.elapsed >= lifeTime)
            {
                Destroy(e.text.gameObject);
                list.RemoveAt(i);
            }
        }
    }
}
