/*
 * Makes Mika or Ren sit at the kit and move their arms with the recorded beat.
 * PerformanceSequence creates/reuses this active game component and supplies
 * the selected musician's body parts, seat markers and stick adjustments.
 * DrumRecorder provides playback time, so hands and sounds use the same clock.
 * CharacterMotion stops standing/walking first; other model animations pause
 * too. Two separate visual sticks attach to the hands. They cannot make hits.
 * Release removes those sticks and puts the character back for standing.
 * This also runs in Editor Play Mode. A bone is an invisible body joint in the
 * model. Its Transform stores position, turn and size. A parent is the object
 * containing a body part; local values are measured from that parent. A pose
 * is where the parts are and which way they face. Animator normally plays model
 * animations; an avatar maps bones to human body parts. Quaternion stores a
 * rotation. Multiplying rotations combines turns; Inverse undoes a turn.
 * A prefab is a saved reusable object; this code can make separate copies.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use attributes for saving musician settings and exact sound-name comparisons.
using System;
// Use lists and sets to remember body parts, drum hits and stick objects.
using System.Collections.Generic;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;

// Makes Mika or Ren sit at the kit and move their arms with the recorded beat.
public sealed class ProceduralDrummer : MonoBehaviour
{
    // Group this musician's body parts, seat positions and stick adjustments
    // for setup.
    [Serializable]
    public class Rig
    {
        // Keep an optional Animator to help find this musician's body parts.
        [Tooltip("Optional. Valid Humanoid avatars fill only unassigned bone fields.")]
        public Animator animator;
        // Keep the pelvis and torso bones used for sitting and facing the kit.
        public Transform hips, spine;
        // Keep the left upper arm, lower arm and hand used for left-stick
        // movement.
        public Transform leftUpperArm, leftForearm, leftHand;
        // Keep the matching right arm and hand references.
        public Transform rightUpperArm, rightForearm, rightHand;
        // Keep the left upper leg, lower leg and foot for sitting with bent
        // knees.
        public Transform leftThigh, leftShin, leftFoot;
        // Keep the matching right leg and foot.
        public Transform rightThigh, rightShin, rightFoot;
        // Keep the stationary marker where this musician's hips should sit.
        [Tooltip("World marker at the hips pivot when seated; blue Z points toward the kit.")]
        public Transform seatPoint;
        // Keep optional room markers for the sitting musician's left and right
        // feet.
        public Transform leftFootPoint, rightFootPoint;
        // Keep this model's extra hip-position adjustment from the seat marker.
        public Vector3 hipsOffset;
        // Keep the musician-only stick visual to copy, never the player's stick
        // objects.
        [Tooltip("Musician-only visual prefab: grip at origin, tip along local +Z. Never assign a player/controller stick.")]
        public GameObject drumstickPrefab;
        // Keep separate hand-relative positions for the left and right stick
        // grips.
        public Vector3 leftGripOffset, rightGripOffset;
        // Keep separate turn adjustments, in degrees, for the two hand grips.
        public Vector3 leftGripEuler, rightGripEuler;
        // Keep the stick length in metres so the wrist can be placed behind its
        // tip.
        [Min(.05f)] public float stickLength = .34f;
        // Keep how far above the drum the stick tip rises between hits.
        [Min(.02f)] public float liftHeight = .18f;
        // Keep the preferred time in seconds for the downward strike.
        [Min(.01f)] public float strikeDuration = .09f;
        // Keep the preferred time in seconds for lifting back after a hit.
        [Min(.01f)] public float recoveryDuration = .18f;
    }

    // Choose alternating hands, a fixed left/right hand, or pedal-only sound.
    public enum Hand { Alternate, Left, Right, Pedal }

    // Match one recorded sound name to a drum surface and hand choice.
    [Serializable]
    public class DrumTarget
    {
        // Keep the sound-file name used to match a recorded hit to this drum
        // target.
        public string soundName;
        // Keep a marker on the drum surface where the stick tip should touch.
        [Tooltip("Place on the playable surface; position is the intended stick tip contact.")]
        public Transform target;
        // Keep which hand should perform this kind of hit.
        public Hand hand;
    }

    // Keep one body part and its saved parent-relative position/turn together.
    private struct Pose
    {
        // Remember which body part this saved pose belongs to.
        public Transform bone;
        // Remember that part's position relative to its parent.
        public Vector3 position;
        // Remember its turn relative to its parent too.
        public Quaternion rotation;
        // Save one body part. t is that part's Transform. Remember its position
        // and turn relative to its parent so it can be put back later.
        public Pose(Transform t) { bone = t; position = t.localPosition; rotation = t.localRotation; }
        // Put this saved body part back at its starting position and turn
        // relative to its parent.
        public void Restore() { bone.localPosition = position; bone.localRotation = rotation; }
    }

    // Keep one scheduled hit time and the intended stick-tip contact position
    // together.
    private struct Stroke { public float time; public Vector3 point; }
    // Keep the saved sitting pose so each update starts from the same body
    // position.
    private readonly List<Pose> pose = new List<Pose>();
    // Keep separate ordered hit targets/times for the left and right hands.
    private readonly List<Stroke> left = new List<Stroke>(), right = new List<Stroke>();
    // Keep the musician stick objects so Release can remove them later.
    private readonly List<GameObject> sticks = new List<GameObject>();
    // Remember every Animator that could otherwise move these same body parts.
    private Animator[] animators;
    // Save which of those Animators were enabled before sitting.
    private bool[] animatorEnabled;
    // Keep the chosen musician's body-part, seat and stick settings.
    private Rig rig;
    // Keep the whole character object currently being sitting.
    private Transform musician;
    // Remember where the character was before sitting and where it sits now.
    private Vector3 originalPosition, seatedPosition;
    // Remember the character's facing before sitting and while sitting.
    private Quaternion originalRotation, seatedRotation;
    // Keep the sitting directions and resting contact positions for each stick.
    private Vector3 forward, up, sideways, leftRest, rightRest;
    // Keep each hand's stick-turn adjustment so different model wrists aim
    // correctly.
    private Quaternion leftGrip, rightGrip;
    // Keep the duration in seconds of the current performance loop.
    private float duration;
    // Remember whether sitting setup succeeded and arm movement is allowed.
    private bool seated;

    // Fill missing body-part references in r, the musician's settings. Search
    // inside root, the whole musician object. Keep explicit Inspector
    // assignments ahead of automatic lookups.
    public static void ResolveBones(Rig r, Transform root)
    {
        // Use the assigned Animator, or look inside this musician for one if
        // missing.
        if (r.animator == null) r.animator = root.GetComponentInChildren<Animator>(true);
        // Check that the Animator has a usable human-body avatar before asking
        // it for bones.
        bool human = r.animator != null && r.animator.avatar != null &&
                     r.animator.avatar.isValid && r.animator.avatar.isHuman;
        // Fill the hips if missing, with the known imported-name fallback.
        Resolve(ref r.hips, HumanBodyBones.Hips, "J_Bip_C_Hips");
        // Resolve the spine used to determine body facing and sitting posture.
        Resolve(ref r.spine, HumanBodyBones.Spine, "J_Bip_C_Spine");
        // Resolve the left upper arm for stick reach.
        Resolve(ref r.leftUpperArm, HumanBodyBones.LeftUpperArm, "J_Bip_L_UpperArm");
        // Resolve the left elbow/lower arm for bending.
        Resolve(ref r.leftForearm, HumanBodyBones.LeftLowerArm, "J_Bip_L_LowerArm");
        // Resolve the left hand that will carry the left visual stick.
        Resolve(ref r.leftHand, HumanBodyBones.LeftHand, "J_Bip_L_Hand");
        // Resolve the right upper arm for the other stick.
        Resolve(ref r.rightUpperArm, HumanBodyBones.RightUpperArm, "J_Bip_R_UpperArm");
        // Resolve the right elbow/lower arm.
        Resolve(ref r.rightForearm, HumanBodyBones.RightLowerArm, "J_Bip_R_LowerArm");
        // Resolve the right hand that will carry the right visual stick.
        Resolve(ref r.rightHand, HumanBodyBones.RightHand, "J_Bip_R_Hand");
        // Resolve the left thigh for the sitting knee bend.
        Resolve(ref r.leftThigh, HumanBodyBones.LeftUpperLeg, "J_Bip_L_UpperLeg");
        // Resolve the left shin for the lower leg.
        Resolve(ref r.leftShin, HumanBodyBones.LeftLowerLeg, "J_Bip_L_LowerLeg");
        // Resolve the left foot for grounded sitting placement.
        Resolve(ref r.leftFoot, HumanBodyBones.LeftFoot, "J_Bip_L_Foot");
        // Resolve the right thigh for the other sitting leg.
        Resolve(ref r.rightThigh, HumanBodyBones.RightUpperLeg, "J_Bip_R_UpperLeg");
        // Resolve the right shin.
        Resolve(ref r.rightShin, HumanBodyBones.RightLowerLeg, "J_Bip_R_LowerLeg");
        // Resolve the right foot.
        Resolve(ref r.rightFoot, HumanBodyBones.RightFoot, "J_Bip_R_Foot");
        // Fill one missing body-part reference. bone is updated in place; id
        // names its human-body slot, and prefix is its known imported-model
        // name. Keep an explicit assignment.
        void Resolve(ref Transform bone, HumanBodyBones id, string prefix)
        {
            // Keep a body part already assigned in the Inspector or found
            // earlier.
            if (bone != null) return;
            // Try Unity's human-body mapping when this avatar supports it.
            if (human) bone = r.animator.GetBoneTransform(id);
            // Stop searching if the mapping already found the requested part.
            if (bone != null) return;
            // Choose the alternative bone name used by these imported
            // characters.
            string native = id switch
            {
                HumanBodyBones.Hips => "Hips", HumanBodyBones.Spine => "Spine",
                HumanBodyBones.LeftUpperArm => "Left arm", HumanBodyBones.LeftLowerArm => "Left elbow",
                HumanBodyBones.LeftHand => "Left wrist", HumanBodyBones.RightUpperArm => "Right arm",
                HumanBodyBones.RightLowerArm => "Right elbow", HumanBodyBones.RightHand => "Right wrist",
                HumanBodyBones.LeftUpperLeg => "Left leg", HumanBodyBones.LeftLowerLeg => "Left knee",
                HumanBodyBones.LeftFoot => "Left ankle", HumanBodyBones.RightUpperLeg => "Right leg",
                HumanBodyBones.RightLowerLeg => "Right knee", HumanBodyBones.RightFoot => "Right ankle",
                _ => ""
            };
            // Look through all body parts under this musician, including
            // inactive objects.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                // Match the expected model name or its allowed suffix form.
                if (t.name == native || t.name == prefix || t.name.StartsWith(prefix + "_", StringComparison.Ordinal))
                // Keep the first matching part and stop looking for another.
                { bone = t; break; }
        }
    }

    // Set up sitting and sticks for root, the chosen musician. settings
    // contains their body parts and grip adjustments. Return true after
    // successful setup, or false if required references are invalid.
    public bool Seat(Transform root, Rig settings)
    {
        // Remove any old sitting pose/sticks before preparing this musician
        // again.
        Release();
        // Return false without a character or settings object to use.
        if (root == null || settings == null) return false;
        // Find any missing body parts before checking whether sitting can work.
        ResolveBones(settings, root);
        // Keep these rig settings for later posing and stick adjustment.
        rig = settings;
        // Check whether a valid human-body Animator mapping is available for
        // this model.
        bool humanoid = rig.animator != null && rig.animator.avatar != null &&
            rig.animator.avatar.isValid && rig.animator.avatar.isHuman;
        // Log whether bone lookup can use a human-body avatar or the
        // explicit/model-name references.
        Debug.Log(root.name + ": " + (humanoid ? "valid Humanoid Animator; explicit bones take priority." :
            "no valid Humanoid Animator; using explicit/named bone Transforms."), root);
        // Collect all torso, arm and leg parts required to make this sitting
        // pose.
        Transform[] bones = { rig.hips, rig.spine, rig.leftUpperArm, rig.leftForearm, rig.leftHand,
            rig.rightUpperArm, rig.rightForearm, rig.rightHand, rig.leftThigh, rig.leftShin,
            rig.leftFoot, rig.rightThigh, rig.rightShin, rig.rightFoot };
        // Visit every required bone before any sitting pose is applied.
        foreach (var t in bones)
            // Reject missing parts or references belonging outside this
            // musician's object list.
            if (t == null || !t.IsChildOf(root))
            // Report the bad rig and return failure rather than pose an
            // incomplete body.
            { Debug.LogError(root.name + ": assign all Procedural Drumming bones under this musician.", root); return false; }
        // Require a stationary seat marker outside the musician, so moving the
        // body does not move its target.
        if (rig.seatPoint == null || rig.seatPoint.IsChildOf(root))
        // Report the moving/missing seat marker and return failure.
        { Debug.LogError("Assign a stationary Seat Point outside the musician hierarchy.", root); return false; }
        // Check that both arms and both legs have connected upper, lower and
        // end parts.
        if (!Chain(rig.leftUpperArm, rig.leftForearm, rig.leftHand) ||
            !Chain(rig.rightUpperArm, rig.rightForearm, rig.rightHand) ||
            !Chain(rig.leftThigh, rig.leftShin, rig.leftFoot) ||
            !Chain(rig.rightThigh, rig.rightShin, rig.rightFoot))
        // Return failure if any limb object list cannot support this pose
        // calculation.
        { Debug.LogError("Drummer bones must form upper/lower/end descendant chains.", root); return false; }
        // Find the standing animation system on this same whole musician
        // object.
        var motion = root.GetComponent<CharacterMotion>();
        // Stop standing/walking before this script saves and changes the
        // sitting body pose.
        if (motion != null) motion.SuspendForPerformance();
        // Remember which character is being posed.
        musician = root;
        // Save the character's current position and facing so Release can put
        // it back later.
        originalPosition = root.position; originalRotation = root.rotation;
        // Find child Animators that might otherwise move the body during
        // drumming.
        var writers = new HashSet<Animator>(root.GetComponentsInChildren<Animator>(true));
        // Include any parent Animators too; the set avoids adding the same one
        // twice.
        foreach (var a in root.GetComponentsInParent<Animator>(true)) writers.Add(a);
        // Store those Animators in an array for matching them to saved
        // settings.
        animators = new Animator[writers.Count]; writers.CopyTo(animators);
        // Reserve one saved enabled flag per Animator.
        animatorEnabled = new bool[animators.Length];
        // Visit each Animator before letting this script move the sitting body
        // parts.
        for (int i = 0; i < animators.Length; i++)
        // Save its enabled state, then pause it so the two animations do not
        // compete.
        { animatorEnabled[i] = animators[i].enabled; animators[i].enabled = false; }
        // Save the original body pose separately from the sitting pose made
        // later.
        foreach (var t in bones) { pose.Add(new Pose(t)); originalPose.Add(new Pose(t)); }
        // Use room up for the sitting body and limb targets.
        up = Vector3.up;
        // Use the seat marker's forward arrow across the floor as the
        // musician's facing.
        forward = Vector3.ProjectOnPlane(rig.seatPoint.forward, up).normalized;
        // Use the room's forward direction if the seat arrow gives no usable
        // floor direction.
        if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
        // Find sideways from room up and the intended sitting direction.
        sideways = Vector3.Cross(up, forward).normalized;
        // Work out the body's actual facing from the thighs and torso, not the
        // imported object's arrows.
        Vector3 anatomicalForward = Vector3.Cross(rig.rightThigh.position - rig.leftThigh.position,
            rig.spine.position - rig.hips.position);
        // Keep only the horizontal part so the character turns without leaning.
        anatomicalForward = Vector3.ProjectOnPlane(anatomicalForward, up);
        // Only turn when the body's facing direction is long enough to use.
        if (anatomicalForward.sqrMagnitude > .000001f)
            // Turn the whole musician toward the kit, keeping the model's other
            // turn adjustments.
            root.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(anatomicalForward, forward, up), up) * root.rotation;
        // Move the musician until their hips reach the seat marker plus this
        // model's offset.
        root.position += rig.seatPoint.position + rig.seatPoint.TransformDirection(rig.hipsOffset) - rig.hips.position;
        // Bend the left sitting leg toward its marker or fallback foot target.
        SeatLeg(rig.leftThigh, rig.leftShin, rig.leftFoot, rig.leftFootPoint);
        // Bend the right sitting leg in the same way.
        SeatLeg(rig.rightThigh, rig.rightShin, rig.rightFoot, rig.rightFootPoint);
        // Save the newly sitting body parts for later updates, keeping the
        // original pose separate.
        pose.Clear(); foreach (var t in bones) pose.Add(new Pose(t));
        // Keep the whole character's sitting position and facing steady during
        // performance.
        seatedPosition = root.position; seatedRotation = root.rotation;
        // Choose a left-hand rest position ahead of and beside the sitting
        // hips.
        leftRest = rig.hips.position + forward * .42f - sideways * .18f + up * .22f;
        // Choose a matching right-hand rest position on the other side.
        rightRest = rig.hips.position + forward * .42f + sideways * .18f + up * .22f;
        // Find this model's left-hand stick turn, then add its Inspector grip
        // adjustment.
        leftGrip = CalibrateGrip(rig.leftForearm, rig.leftHand) * Quaternion.Euler(rig.leftGripEuler);
        // Work out the right-hand turn separately so each stick fits its own
        // palm.
        rightGrip = CalibrateGrip(rig.rightForearm, rig.rightHand) * Quaternion.Euler(rig.rightGripEuler);
        // Create the left visual stick on its own hand grip.
        CreateStick(rig.leftHand, rig.leftGripOffset, leftGrip, "Left");
        // Create the right visual stick on the other hand grip.
        CreateStick(rig.rightHand, rig.rightGripOffset, rightGrip, "Right");
        // Allow arm updates now that sitting setup has succeeded.
        seated = true;
        // Apply the first sitting arm pose immediately.
        Evaluate(0f);
        // Return true so the caller knows sitting setup succeeded.
        return true;
    }

    // Find the stick's turn relative to hand using the forearm-to-hand
    // direction. Return that adjustment so this model's wrist can aim the stick
    // toward the kit.
    private Quaternion CalibrateGrip(Transform forearm, Transform hand)
    {
        // Use the direction from forearm to hand as the starting stick aim.
        Vector3 direction = (hand.position - forearm.position).normalized;
        // Find an upward direction across that aim to keep the stick's turn
        // stable.
        Vector3 rollUp = Vector3.ProjectOnPlane(up, direction);
        // Use the kit-facing direction if up points too closely along the arm.
        if (rollUp.sqrMagnitude < .001f) rollUp = Vector3.ProjectOnPlane(forward, direction);
        // Return this aiming turn measured relative to the hand's current turn.
        return Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(direction, rollUp);
    }

    // Check that b is below a and c below b in the model's object list. Return
    // true when these upper/lower/end body parts are connected in that order.
    private static bool Chain(Transform a, Transform b, Transform c) => b.IsChildOf(a) && c.IsChildOf(b);

    // Bend thigh and shin to put foot at marker. If marker is missing,
    // calculate a foot target from the leg lengths instead.
    private void SeatLeg(Transform thigh, Transform shin, Transform foot, Transform marker)
    {
        // Measure the upper-leg length for placing the foot when no marker is
        // assigned.
        float upper = Vector3.Distance(thigh.position, shin.position);
        // Measure the lower-leg length too.
        float lower = Vector3.Distance(shin.position, foot.position);
        // Prefer marker's foot position; otherwise calculate one ahead of and
        // below the thigh.
        Vector3 goal = marker != null ? marker.position :
            thigh.position + forward * (upper * .92f) - up * (lower * 1.02f);
        // Remember how the foot faces before bending the knee.
        Quaternion footRotation = foot.rotation;
        // Bend the leg toward its foot target, with the knee pointing forward.
        Solve(thigh, shin, foot, goal, forward);
        // Keep the foot's earlier facing so bending the leg does not twist the
        // ankle.
        foot.rotation = footRotation;
    }

    // Bend a two-part arm or leg without stretching it. a is its upper part, b
    // its lower part, and c its hand/foot. goal is the wanted end position;
    // bend is the preferred elbow/knee direction.
    private static void Solve(Transform a, Transform b, Transform c, Vector3 goal, Vector3 bend)
    {
        // Begin from the upper joint's current position in the room.
        Vector3 start = a.position;
        // Measure the upper and lower part lengths without assuming model bone
        // directions.
        float l1 = Vector3.Distance(start, b.position), l2 = Vector3.Distance(b.position, c.position);
        // Leave the part alone if either section is too short for safe
        // calculations.
        if (l1 < .00001f || l2 < .00001f) return;
        // Find the direction and distance from the upper joint to the desired
        // hand/foot position.
        Vector3 delta = goal - start;
        // Skip a target almost on top of the upper joint, where no clear aim
        // exists.
        if (delta.sqrMagnitude < .00000001f) return;
        // Keep the target just inside the shortest/longest distance these parts
        // can reach.
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + .0001f, l1 + l2 - .0001f);
        // Use a direction of length one so the reach distance can be applied
        // separately.
        Vector3 axis = delta.normalized;
        // Choose an elbow/knee bend direction sideways from the line toward the
        // target.
        Vector3 plane = Vector3.ProjectOnPlane(bend, axis).normalized;
        // Try room up if the preferred bend points along that line instead.
        if (plane.sqrMagnitude < .5f) plane = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        // Try room right if the up direction also gives no usable sideways
        // bend.
        if (plane.sqrMagnitude < .5f) plane = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
        // Use the triangle's three lengths to find how far along the target
        // line the joint lies.
        float along = (l1 * l1 - l2 * l2 + distance * distance) / (2f * distance);
        // Add the triangle's sideways height to find the bent elbow/knee
        // position.
        Vector3 elbow = start + axis * along + plane * Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
        // Turn the upper part toward that calculated joint position.
        a.rotation = Quaternion.FromToRotation(b.position - start, elbow - start) * a.rotation;
        // Find the reachable hand/foot position using the limited target
        // distance.
        Vector3 reachable = start + axis * distance;
        // Turn the lower part toward the reachable end without changing its
        // length.
        b.rotation = Quaternion.FromToRotation(c.position - b.position, reachable - b.position) * b.rotation;
    }

    // Turn hits into separate left/right hand movements for one loopDuration.
    // Match sound names to targets, the drum-surface markers. Pedal sounds stay
    // in audio playback and do not become hand strokes.
    public void PrepareRecording(IReadOnlyList<DrumRecorder.DrumHit> hits, float loopDuration, DrumTarget[] targets)
    {
        // Clear old hand targets and remember the duration of this new take.
        left.Clear(); right.Clear(); duration = loopDuration;
        // Start alternating-hand hits with the left hand.
        bool useLeft = true;
        // Remember missing sound names so each gives only one warning during
        // this setup.
        var missing = new HashSet<string>();
        // Read each recorded hit and look for its drum animation target.
        foreach (var hit in hits)
        {
            // Start without a matching target for this recorded sound.
            DrumTarget match = null;
            // Search only if the scene has a drum-target list.
            if (targets != null)
                // Try each sound-name and drum-marker pair in that list.
                foreach (var target in targets)
                    // Compare sound names ignoring case and outer spaces,
                    // allowing missing names safely.
                    if (target != null && string.Equals(target.soundName?.Trim(), hit.soundName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    // Keep the first matching drum target and stop the search
                    // for this hit.
                    { match = target; break; }
            // Skip a missing match or a hand hit without a drum-surface marker.
            if (match == null || (match.hand != Hand.Pedal && match.target == null))
            // Warn once about this missing sound target, then continue with the
            // other hits.
            { if (missing.Add(hit.soundName)) Debug.LogWarning("No animation target for recorded sound: " + hit.soundName, this); continue; }
            // Leave pedal hits to DrumRecorder's audio; they should not move a
            // stick.
            if (match.hand == Hand.Pedal) continue;
            // Choose the fixed left hand or the current left/right alternating
            // choice.
            bool isLeft = match.hand == Hand.Left || (match.hand == Hand.Alternate && useLeft);
            // Change the alternating choice only for targets that actually
            // alternate hands.
            if (match.hand == Hand.Alternate) useLeft = !useLeft;
            // Remember this hit's time and contact position in the chosen
            // hand's list.
            (isLeft ? left : right).Add(new Stroke { time = hit.time, point = match.target.position });
        }
    }

    // Show the sitting pose at playbackTime seconds into the beat. Put back the
    // saved sitting body positions before calculating both arms again.
    public void Evaluate(float playbackTime)
    {
        // Leave the body alone until sitting setup has succeeded.
        if (!seated) return;
        // Keep the whole musician at the saved sitting position and facing.
        musician.SetPositionAndRotation(seatedPosition, seatedRotation);
        // Put each body part back in its saved sitting pose before moving the
        // arms again.
        foreach (var p in pose) p.Restore();
        // Move the left arm for the recorder's current time.
        AnimateArm(left, playbackTime, true);
        // Move the right arm for that same time so both match the beat.
        AnimateArm(right, playbackTime, false);
    }

    // Move one arm through its strokes, the ordered contact targets/times. time
    // is seconds into this loop. isLeft selects the left or right arm and its
    // saved grip adjustment.
    private void AnimateArm(List<Stroke> strokes, float time, bool isLeft)
    {
        // Start from this hand's resting contact position when no recorded hits
        // need it.
        Vector3 contact = isLeft ? leftRest : rightRest;
        // Start with the stick raised above the contact position.
        float lift = 1f;
        // Look for hit times only if this hand has hits and the loop has
        // positive length.
        if (strokes.Count > 0 && duration > 0f)
        {
            // Start with candidates across the end/start of the repeating beat.
            Stroke previous = strokes[strokes.Count - 1], next = strokes[0];
            // Shift their times into the previous/next loop so the gap remains
            // continuous.
            float previousTime = previous.time - duration, nextTime = next.time + duration;
            // Look through this hand's hits to find the ones around the current
            // time.
            foreach (var stroke in strokes)
                // Remember the latest hit already reached as the previous
                // contact.
                if (stroke.time <= time) { previous = stroke; previousTime = stroke.time; }
                // Remember the first future hit as the next contact, then stop
                // searching.
                else { next = stroke; nextTime = stroke.time; break; }
            // Keep the gap above zero so later time divisions are safe.
            float gap = Mathf.Max(.001f, nextTime - previousTime);
            // Fit the lifting time into the first part of the gap between hits.
            float recovery = Mathf.Min(rig.recoveryDuration, gap * .55f);
            // Fit the downward strike time into the last part of that gap.
            float strike = Mathf.Min(rig.strikeDuration, gap * .45f);
            // Measure seconds since the previous contact.
            float age = time - previousTime;
            // Lift from the last drum first while that contact is still recent.
            if (age < recovery)
            // Keep the last contact position while the stick rises smoothly
            // away from it.
            { contact = previous.point; lift = Mathf.SmoothStep(0f, 1f, age / recovery); }
            // After lifting, move across to the next drum and lower for the
            // strike.
            else
            {
                // Find how far the arm should be through travelling between
                // drum targets.
                float travel = Mathf.InverseLerp(previousTime + recovery, nextTime - strike, time);
                // Move the intended stick-tip contact smoothly from the last
                // drum to the next.
                contact = Vector3.Lerp(previous.point, next.point, Mathf.SmoothStep(0f, 1f, travel));
                // Keep the stick raised until its final approach, then lower it
                // toward the next hit.
                lift = time > nextTime - strike ? Mathf.SmoothStep(0f, 1f, (nextTime - time) / strike) : 1f;
            }
        }
        // Choose this hand's upper arm.
        Transform upper = isLeft ? rig.leftUpperArm : rig.rightUpperArm;
        // Choose its lower arm.
        Transform forearm = isLeft ? rig.leftForearm : rig.rightForearm;
        // Choose the character's hand bone carrying the stick grip.
        Transform hand = isLeft ? rig.leftHand : rig.rightHand;
        // Choose this palm's own grip-position adjustment.
        Vector3 gripOffset = isLeft ? rig.leftGripOffset : rig.rightGripOffset;
        // Choose its saved hand-to-stick turn adjustment.
        Quaternion gripCorrection = isLeft ? leftGrip : rightGrip;
        // Aim the stick forward and down toward the drum kit.
        Vector3 stickDirection = (forward - up * .55f).normalized;
        // Convert that stick aim into a turn in the room.
        Quaternion stickWorld = Quaternion.LookRotation(stickDirection, up);
        // Undo the hand-to-stick adjustment to find the hand turn needed for
        // that aim.
        Quaternion wristWorld = stickWorld * Quaternion.Inverse(gripCorrection);
        // Place the tip above the drum according to how far the stick is
        // lifted.
        Vector3 tip = contact + up * (rig.liftHeight * lift);
        // Work back from the tip through stick length and grip offset to find
        // the wrist position.
        Vector3 wrist = tip - stickDirection * rig.stickLength - wristWorld * Vector3.Scale(hand.lossyScale, gripOffset);
        // Bend the arm to that wrist position, keeping the elbow outward and
        // down.
        Solve(upper, forearm, hand, wrist, -forward * .3f + sideways * (isLeft ? -1f : 1f) - up * .5f);
        // Turn the hand so its attached stick faces the intended drum
        // direction.
        hand.rotation = wristWorld;
    }

    // Attach one separate visual stick to hand. offset and gripRotation
    // position it in that musician's palm; side names it Left or Right. Disable
    // copied hit/audio/physics behaviour before showing it.
    private void CreateStick(Transform hand, Vector3 offset, Quaternion gripRotation, string side)
    {
        // Create a separate grip object for this musician's hand; leave player
        // sticks alone.
        var grip = new GameObject(side + " musician drumstick grip");
        // Keep the grip hidden/inactive while removing unwanted copied
        // behaviour.
        grip.SetActive(false);
        // Attach it under this hand so hand movement carries the stick
        // throughout the beat.
        grip.transform.SetParent(hand, false);
        // Use a separate copy of the musician visual prefab when one is
        // assigned.
        if (rig.drumstickPrefab != null)
        {
            // Copy that visual under the inactive hand grip.
            var visual = Instantiate(rig.drumstickPrefab, grip.transform, false);
            // Name the visual by left/right hand so it is easy to find in
            // Unity.
            visual.name = side + " musician drumstick visual";
            // Enable the visual itself; the inactive grip still keeps it from
            // showing yet.
            visual.SetActive(true);
        }
        // If no prefab exists, make a simple visible stick shaft instead.
        else
        {
            // Create a cylinder for that fallback shaft.
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Name it clearly as a visual stick without hit contacts.
            shaft.name = "Visual drumstick (no collisions)";
            // Place it under the grip without keeping an old room position.
            shaft.transform.SetParent(grip.transform, false);
            // Place the shaft's centre halfway along the intended stick length.
            shaft.transform.localPosition = Vector3.forward * rig.stickLength * .5f;
            // Turn the cylinder's long side along the grip's forward arrow.
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Make it thin and set the intended full stick length.
            shaft.transform.localScale = new Vector3(.012f, rig.stickLength * .5f, .012f);
            // Disable/remove the cylinder's default contact shape before
            // showing it.
            var collider = shaft.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        // Disable copied scripts, including hit detection and XR interaction.
        foreach (var b in grip.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        // Disable every copied contact shape so musician sticks cannot hit
        // player drums.
        foreach (var c in grip.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        // Stop and disable copied AudioSources so these visuals cannot make
        // extra sounds.
        foreach (var a in grip.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; }
        // A Rigidbody lets Unity move an object with physics. Find copied ones
        // so their motion/contacts can be disabled.
        foreach (var body in grip.GetComponentsInChildren<Rigidbody>(true))
        // Stop physics moving the stick, applying gravity or detecting
        // collisions.
        { body.isKinematic = true; body.useGravity = false; body.detectCollisions = false; }
        // Place the grip at this musician's hand-relative palm adjustment.
        grip.transform.localPosition = offset;
        // Apply the saved hand-relative stick turn.
        grip.transform.localRotation = gripRotation;
        // Read the hand's final inherited scale, including any negative
        // mirrored direction.
        Vector3 scale = hand.lossyScale;
        // Cancel that scale on the grip so stick dimensions remain metres in
        // the room.
        grip.transform.localScale = new Vector3(InverseScale(scale.x), InverseScale(scale.y), InverseScale(scale.z));
        // Remember this grip so it can be removed before another performance.
        sticks.Add(grip);
        // Show the stick only after disabling its scripts, sounds and physics.
        grip.SetActive(true);
    }

    // Return one divided by value, keeping negative signs for mirrored models.
    // Use one instead when the scale is too close to zero.
    private static float InverseScale(float value) => Mathf.Abs(value) > .00001f ? 1f / value : 1f;

    // Hide/remove the musician's sticks, restore the earlier body pose and let
    // CharacterMotion resume standing. Clear old hit targets before another
    // performance.
    public void Release()
    {
        // Hide all existing grips immediately, then schedule their removal at
        // frame end.
        foreach (var go in sticks) if (go != null) { go.SetActive(false); Destroy(go); }
        // Forget the old grip references so the next performance cannot reuse
        // stale sticks.
        sticks.Clear();
        // Stop here if no successful sitting pose needs restoring.
        if (!seated) return;
        // Put surviving body parts back to the original pose from before
        // sitting.
        foreach (var p in originalPose) if (p.bone != null) p.Restore();
        // Clear all old body poses and hand targets before another performance.
        originalPose.Clear(); pose.Clear(); left.Clear(); right.Clear();
        // Put the whole musician back at their earlier position and facing if
        // they still exist.
        if (musician != null) musician.SetPositionAndRotation(originalPosition, originalRotation);
        // Visit the Animators that were paused for sitting.
        for (int i = 0; i < animators.Length; i++)
            // Put each surviving Animator back to its earlier enabled state.
            if (animators[i] != null) animators[i].enabled = animatorEnabled[i];
        // Remember that this script has finished moving the sitting body.
        seated = false;
        // Let CharacterMotion resume standing only after the original body pose
        // is back.
        if (musician != null) musician.GetComponent<CharacterMotion>()?.ResumeAfterPerformance();
    }
    // Keep the pose from before sitting separately, so Release restores the
    // right one.
    private readonly List<Pose> originalPose = new List<Pose>();
    // Release the sitting movements and sticks when the musician/component is
    // hidden.
    private void OnDisable() => Release();
}
