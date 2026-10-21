/*
 * Shows a short explanation when a Quest pointer rests on a recording button.
 * DrumRecorder adds this to its existing buttons when it creates their layout.
 * The label follows the recorder's current action without changing clicks.
 * Pointer-enter and pointer-exit are Unity's notices that a pointer arrived
 * at or left a button. Only tracked Quest-style pointers show these labels.
 * This is gameplay code. It does not add keyboard or mouse controls.
 * A RectTransform sets a UI object's position and size inside its parent.
 * A Canvas holds UI; a CanvasGroup can hide it or stop it receiving input.
 */
// Remember each controller pointer separately, so either hand can hover.
using System.Collections.Generic;
// Use the text labels already used by the recording panel.
using TMPro;
// Use Unity objects and the methods Unity calls during the game.
using UnityEngine;
// Receive pointer arrival and departure through the existing UI input system.
using UnityEngine.EventSystems;
// Read the existing button's availability without changing its click action.
using UnityEngine.UI;
// Distinguish tracked controller events from desktop mouse events.
using UnityEngine.XR.Interaction.Toolkit.UI;

// Explain recording buttons without taking over their trigger clicks.
public sealed class RecordingButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    // Identify which existing recording action this label explains.
    public enum Action { Record, Retake, Playback, Save }

    // Read whether the take is recording, playing or paused.
    private DrumRecorder recorder;
    // Keep the existing button so unavailable controls can hide their label.
    private Button button;
    // Remember which action belongs to this particular button.
    private Action action;
    // Keep the small label background created for this button.
    private GameObject panel;
    // Keep its text so Play/Pause or Start/Stop can change while hovered.
    private TMP_Text label;
    // Remember both hands if both controller rays point at the same button.
    private readonly HashSet<int> pointers = new HashSet<int>();

    // Add one label to target, an existing button. owner supplies its state;
    // kind selects its wording; offset places the label near that button.
    public static void Attach(GameObject target, DrumRecorder owner,
        Action kind, Vector2 offset)
    {
        // Skip an optional missing button instead of changing the menu setup.
        if (target == null) return;
        // Reuse an existing helper so setup cannot add duplicate labels.
        var hover = target.GetComponent<RecordingButtonHover>();
        // Add only the missing helper; keep the existing Button component.
        if (hover == null) hover = target.AddComponent<RecordingButtonHover>();
        // Remember the recorder that decides what this button currently does.
        hover.recorder = owner;
        // Keep the button's current interaction settings available for checks.
        hover.button = target.GetComponent<Button>();
        // Remember which wording this button needs.
        hover.action = kind;
        // Create the label once, leaving button placement and clicks alone.
        if (hover.panel == null) hover.CreateLabel(offset);
    }

    // Create a readable label at offset, measured from the button's centre.
    private void CreateLabel(Vector2 offset)
    {
        // Keep the label narrow enough to fit between adjacent review buttons.
        var size = new Vector2(244f, 44f);
        // Give the white wording a dark background so it is readable in VR.
        var background = QuestUI.Background("Recording Hover Label", transform,
            size, new Color(.04f, .06f, .1f, .96f));
        // Save the label object so it can be hidden immediately on pointer
        // exit.
        panel = background.gameObject;
        // Place only the new label; do not move or resize the existing button.
        background.rectTransform.anchoredPosition = offset;
        // Add a CanvasGroup to the label panel and call it group.
        var group = panel.AddComponent<CanvasGroup>();
        // Make the label a visual explanation rather than another control.
        group.interactable = false;
        // Stop the label from blocking button clicks.
        group.blocksRaycasts = false;
        // Create text that also has raycastTarget disabled by QuestUI.Text.
        label = QuestUI.Text("Action", panel.transform, "", Vector2.zero,
            size - new Vector2(12f, 4f), 24f, Color.white);
        // Keep the label hidden until a tracked pointer arrives.
        panel.SetActive(false);
    }

    // Show this button's explanation when a tracked controller pointer arrives.
    // eventData tells us which pointer arrived and what kind of pointer it is.
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Ignore mouse events; this feature uses the existing Quest UI rays.
        if (!(eventData is TrackedDeviceEventData)) return;
        // Remember this hand without adding the same pointer more than once.
        pointers.Add(eventData.pointerId);
        // Show the wording for the button's action right now.
        RefreshLabel();
    }

    // Hide the explanation after the last tracked pointer leaves this button.
    // eventData identifies the departing pointer, including left/right hands.
    public void OnPointerExit(PointerEventData eventData)
    {
        // Do not let an unrelated mouse event change a controller hover.
        if (!(eventData is TrackedDeviceEventData)) return;
        // Forget only the hand that left, keeping the other hand if still here.
        pointers.Remove(eventData.pointerId);
        // Hide or update the label using the remaining pointers.
        RefreshLabel();
    }

    // Check after normal updates so state changes update an already shown
    // label.
    private void LateUpdate()
    {
        // Avoid checking hidden labels when neither controller is hovering.
        if (pointers.Count == 0) return;
        // Follow recording, preview, panel visibility and Help blocking
        // changes.
        RefreshLabel();
    }

    // Check whether this recording button is still visible and available.
    private bool CanShowLabel()
    {
        // Stop showing a label after its recorder or button has disappeared.
        if (recorder == null || button == null || !button.IsActive() ||
            !button.IsInteractable()) return false;
        // Respect a disabled canvas, such as during musician performance.
        foreach (var canvas in GetComponentsInParent<Canvas>())
            // Do not show a label for a canvas that is no longer drawing.
            if (!canvas.isActiveAndEnabled) return false;
        // Respect hidden menu groups, including the group hidden by Help.
        foreach (var group in GetComponentsInParent<CanvasGroup>())
        {
            // Disabled groups do not apply their visibility or input settings.
            if (!group.isActiveAndEnabled) continue;
            // Treat invisible or blocked recording controls as unavailable.
            if (group.alpha <= .001f || !group.interactable ||
                !group.blocksRaycasts) return false;
            // Stop at a group that deliberately ignores groups above it.
            if (group.ignoreParentGroups) break;
        }
        // The button is visible and available to the controller pointer.
        return true;
    }

    // Show the current wording, or clear hover when the button stops being
    // usable.
    private void RefreshLabel()
    {
        // Wait for setup if the label object has not been created yet.
        if (panel == null) return;
        // Clear old pointer state when a panel closes or a button becomes
        // hidden.
        if (!CanShowLabel()) pointers.Clear();
        // Show a label only while at least one tracked pointer is still here.
        bool visible = pointers.Count > 0;
        // Only change visibility when needed, keeping the existing button
        // alone.
        if (panel.activeSelf != visible) panel.SetActive(visible);
        // Do not update wording for a label that is hidden.
        if (!visible) return;
        // Use recording state for Start/Stop and preview state for Play/Pause.
        label.text = action == Action.Record
            ? (recorder.IsRecording ? "Stop recording" : "Start recording")
            : action == Action.Retake ? "Record again"
            : action == Action.Save ? "Save recording"
            : recorder.IsReplaying && !recorder.IsPlaybackPaused
                ? "Pause playback" : "Play recording";
    }

    // Forget hover when the button or its panel becomes inactive.
    private void OnDisable()
    {
        // A later panel opening must wait for a fresh pointer-enter event.
        pointers.Clear();
        // Hide the label even if Unity did not send a pointer-exit event.
        if (panel != null) panel.SetActive(false);
    }
}
