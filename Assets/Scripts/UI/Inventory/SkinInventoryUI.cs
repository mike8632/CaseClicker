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
        if (contentParent == null || cardPrefab == null || SkinInventoryManager.Instance == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

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
        if (contentParent == null || cardPrefab == null || entry == null)
            return;

        var go = Instantiate(cardPrefab, contentParent);
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
}
