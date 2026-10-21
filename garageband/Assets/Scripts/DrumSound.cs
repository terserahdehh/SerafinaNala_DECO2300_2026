/*
 * Plays the sound assigned to one drum and tells DrumRecorder about the hit.
 * DrumstickHit calls PlaySound after a valid player-stick trigger contact.
 * For the kick drum, B on the right Quest controller also calls PlaySound
 * while the recording canvas is active. Keyboard B does the same in Editor.
 * This is active game code on drum sound objects in MusicRoom.
 * An AudioClip is a sound file. An AudioSource is the component that plays it.
 * A Canvas holds recording text/buttons. Its active state gates pedal input;
 * PlaySound itself does not check the player's distance from the kit.
 * The Inspector is Unity's settings panel for assigning objects and values.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use keyboard, mouse and controller input actions.
using UnityEngine.InputSystem;

// Plays the sound assigned to one drum and tells DrumRecorder about the hit.
[RequireComponent(typeof(AudioSource))]
public class DrumSound : MonoBehaviour
{
    // Choose whether this drum also responds to the Quest B-button pedal input.
    [Header("Pedal Input")]
    [SerializeField] private bool triggerWithB;
    // Keep the recording UI canvas, whose GameObject is controlled by
    // proximity.
    [SerializeField] private GameObject recordingCanvas;

    // Keep the AudioSource on this drum sound object.
    private AudioSource audioSource;
    // Remember whether B was already held, so holding it does not repeat the sound.
    private bool previousQuestBState;

    // Set up the drum's sound when this object loads.
    private void Awake()
    {
        // Find this object's AudioSource so PlaySound() can use it later.
        audioSource = GetComponent<AudioSource>();
    }

    // Check for a B press while the player is at the drums.
    // Play the kick once per press, even if B is held down.
    private void Update()
    {
        // Skip this part if this drum should not play when B is pressed.
        if (!triggerWithB)
            // Do not read/play pedal input on this path; the kick setting or
            // active recording UI is missing.
            return;

        // Stop if the recording panel is missing or hidden. It doesn't check if the recording is active.
        if (recordingCanvas == null ||
            !recordingCanvas.activeInHierarchy)
        {
            // Forget the old held-button state while pedal input is
            // unavailable.
            previousQuestBState = false;
            // Do not read/play pedal input on this path; the kick setting or
            // active recording UI is missing.
            return;
        }

        // Start with no desktop pedal press; Quest builds leave this false.
        bool keyboardBPressed = false;
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
        // In Editor, recognise a new keyboard B press as a pedal strike.
        keyboardBPressed = Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
// End the Editor-specific choice; the following shared code is kept for both.
#endif

        // Prepare a device reference for reading the right-hand Quest
        // controller. Ask Unity for the tracked device at the specified hand
        // node.
        UnityEngine.XR.InputDevice rightController =
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                UnityEngine.XR.XRNode.RightHand
            );

        // Default to B not being pressed if the device cannot provide a value.
        bool currentQuestBState = false;

        // Read the secondary face button from that device into the
        // current-state variable.
        rightController.TryGetFeatureValue(
            UnityEngine.XR.CommonUsages.secondaryButton,
            out currentQuestBState
        );

        // Calculate whether B has just become pressed instead of remaining
        // held. Require a fresh B press rather than another frame of the same
        // held button.
        bool questBPressed =
            currentQuestBState && !previousQuestBState;

        // Accept either the Editor keyboard press or the fresh Quest B press.
        if (keyboardBPressed || questBPressed)
            // Use the same sound/recording play or pass on method as a physical
            // stick contact.
            PlaySound();

        // Remember the current B state to avoid repeating a held press next
        // frame.
        previousQuestBState = currentQuestBState;
    }

    // Play this drum's clip once, allowing earlier hits to keep sounding. Give
    // the same clip to DrumRecorder, which decides whether recording is active.
    public void PlaySound()
    {
        // Check whether the drum is missing an AudioSource or a sound file.
        if (audioSource == null || audioSource.clip == null)
        {
            // Show which drum needs its sound setup checked.
            Debug.LogWarning(
                gameObject.name + " HAS NO AUDIO CLIP"
            );

            // Stop here because there is no sound to play.
            return;
        }

        // Play the sound for this hit without stopping the previous hit's sound.
        audioSource.PlayOneShot(audioSource.clip);

        // Save this drum hit if recording is active.
        DrumRecorder.Instance?.RecordHit(audioSource.clip);

        // Show the drum name and sound name in the Console when it is hit.
        Debug.Log(
            gameObject.name
            + " HIT: "
            + audioSource.clip.name
        );
    }
}