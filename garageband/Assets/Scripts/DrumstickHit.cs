/*
 * Plays a drum when a player's stick tip enters its invisible hit shape.
 * This is the active contact path for the drums, including the cymbal.
 * A Collider is an invisible shape Unity uses to notice contacts. A trigger
 * reports contact without pushing objects away. Unity calls OnTriggerEnter
 * when this tip enters another trigger/contact shape in the physics setup.
 * DrumSound plays the hit and tells DrumRecorder when recording is active.
 * A short wait between accepted contacts helps prevent repeated sounds from
 * one stick. ProceduralDrummer disables contacts/scripts on musician sticks.
 * Runs in the game and in Editor Play Mode.
 * The Inspector is Unity's settings panel for assigning objects and values.
 */
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;

// Plays a drum when a player's stick tip enters its invisible hit shape.
public class DrumstickHit : MonoBehaviour
{
    // Keep the minimum time between accepted contacts on this stick.
    [SerializeField] private float hitCooldown = 0.1f;

    // Keep the earliest time this stick may accept another drum contact.
    private float nextHitTime;

    // Check a new stick contact. other is the collider touched by this tip. If
    // it belongs to a drum and enough time has passed since this stick's last
    // hit, play it.
    private void OnTriggerEnter(Collider other)
    {
        // Find a DrumSound on the contacted collider or its parents, including
        // child hit zones.
        DrumSound drum = other.GetComponentInParent<DrumSound>();

        // Reject non-drum contacts and any contact arriving before this stick's
        // cooldown expires.
        if (drum == null || Time.time < nextHitTime)
            // Ignore a non-drum contact or one arriving before this stick can
            // hit again.
            return;

        // Ask this drum to play its sound and send the hit to the recorder.
        drum.PlaySound();
        // Start this stick's shared cooldown from the accepted contact's
        // current game time.
        nextHitTime = Time.time + hitCooldown;
    }
}