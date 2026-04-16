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
    public string weaponName;
    public string skinName;
    public Sprite itemIcon;
    public ItemRarity rarity;
    public ItemWear wear;
    public bool isStatTrak;
    public float marketValue;
}

/// <summary>
/// Stores skins/items obtained from opening cases.
/// Each drop is stored as its own entry so UI can create a new card per drop.
/// </summary>
public class SkinInventoryManager : MonoBehaviour
{
    public static SkinInventoryManager Instance { get; private set; }

    [Header("Drop Settings")]
    [SerializeField, Range(0f, 1f)] private float statTrakChance = 0.1f;

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

        bool isStatTrak = UnityEngine.Random.value <= statTrakChance;

        // Prefer explicit fields; fallback to parsing itemName like "Weapon | Skin"
        string weaponName = item.weaponName;
        string skinName = item.skinName;
        if (string.IsNullOrEmpty(weaponName) && string.IsNullOrEmpty(skinName))
        {
            TrySplitItemName(item.itemName, out weaponName, out skinName);
        }

        var entry = new SkinInventoryEntry
        {
            instanceId = Guid.NewGuid().ToString("N"),
            sourceCaseId = sourceCaseId,
            itemId = item.itemId,
            itemName = item.itemName,
            weaponName = weaponName,
            skinName = skinName,
            itemIcon = item.itemIcon,
            rarity = item.rarity,
            wear = item.wear,
            isStatTrak = isStatTrak,
            marketValue = marketValue
        };

        entries.Add(entry);
        OnSkinAdded?.Invoke(entry);
    }

    private static void TrySplitItemName(string itemName, out string weaponName, out string skinName)
    {
        weaponName = string.Empty;
        skinName = string.Empty;

        if (string.IsNullOrEmpty(itemName))
            return;

        int pipeIndex = itemName.IndexOf('|');
        if (pipeIndex >= 0)
        {
            weaponName = itemName.Substring(0, pipeIndex).Trim();
            skinName = itemName.Substring(pipeIndex + 1).Trim();
            return;
        }

        // Fallback: keep original as weaponName if no separator exists
        weaponName = itemName;
    }
}
