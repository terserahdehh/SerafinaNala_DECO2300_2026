using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;

[DefaultExecutionOrder(-10000)]
public class QuestExperienceControls : MonoBehaviour
{
    private IntroSequence intro;
    private DrumRecorder recorder;
    private HelpMenu help;
    private LocomotionProvider[] manualMovement;
    private bool previousA;
    private bool previousX;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && FindFirstObjectByType<IntroSequence>() != null &&
            origin.GetComponent<QuestExperienceControls>() == null)
            origin.gameObject.AddComponent<QuestExperienceControls>();
    }

    private void Awake()
    {
        intro = FindFirstObjectByType<IntroSequence>();
        recorder = FindFirstObjectByType<DrumRecorder>();
        help = FindFirstObjectByType<HelpMenu>();
        manualMovement = GetComponentsInChildren<LocomotionProvider>(true);
        LockManualMovement();
        // Stop thumbstick teleport mode from replacing the UI ray interactor.
        foreach (var manager in GetComponentsInChildren<ControllerInputActionManager>(true))
            manager.enabled = false;
        foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true)) ray.enableUIInteraction = true;
        foreach (var ray in GetComponentsInChildren<NearFarInteractor>(true)) ray.enableUIInteraction = true;
        var eventSystem = EventSystem.current;
        if (eventSystem == null) eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
            eventSystem = new GameObject("Quest EventSystem", typeof(EventSystem), typeof(XRUIInputModule)).GetComponent<EventSystem>();
        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;
        var xrModule = eventSystem.GetComponent<XRUIInputModule>();
        if (xrModule == null) xrModule = eventSystem.gameObject.AddComponent<XRUIInputModule>();
        // One EventSystem and one input module handle both input paths.
        foreach (var otherSystem in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (otherSystem != eventSystem) otherSystem.enabled = false;
        foreach (var module in FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            module.enabled = module == xrModule;
        xrModule.enableGamepadInput = false;
        xrModule.enableJoystickInput = false;
        xrModule.enableTouchInput = false;
#if UNITY_EDITOR
        // Mouse owns UI clicks in desktop testing. Simulated trigger clicks must not also press UI.
        xrModule.enableMouseInput = true;
        xrModule.enableXRInput = false;
        xrModule.enableBuiltinActionsAsFallback = true;
        EditorExperienceTesting.Install(GetComponent<XROrigin>());
#else
        xrModule.enableMouseInput = false;
        xrModule.enableXRInput = true;
#endif
        // A/X belong to this experience; UI clicks come from the controller trigger.
        eventSystem.sendNavigationEvents = false;
        previousA = ReadPrimary(XRNode.RightHand);
        previousX = ReadPrimary(XRNode.LeftHand);
    }

    private void LockManualMovement()
    {
        if (manualMovement == null) return;
        foreach (var provider in manualMovement)
            if (provider != null) provider.enabled = false;
    }

    private void Update()
    {
        LockManualMovement();
        bool a = ReadPrimary(XRNode.RightHand);
        bool x = ReadPrimary(XRNode.LeftHand);
#if UNITY_EDITOR
        var keyboard = Keyboard.current;
        a |= keyboard != null && keyboard.aKey.isPressed;
        x |= keyboard != null && keyboard.xKey.isPressed;
#endif
        HandleButtons(a, x);
    }

    // One dispatcher prevents a dialogue-closing press from also starting recording.
    public void HandleButtons(bool a, bool x)
    {
        bool aPressed = a && !previousA;
        bool xPressed = x && !previousX;
        previousA = a;
        previousX = x;
        if (xPressed && help != null) help.ToggleHelp();
        if (!aPressed || (help != null && help.IsOpen)) return;
        if (intro != null && intro.IsDialogueActive) intro.NextDialogue();
        else if (intro != null && intro.IsAtDrumSeat && recorder != null && recorder.CanToggleRecording)
            recorder.ToggleRecording();
    }

    private void LateUpdate() { LockManualMovement(); }

    private static bool ReadPrimary(XRNode hand)
    {
        var device = InputDevices.GetDeviceAtXRNode(hand);
        return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed;
    }
}
