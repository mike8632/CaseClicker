using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each content panel you want to be a tab. Set a unique TabId (e.g. "clickers", "inventory", "stats").
/// The panel should contain a CanvasGroup for fade and optionally be a child of the sidebar content area.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TabPanel : MonoBehaviour
{
    [Tooltip("Unique id string for this panel. Buttons use this id to open it.")]
    public string TabId;

    [Tooltip("How long show/hide animations take.")]
    public float transitionDuration = 0.18f;

    [Tooltip("Slide offset in local X when hiding (negative slides out to left).")]
    public float hideSlideOffset = -20f;

    [Tooltip("If true, panel will start hidden.")]
    public bool startHidden = true;

    private CanvasGroup cg;
    private RectTransform rt;
    private Coroutine running;

    // quick state flag
    public bool IsVisible { get; private set; } = false;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
        if (startHidden) HideImmediate(); else ShowImmediate();
    }

    #region Show / Hide API

    public void Show()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(DoShow());
    }

    public void Hide()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(DoHide());
    }

    public void ShowImmediate()
    {
        if (running != null) StopCoroutine(running);
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        rt.anchoredPosition = Vector2.zero;
        IsVisible = true;
    }

    public void HideImmediate()
    {
        if (running != null) StopCoroutine(running);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        rt.anchoredPosition = new Vector2(hideSlideOffset, 0f);
        IsVisible = false;
    }

    private IEnumerator DoShow()
    {
        IsVisible = true;
        float elapsed = 0f;
        float startAlpha = cg.alpha;
        float startX = rt.anchoredPosition.x;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            cg.alpha = Mathf.Lerp(startAlpha, 1f, t);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, 0f, t), 0f);
            yield return null;
        }
        cg.alpha = 1f;
        rt.anchoredPosition = Vector2.zero;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        running = null;
    }

    private IEnumerator DoHide()
    {
        float elapsed = 0f;
        float startAlpha = cg.alpha;
        float startX = rt.anchoredPosition.x;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, hideSlideOffset, t), 0f);
            yield return null;
        }
        cg.alpha = 0f;
        rt.anchoredPosition = new Vector2(hideSlideOffset, 0f);
        IsVisible = false;
        running = null;
    }

    #endregion
}