using System;
using System.Collections.Generic;
using UnityEngine;

// This component has no audio source and no independent clock. DrumRecorder evaluates it.
public sealed class ProceduralDrummer : MonoBehaviour
{
    [Serializable]
    public class Rig
    {
        [Tooltip("Optional. Valid Humanoid avatars fill only unassigned bone fields.")]
        public Animator animator;
        public Transform hips, spine;
        public Transform leftUpperArm, leftForearm, leftHand;
        public Transform rightUpperArm, rightForearm, rightHand;
        public Transform leftThigh, leftShin, leftFoot;
        public Transform rightThigh, rightShin, rightFoot;
        [Tooltip("World marker at the hips pivot when seated; blue Z points toward the kit.")]
        public Transform seatPoint;
        public Transform leftFootPoint, rightFootPoint;
        public Vector3 hipsOffset;
        [Tooltip("Optional visual-only prefab, grip at origin, tip along local +Z. No physics or audio scripts.")]
        public GameObject drumstickPrefab;
        public Vector3 leftGripOffset, rightGripOffset;
        public Vector3 leftGripEuler, rightGripEuler;
        [Min(.05f)] public float stickLength = .34f;
        [Min(.02f)] public float liftHeight = .18f;
        [Min(.01f)] public float strikeDuration = .09f;
        [Min(.01f)] public float recoveryDuration = .18f;
    }

    public enum Hand { Alternate, Left, Right, Pedal }

    [Serializable]
    public class DrumTarget
    {
        public string soundName;
        [Tooltip("Place on the playable surface; position is the intended stick tip contact.")]
        public Transform target;
        public Hand hand;
    }

    private struct Pose
    {
        public Transform bone;
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Transform t) { bone = t; position = t.localPosition; rotation = t.localRotation; }
        public void Restore() { bone.localPosition = position; bone.localRotation = rotation; }
    }

    private struct Stroke { public float time; public Vector3 point; }
    private readonly List<Pose> pose = new List<Pose>();
    private readonly List<Stroke> left = new List<Stroke>(), right = new List<Stroke>();
    private readonly List<GameObject> sticks = new List<GameObject>();
    private Animator[] animators;
    private bool[] animatorEnabled;
    private Rig rig;
    private Transform musician;
    private Vector3 originalPosition, seatedPosition;
    private Quaternion originalRotation, seatedRotation;
    private Vector3 forward, up, sideways, leftRest, rightRest;
    private Quaternion leftGrip, rightGrip;
    private float duration;
    private bool seated;

    public static void ResolveBones(Rig r, Transform root)
    {
        if (r.animator == null) r.animator = root.GetComponentInChildren<Animator>(true);
        bool human = r.animator != null && r.animator.avatar != null &&
                     r.animator.avatar.isValid && r.animator.avatar.isHuman;
        Resolve(ref r.hips, HumanBodyBones.Hips, "J_Bip_C_Hips");
        Resolve(ref r.spine, HumanBodyBones.Spine, "J_Bip_C_Spine");
        Resolve(ref r.leftUpperArm, HumanBodyBones.LeftUpperArm, "J_Bip_L_UpperArm");
        Resolve(ref r.leftForearm, HumanBodyBones.LeftLowerArm, "J_Bip_L_LowerArm");
        Resolve(ref r.leftHand, HumanBodyBones.LeftHand, "J_Bip_L_Hand");
        Resolve(ref r.rightUpperArm, HumanBodyBones.RightUpperArm, "J_Bip_R_UpperArm");
        Resolve(ref r.rightForearm, HumanBodyBones.RightLowerArm, "J_Bip_R_LowerArm");
        Resolve(ref r.rightHand, HumanBodyBones.RightHand, "J_Bip_R_Hand");
        Resolve(ref r.leftThigh, HumanBodyBones.LeftUpperLeg, "J_Bip_L_UpperLeg");
        Resolve(ref r.leftShin, HumanBodyBones.LeftLowerLeg, "J_Bip_L_LowerLeg");
        Resolve(ref r.leftFoot, HumanBodyBones.LeftFoot, "J_Bip_L_Foot");
        Resolve(ref r.rightThigh, HumanBodyBones.RightUpperLeg, "J_Bip_R_UpperLeg");
        Resolve(ref r.rightShin, HumanBodyBones.RightLowerLeg, "J_Bip_R_LowerLeg");
        Resolve(ref r.rightFoot, HumanBodyBones.RightFoot, "J_Bip_R_Foot");
        void Resolve(ref Transform bone, HumanBodyBones id, string prefix)
        {
            if (bone != null) return;
            if (human) bone = r.animator.GetBoneTransform(id);
            if (bone != null) return;
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
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == native || t.name == prefix || t.name.StartsWith(prefix + "_", StringComparison.Ordinal))
                { bone = t; break; }
        }
    }

    public bool Seat(Transform root, Rig settings)
    {
        Release();
        if (root == null || settings == null) return false;
        ResolveBones(settings, root);
        rig = settings;
        bool humanoid = rig.animator != null && rig.animator.avatar != null &&
            rig.animator.avatar.isValid && rig.animator.avatar.isHuman;
        Debug.Log(root.name + ": " + (humanoid ? "valid Humanoid Animator; explicit bones take priority." :
            "no valid Humanoid Animator; using explicit/named bone Transforms."), root);
        Transform[] bones = { rig.hips, rig.spine, rig.leftUpperArm, rig.leftForearm, rig.leftHand,
            rig.rightUpperArm, rig.rightForearm, rig.rightHand, rig.leftThigh, rig.leftShin,
            rig.leftFoot, rig.rightThigh, rig.rightShin, rig.rightFoot };
        foreach (var t in bones)
            if (t == null || !t.IsChildOf(root))
            { Debug.LogError(root.name + ": assign all Procedural Drumming bones under this musician.", root); return false; }
        if (rig.seatPoint == null || rig.seatPoint.IsChildOf(root))
        { Debug.LogError("Assign a stationary Seat Point outside the musician hierarchy.", root); return false; }
        if (!Chain(rig.leftUpperArm, rig.leftForearm, rig.leftHand) ||
            !Chain(rig.rightUpperArm, rig.rightForearm, rig.rightHand) ||
            !Chain(rig.leftThigh, rig.leftShin, rig.leftFoot) ||
            !Chain(rig.rightThigh, rig.rightShin, rig.rightFoot))
        { Debug.LogError("Drummer bones must form upper/lower/end descendant chains.", root); return false; }
        musician = root;
        originalPosition = root.position; originalRotation = root.rotation;
        // Disable every Animator that could write this skeleton, remembering its state.
        var writers = new HashSet<Animator>(root.GetComponentsInChildren<Animator>(true));
        foreach (var a in root.GetComponentsInParent<Animator>(true)) writers.Add(a);
        animators = new Animator[writers.Count]; writers.CopyTo(animators);
        animatorEnabled = new bool[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        { animatorEnabled[i] = animators[i].enabled; animators[i].enabled = false; }
        foreach (var t in bones) { pose.Add(new Pose(t)); originalPose.Add(new Pose(t)); }
        up = Vector3.up;
        forward = Vector3.ProjectOnPlane(rig.seatPoint.forward, up).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
        sideways = Vector3.Cross(up, forward).normalized;
        // Derive facing from anatomy, not the GLB/FBX root axes. Apply yaw to movement root only.
        Vector3 anatomicalForward = Vector3.Cross(rig.rightThigh.position - rig.leftThigh.position,
            rig.spine.position - rig.hips.position);
        anatomicalForward = Vector3.ProjectOnPlane(anatomicalForward, up);
        if (anatomicalForward.sqrMagnitude > .000001f)
            root.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(anatomicalForward, forward, up), up) * root.rotation;
        root.position += rig.seatPoint.position + rig.seatPoint.TransformDirection(rig.hipsOffset) - rig.hips.position;
        SeatLeg(rig.leftThigh, rig.leftShin, rig.leftFoot, rig.leftFootPoint);
        SeatLeg(rig.rightThigh, rig.rightShin, rig.rightFoot, rig.rightFootPoint);
        // Cache the seated reference pose, including feet, for absolute per-frame evaluation.
        pose.Clear(); foreach (var t in bones) pose.Add(new Pose(t));
        seatedPosition = root.position; seatedRotation = root.rotation;
        leftRest = rig.hips.position + forward * .42f - sideways * .18f + up * .22f;
        rightRest = rig.hips.position + forward * .42f + sideways * .18f + up * .22f;
        // Calibrate stick axes from each imported wrist and forearm, retaining wrist roll.
        leftGrip = CalibrateGrip(rig.leftForearm, rig.leftHand) * Quaternion.Euler(rig.leftGripEuler);
        rightGrip = CalibrateGrip(rig.rightForearm, rig.rightHand) * Quaternion.Euler(rig.rightGripEuler);
        CreateStick(rig.leftHand, rig.leftGripOffset, leftGrip);
        CreateStick(rig.rightHand, rig.rightGripOffset, rightGrip);
        seated = true;
        Evaluate(0f);
        return true;
    }

    private Quaternion CalibrateGrip(Transform forearm, Transform hand)
    {
        Vector3 direction = (hand.position - forearm.position).normalized;
        Vector3 rollUp = Vector3.ProjectOnPlane(up, direction);
        if (rollUp.sqrMagnitude < .001f) rollUp = Vector3.ProjectOnPlane(forward, direction);
        return Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(direction, rollUp);
    }

    private static bool Chain(Transform a, Transform b, Transform c) => b.IsChildOf(a) && c.IsChildOf(b);

    private void SeatLeg(Transform thigh, Transform shin, Transform foot, Transform marker)
    {
        float upper = Vector3.Distance(thigh.position, shin.position);
        float lower = Vector3.Distance(shin.position, foot.position);
        Vector3 goal = marker != null ? marker.position :
            thigh.position + forward * (upper * .92f) - up * (lower * 1.02f);
        Quaternion footRotation = foot.rotation;
        Solve(thigh, shin, foot, goal, forward);
        foot.rotation = footRotation;
    }

    // Analytic two-bone IK; uses current bone directions, so arbitrary imported bone axes work.
    private static void Solve(Transform a, Transform b, Transform c, Vector3 goal, Vector3 bend)
    {
        Vector3 start = a.position;
        float l1 = Vector3.Distance(start, b.position), l2 = Vector3.Distance(b.position, c.position);
        if (l1 < .00001f || l2 < .00001f) return;
        Vector3 delta = goal - start;
        if (delta.sqrMagnitude < .00000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + .0001f, l1 + l2 - .0001f);
        Vector3 axis = delta.normalized;
        Vector3 plane = Vector3.ProjectOnPlane(bend, axis).normalized;
        if (plane.sqrMagnitude < .5f) plane = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        if (plane.sqrMagnitude < .5f) plane = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
        float along = (l1 * l1 - l2 * l2 + distance * distance) / (2f * distance);
        Vector3 elbow = start + axis * along + plane * Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
        a.rotation = Quaternion.FromToRotation(b.position - start, elbow - start) * a.rotation;
        Vector3 reachable = start + axis * distance;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, reachable - b.position) * b.rotation;
    }

    public void PrepareRecording(IReadOnlyList<DrumRecorder.DrumHit> hits, float loopDuration, DrumTarget[] targets)
    {
        left.Clear(); right.Clear(); duration = loopDuration;
        bool useLeft = true;
        var missing = new HashSet<string>();
        foreach (var hit in hits)
        {
            DrumTarget match = null;
            if (targets != null)
                foreach (var target in targets)
                    if (target != null && string.Equals(target.soundName?.Trim(), hit.soundName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    { match = target; break; }
            if (match == null || (match.hand != Hand.Pedal && match.target == null))
            { if (missing.Add(hit.soundName)) Debug.LogWarning("No animation target for recorded sound: " + hit.soundName, this); continue; }
            if (match.hand == Hand.Pedal) continue; // Pedal audio stays in DrumRecorder; it is not a stick hit.
            bool isLeft = match.hand == Hand.Left || (match.hand == Hand.Alternate && useLeft);
            if (match.hand == Hand.Alternate) useLeft = !useLeft;
            (isLeft ? left : right).Add(new Stroke { time = hit.time, point = match.target.position });
        }
    }

    public void Evaluate(float playbackTime)
    {
        if (!seated) return;
        musician.SetPositionAndRotation(seatedPosition, seatedRotation);
        foreach (var p in pose) p.Restore();
        AnimateArm(left, playbackTime, true);
        AnimateArm(right, playbackTime, false);
    }

    private void AnimateArm(List<Stroke> strokes, float time, bool isLeft)
    {
        Vector3 contact = isLeft ? leftRest : rightRest;
        float lift = 1f;
        if (strokes.Count > 0 && duration > 0f)
        {
            // Find surrounding hits on the same circular timeline as the audio loop.
            Stroke previous = strokes[strokes.Count - 1], next = strokes[0];
            float previousTime = previous.time - duration, nextTime = next.time + duration;
            foreach (var stroke in strokes)
                if (stroke.time <= time) { previous = stroke; previousTime = stroke.time; }
                else { next = stroke; nextTime = stroke.time; break; }
            float gap = Mathf.Max(.001f, nextTime - previousTime);
            float recovery = Mathf.Min(rig.recoveryDuration, gap * .55f);
            float strike = Mathf.Min(rig.strikeDuration, gap * .45f);
            float age = time - previousTime;
            if (age < recovery)
            { contact = previous.point; lift = Mathf.SmoothStep(0f, 1f, age / recovery); }
            else
            {
                float travel = Mathf.InverseLerp(previousTime + recovery, nextTime - strike, time);
                contact = Vector3.Lerp(previous.point, next.point, Mathf.SmoothStep(0f, 1f, travel));
                lift = time > nextTime - strike ? Mathf.SmoothStep(0f, 1f, (nextTime - time) / strike) : 1f;
            }
        }
        Transform upper = isLeft ? rig.leftUpperArm : rig.rightUpperArm;
        Transform forearm = isLeft ? rig.leftForearm : rig.rightForearm;
        Transform hand = isLeft ? rig.leftHand : rig.rightHand;
        Vector3 gripOffset = isLeft ? rig.leftGripOffset : rig.rightGripOffset;
        Quaternion gripCorrection = isLeft ? leftGrip : rightGrip;
        Vector3 stickDirection = (forward - up * .55f).normalized;
        Quaternion stickWorld = Quaternion.LookRotation(stickDirection, up);
        Quaternion wristWorld = stickWorld * Quaternion.Inverse(gripCorrection);
        Vector3 tip = contact + up * (rig.liftHeight * lift);
        Vector3 wrist = tip - stickDirection * rig.stickLength - wristWorld * Vector3.Scale(hand.lossyScale, gripOffset);
        Solve(upper, forearm, hand, wrist, -forward * .3f + sideways * (isLeft ? -1f : 1f) - up * .5f);
        // Grip Euler offsets calibrate each model's imported wrist axes.
        hand.rotation = wristWorld;
    }

    private void CreateStick(Transform hand, Vector3 offset, Quaternion gripRotation)
    {
        // Build under an inactive grip so prefab audio/physics cannot activate before sanitising.
        var grip = new GameObject("Procedural drumstick grip");
        grip.SetActive(false);
        grip.transform.SetParent(hand, false);
        if (rig.drumstickPrefab != null) Instantiate(rig.drumstickPrefab, grip.transform, false);
        else
        {
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Visual drumstick (no collisions)";
            shaft.transform.SetParent(grip.transform, false);
            shaft.transform.localPosition = Vector3.forward * rig.stickLength * .5f;
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(.012f, rig.stickLength * .5f, .012f);
            var collider = shaft.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        // Visual-only custom prefabs cannot trigger DrumSound or DrumstickHit.
        foreach (var b in grip.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        foreach (var c in grip.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var a in grip.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; }
        foreach (var body in grip.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.detectCollisions = false; }
        grip.transform.localPosition = offset;
        grip.transform.localRotation = gripRotation;
        // Imported skeletons can be scaled; make stick dimensions world metres.
        Vector3 scale = hand.lossyScale;
        grip.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
        sticks.Add(grip);
        grip.SetActive(true);
    }

    public void Release()
    {
        if (!seated) return;
        foreach (var go in sticks) if (go != null) { go.SetActive(false); Destroy(go); }
        sticks.Clear();
        // Restore the original local bone pose, not the seated cache.
        foreach (var p in originalPose) if (p.bone != null) p.Restore();
        originalPose.Clear(); pose.Clear(); left.Clear(); right.Clear();
        if (musician != null) musician.SetPositionAndRotation(originalPosition, originalRotation);
        for (int i = 0; i < animators.Length; i++)
            if (animators[i] != null) animators[i].enabled = animatorEnabled[i];
        seated = false;
    }
    private readonly List<Pose> originalPose = new List<Pose>();
    private void OnDisable() => Release();
}
