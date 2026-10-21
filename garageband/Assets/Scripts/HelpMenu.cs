/*
 * Opens Help when the player clicks its button or presses X on Quest.
 * HelpPanelLayout arranges the existing instructions and scrolling controls.
 * The Help shortcut follows the headset. The full panel stays in front of the
 * view where it was opened, so the player can read and point at it.
 * Other experience menus are hidden and cannot be clicked while Help is open.
 * This blocking does not need a dark full-screen background. Recording and
 * playback clocks are not paused here. This is active game/Editor Play Mode
 * code.
 * A Canvas holds UI. A CanvasGroup controls visibility and pointer interaction
 * for a group of buttons/text. DrumModeVisuals supplies the menu pointers and
 * switches back to sticks or the recording review controls when Help closes.
 * UI means the buttons and text the player sees and uses.
 * The Inspector is Unity's settings panel for assigning objects and values.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;

// Opens Help when the player clicks its button or presses X on Quest.
public class HelpMenu : MonoBehaviour
{
    // Keep the existing Help popup and its instruction panel.
    [Header("Help UI")]
    [SerializeField] private GameObject helpPopup;
    // Keep the assigned left controller ray-line visual.
    [Header("Controller Laser Visuals")]
    [SerializeField] private GameObject leftLineVisual;
    // Keep the assigned right controller ray-line visual.
    [SerializeField] private GameObject rightLineVisual;

    // Return whether the assigned Help popup is currently active.
    public bool IsOpen => helpPopup != null && helpPopup.activeSelf;
    // Keep the headset camera used to place and raycast room UI.
    private Camera eye;
    // Keep the main Help canvas placed for reading.
    private Canvas helpCanvas;
    // Keep the headset-following Help shortcut canvas.
    private Canvas badgeCanvas;
    // Keep the experience UI groups hidden and blocked while Help is open.
    private CanvasGroup[] underlyingUI;
    // Keep the controller/stick and menu-ray switcher.
    private DrumModeVisuals drumModeVisuals;
    // Keep the component arranging the Help panel and scrolling text.
    private HelpPanelLayout panelLayout;

    // Prepare Help using the existing button, panel, instructions and Close
    // event. Create the headset shortcut and add scrolling through
    // HelpPanelLayout.
    private void Start()
    {
        // Skip Help changes if the existing popup reference was not assigned.
        if (helpPopup == null) return;
        // Hide the existing Help popup and its instruction panel.
        helpPopup.SetActive(false);
        // Find the scene player rig for this UI or controller setup.
        var origin = FindFirstObjectByType<XROrigin>();
        // Prefer the XR camera for UI placement, with the main camera as a
        // desktop fallback.
        eye = origin != null ? origin.Camera : Camera.main;
        // Stop UI setup if there is no usable viewing camera.
        if (eye == null) return;
        // Reuse the Canvas component on this existing Help object.
        helpCanvas = GetComponent<Canvas>();
        // Configure this main Help canvas for headset UI at its existing draw
        // priority.
        QuestUI.ConfigureCanvas(helpCanvas, eye, new Vector2(1000f, 720f), 300);
        // Parent it under the player rig so it travels with automatic viewing
        // position changes.
        helpCanvas.transform.SetParent(origin != null ? origin.transform : null, false);
        // Initially place the reading panel just over a metre in front of the
        // view.
        QuestUI.PlaceInFront(helpCanvas, eye, new Vector3(0f, 0f, 1.05f));

        // Find the scene controller/stick switcher used by UI and recording.
        drumModeVisuals = FindFirstObjectByType<DrumModeVisuals>();
        // Begin without a found Help button; the Inspector event search
        // identifies it.
        Button helpButton = null;
        // Search the existing buttons, including ones on the at first hidden
        // popup.
        foreach (var button in GetComponentsInChildren<Button>(true))
            // Inspect each persistent button callback rather than relying only
            // on object names.
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                // Recognise the existing shortcut by its OpenHelp event and
                // keep that button.
                if (button.onClick.GetPersistentMethodName(i) == "OpenHelp") helpButton = button;
        // Create a separate canvas for the small always-available Help
        // shortcut.
        badgeCanvas = QuestUI.CreateCanvas("Help Shortcut Canvas", eye, new Vector2(1000f, 720f), 310);
        // Attach the shortcut canvas to the headset so it follows the player's
        // view.
        badgeCanvas.transform.SetParent(eye.transform, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        badgeCanvas.transform.localPosition = new Vector3(0f, 0f, .95f);
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        badgeCanvas.transform.localScale = Vector3.one * .001f;
        // Move the existing shortcut only if its OpenHelp listener was found.
        if (helpButton != null)
        {
            // Set the position and size of the Help shortcut inside its parent
            // panel.
            QuestUI.Layout(helpButton.GetComponent<RectTransform>(), badgeCanvas.transform,
                new Vector2(345f, 235f), new Vector2(75f, 75f));
            // Show the existing Help shortcut button after placing it on the
            // headset canvas.
            helpButton.gameObject.SetActive(true);
            // Draw this UI over room geometry using the shared overlay shader
            // setup.
            QuestUI.ApplyOverlay(helpButton.gameObject);
        }
        // Show the player this menu text: "X  •  HELP".
        QuestUI.Text("Help X Hint", badgeCanvas.transform, "X  •  HELP",
            new Vector2(345f, 177f), new Vector2(180f, 36f), 24f, Color.white);

        // Set the position and size of the Help popup inside its parent panel.
        QuestUI.Layout(helpPopup.GetComponent<RectTransform>(), helpCanvas.transform,
            Vector2.zero, new Vector2(1000f, 720f));
        // Find the existing instruction panel by name without replacing it.
        var instructions = helpPopup.transform.Find("Instruction Panel") as RectTransform;
        // Configure scrolling only when that expected panel exists.
        if (instructions != null)
        {
            // Set the position and size of the Help instructions inside its
            // parent panel.
            QuestUI.Layout(instructions, helpPopup.transform, Vector2.zero, new Vector2(900f, 650f));
            // Reuse a layout helper if one is already present on the
            // instruction panel.
            panelLayout = instructions.GetComponent<HelpPanelLayout>();
            // Otherwise add one helper at game-created, avoiding duplicate
            // layout components.
            if (panelLayout == null) panelLayout = instructions.gameObject.AddComponent<HelpPanelLayout>();
            // Arrange the existing title/text/close button and create reusable
            // scroll controls.
            panelLayout.Configure();
        }
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(helpPopup);
        // Keep the assigned controller ray lines visible for menu pointing.
        SetLaserVisuals(true);
    }

    // Open Help if closed, or close it if already open.
    public void ToggleHelp() { if (IsOpen) CloseHelp(); else OpenHelp(); }

    // Place Help in front of the current view. Hide other experience menus and
    // stop their buttons receiving pointer clicks.
    public void OpenHelp()
    {
        // Skip Help changes if the existing popup reference was not assigned.
        if (helpPopup == null) return;
        // Position Help at the current view when opened, then leave it steady
        // for reading.
        QuestUI.PlaceInFront(helpCanvas, eye, new Vector3(0f, 0f, 1.05f));
        // Activate the existing popup so its panel and controls become visible.
        helpPopup.SetActive(true);
        // Re-measure instructions and start at the top whenever Help opens.
        if (panelLayout != null) panelLayout.Refresh(true);
        // Hide the shortcut while its full panel is already open.
        if (badgeCanvas != null) badgeCanvas.gameObject.SetActive(false);
        // Switch to pointing rays while Help needs UI interaction.
        if (drumModeVisuals != null) drumModeVisuals.SetMenuMode(true);
        // Find the other experience menus the first time Help needs to hide
        // them.
        if (underlyingUI == null)
        {
            // Collect the experience UI groups that Help must hide and block.
            var groups = new System.Collections.Generic.List<CanvasGroup>();
            // Search even inactive canvases because some experience stages are
            // at first hidden.
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Only include these named experience canvases; do not block
                // Help's own controls.
                if (canvas.name != "Conversation Canvas" && canvas.name != "Start Experience Canvas" && canvas.name != "Canvas Recording" && canvas.name != "Performance Controls Canvas") continue;
                // Reuse an existing CanvasGroup rather than adding another one
                // each open.
                var group = canvas.GetComponent<CanvasGroup>();
                // Add a group when needed to control visibility and input
                // together.
                if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
                // Remember the group in this first-open collection.
                groups.Add(group);
            }
            // Cache this list of groups for later Help open/close operations.
            underlyingUI = groups.ToArray();
        }
        // Hide those groups and disable their interaction/pointer blocking
        // while Help is open.
        foreach (var group in underlyingUI) { group.interactable = false; group.blocksRaycasts = false; group.alpha = 0f; }
        // Keep the assigned controller ray lines visible for menu pointing.
        SetLaserVisuals(true);
    }

    // Hide Help, show its shortcut and restore the underlying menu groups.
    // Return to sticks unless the recorder still needs review pointers.
    public void CloseHelp()
    {
        // Hide the popup when closing, if its existing reference still exists.
        if (helpPopup != null) helpPopup.SetActive(false);
        // Show the small Help shortcut again after the popup closes.
        if (badgeCanvas != null) badgeCanvas.gameObject.SetActive(true);
        // Only switch sticks/menu rays when the mode controller is available.
        if (drumModeVisuals != null)
        {
            // Read recorder review state to decide whether closing Help should
            // show rays or sticks.
            var recorder = FindFirstObjectByType<DrumRecorder>();
            // Keep menu rays for review/non-drum mode; otherwise restore the
            // remembered drum mode.
            drumModeVisuals.SetMenuMode(!drumModeVisuals.IsDrumModeActive || (recorder != null && recorder.IsReviewing));
        }
        // Show the other menus again only if their saved list exists.
        if (underlyingUI != null)
            // Only restore underlying groups if the first-open collection was
            // created.
            foreach (var group in underlyingUI) { group.interactable = true; group.blocksRaycasts = true; group.alpha = 1f; }
        // Restore the cached groups to visible and interactive; this sets alpha
        // to one.
        SetLaserVisuals(true);
    }

    // Show or hide the assigned controller pointer lines according to visible.
    public void SetLaserVisuals(bool visible)
    {
        // Change the existing left controller line visual only if it was
        // assigned.
        if (leftLineVisual != null) leftLineVisual.SetActive(visible);
        // Change the right controller line to the same requested visibility.
        if (rightLineVisual != null) rightLineVisual.SetActive(visible);
    }
}
