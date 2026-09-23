using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class DrumSound : MonoBehaviour
{
    [Header("Pedal Input")]
    [SerializeField] private bool triggerWithB;
    [SerializeField] private GameObject recordingCanvas;

    private AudioSource audioSource;
    private bool previousQuestBState;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (!triggerWithB)
            return;

        // Pedal hanya bisa dimainkan saat UI drum sedang aktif.
        if (recordingCanvas == null ||
            !recordingCanvas.activeInHierarchy)
        {
            previousQuestBState = false;
            return;
        }

        bool keyboardBPressed =
            Keyboard.current != null &&
            Keyboard.current.bKey.wasPressedThisFrame;

        UnityEngine.XR.InputDevice rightController =
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                UnityEngine.XR.XRNode.RightHand
            );

        bool currentQuestBState = false;

        rightController.TryGetFeatureValue(
            UnityEngine.XR.CommonUsages.secondaryButton,
            out currentQuestBState
        );

        bool questBPressed =
            currentQuestBState && !previousQuestBState;

        if (keyboardBPressed || questBPressed)
            PlaySound();

        previousQuestBState = currentQuestBState;
    }

    public void PlaySound()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning(
                gameObject.name + " HAS NO AUDIO CLIP"
            );

            return;
        }

        audioSource.PlayOneShot(audioSource.clip);

        DrumRecorder.Instance?.RecordHit(audioSource.clip);

        Debug.Log(
            gameObject.name
            + " HIT: "
            + audioSource.clip.name
        );
    }
}