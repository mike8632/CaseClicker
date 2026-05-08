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
    public float floatValue;
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
    [SerializeField, Min(1f)] private float statTrakValueMultiplier = 1.5f;

    public float StatTrakValueMultiplier => statTrakValueMultiplier;

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

    public void AddSkin(CaseItemData item, string sourceCaseId, float marketValue, float floatValue)
    {
        if (item == null) return;

        bool isStatTrak = UnityEngine.Random.value <= statTrakChance;
        if (isStatTrak)
        {
            marketValue *= Mathf.Max(1f, statTrakValueMultiplier);
        }

        item.GetDisplayNames(out var weaponName, out var skinName, out _);

        var entry = new SkinInventoryEntry
        {
            instanceId = Guid.NewGuid().ToString("N"),
            sourceCaseId = sourceCaseId,
            itemId = item.itemId,
            itemName = item.itemName,
            weaponName = weaponName,
            skinName = skinName,
            itemIcon = item.itemIcon,
            rarity = item.GetEffectiveRarity(),
            wear = GetWearFromFloat(floatValue),
            isStatTrak = isStatTrak,
            marketValue = marketValue,
            floatValue = floatValue
        };

        entries.Add(entry);
        OnSkinAdded?.Invoke(entry);
    }

    private static ItemWear GetWearFromFloat(float value)
    {
        float v = Mathf.Clamp01(value);
        if (v < 0.07f) return ItemWear.FactoryNew;
        if (v < 0.15f) return ItemWear.MinimalWear;
        if (v < 0.38f) return ItemWear.FieldTested;
        if (v < 0.45f) return ItemWear.WellWorn;
        return ItemWear.BattleScarred;
    }

}
