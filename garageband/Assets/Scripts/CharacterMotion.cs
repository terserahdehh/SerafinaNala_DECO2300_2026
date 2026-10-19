using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Bone animation only. IntroSequence remains the sole owner of routes and translation.
[DefaultExecutionOrder(100)]
public sealed class CharacterMotion : MonoBehaviour
{
    private struct BonePose
    {
        public Transform bone;
        public Vector3 position;
        public Quaternion rotation;
        public BonePose(Transform t) { bone = t; position = t.localPosition; rotation = t.localRotation; }
        public void Restore() { if (bone != null) { bone.localPosition = position; bone.localRotation = rotation; } }
    }

    private readonly List<BonePose> bindPose = new List<BonePose>();
    private readonly ProceduralDrummer.Rig rig = new ProceduralDrummer.Rig();
    private Animator[] animators;
    private bool[] animatorStates, rootMotionStates;
    private AnimationClip walkClip, idleClip;
    private PlayableGraph graph;
    private AnimationClipPlayable walkPlayable, idlePlayable;
    private AnimationMixerPlayable mixer;
    private Transform poseFrame;
    private Vector3 leftFootRest, rightFootRest, previousPosition;
    private Quaternion leftFootRotation, rightFootRotation;
    private float phase, idleTime, strideLength, walkingWeight;
    private bool initialized, ownsBones, suspended, useClips;
    public enum StandingStyle { Relaxed, Mika, Maya }
    private StandingStyle style;
    private struct FootPlant { public Vector3 position; public bool planted; }
    private FootPlant leftPlant, rightPlant;
    private const float ClipWalkWeight = .65f;

    public float WalkingWeight => walkingWeight;
    public bool IsSuspended => suspended;
    public bool UsesClips => useClips;

    public static CharacterMotion Attach(Transform root, Animator animator, AnimationClip walk, AnimationClip idle, StandingStyle style = StandingStyle.Relaxed)
    {
        if (root == null) return null;
        var motion = root.GetComponent<CharacterMotion>();
        if (motion == null) motion = root.gameObject.AddComponent<CharacterMotion>();
        motion.style = style;
        motion.Configure(animator, walk, idle);
        return motion;
    }

    private void Configure(Animator animator, AnimationClip walk, AnimationClip idle)
    {
        if (initialized) return;
        rig.animator = animator;
        ProceduralDrummer.ResolveBones(rig, transform);
        Transform[] bones = { rig.hips, rig.spine, rig.leftUpperArm, rig.leftForearm, rig.leftHand,
            rig.rightUpperArm, rig.rightForearm, rig.rightHand, rig.leftThigh, rig.leftShin,
            rig.leftFoot, rig.rightThigh, rig.rightShin, rig.rightFoot };
        foreach (var bone in bones)
            if (bone == null || !bone.IsChildOf(transform))
            { Debug.LogError(name + ": cannot resolve standing/walking bones.", this); return; }
        foreach (var bone in bones) bindPose.Add(new BonePose(bone));
        poseFrame = rig.hips.parent;
        leftFootRest = poseFrame.InverseTransformPoint(rig.leftFoot.position);
        rightFootRest = poseFrame.InverseTransformPoint(rig.rightFoot.position);
        leftFootRotation = Quaternion.Inverse(poseFrame.rotation) * rig.leftFoot.rotation;
        rightFootRotation = Quaternion.Inverse(poseFrame.rotation) * rig.rightFoot.rotation;
        float legLength = Vector3.Distance(rig.leftThigh.position, rig.leftShin.position) +
                          Vector3.Distance(rig.leftShin.position, rig.leftFoot.position);
        strideLength = Mathf.Clamp(legLength * 1.2f, .65f, 1.5f);
        animators = GetComponentsInChildren<Animator>(true);
        animatorStates = new bool[animators.Length]; rootMotionStates = new bool[animators.Length];
        walkClip = walk; idleClip = idle;
        useClips = rig.animator != null && rig.animator.avatar != null && rig.animator.avatar.isValid &&
            rig.animator.avatar.isHuman && walk != null && idle != null && walk.isHumanMotion && idle.isHumanMotion;
        if (useClips && walk.averageSpeed.magnitude > .1f)
            strideLength = Mathf.Clamp(walk.averageSpeed.magnitude * walk.length * ClipWalkWeight, .55f, 1.8f);
        initialized = true;
        if (isActiveAndEnabled) TakeControl();
    }

    private void TakeControl()
    {
        if (!initialized || ownsBones || suspended) return;
        for (int i = 0; i < animators.Length; i++)
        {
            animatorStates[i] = animators[i].enabled;
            rootMotionStates[i] = animators[i].applyRootMotion;
            animators[i].applyRootMotion = false;
            animators[i].enabled = false;
        }
        ownsBones = true;
        previousPosition = transform.position;
        walkingWeight = 0f;
        leftPlant = default; rightPlant = default;
        if (useClips)
        {
            rig.animator.enabled = true;
            graph = PlayableGraph.Create(name + " standing locomotion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
            walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
            idlePlayable.SetSpeed(0); walkPlayable.SetSpeed(0);
            walkPlayable.SetApplyFootIK(true);
            graph.Connect(idlePlayable, 0, mixer, 0); graph.Connect(walkPlayable, 0, mixer, 1);
            var output = AnimationPlayableOutput.Create(graph, "Standing pose", rig.animator);
            output.SetSourcePlayable(mixer); graph.Play();
        }
        EvaluatePose(0f, 0f);
    }

    private void RestoreBindPose() { foreach (var bone in bindPose) bone.Restore(); }

    private void GiveUpControl()
    {
        if (!ownsBones) return;
        if (graph.IsValid()) graph.Destroy();
        RestoreBindPose();
        for (int i = 0; i < animators.Length; i++)
            if (animators[i] != null)
            { animators[i].applyRootMotion = rootMotionStates[i]; animators[i].enabled = animatorStates[i]; }
        ownsBones = false;
    }

    // Called before ProceduralDrummer caches the standing pose, and after its Release restores it.
    public void SuspendForPerformance()
    {
        suspended = true;
        GiveUpControl();
        previousPosition = transform.position;
    }

    public void ResumeAfterPerformance()
    {
        suspended = false;
        if (isActiveAndEnabled) TakeControl();
    }

    private void OnEnable() { if (initialized && !suspended) TakeControl(); }
    private void OnDisable() => GiveUpControl();
    private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    private void LateUpdate()
    {
        if (!initialized || suspended || !ownsBones) return;
        float dt = Time.deltaTime;
        Vector3 delta = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up);
        previousPosition = transform.position;
        // Seated placement, activation and teleports must not be interpreted as a stride.
        float distance = delta.magnitude;
        if (distance > Mathf.Max(.5f, dt * 5f))
        { distance = 0f; leftPlant = default; rightPlant = default; }
        EvaluatePose(dt, distance);
    }

    private void EvaluatePose(float dt, float distance)
    {
        float speed = dt > .00001f ? distance / dt : 0f;
        float target = speed > .025f ? 1f : 0f;
        walkingWeight = Mathf.MoveTowards(walkingWeight, target, dt / .22f);
        phase = Mathf.Repeat(phase + distance / strideLength, 1f);
        idleTime += dt;
        if (useClips)
        {
            // Sample in-place at travelled-distance cadence. Never write the movement/import root.
            Vector3 p = rig.animator.transform.localPosition;
            Quaternion r = rig.animator.transform.localRotation;
            idlePlayable.SetTime(idleTime % idleClip.length);
            walkPlayable.SetTime(phase * walkClip.length);
            // The supplied anime walk has an exaggerated knee/arm lift. Retain some relaxed
            // idle to soften it at the existing route speeds, without editing the source clip.
            float clipWeight = walkingWeight * ClipWalkWeight;
            mixer.SetInputWeight(0, 1f - clipWeight); mixer.SetInputWeight(1, clipWeight);
            graph.Evaluate(0f);
            rig.animator.transform.SetLocalPositionAndRotation(p, r);
            return;
        }
        RestoreBindPose();
        // These actors stand upright. Imported hip/spine offsets include a forward lean,
        // so using that bone vector as gravity pulls hanging arms behind their bodies.
        Vector3 up = Vector3.up;
        Vector3 side = Vector3.ProjectOnPlane(rig.rightThigh.position - rig.leftThigh.position, up).normalized;
        Vector3 forward = Vector3.Cross(side, up).normalized;
        float breath = Mathf.Sin(idleTime * (style == StandingStyle.Maya ? 1.35f : 1.5f));
        float restShift = Mathf.Sin(idleTime * .48f) * (1f - walkingWeight);
        float cycle = phase * Mathf.PI * 2f;
        // Small pelvis movement, never actor-root movement. Lowering provides knee flexion.
        rig.hips.position += up * (-.005f + breath * .0015f - walkingWeight * .014f +
            Mathf.Cos(cycle * 2f) * walkingWeight * .003f) +
            side * (Mathf.Sin(cycle) * walkingWeight * .004f + restShift * .004f);
        // Restrained breathing and counter-rotation soften the torso without moving the actor root.
        rig.spine.rotation = Quaternion.AngleAxis(breath * .4f, side) *
            Quaternion.AngleAxis(Mathf.Sin(cycle) * walkingWeight * .8f + restShift * .35f, up) * rig.spine.rotation;
        AnimateLeg(rig.leftThigh, rig.leftShin, rig.leftFoot, leftFootRest, leftFootRotation, phase, up, forward, ref leftPlant);
        AnimateLeg(rig.rightThigh, rig.rightShin, rig.rightFoot, rightFootRest, rightFootRotation,
            Mathf.Repeat(phase + .5f, 1f), up, forward, ref rightPlant);
        AnimateArm(rig.leftUpperArm, rig.leftForearm, rig.leftHand, -side, up, forward, -Mathf.Cos(cycle));
        AnimateArm(rig.rightUpperArm, rig.rightForearm, rig.rightHand, side, up, forward, Mathf.Cos(cycle));
    }

    private void AnimateLeg(Transform thigh, Transform shin, Transform foot, Vector3 rest,
        Quaternion rotation, float step, Vector3 up, Vector3 forward, ref FootPlant plant)
    {
        // Stance moves the foot backwards at actor speed; swing returns it with a bent knee.
        float z, lift;
        if (step < .5f) { z = Mathf.Lerp(.25f, -.25f, step * 2f); lift = 0f; }
        else { float t = (step - .5f) * 2f; z = Mathf.Lerp(-.25f, .25f, Mathf.SmoothStep(0, 1, t)); lift = Mathf.Sin(t * Mathf.PI) * .045f; }
        Vector3 target = poseFrame.TransformPoint(rest) + walkingWeight * (forward * (z * strideLength) + up * lift);
        // Hold stance feet in world space once blended in, including during route turns.
        // On stop, release into the relaxed pose over the existing blend instead of freezing a stride.
        bool stance = step < .5f && walkingWeight >= .99f;
        if (stance && !plant.planted) plant.position = target;
        if (stance) target = plant.position;
        plant.planted = stance;
        SolveLeg(thigh, shin, foot, target, forward);
        foot.rotation = poseFrame.rotation * rotation;
    }

    private void AnimateArm(Transform upper, Transform forearm, Transform hand, Vector3 outward,
        Vector3 up, Vector3 forward, float swing)
    {
        float armSwing = style == StandingStyle.Mika ? .16f : .13f;
        float a = swing * walkingWeight;
        float breath = Mathf.Sin(idleTime * 1.5f) * .004f;
        // Upper arms rest close to the ribs; forearms retain a soft, visible elbow bend.
        Vector3 upperDirection = (-up + outward * .16f + forward * (-.055f + a * armSwing + breath)).normalized;
        upper.rotation = Quaternion.FromToRotation(forearm.position - upper.position, upperDirection) * upper.rotation;
        Vector3 lowerDirection = (-up + outward * .09f + forward * (.29f + a * armSwing * .85f + breath)).normalized;
        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, lowerDirection) * forearm.rotation;
    }

    private static void SolveLeg(Transform a, Transform b, Transform c, Vector3 goal, Vector3 bend)
    {
        Vector3 start = a.position;
        float l1 = Vector3.Distance(start, b.position), l2 = Vector3.Distance(b.position, c.position);
        if (l1 < .00001f || l2 < .00001f) return;
        Vector3 delta = goal - start;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1-l2) + .0001f, l1+l2-.0001f);
        Vector3 axis = delta.normalized;
        Vector3 plane = Vector3.ProjectOnPlane(bend, axis).normalized;
        float along = (l1*l1-l2*l2+distance*distance)/(2f*distance);
        Vector3 knee = start + axis*along + plane*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        a.rotation = Quaternion.FromToRotation(b.position-start, knee-start)*a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position-b.position, start+axis*distance-b.position)*b.rotation;
    }
}
