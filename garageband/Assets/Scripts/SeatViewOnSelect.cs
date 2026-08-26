using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;

public class SeatViewOnSelect : MonoBehaviour
{
    [SerializeField] private Transform seatView;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private DrumModeVisuals drumModeVisuals;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (drumModeVisuals == null)
            drumModeVisuals = GetComponent<DrumModeVisuals>();

        interactable =
            GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
    }

    private void OnEnable()
    {
        if (interactable == null)
        {
            interactable =
                GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        }

        if (interactable != null)
            interactable.selectEntered.AddListener(MoveToSeat);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(MoveToSeat);
    }

    private void MoveToSeat(SelectEnterEventArgs _)
    {
        if (xrOrigin == null || seatView == null)
            return;

        xrOrigin.MatchOriginUpCameraForward(
            Vector3.up,
            seatView.forward
        );

        xrOrigin.MoveCameraToWorldLocation(
            seatView.position
        );

        if (drumModeVisuals != null)
            drumModeVisuals.EnableDrumMode();
    }

    private void Update()
    {
        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (Physics.Raycast(ray, out RaycastHit hit) &&
            hit.collider.GetComponentInParent<SeatViewOnSelect>() == this)
        {
            MoveToSeat(default);
        }
    }
}