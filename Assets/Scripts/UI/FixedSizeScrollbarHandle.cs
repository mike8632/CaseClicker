using UnityEngine;

/// <summary>
/// Forces a Scrollbar handle to a constant size instead of letting the parent
/// ScrollRect resize it to the content/viewport ratio. The handle keeps the
/// natural proportions of its sprite (so fixed-art handles never distort) and
/// simply slides along the track to represent the scroll position.
///
/// The size is enforced in Canvas.willRenderCanvases, which runs AFTER
/// ScrollRect.Rebuild() in the same frame, so the ScrollRect can no longer
/// overwrite the handle size.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Scrollbar))]
public class FixedSizeScrollbarHandle : MonoBehaviour
{
    [Tooltip("When on, handle length is derived from the handle sprite's aspect ratio and the track cross size, so the sprite shows at natural proportions and never distorts.")]
    [SerializeField] private bool matchSpriteAspect = true;

    [Tooltip("Handle length (pixels) along the scroll axis. Used when Match Sprite Aspect is off, or as a fallback when no sprite is found.")]
    [SerializeField] private float handlePixels = 150f;

    private UnityEngine.UI.Scrollbar scrollbar;
    private RectTransform slidingArea;

    private void Awake()
    {
        scrollbar = GetComponent<UnityEngine.UI.Scrollbar>();
        if (scrollbar != null && scrollbar.handleRect != null)
            slidingArea = scrollbar.handleRect.parent as RectTransform;
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += Enforce;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Enforce;
    }

    // Also enforce in LateUpdate for editor/edit-time preview and as a safety net.
    private void LateUpdate()
    {
        Enforce();
    }

    private void Enforce()
    {
        if (scrollbar == null || scrollbar.handleRect == null)
            return;
        if (slidingArea == null)
            slidingArea = scrollbar.handleRect.parent as RectTransform;
        if (slidingArea == null)
            return;

        bool vertical = scrollbar.direction == UnityEngine.UI.Scrollbar.Direction.BottomToTop
                     || scrollbar.direction == UnityEngine.UI.Scrollbar.Direction.TopToBottom;

        float trackLength = vertical ? slidingArea.rect.height : slidingArea.rect.width;
        if (trackLength <= 0f)
            return;

        float desiredPixels = handlePixels;

        if (matchSpriteAspect)
        {
            var img = scrollbar.handleRect.GetComponent<UnityEngine.UI.Image>();
            if (img != null && img.sprite != null)
            {
                Rect spriteRect = img.sprite.rect;
                float crossSize = vertical ? scrollbar.handleRect.rect.width : scrollbar.handleRect.rect.height;
                float spriteAlong = vertical ? spriteRect.height : spriteRect.width;
                float spriteCross = vertical ? spriteRect.width : spriteRect.height;
                if (spriteCross > 0f && crossSize > 0f)
                    desiredPixels = crossSize * (spriteAlong / spriteCross);
            }
        }

        float fraction = Mathf.Clamp01(desiredPixels / trackLength);
        if (!Mathf.Approximately(scrollbar.size, fraction))
            scrollbar.size = fraction;
    }
}
