using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which collection completion rewards the player has already claimed.
/// Lives on the same GameManager object (or any DontDestroyOnLoad object).
///
/// Save/load integration:
///   CollectSaveData  → calls GetSnapshot()  → stored in SaveData.claimedCollectionRewardIds
///   ApplySaveData    → calls ApplySnapshot() → restores claimed ids (null-safe for old saves)
/// </summary>
public class CollectionRewardManager : MonoBehaviour
{
    public static CollectionRewardManager Instance { get; private set; }

    private readonly HashSet<string> _claimedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public query API ───────────────────────────────────────────────────────

    /// <summary>Returns true if the reward for this collection has already been claimed.</summary>
    public bool IsCollectionRewardClaimed(string collectionId)
    {
        return !string.IsNullOrEmpty(collectionId) && _claimedIds.Contains(collectionId);
    }

    // ── Claim API (called by ContentsPanelUI) ──────────────────────────────────

    /// <summary>
    /// Record that the reward for collectionId has been claimed.
    /// Triggers a debounced save so the claim persists through restarts.
    /// </summary>
    public void MarkCollectionRewardClaimed(string collectionId)
    {
        if (string.IsNullOrEmpty(collectionId)) return;
        if (!_claimedIds.Add(collectionId)) return; // already in set — no-op

        SaveSystem.Instance?.RequestSave();
        Debug.Log($"[CollectionReward] Reward claimed for collection: '{collectionId}'");
    }

    // ── Save / Load snapshot ───────────────────────────────────────────────────

    /// <summary>Returns a serialisable list of all claimed collection ids for SaveData.</summary>
    public List<string> GetSnapshot()
    {
        return new List<string>(_claimedIds);
    }

    /// <summary>
    /// Restores claimed ids from a saved snapshot.
    /// Null-safe: passing null (= old save with no reward data) is treated as an empty list.
    /// </summary>
    public void ApplySnapshot(List<string> snapshot)
    {
        _claimedIds.Clear();
        if (snapshot == null) return;

        foreach (var id in snapshot)
            if (!string.IsNullOrEmpty(id)) _claimedIds.Add(id);
    }
}
