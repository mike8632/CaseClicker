using UnityEngine;
using UnityEngine.UI;

public class InventorySearchToggleUI : MonoBehaviour
{
    [SerializeField] private Button searchButton;
    [SerializeField] private GameObject searchBarRoot;
    [SerializeField] private bool hideOnStart = true;

    private void Awake()
    {
        if (hideOnStart && searchBarRoot != null)
            searchBarRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (searchButton != null)
        {
            searchButton.onClick.RemoveListener(ToggleSearchBar);
            searchButton.onClick.AddListener(ToggleSearchBar);
        }
    }

    private void OnDisable()
    {
        if (searchButton != null)
            searchButton.onClick.RemoveListener(ToggleSearchBar);
    }

    private void ToggleSearchBar()
    {
        if (searchBarRoot != null)
            searchBarRoot.SetActive(!searchBarRoot.activeSelf);
    }
}
