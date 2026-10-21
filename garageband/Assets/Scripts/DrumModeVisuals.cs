/*
 * Shows the player's sticks for drumming and controller pointers for menus.
 * IntroSequence decides when the player should use drumsticks.
 * It calls this script after moving the player to the drum seat.
 * This script shows the sticks and hides the controller models.
 * When the player uses menus, it shows controllers and pointers instead.
 * DrumRecorder, PerformanceSequence and HelpMenu switch the visible controls
 * as the player records, reviews, watches a musician or opens Help.
 * This is active game code and also runs during Play Mode inside the Editor.
 * A GameObject is an object in the Unity scene. A component adds behaviour
 * to it. A ray is the controller's laser-like pointer used to click buttons.
 * XR Origin groups the tracked player camera and controllers. The menu ray
 * copies come from Resources/QuestMenuRay; QuestControllerMenuRay reads input.
 * The Inspector is Unity's settings panel for assigning objects and values.
 * A prefab is a saved reusable object; this code can make separate copies.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use keyboard, mouse and controller input actions.
using UnityEngine.InputSystem;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;
// Use the scene and menu XR pointing interactors.
using UnityEngine.XR.Interaction.Toolkit.Interactors;
// Use XR ray visual types available to this script.
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

// Shows the player's sticks for drumming and controller pointers for menus.
public class DrumModeVisuals : MonoBehaviour
{
    // Keep the left controller model shown while using menus.
    [Header("Controller Models")]
    [SerializeField] private GameObject leftControllerVisual;
    // Keep the right controller model shown while using menus.
    [SerializeField] private GameObject rightControllerVisual;

    // Keep the player left-hand stick object, separate from musician visuals.
    [Header("Drumsticks")]
    [SerializeField] private GameObject leftDrumstick;
    // Keep the player right-hand stick object, separate from musician visuals.
    [SerializeField] private GameObject rightDrumstick;

    // Keep the existing input asset supplying menu scroll bindings.
    [Header("Menu UI Input")]
    [SerializeField] private InputActionAsset menuInputActions;

    // Remember whether drum mode is remembered as active, so Help can return to
    // drumming after closing.
    private bool drumModeActive;
    // Remember whether menu mode is active, so rays replace sticks temporarily.
    private bool menuModeActive;
    // Keep the game-created left-controller menu ray, reused after transitions.
    private GameObject leftMenuRay;
    // Keep the game-created right-controller menu ray, reused after
    // transitions.
    private GameObject rightMenuRay;
    // Return the remembered drum mode, including while Help temporarily shows
    // menu rays.
    public bool IsDrumModeActive => drumModeActive;
    // Return true only when drum mode is active and menu mode is not overriding
    // it.
    public bool IsUsingDrumsticks => drumModeActive && !menuModeActive;

    // Start with controller models and menu pointers visible for onboarding,
    // instead of player sticks.
    private void Start()
    {
        // Start or return in controller/menu mode rather than drumming.
        drumModeActive = false;

        // Show the left controller model at startup if it has been assigned.
        if (leftControllerVisual != null)
            // Activate the left controller model shown while using menus.
            leftControllerVisual.SetActive(true);

        // Show the right controller model at startup if it has been assigned.
        if (rightControllerVisual != null)
            // Activate the right controller model shown while using menus.
            rightControllerVisual.SetActive(true);

        // Hide the player's left stick at startup if it has been assigned.
        if (leftDrumstick != null)
            // Hide the player left-hand stick object, separate from musician
            // visuals.
            leftDrumstick.SetActive(false);

        // Hide the player's right stick at startup if it has been assigned.
        if (rightDrumstick != null)
            // Hide the player right-hand stick object, separate from musician
            // visuals.
            rightDrumstick.SetActive(false);

        // Start with menu rays available for the Start and Help controls.
        SetMenuMode(true);
    }

    // In Editor Play Mode, let a new P press show the sticks for desktop
    // testing.
    private void Update()
    {
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
        // Check that desktop keyboard input exists before reading the P
        // shortcut.
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            // Enable sticks through the same visual-mode method used by
            // automatic seating.
            EnableDrumMode();
        }
// End the Editor-specific choice; the following shared code is kept for both.
#endif
    }

    // Apply the current stick/controller visibility after other game scripts
    // update their states.
    private void LateUpdate()
    {
        // Reapply visibility after other updates so models and sticks reflect
        // the flags.
        ApplyCurrentMode();
    }

    // Remember that the player is at the drumming stage, then hide menu
    // pointers and show sticks.
    public void EnableDrumMode()
    {
        // Remember sitting drum mode so menu overrides can restore the player
        // sticks.
        drumModeActive = true;
        // Turn off the temporary menu override when entering drum mode.
        SetMenuMode(false);

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("DRUM MODE ENABLED");
    }

    // Leave drumming and show controller/menu controls while the player watches
    // the musician.
    public void DisableDrumMode()
    {
        // Start or return in controller/menu mode rather than drumming.
        drumModeActive = false;
        // Turn on menu pointing when leaving drum mode.
        SetMenuMode(true);
        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("DRUM MODE DISABLED");
    }

    // Use menu pointers when active is true. Keep the remembered drum stage
    // separately so closing Help can return to sticks.
    public void SetMenuMode(bool active)
    {
        // Remember the menu override separately from the sitting drum-mode
        // flag.
        menuModeActive = active;
        // Immediately update the controller/stick models for the new menu
        // choice.
        ApplyCurrentMode();
        // Find the scene player rig for this UI or controller setup.
        var origin = FindFirstObjectByType<XROrigin>();
        // Stop ray setup when there is no player XR Origin to parent rays
        // under.
        if (origin == null) return;
        // Create the left menu ray once, only when menus first need it.
        if (active && leftMenuRay == null) leftMenuRay = CreateMenuRay(origin, true);
        // Create the right menu ray once under the same conditions.
        if (active && rightMenuRay == null) rightMenuRay = CreateMenuRay(origin, false);
        // Visit the scene's NearFar interactors, including inactive ones.
        foreach (var ray in origin.GetComponentsInChildren<NearFarInteractor>(true))
            // Disable those interactors in menu mode so they do not compete
            // with menu rays.
            ray.enabled = !active;
        // Show or hide the already created left menu ray with menu mode.
        if (leftMenuRay != null) leftMenuRay.SetActive(active);
        // Apply the same mode to the right menu ray.
        if (rightMenuRay != null) rightMenuRay.SetActive(active);
        // Restore ray registration only when menus are being shown.
        if (active)
        {
            // Refresh the left ray's UI interaction if that ray and helper
            // exist.
            leftMenuRay?.GetComponent<QuestControllerMenuRay>()?.RestoreUIInteraction();
            // Refresh the right ray without creating another copy.
            rightMenuRay?.GetComponent<QuestControllerMenuRay>()?.RestoreUIInteraction();
        }
    }

    // Create one pointer for origin, the player's XR Origin. left chooses the
    // hand. Return the pointer object, or no object if its Resources prefab is
    // missing.
    private GameObject CreateMenuRay(XROrigin origin, bool left)
    {
        // Load the existing menu-ray prefab from the project's Resources
        // assets.
        var prefab = Resources.Load<GameObject>("QuestMenuRay");
        // Report a missing prefab and return missing instead of creating an
        // incomplete ray.
        if (prefab == null) { Debug.LogError("Quest menu ray prefab missing"); return null; }
        // Create a ray under the tracked camera-offset object so controller
        // poses share its frame.
        var ray = Instantiate(prefab, origin.CameraFloorOffsetObject.transform);
        // Give the ray a hand-specific name for identifying it during testing.
        ray.name = left ? "Left Controller Menu Ray" : "Right Controller Menu Ray";
        // Add the controller helper and bind this hand's input and scroll
        // actions.
        ray.AddComponent<QuestControllerMenuRay>().Initialize(left, menuInputActions);
        // Return the created object so later menu changes can reuse it.
        return ray;
    }

    // Show player sticks only when drum mode is active and no menu is
    // temporarily replacing them. Otherwise show controller models.
    private void ApplyCurrentMode()
    {
        // Show sticks only when drum mode is active and menu mode is not
        // overriding it.
        bool showDrumsticks = IsUsingDrumsticks;

        // Check that the left controller model exists before changing its
        // visibility.
        if (leftControllerVisual != null)
            // Show while player sticks are not in use the left controller model
            // shown while using menus.
            leftControllerVisual.SetActive(!showDrumsticks);

        // Check that the right controller model exists before changing its
        // visibility.
        if (rightControllerVisual != null)
            // Show while player sticks are not in use the right controller
            // model shown while using menus.
            rightControllerVisual.SetActive(!showDrumsticks);

        // Check that the player's left stick exists before changing its
        // visibility.
        if (leftDrumstick != null)
            // Show only while player sticks are in use the player left-hand
            // stick object, separate from musician visuals.
            leftDrumstick.SetActive(showDrumsticks);

        // Check that the player's right stick exists before changing its
        // visibility.
        if (rightDrumstick != null)
            // Show only while player sticks are in use the player right-hand
            // stick object, separate from musician visuals.
            rightDrumstick.SetActive(showDrumsticks);
    }
}
