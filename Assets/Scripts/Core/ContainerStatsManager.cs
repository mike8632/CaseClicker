using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks per-container open counts and economic stats across sessions.
///
/// Stats persist through SaveSystem so data survives selling skins or repricing.
/// ContainerInfoPanelUI reads these instead of counting current inventory.
///
/// Call:  ContainerStatsManager.Instance?.RecordContainerOpened(caseId, cost, value)
/// Query: ContainerStatsManager.Instance?.GetProfit(caseId)
/// </summary>
public class ContainerStatsManager : MonoBehaviour
{
    public static ContainerStatsManager Instance { get; private set; }

    private readonly Dictionary<string, ContainerStatEntry> _stats =
        new Dictionary<string, ContainerStatEntry>(System.StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Record / Query API ─────────────────────────────────────────────────────

    /// <summary>Called every time a case is successfully opened.</summary>
    public void RecordContainerOpened(string containerId, double openingCost, double receivedValue)
    {
        if (string.IsNullOrEmpty(containerId)) return;

        if (!_stats.TryGetValue(containerId, out var entry))
        {
            entry = new ContainerStatEntry { containerId = containerId };
            _stats[containerId] = entry;
        }

        entry.openedCount++;
        entry.totalOpeningCost   += openingCost;
        entry.totalValueReceived += receivedValue;
    }

    public int    GetOpenedCount(string containerId)
        => TryGet(containerId, out var e) ? e.openedCount : 0;

    public double GetTotalOpeningCost(string containerId)
        => TryGet(containerId, out var e) ? e.totalOpeningCost : 0.0;

    public double GetTotalValueReceived(string containerId)
        => TryGet(containerId, out var e) ? e.totalValueReceived : 0.0;

    /// <summary>profit = totalValueReceived − totalOpeningCost (negative = loss)</summary>
    public double GetProfit(string containerId)
        => GetTotalValueReceived(containerId) - GetTotalOpeningCost(containerId);

    private bool TryGet(string containerId, out ContainerStatEntry entry)
    {
        entry = null;
        return !string.IsNullOrEmpty(containerId) && _stats.TryGetValue(containerId, out entry);
    }

    // ── Save / Load ────────────────────────────────────────────────────────────

    public List<ContainerStatDTO> GetSnapshot()
    {
        var list = new List<ContainerStatDTO>(_stats.Count);
        foreach (var kvp in _stats)
        {
            list.Add(new ContainerStatDTO
            {
                containerId        = kvp.Value.containerId,
                openedCount        = kvp.Value.openedCount,
                totalOpeningCost   = kvp.Value.totalOpeningCost,
                totalValueReceived = kvp.Value.totalValueReceived,
            });
        }
        return list;
    }

    public void ApplySnapshot(List<ContainerStatDTO> snapshot)
    {
        _stats.Clear();
        if (snapshot == null) return;  // null = old save with no container stats — start fresh

        foreach (var dto in snapshot)
        {
            if (string.IsNullOrEmpty(dto.containerId)) continue;
            _stats[dto.containerId] = new ContainerStatEntry
            {
                containerId        = dto.containerId,
                openedCount        = dto.openedCount,
                totalOpeningCost   = dto.totalOpeningCost,
                totalValueReceived = dto.totalValueReceived,
            };
        }
    }

    // ── Internal runtime entry (not serialized) ────────────────────────────────
    private class ContainerStatEntry
    {
        public string containerId;
        public int    openedCount;
        public double totalOpeningCost;
        public double totalValueReceived;
    }
}
