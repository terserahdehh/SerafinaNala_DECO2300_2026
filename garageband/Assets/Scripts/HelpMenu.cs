using UnityEngine;

public class HelpMenu : MonoBehaviour
{
    [Header("Help UI")]
    [SerializeField] private GameObject helpPopup;

    [Header("Controller Laser Visuals")]
    [SerializeField] private GameObject leftLineVisual;
    [SerializeField] private GameObject rightLineVisual;

    private void Start()
    {
        if (helpPopup != null)
            helpPopup.SetActive(false);

        SetLaserVisuals(true);
    }

    public void OpenHelp()
    {
        if (helpPopup != null)
            helpPopup.SetActive(true);

        SetLaserVisuals(false);
    }

    public void CloseHelp()
    {
        if (helpPopup != null)
            helpPopup.SetActive(false);

        SetLaserVisuals(true);
    }

    private void SetLaserVisuals(bool isVisible)
    {
        if (leftLineVisual != null)
            leftLineVisual.SetActive(isVisible);

        if (rightLineVisual != null)
            rightLineVisual.SetActive(isVisible);
    }
}