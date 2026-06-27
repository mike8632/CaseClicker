using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper component that physically activates/deactivates the Checkmark GameObject 
/// on a Toggle to support slide switches with text/knob child hierarchies.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class SlideToggleVisuals : MonoBehaviour
{
    [SerializeField] private GameObject checkmarkGameObject;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.onValueChanged.AddListener(UpdateVisuals);
            UpdateVisuals(toggle.isOn);
        }
    }

    private void OnDestroy()
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(UpdateVisuals);
        }
    }

    public void UpdateVisuals(bool isOn)
    {
        if (checkmarkGameObject != null)
        {
            checkmarkGameObject.SetActive(isOn);
        }
    }

    public void SetCheckmarkGameObject(GameObject go)
    {
        checkmarkGameObject = go;
        if (toggle != null)
        {
            UpdateVisuals(toggle.isOn);
        }
    }
}
