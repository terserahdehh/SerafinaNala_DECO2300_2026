/*
 * Connects Quest buttons to the experience and keeps manual travel disabled.
 * Unity adds this component to XR Origin when the first scene has
 * IntroSequence.
 * A advances dialogue or starts/stops seated recording, using one action per
 * new press. X toggles Help. Trigger clicks and the B kick are handled
 * elsewhere.
 * This is active game code. In Editor Play Mode it also reads keyboard A/X.
 * XR Origin groups the player camera and controllers. An EventSystem sends
 * UI clicks to buttons. An input module reads
 * pointer input for it. This script keeps one such system/module active to
 * avoid duplicate clicks. Movement providers are the components that normally
 * let the player travel manually; scripted travel is used here instead.
 * UI means the buttons and text the player sees and uses.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use the UI event system that sends pointer/button events.
using UnityEngine.EventSystems;
// Use keyboard, mouse and controller input actions.
using UnityEngine.InputSystem;
// Use the tracked Quest device and button-reading API.
using UnityEngine.XR;
// Use movement providers kept disabled during scripted player travel.
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
// Use the scene and menu XR pointing interactors.
using UnityEngine.XR.Interaction.Toolkit.Interactors;
// Use XR canvas raycasters and the headset UI input module.
using UnityEngine.XR.Interaction.Toolkit.UI;
// Use the existing controller action manager that this experience disables.
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;

// Connects Quest buttons to the experience and keeps manual travel disabled.
[DefaultExecutionOrder(-10000)]
public class QuestExperienceControls : MonoBehaviour
{
    // Keep the onboarding coordinator used for dialogue and sitting input
    // gating.
    private IntroSequence intro;
    // Keep the recording controller used by the button handler.
    private DrumRecorder recorder;
    // Keep the Help controller used to respect its open state.
    private HelpMenu help;
    // Keep the locomotion providers kept disabled during scripted travel.
    private LocomotionProvider[] manualMovement;
    // Remember whether A was held on the previous frame, so holding A cannot
    // repeat clicks.
    private bool previousA;
    // Remember whether X was held on the previous frame, so holding X cannot
    // repeatedly toggle Help.
    private bool previousX;

    // After the first scene loads, add this button helper when XR Origin and
    // IntroSequence exist. Do not add a second helper to the same player.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Find the scene player rig for this UI or controller setup.
        var origin = FindFirstObjectByType<XROrigin>();
        // Install only in an experience scene that has both XR Origin and
        // IntroSequence.
        if (origin != null && FindFirstObjectByType<IntroSequence>() != null &&
            origin.GetComponent<QuestExperienceControls>() == null)
            // Add the button handler at game-created, which immediately runs
            // its Awake setup.
            origin.gameObject.AddComponent<QuestExperienceControls>();
    }

    // Set up one UI input path. Stop manual movement and the template
    // controller managers from replacing the experience's menu pointers.
    private void Awake()
    {
        // Find the onboarding controller so A can advance the active dialogue.
        intro = FindFirstObjectByType<IntroSequence>();
        // Find the recorder for the sitting A-button recording shortcut.
        recorder = FindFirstObjectByType<DrumRecorder>();
        // Find Help so this script can respect its current visibility and input
        // block.
        help = FindFirstObjectByType<HelpMenu>();
        // Collect every locomotion provider under XR Origin, including inactive
        // ones.
        manualMovement = GetComponentsInChildren<LocomotionProvider>(true);
        // Keep locomotion providers disabled so automatic travel remains in
        // control.
        LockManualMovement();
        // Find the template action managers that otherwise switch thumbstick
        // teleport mode.
        foreach (var manager in GetComponentsInChildren<ControllerInputActionManager>(true))
            // Disable those managers so they cannot replace the experience's
            // menu rays.
            manager.enabled = false;
        // Allow existing ray interactors to work with room canvases.
        foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true)) ray.enableUIInteraction = true;
        // Allow NearFar interactors to work with the same UI when they are
        // active.
        foreach (var ray in GetComponentsInChildren<NearFarInteractor>(true)) ray.enableUIInteraction = true;
        // Prefer the currently active EventSystem as the single UI button
        // handler.
        var eventSystem = EventSystem.current;
        // Look for an existing inactive EventSystem before creating a new one.
        if (eventSystem == null) eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        // Create a UI button handler only when the scene has none to reuse.
        if (eventSystem == null)
            // Create one EventSystem with the XR UI input module needed by
            // Quest menus.
            eventSystem = new GameObject("Quest EventSystem", typeof(EventSystem), typeof(XRUIInputModule)).GetComponent<EventSystem>();
        // Activate the chosen EventSystem GameObject so UI can receive updates.
        eventSystem.gameObject.SetActive(true);
        // Enable its component as well as its containing object.
        eventSystem.enabled = true;
        // Find the XR input module on that chosen EventSystem.
        var xrModule = eventSystem.GetComponent<XRUIInputModule>();
        // Add it only when missing, avoiding duplicate UI input modules.
        if (xrModule == null) xrModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        // Inspect other scene EventSystems, including inactive ones.
        foreach (var otherSystem in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            // Disable competing EventSystems so a click is sent through only
            // one system.
            if (otherSystem != eventSystem) otherSystem.enabled = false;
        // Inspect all UI input modules that could otherwise play or pass on the
        // same press.
        foreach (var module in FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            // Leave only the selected XR module enabled.
            module.enabled = module == xrModule;
        // Do not interpret gamepad automatic button selection as another
        // experience UI input path.
        xrModule.enableGamepadInput = false;
        // Do not interpret joystick automatic button selection as extra menu
        // movement/clicks.
        xrModule.enableJoystickInput = false;
        // Keep touch input from competing with the intended headset/mouse path.
        xrModule.enableTouchInput = false;
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
        // Enable mouse UI clicks in desktop Play Mode.
        xrModule.enableMouseInput = true;
        // Disable simulated XR UI presses there so one desktop click cannot be
        // sent twice.
        xrModule.enableXRInput = false;
        // Allow the module's built-in mouse actions for desktop testing.
        xrModule.enableBuiltinActionsAsFallback = true;
// Use this alternative setup in the built game, including on Quest.
#else
        // Disable desktop mouse input in the headset player build.
        xrModule.enableMouseInput = false;
        // Use real XR controller input for Quest UI clicks.
        xrModule.enableXRInput = true;
// End the Editor-specific choice; the following shared code is kept for both.
#endif
        // Reserve A/X for this button handler instead of also treating them as
        // UI automatic button selection.
        eventSystem.sendNavigationEvents = false;
        // Remember whether A is already held so startup does not invent a new
        // press.
        previousA = ReadPrimary(XRNode.RightHand);
        // Remember the initial X state for the same reason.
        previousX = ReadPrimary(XRNode.LeftHand);
    }

    // Keep the collected manual movement components disabled. IntroSequence and
    // PerformanceSequence move the player automatically.
    private void LockManualMovement()
    {
        // Stop here if manual movement components have not been collected yet.
        if (manualMovement == null) return;
        // Visit each movement provider collected during setup.
        foreach (var provider in manualMovement)
            // Disable surviving providers so thumbsticks cannot override
            // scripted travel.
            if (provider != null) provider.enabled = false;
    }

    // Keep manual travel off and read whether A/X are currently pressed. Editor
    // Play Mode can also read keyboard A/X.
    private void Update()
    {
        // Keep locomotion providers disabled so automatic travel remains in
        // control.
        LockManualMovement();
        // Read the current right-controller A state.
        bool a = ReadPrimary(XRNode.RightHand);
        // Read the current left-controller X state.
        bool x = ReadPrimary(XRNode.LeftHand);
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
        // Get desktop keyboard input only inside the Editor branch.
        var keyboard = Keyboard.current;
        // Let keyboard A provide the same current-state input while desktop
        // testing.
        a |= keyboard != null && keyboard.aKey.isPressed;
        // Let keyboard X provide the same Help shortcut while desktop testing.
        x |= keyboard != null && keyboard.xKey.isPressed;
// End the Editor-specific choice; the following shared code is kept for both.
#endif
        // Send both current states to the one press-edge button handler.
        HandleButtons(a, x);
    }

    // Use only new presses. a is the current right-hand A state; x is the
    // current left-hand X state. Give A to dialogue first, otherwise to ready
    // sitting recording. X toggles Help.
    public void HandleButtons(bool a, bool x)
    {
        // Accept A only on its change from released to held.
        bool aPressed = a && !previousA;
        // Detect X the same way so holding it does not toggle Help repeatedly.
        bool xPressed = x && !previousX;
        // Remember A's current state for the next frame's comparison.
        previousA = a;
        // Remember X's current state for the next frame.
        previousX = x;
        // Give a new X press to Help if the scene has a Help controller.
        if (xPressed && help != null) help.ToggleHelp();
        // Ignore A when not newly pressed or while Help blocks the experience
        // menus.
        if (!aPressed || (help != null && help.IsOpen)) return;
        // Give A to open dialogue first, so closing dialogue does not also
        // start recording.
        if (intro != null && intro.IsDialogueActive) intro.NextDialogue();
        // Otherwise require completed seating, a recorder and a ready recording
        // state.
        else if (intro != null && intro.IsAtDrumSeat && recorder != null && recorder.CanToggleRecording)
            // Use that accepted A press to start or stop recording.
            recorder.ToggleRecording();
    }

    // Keep manual movement disabled after other game scripts finish their
    // updates too.
    private void LateUpdate() { LockManualMovement(); }

    // Read the primary face button on hand, the requested controller. Return
    // true only if the device reports that button is pressed.
    private static bool ReadPrimary(XRNode hand)
    {
        // Get the tracked device at the requested controller hand node.
        var device = InputDevices.GetDeviceAtXRNode(hand);
        // Return true only if the device exposes the primary button and it is
        // currently pressed.
        return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed;
    }
}
