using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class SkinInventoryEntry
{
    public string instanceId;
    public string sourceCaseId;
    public string itemId;
    public string itemName;
    public Sprite itemIcon;
    public ItemRarity rarity;
    public ItemWear wear;
    public float marketValue;
}

/// <summary>
/// Stores skins/items obtained from opening cases.
/// Each drop is stored as its own entry so UI can create a new card per drop.
/// </summary>
public class SkinInventoryManager : MonoBehaviour
{
    public static SkinInventoryManager Instance { get; private set; }

    public UnityEvent<SkinInventoryEntry> OnSkinAdded;

    private readonly List<SkinInventoryEntry> entries = new List<SkinInventoryEntry>();

    public IReadOnlyList<SkinInventoryEntry> Entries => entries;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        OnSkinAdded ??= new UnityEvent<SkinInventoryEntry>();
    }

    public void AddSkin(CaseItemData item, string sourceCaseId, float marketValue)
    {
        if (item == null) return;

        var entry = new SkinInventoryEntry
        {
            instanceId = Guid.NewGuid().ToString("N"),
            sourceCaseId = sourceCaseId,
            itemId = item.itemId,
            itemName = item.itemName,
            itemIcon = item.itemIcon,
            rarity = item.rarity,
            wear = item.wear,
            marketValue = marketValue
        };

        entries.Add(entry);
        OnSkinAdded?.Invoke(entry);
    }
}
