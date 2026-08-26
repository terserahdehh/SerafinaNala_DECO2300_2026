using UnityEngine;

public class DrumSeatUI : MonoBehaviour
{
    public GameObject recordingCanvas;

    public void ShowUI()
    {
        recordingCanvas.SetActive(true);
    }

    public void HideUI()
    {
        recordingCanvas.SetActive(false);
    }
}