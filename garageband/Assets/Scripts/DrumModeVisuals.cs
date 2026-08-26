using UnityEngine;
using UnityEngine.InputSystem;

public class DrumModeVisuals : MonoBehaviour
{
    [Header("Controller Models")]
    [SerializeField] private GameObject leftControllerVisual;
    [SerializeField] private GameObject rightControllerVisual;

    [Header("Drumsticks")]
    [SerializeField] private GameObject leftDrumstick;
    [SerializeField] private GameObject rightDrumstick;

    private bool drumModeActive;

    private void Start()
    {
        drumModeActive = false;

        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(true);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(true);

        if (leftDrumstick != null)
            leftDrumstick.SetActive(false);

        if (rightDrumstick != null)
            rightDrumstick.SetActive(false);
    }

    private void Update()
    {
        // Tombol P untuk testing di Unity Editor.
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            EnableDrumMode();
        }
    }

    private void LateUpdate()
    {
        if (drumModeActive)
        {
            ApplyDrumMode();
        }
    }

    public void EnableDrumMode()
    {
        drumModeActive = true;
        ApplyDrumMode();

        Debug.Log("DRUM MODE ENABLED");
    }

    public void DisableDrumMode()
    {
        drumModeActive = false;

        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(true);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(true);

        if (leftDrumstick != null)
            leftDrumstick.SetActive(false);

        if (rightDrumstick != null)
            rightDrumstick.SetActive(false);

        Debug.Log("DRUM MODE DISABLED");
    }

    private void ApplyDrumMode()
    {
        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(false);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(false);

        if (leftDrumstick != null)
            leftDrumstick.SetActive(true);

        if (rightDrumstick != null)
            rightDrumstick.SetActive(true);
    }
}