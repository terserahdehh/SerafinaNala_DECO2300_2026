using UnityEngine;

public class DrumProximity : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerHead;
    [SerializeField] private Behaviour drumInteractable;
    [SerializeField] private GameObject recordingCanvas;

    [Header("Settings")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Debug")]
    [SerializeField] private float currentDistance;
    [SerializeField] private bool playerIsClose;

    private void Start()
    {
        if (recordingCanvas != null)
            recordingCanvas.SetActive(false);
    }

    private void Update()
    {
        if (playerHead == null || drumInteractable == null)
            return;

        Vector2 playerPosition = new Vector2(
            playerHead.position.x,
            playerHead.position.z
        );

        Vector2 drumPosition = new Vector2(
            transform.position.x,
            transform.position.z
        );

        currentDistance = Vector2.Distance(
            playerPosition,
            drumPosition
        );

        playerIsClose = currentDistance <= interactionDistance;

        drumInteractable.enabled = playerIsClose;

        if (recordingCanvas != null)
            recordingCanvas.SetActive(playerIsClose);
    }
}