using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to any GameObject that should trigger a "Reset Progress" flow.
/// Assign the Button field in the Inspector, or call HandleClick() directly
/// from a Button's OnClick() event in the Inspector.
/// Requires a ConfirmDialogUI in the scene to show the confirmation popup.
/// </summary>
public class ResetProgressButton : MonoBehaviour
{
    [Header("Button (optional — or wire OnClick manually in Inspector)")]
    [SerializeField] private Button resetButton;

    [Header("Dialog Text")]
    [SerializeField] private string dialogTitle = "Reset Progress?";
    [SerializeField, TextArea(3, 6)] private string dialogMessage =
        "This will delete ALL saved progress and restart the game from the beginning.\n\nThis cannot be undone.";

    private void Start()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (resetButton != null)
            resetButton.onClick.RemoveListener(HandleClick);
    }

    /// <summary>
    /// Show the confirmation dialog. Wire this to a Button's OnClick() in the Inspector
    /// if you are not using the resetButton field.
    /// </summary>
    public void HandleClick()
    {
        if (ConfirmDialogUI.Instance == null)
        {
            Debug.LogError("[ResetProgressButton] Cannot reset progress — ConfirmDialogUI is missing from the scene. Add it before using this button.");
            return;
        }

        ConfirmDialogUI.Instance.Show(dialogTitle, dialogMessage, DoReset);
    }

    private static void DoReset()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAndReload();
        }
        else
        {
            // Fallback if GameManager is already gone (shouldn't happen in normal play)
            SaveSystem.Instance?.DeleteSaveData();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
