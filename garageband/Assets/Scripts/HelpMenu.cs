using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class HelpMenu : MonoBehaviour
{
    [Header("Help UI")]
    [SerializeField] private GameObject helpPopup;
    [Header("Controller Laser Visuals")]
    [SerializeField] private GameObject leftLineVisual;
    [SerializeField] private GameObject rightLineVisual;

    public bool IsOpen => helpPopup != null && helpPopup.activeSelf;
    private Camera eye;
    private Canvas helpCanvas;
    private Canvas badgeCanvas;
    private CanvasGroup[] underlyingUI;
    private DrumModeVisuals drumModeVisuals;
    private HelpPanelLayout panelLayout;

    private void Start()
    {
        if (helpPopup == null) return;
        helpPopup.SetActive(false);
        var origin = FindFirstObjectByType<XROrigin>();
        eye = origin != null ? origin.Camera : Camera.main;
        if (eye == null) return;
        helpCanvas = GetComponent<Canvas>();
        QuestUI.ConfigureCanvas(helpCanvas, eye, new Vector2(1000f, 720f), 300);
        helpCanvas.transform.SetParent(origin != null ? origin.transform : null, false);
        QuestUI.PlaceInFront(helpCanvas, eye, new Vector3(0f, 0f, 1.05f));

        drumModeVisuals = FindFirstObjectByType<DrumModeVisuals>();
        Button helpButton = null;
        foreach (var button in GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == "OpenHelp") helpButton = button;
        badgeCanvas = QuestUI.CreateCanvas("Help Shortcut Canvas", eye, new Vector2(1000f, 720f), 310);
        badgeCanvas.transform.SetParent(eye.transform, false);
        badgeCanvas.transform.localPosition = new Vector3(0f, 0f, .95f);
        badgeCanvas.transform.localScale = Vector3.one * .001f;
        if (helpButton != null)
        {
            QuestUI.Layout(helpButton.GetComponent<RectTransform>(), badgeCanvas.transform,
                new Vector2(345f, 235f), new Vector2(75f, 75f));
            helpButton.gameObject.SetActive(true);
            QuestUI.ApplyOverlay(helpButton.gameObject);
        }
        QuestUI.Text("Help X Hint", badgeCanvas.transform, "X  •  HELP",
            new Vector2(345f, 177f), new Vector2(180f, 36f), 24f, Color.white);

        // Keep the authored instruction panel, text, close button and artwork.
        QuestUI.Layout(helpPopup.GetComponent<RectTransform>(), helpCanvas.transform,
            Vector2.zero, new Vector2(1000f, 720f));
        var instructions = helpPopup.transform.Find("Instruction Panel") as RectTransform;
        if (instructions != null)
        {
            QuestUI.Layout(instructions, helpPopup.transform, Vector2.zero, new Vector2(900f, 650f));
            panelLayout = instructions.GetComponent<HelpPanelLayout>();
            if (panelLayout == null) panelLayout = instructions.gameObject.AddComponent<HelpPanelLayout>();
            panelLayout.Configure();
        }
        QuestUI.ApplyOverlay(helpPopup);
        SetLaserVisuals(true);
    }

    public void ToggleHelp() { if (IsOpen) CloseHelp(); else OpenHelp(); }

    public void OpenHelp()
    {
        if (helpPopup == null) return;
        QuestUI.PlaceInFront(helpCanvas, eye, new Vector3(0f, 0f, 1.05f));
        helpPopup.SetActive(true);
        if (panelLayout != null) panelLayout.Refresh(true);
        if (badgeCanvas != null) badgeCanvas.gameObject.SetActive(false);
        if (drumModeVisuals != null) drumModeVisuals.SetMenuMode(true);
        if (underlyingUI == null)
        {
            var groups = new System.Collections.Generic.List<CanvasGroup>();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.name != "Conversation Canvas" && canvas.name != "Start Experience Canvas" && canvas.name != "Canvas Recording" && canvas.name != "Performance Controls Canvas") continue;
                var group = canvas.GetComponent<CanvasGroup>();
                if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
                groups.Add(group);
            }
            underlyingUI = groups.ToArray();
        }
        foreach (var group in underlyingUI) { group.interactable = false; group.blocksRaycasts = false; group.alpha = 0f; }
        SetLaserVisuals(true);
    }

    public void CloseHelp()
    {
        if (helpPopup != null) helpPopup.SetActive(false);
        if (badgeCanvas != null) badgeCanvas.gameObject.SetActive(true);
        if (drumModeVisuals != null)
        {
            var recorder = FindFirstObjectByType<DrumRecorder>();
            drumModeVisuals.SetMenuMode(!drumModeVisuals.IsDrumModeActive || (recorder != null && recorder.IsReviewing));
        }
        if (underlyingUI != null)
            foreach (var group in underlyingUI) { group.interactable = true; group.blocksRaycasts = true; group.alpha = 1f; }
        SetLaserVisuals(true);
    }

    public void SetLaserVisuals(bool visible)
    {
        if (leftLineVisual != null) leftLineVisual.SetActive(visible);
        if (rightLineVisual != null) rightLineVisual.SetActive(visible);
    }
}
