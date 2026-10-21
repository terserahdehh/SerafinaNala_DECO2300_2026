/*
 * Makes one menu pointer follow the player's left or right controller.
 * DrumModeVisuals adds it to a copy of Resources/QuestMenuRay while the game
 * runs. The trigger clicks buttons. The thumbstick scrolls Help only when the
 * pointer is over its scroll area. This pointer does not select room objects.
 * This is active game code and can run in Editor Play Mode; desktop mouse
 * clicks are handled by the separate Editor input setup.
 * An InputAction reads a controller value. This helper creates its own actions
 * and copies the existing scroll bindings without changing the shared asset.
 * A raycaster checks what the pointer touches. Unity layers group objects;
 * this pointer checks the UI layer. A material controls the pointer's look.
 * UI means the buttons and text the player sees and uses.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use keyboard, mouse and controller input actions.
using UnityEngine.InputSystem;
// Use the scene and menu XR pointing interactors.
using UnityEngine.XR.Interaction.Toolkit.Interactors;
// Use input readers that connect owned actions to an XR ray.
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

// Makes one menu pointer follow the player's left or right controller.
[DefaultExecutionOrder(-200)]
public class QuestControllerMenuRay : MonoBehaviour
{
    // Keep the owned action reading this controller's tracked aim position.
    private InputAction position;
    // Keep the owned action reading its tracked aim rotation.
    private InputAction rotation;
    // Keep the owned action reading a new menu trigger press.
    private InputAction trigger;
    // Keep the owned action reading how far the menu trigger is pressed.
    private InputAction triggerValue;
    // Keep the owned copy of this controller's menu scroll action.
    private InputAction scroll;
    // Keep the bridge sending manual vertical scroll values to XR UI.
    private XRInputValueReader<Vector2> scrollReader;
    // Keep the scroll rate before XR UI and ScrollRect scaling.
    private const float ScrollUnitsPerSecond = 3f;

    // Connect this pointer to one controller. left chooses the hand;
    // inputActions supplies the existing scroll bindings. Keep one owned set of
    // inputs per pointer.
    public void Initialize(bool left, InputActionAsset inputActions)
    {
        // Avoid creating a second owned set of input actions on an already set
        // up ray.
        if (position != null) return;
        // Select the left or right controller binding path for this particular
        // ray.
        string path = left ? "<XRController>{LeftHand}" : "<XRController>{RightHand}";
        // Read the controller's tracked pointer position rather than a
        // hand-tracking pose.
        position = new InputAction("Menu Aim Position", InputActionType.Value, path + "/pointerPosition", expectedControlType: "Vector3");
        // Read the matching tracked pointer rotation for the aiming direction.
        rotation = new InputAction("Menu Aim Rotation", InputActionType.Value, path + "/pointerRotation", expectedControlType: "Quaternion");
        // Create the trigger-press-press action used for menu clicks.
        trigger = new InputAction("Menu Click", InputActionType.Button, path + "/triggerPressed");
        // Create the trigger amount action expected by the XR button reader.
        triggerValue = new InputAction("Menu Click Value", InputActionType.Value, path + "/trigger");
        // Choose the existing UI Scroll action name for this controller side.
        string scrollPath = left ? "XRI Left Interaction/UI Scroll" : "XRI Right Interaction/UI Scroll";
        // Look up that action in the supplied asset without throwing if it is
        // missing.
        var sourceScroll = inputActions != null ? inputActions.FindAction(scrollPath, false) : null;
        // Clone scroll bindings only when the expected project action was
        // found.
        if (sourceScroll != null)
        {
            // Own an independent copy so enabling this ray does not change the
            // shared action asset.
            scroll = sourceScroll.Clone();
        }
        // Report a missing scroll binding instead of inventing a new controller
        // mapping.
        else Debug.LogError("Menu ray is missing the existing " + scrollPath + " action. Assign Menu Input Actions on DrumModeVisuals.", this);
        // Find the XR ray on this game-created menu-ray object.
        var ray = GetComponent<XRRayInteractor>();
        // Let this controller pointer send clicks to the canvas buttons it
        // points at.
        ray.enableUIInteraction = true;
        // Allow UI clicks even if another world selectable object is selected.
        ray.blockUIOnInteractableSelection = false;
        // Exclude world interactables from this dedicated UI ray.
        ray.interactionLayers = 0;
        // Limit its physics raycast to the UI layer, whose numeric index is
        // five.
        ray.raycastMask = 1 << 5;
        // Create the XR button reader that translates owned trigger input to UI
        // pressing.
        ray.uiPressInput = new XRInputButtonReader("Menu Click") {
            inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction,
            inputActionPerformed = trigger,
            inputActionValue = triggerValue
        };
        // Create a two-dimensional scroll reader whose values this helper
        // supplies each frame.
        scrollReader = new XRInputValueReader<Vector2>("Menu UI Scroll")
        {
            inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue
        };
        // Connect that reader to the XR ray's UI scroll input.
        ray.uiScrollInput = scrollReader;
        // Find the ray's visible line so it can use the shared UI overlay
        // shader.
        var line = GetComponent<LineRenderer>();
        // Only change line appearance if this prefab has a LineRenderer.
        if (line != null)
        {
            // Load the shader that keeps the pointer line visible over room
            // geometry.
            var shader = Resources.Load<Shader>("QuestOverlayUI");
            // Create the game-created line material only when that shader is
            // available.
            if (shader != null)
            {
                // Make a separate material instead of editing a shared project
                // material.
                var material = new Material(shader);
                // Draw this overlay material after ordinary room geometry.
                material.renderQueue = 3100;
                // Assign the material to this ray's visible line.
                line.material = material;
            }
        }
        // Enable newly assigned actions now if OnEnable already ran before
        // Initialize.
        if (isActiveAndEnabled) EnableActions();
    }

    // Turn on this pointer's own input actions so they can read the controller.
    private void EnableActions()
    {
        // Enable all five owned actions; optional scroll is safe when its
        // binding is absent.
        position?.Enable(); rotation?.Enable(); trigger?.Enable(); triggerValue?.Enable(); scroll?.Enable();
    }

    // Make a reused menu pointer work again after a transition. Refresh its UI
    // connection instead of adding another pointer or input module.
    public void RestoreUIInteraction()
    {
        // Do not restore input for a ray that is currently hidden or disabled.
        if (!isActiveAndEnabled) return;
        // Enable this ray's owned actions so current controller values can
        // reach UI.
        EnableActions();
        // Find the XR ray on this game-created menu-ray object.
        var ray = GetComponent<XRRayInteractor>();
        // Stop restoration if this object no longer has its expected XR ray.
        if (ray == null) return;
        // Re-enable the existing XR ray rather than creating a duplicate
        // pointer.
        ray.enabled = true;
        // Briefly disconnect the old UI pointer so a repeated performance can
        // clear its stale click state.
        ray.enableUIInteraction = false;
        // Reconnect this existing pointer to UI without making another pointer
        // object.
        ray.enableUIInteraction = true;
    }

    // Enable owned input actions when this pointer is shown.
    private void OnEnable()
    {
        // Enable this ray's owned actions so current controller values can
        // reach UI.
        EnableActions();
    }

    // Read vertical scrolling and send it through the UI pointer. Follow this
    // controller's tracked aim position and turn.
    private void Update()
    {
        // Send scroll input only if the pointer's scroll reader exists.
        if (scrollReader != null)
        {
            // Read the current scroll direction, or use no scroll if the action
            // is missing.
            Vector2 axis = scroll != null ? scroll.ReadValue<Vector2>() : Vector2.zero;
            // Send only up/down scroll, scaled by real seconds since the last
            // update so it scrolls at a steady rate.
            scrollReader.manualValue = new Vector2(0f, axis.y) * (ScrollUnitsPerSecond * Time.unscaledDeltaTime);
        }
        // Do not overwrite the ray pose until both pose actions and a tracked
        // control are available.
        if (position == null || rotation == null || position.activeControl == null) return;
        // Move this menu pointer to the controller position reported inside the
        // player tracking group.
        transform.localPosition = position.ReadValue<Vector3>();
        // Turn it to the controller's reported aiming direction.
        transform.localRotation = rotation.ReadValue<Quaternion>();
    }

    // Clear scrolling and stop owned input actions when the pointer is hidden.
    private void OnDisable()
    {
        // Clear pending scroll when hiding the ray so the menu cannot keep
        // scrolling.
        if (scrollReader != null) scrollReader.manualValue = Vector2.zero;
        // Disable the five owned actions so hidden rays stop consuming input.
        position?.Disable(); rotation?.Disable(); trigger?.Disable(); triggerValue?.Disable(); scroll?.Disable();
    }

    // Release the owned actions when the pointer is destroyed, so no unused
    // input resources remain.
    private void OnDestroy()
    {
        // Dispose those owned actions when the ray is destroyed, freeing their
        // input resources.
        position?.Dispose(); rotation?.Dispose(); trigger?.Dispose(); triggerValue?.Dispose(); scroll?.Dispose();
    }
}
