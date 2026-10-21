/*
 * Shows recording controls when the player is near the drum kit.
 * It also enables selecting the kit within the same distance. This is active
 * game code in MusicRoom, including during Editor Play Mode. It does not
 * move the player; IntroSequence handles the automatic move to the drum seat.
 * DrumRecorder and the B-button kick in DrumSound also check this canvas.
 * A Transform stores an object's position, turn and size. The playerHead
 * Transform follows the headset. A Canvas holds the recording buttons/text.
 * Only floor distance matters here: headset height is ignored.
 * The Inspector is Unity's settings panel for assigning objects and values.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;

// Shows recording controls when the player is near the drum kit.
public class DrumProximity : MonoBehaviour
{
    // Keep the tracked headset transform used for the range check.
    [Header("References")]
    [SerializeField] private Transform playerHead;
    // Keep the drum-kit interaction component controlled by proximity.
    [SerializeField] private Behaviour drumInteractable;
    // Keep the recording UI canvas, whose GameObject is controlled by
    // proximity.
    [SerializeField] private GameObject recordingCanvas;

    // Keep the allowed horizontal distance from the drum kit in metres.
    [Header("Settings")]
    [SerializeField] private float interactionDistance = 3f;

    // Keep the latest horizontal headset-to-kit distance.
    [Header("Debug")]
    [SerializeField] private float currentDistance;
    // Remember whether the headset is inside the interaction range.
    [SerializeField] private bool playerIsClose;

    // Hide the recording canvas before the first distance check.
    private void Start()
    {
        // Only change recording UI when its canvas reference is available.
        if (recordingCanvas != null)
            // Hide the recording UI GameObject until proximity activates it.
            recordingCanvas.SetActive(false);
    }

    // Measure floor distance from the headset to the kit every update. Use that
    // result for recording UI and kit selection.
    private void Update()
    {
        // Skip the range calculation if either the headset or target
        // interaction is missing.
        if (playerHead == null || drumInteractable == null)
            // Leave UI and selection unchanged because the headset or kit
            // interaction reference is missing.
            return;

        // Build a floor-plane headset position from X and Z, intentionally
        // excluding Y.
        Vector2 playerPosition = new Vector2(
            playerHead.position.x,
            playerHead.position.z
        );

        // Build the matching horizontal kit position for the distance check.
        Vector2 drumPosition = new Vector2(
            transform.position.x,
            transform.position.z
        );

        // Measure the two-dimensional distance between the headset and kit.
        currentDistance = Vector2.Distance(
            playerPosition,
            drumPosition
        );

        // Remember whether that distance is within the configured metre limit.
        playerIsClose = currentDistance <= interactionDistance;

        // Allow kit selection only while the player is within that same range.
        drumInteractable.enabled = playerIsClose;

        // Only change recording UI when its canvas reference is available.
        if (recordingCanvas != null)
            // Match the proximity result for the recording UI canvas, whose
            // GameObject is controlled by proximity.
            recordingCanvas.SetActive(playerIsClose);
    }
}