using UnityEngine;

public class DrumSeatLock : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Behaviour moveProvider;

    public void LockMovement()
    {
        if (moveProvider != null)
            moveProvider.enabled = false;

        Debug.Log("SEATED: MOVEMENT LOCKED");
    }

    public void UnlockMovement()
    {
        if (moveProvider != null)
            moveProvider.enabled = true;

        Debug.Log("SEATED: MOVEMENT UNLOCKED");
    }
}