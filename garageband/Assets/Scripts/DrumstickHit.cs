using UnityEngine;

public class DrumstickHit : MonoBehaviour
{
    [SerializeField] private float hitCooldown = 0.1f;

    private float nextHitTime;

    private void OnTriggerEnter(Collider other)
    {
        DrumSound drum = other.GetComponentInParent<DrumSound>();

        if (drum == null || Time.time < nextHitTime)
            return;

        drum.PlaySound();
        nextHitTime = Time.time + hitCooldown;
    }
}