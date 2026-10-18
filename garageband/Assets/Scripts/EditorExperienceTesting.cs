#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

// Only compiled in the Editor. Uses the scene's simulator and never moves the XR Origin.
[DefaultExecutionOrder(-30000)]
public sealed class EditorExperienceTesting : MonoBehaviour
{
    private XRDeviceSimulator simulator;
    private InputActionAsset controls;
    private InputActionMap positioning;
    private readonly List<InputActionReference> references = new List<InputActionReference>();
    private static XRGeneralSettings originalXRSettings;
    private static XRGeneralSettings desktopXRSettings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void UseSimulatedTracking()
    {
        // Before XR Management's AfterAssembliesLoaded startup: no OpenXR runtime/headset needed.
        // Clone settings in memory; Android and project settings assets are never edited.
        originalXRSettings = XRGeneralSettings.Instance;
        if (originalXRSettings == null) return;
        desktopXRSettings = Instantiate(originalXRSettings);
        desktopXRSettings.hideFlags = HideFlags.HideAndDontSave;
        desktopXRSettings.InitManagerOnStart = false;
        XRGeneralSettings.Instance = desktopXRSettings;
        EditorApplication.playModeStateChanged -= RestoreXRSettings;
        EditorApplication.playModeStateChanged += RestoreXRSettings;
    }

    private static void RestoreXRSettings(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (originalXRSettings != null) XRGeneralSettings.Instance = originalXRSettings;
        if (desktopXRSettings != null) DestroyImmediate(desktopXRSettings);
        originalXRSettings = null;
        desktopXRSettings = null;
        EditorApplication.playModeStateChanged -= RestoreXRSettings;
    }

    public static void Install(XROrigin origin)
    {
        if (origin.GetComponent<EditorExperienceTesting>() == null)
            origin.gameObject.AddComponent<EditorExperienceTesting>();
    }

    private void Awake()
    {
        var candidates = FindObjectsByType<XRDeviceSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (candidates.Length != 1)
        {
            Debug.LogError("Desktop testing requires exactly one existing XR Device Simulator in the scene (including inactive objects). Found " + candidates.Length, this);
            return;
        }
        simulator = candidates[0];
        simulator.cameraTransform = GetComponent<XROrigin>().Camera.transform;
        // Hide the sample's default-key overlay: this experience reserves A/X/B/P.
        simulator.deviceSimulatorUI = null;
        simulator.gameObject.SetActive(true);
        simulator.enabled = true;
        simulator.desiredCursorLockMode = CursorLockMode.None;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        controls = ScriptableObject.CreateInstance<InputActionAsset>();
        controls.name = "Editor Experience Controls (runtime only)";
        var map = controls.AddActionMap("Desktop testing");
        positioning = controls.AddActionMap("Device positioning");
        var horizontal = positioning.AddAction("Controller horizontal", InputActionType.Value, expectedControlLayout: "Axis");
        horizontal.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
        var depth = positioning.AddAction("Controller depth", InputActionType.Value, expectedControlLayout: "Axis");
        depth.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
        var vertical = positioning.AddAction("Controller height", InputActionType.Value, expectedControlLayout: "Axis");
        vertical.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");
        var mouse = positioning.AddAction("Device mouse delta", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
        var scroll = positioning.AddAction("Device mouse scroll", InputActionType.Value, "<Mouse>/scroll", expectedControlLayout: "Vector2");
        var constraint = map.AddAction("X constraint", InputActionType.Button, "<Keyboard>/f1");
        var unused = map.AddAction("Reserved by experience", InputActionType.Button);
        // Set via supported setters after OnEnable, which avoids duplicate subscriptions.
        simulator.keyboardXTranslateAction = Reference(horizontal);
        simulator.keyboardZTranslateAction = Reference(depth);
        simulator.keyboardYTranslateAction = Reference(vertical);
        simulator.mouseDeltaAction = Reference(mouse);
        simulator.mouseScrollAction = Reference(scroll);
        simulator.xConstraintAction = Reference(constraint);
        simulator.primaryButtonAction = Reference(unused); // B is the pedal, A/X use the dispatcher.
        simulator.toggleManipulateBodyAction = Reference(unused); // Scripted travel owns the player.
        simulator.handControllerModeAction = Reference(unused); // Keep controller drumsticks available.
        controls.Enable();
        Update();
        Debug.Log("EDITOR DESKTOP TESTING: mouse UI; A dialogue/record; X Help; B pedal; P sticks. " +
            "Hold Shift/Space for left/right controller; mouse, arrows and Q/E position it; Ctrl rotates; Esc releases.", this);
    }

    private void Start()
    {
        // Include authored canvases such as the blackout overlay; runtime canvases use QuestUI.
        var eye = GetComponent<XROrigin>().Camera;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null) canvas.worldCamera = eye;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }
    }

    private void Update()
    {
        if (simulator == null || positioning == null) return;
        // The sample starts/returns to FPS mode when no device is selected. Mouse movement in
        // that mode would turn the camera while clicking menus. Gate pose input, not tracking.
        if (simulator.manipulatingFPS) positioning.Disable();
        else positioning.Enable();
    }

    private InputActionReference Reference(InputAction action)
    {
        var reference = InputActionReference.Create(action);
        references.Add(reference);
        return reference;
    }

    private void OnDestroy()
    {
        if (simulator != null) simulator.enabled = false;
        controls?.Disable();
        foreach (var reference in references) if (reference != null) Destroy(reference);
        if (controls != null) Destroy(controls);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
#endif
