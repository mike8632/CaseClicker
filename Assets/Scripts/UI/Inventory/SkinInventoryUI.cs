using System.Collections;
using UnityEngine;

/// <summary>
/// Creates a new UI card for each dropped skin item.
/// Assign contentParent and cardPrefab (with SkinInventoryCardUI) in inspector.
/// </summary>
public class SkinInventoryUI : MonoBehaviour
{
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private bool newestOnTop = true;

    private Coroutine initRoutine;
    private SkinInventoryCardUI sceneTemplateCard;

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

        if (SkinInventoryManager.Instance != null)
        {
            SkinInventoryManager.Instance.OnSkinAdded.RemoveListener(OnSkinAdded);
        }
    }

    private IEnumerator WaitThenSubscribe()
    {
        while (SkinInventoryManager.Instance == null)
            yield return null;

        SkinInventoryManager.Instance.OnSkinAdded.AddListener(OnSkinAdded);

        RebuildFromSnapshot();
        initRoutine = null;
    }

    public void RebuildFromSnapshot()
    {
        if (contentParent == null || SkinInventoryManager.Instance == null)
            return;

        ResolveTemplateIfNeeded();
        ClearSpawnedCards();

        var list = SkinInventoryManager.Instance.Entries;
        for (int i = 0; i < list.Count; i++)
        {
            SpawnCard(list[i]);
        }
    }

    private void OnSkinAdded(SkinInventoryEntry entry)
    {
        SpawnCard(entry);
    }

    private void SpawnCard(SkinInventoryEntry entry)
    {
        if (contentParent == null || entry == null)
            return;

        GameObject source = GetCardSource();
        if (source == null)
            return;

        var go = Instantiate(source, contentParent);
        go.SetActive(true);
        if (newestOnTop)
        {
            go.transform.SetAsFirstSibling();
        }
        var card = go.GetComponent<SkinInventoryCardUI>();
        if (card != null)
        {
            card.Bind(entry);
        }
    }

    private GameObject GetCardSource()
    {
        if (cardPrefab != null)
            return cardPrefab;

        ResolveTemplateIfNeeded();
        if (sceneTemplateCard != null)
            return sceneTemplateCard.gameObject;

        return null;
    }

    private void ResolveTemplateIfNeeded()
    {
        if (sceneTemplateCard != null || contentParent == null)
            return;

        if (cardPrefab != null)
            return;

        // Never use contentParent itself as the template card.
        // The template must be a child item (e.g. "skincard").
        var cards = contentParent.GetComponentsInChildren<SkinInventoryCardUI>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            if (card.transform == contentParent) continue;

            sceneTemplateCard = card;
            break;
        }

        if (sceneTemplateCard == null)
        {
            var parentCard = contentParent.GetComponent<SkinInventoryCardUI>();
            if (parentCard != null)
            {
                Debug.LogWarning("[SkinInventoryUI] contentParent has SkinInventoryCardUI on it. Move SkinInventoryCardUI to a child card object and keep contentParent as a pure container.");
            }
            return;
        }

        if (sceneTemplateCard != null)
        {
            // Keep one in-scene template hidden, and clone it at runtime.
            sceneTemplateCard.gameObject.SetActive(false);
        }
    }

    private void ClearSpawnedCards()
    {
        if (contentParent == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);

            if (sceneTemplateCard != null && child == sceneTemplateCard.transform)
                continue;

            if (child.GetComponent<SkinInventoryCardUI>() != null)
                Destroy(child.gameObject);
        }
    }
}
