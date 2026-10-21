/*
 * Ms Maya, Mika and Ren lower their arms, breathing, and move their legs
 * when they walk. This script adds those body movements.
 * IntroSequence still decides where each character walks around the room.
 * This script stops before seated drumming so ProceduralDrummer can move the
 * character's body instead. It runs in the game and in Editor Play Mode.
 * Play Mode means testing the game inside Unity.
 *
 * A bone is an invisible joint used to bend part of the character model.
 * Each bone has a Transform: Unity's record of position, rotation and size.
 * Rotation means which way something faces. A pose describes the positions
 * and rotations of the character's body parts.
 *
 * In Unity's object list, one object can sit inside another. The containing
 * object is its parent. A local position is measured from that parent.
 * A world position is measured in the room instead.
 */
// Use a List to remember the character's saved body-part poses.
using System.Collections.Generic;
// Use Unity's scene objects, body-part positions and movement maths.
using UnityEngine;
// Use Unity's tools for playing and mixing animation clips.
using UnityEngine.Animations;
// Use the playback connections that combine the standing and walking clips.
using UnityEngine.Playables;

// A frame is one update of the displayed game image. Unity runs many frames
// each second. Run this script after normal movement so the legs can respond
// to how far the character has just travelled.
[DefaultExecutionOrder(100)]
// Make this a Unity component that can update one character's body movements.
public sealed class CharacterMotion : MonoBehaviour
{
    // Save one body part's starting pose so we can put it back later.
    private struct BonePose
    {
        // Remember which body part this saved pose belongs to, such as a hand.
        public Transform bone;
        // Store that part's starting X, Y and Z position relative to its
        // parent.
        // Restore uses this to put the part back after animation changes it.
        public Vector3 position;
        // Quaternion is Unity's type for storing a rotation.
        public Quaternion rotation;

        // t is the body part to remember. Store its position and rotation
        // relative to its parent, rather than a fixed place in the room.
        public BonePose(Transform t)
        {
            // Keep the body-part Transform so this saved pose knows which part
            // to restore.
            bone = t;
            // Remember where that part starts, measured from its parent.
            position = t.localPosition;
            // Remember its starting turn relative to its parent too.
            rotation = t.localRotation;
        }

        // Put this body part back, as long as it still exists.
        public void Restore()
        {
            // Only restore the body part if it still exists.
            if (bone != null)
            {
                // Put the part back at its saved parent-relative position.
                bone.localPosition = position;
                // Restore its saved parent-relative turn as well.
                bone.localRotation = rotation;
            }
        }
    }

    // bindPose holds the saved starting pose of all the body parts we change.
    // rig groups the bone references. The drummer uses the same Rig type so
    // standing and seated animation can find the same body parts.
    private readonly List<BonePose> bindPose = new List<BonePose>();
    // Keep this character's bone references in the same format the drummer
    // uses.
    private readonly ProceduralDrummer.Rig rig = new ProceduralDrummer.Rig();

    // An Animator is a Unity component that normally plays model animations.
    // Remember its settings before stopping it from moving the same bones.
    // Root motion lets an animation move the whole character. We turn that
    // off here because IntroSequence already controls travel around the room.
    private Animator[] animators;
    // Remember which Animators were enabled and which could move the whole
    // model.
    private bool[] animatorStates, rootMotionStates;

    // An animation clip stores a sequence of body movements.
    // These are the optional walking and standing clips supplied by the intro.
    private AnimationClip walkClip, idleClip;

    // Animation blending means mixing two poses, such as some walking and
    // some relaxed standing. graph holds Unity's playback connections.
    // walkPlayable and idlePlayable let us choose a moment in each clip.
    // mixer combines them into the pose shown on the character.
    private PlayableGraph graph;
    // Keep one playback object for each clip so we can choose its current
    // moment.
    private AnimationClipPlayable walkPlayable, idlePlayable;
    // Keep the mixer that combines the two clips into one visible body pose.
    private AnimationMixerPlayable mixer;

    // poseFrame is the hips' parent, used as a reference for resting feet.
    // Saving feet relative to this parent lets them follow the character
    // around the room. Also remember the whole character's last position.
    private Transform poseFrame;
    // Save each foot's resting position and the character's last room position.
    private Vector3 leftFootRest, rightFootRest, previousPosition;
    // Save the feet's resting turns relative to the hips' parent.
    private Quaternion leftFootRotation, rightFootRotation;

    // phase goes from 0 to 1 through one walking cycle, then repeats.
    // idleTime measures time for breathing. strideLength is the travel
    // distance used for a full walking cycle. walkingWeight is how much
    // walking to show: 0 means standing, 1 means fully walking.
    private float phase, idleTime, strideLength, walkingWeight;

    // initialized: the required body parts have been found and saved.
    // ownsBones: this script is currently allowed to move the body parts.
    // suspended: standing/walking is paused for the seated performance.
    // useClips: the model and supplied clips passed the compatibility checks.
    private bool initialized, ownsBones, suspended, useClips;

    // Select the existing small differences in each character's movements.
    public enum StandingStyle
    {
        // Use the default relaxed movements, also used for Ren.
        Relaxed,
        // Select Mika's existing small movement differences.
        Mika,
        // Select Maya's existing small movement differences.
        Maya
    }
    // Remember which of these styles this character is using.
    private StandingStyle style;

    // Remember where a foot touched the floor. Keeping it at that room
    // position during part of the step helps it slide less while walking.
    private struct FootPlant
    {
        // Remember the room position where this foot should stay during floor
        // contact.
        public Vector3 position;
        // Remember whether this foot is currently being held at that floor
        // position.
        public bool planted;
    }
    // Keep separate floor-contact records for the left and right feet.
    private FootPlant leftPlant, rightPlant;

    // Use at most 65% of the walking clip, with relaxed standing mixed in.
    private const float ClipWalkWeight = .65f;

    // These read-only properties let other code check the walking amount,
    // whether seated drumming has paused this script, and whether clips work.
    public float WalkingWeight => walkingWeight;
    // Let other scripts check whether seated drumming has paused this
    // component.
    public bool IsSuspended => suspended;
    // Let other scripts check whether this character is using compatible clips.
    public bool UsesClips => useClips;

    // Add walking/standing movement to the character and return this component.
    // root is the whole character object, not an individual body part.
    // animator belongs to that model; walk and idle are its optional clips.
    // style selects Maya, Mika or the default movement. A missing root returns
    // null because there is no character to attach this component to.
    public static CharacterMotion Attach(
        // root is the whole character object this component belongs to.
        Transform root,
        // animator is the Unity animation component on this character's model.
        Animator animator,
        // walk is the optional clip containing walking body movements.
        AnimationClip walk,
        // idle is the optional clip containing relaxed standing movements.
        AnimationClip idle,
        // Choose the movement style; use Relaxed if the caller does not choose
        // one.
        StandingStyle style = StandingStyle.Relaxed)
    {
        // Check whether the caller supplied a character object to work on.
        if (root == null)
            // Return no component because a missing character cannot receive
            // animation.
            return null;

        // Reuse an existing component so repeated setup does not add another
        // script trying to move the same character.
        var motion = root.GetComponent<CharacterMotion>();
        // Check whether the character still needs this animation component.
        if (motion == null)
            // Add one component only when missing, avoiding duplicate body
            // movement.
            motion = root.gameObject.AddComponent<CharacterMotion>();

        // Remember the requested character style before setting up its bones.
        motion.style = style;
        // Find the body parts and choose clips or calculated movements for this
        // model.
        motion.Configure(animator, walk, idle);
        // Return the component so the caller can refer to this character's
        // animation.
        return motion;
    }

    // Find this character's body parts and remember their starting pose.
    // animator, walk and idle are the model and clips supplied by Attach.
    // If those clips cannot work with this model, calculate movement instead.
    private void Configure(
        // Use this character's Animator to help find its body parts.
        Animator animator,
        // Use the optional walking clip supplied during Attach.
        AnimationClip walk,
        // Use the optional standing clip supplied during Attach.
        AnimationClip idle)
    {
        // Check whether this character's body parts and starting pose were
        // already saved.
        if (initialized)
            // Stop here so another setup call does not replace the original
            // saved pose.
            return;

        // Give the bone-finding helper the Animator assigned to this character.
        rig.animator = animator;
        // Fill missing bone references by looking through this character's
        // model.
        ProceduralDrummer.ResolveBones(rig, transform);
        // Collect every body part needed before trying to animate the
        // character.
        Transform[] bones =
        {
            // Include the pelvis and torso, which give the body its centre and
            // facing.
            rig.hips, rig.spine,
            // Include the left shoulder-to-hand parts for the left arm
            // movement.
            rig.leftUpperArm, rig.leftForearm, rig.leftHand,
            // Include the matching right arm parts.
            rig.rightUpperArm, rig.rightForearm, rig.rightHand,
            // Include the left thigh, shin and foot for stepping and knee
            // bending.
            rig.leftThigh, rig.leftShin, rig.leftFoot,
            // Include the right leg's parts for its alternating step.
            rig.rightThigh, rig.rightShin, rig.rightFoot
        };

        // Stop setup if a required body part is missing or belongs to another
        // character. Otherwise, save each part before changing its pose.
        foreach (var bone in bones)
            // Reject a missing body part or one outside this character's object
            // hierarchy.
            if (bone == null || !bone.IsChildOf(transform))
            {
                // Report the setup problem in Unity's Console so the missing
                // bones can be found.
                Debug.LogError(
                    // Include the character's name and this component to
                    // identify the problem.
                    name + ": cannot resolve standing/walking bones.", this);
                // Stop setup before trying to move an incomplete or wrong
                // skeleton.
                return;
            }
        // Visit each body part again now that all required references passed
        // the check.
        foreach (var bone in bones)
            // Save that part's starting position and turn for later
            // restoration.
            bindPose.Add(new BonePose(bone));

        // Save where the feet rest relative to the hips' parent.
        // InverseTransformPoint converts room positions to parent-relative
        // positions. Quaternion.Inverse removes the parent's turn from each
        // foot's rotation, leaving its rotation relative to that parent.
        poseFrame = rig.hips.parent;
        // Save the left foot's resting position measured from the hips' parent.
        leftFootRest = poseFrame.InverseTransformPoint(rig.leftFoot.position);
        // Save the right foot's resting position from the same parent.
        rightFootRest = poseFrame.InverseTransformPoint(rig.rightFoot.position);
        // Prepare to save the left foot's turn relative to that parent.
        leftFootRotation =
            // Remove the parent's turn from the left foot's room-facing
            // rotation.
            Quaternion.Inverse(poseFrame.rotation) * rig.leftFoot.rotation;
        // Prepare to save the right foot's parent-relative turn separately.
        rightFootRotation =
            // Remove the parent's turn from the right foot's room-facing
            // rotation.
            Quaternion.Inverse(poseFrame.rotation) * rig.rightFoot.rotation;

        // Scale steps to this character's leg length. Add the hip-to-knee
        // and knee-to-foot distances, then limit the resulting cycle length
        // so unusually sized models do not take extreme steps.
        float legLength =
            // Measure the upper leg from its hip joint to its knee joint.
            Vector3.Distance(rig.leftThigh.position, rig.leftShin.position) +
            // Add the knee-to-foot length to get the full leg length.
            Vector3.Distance(rig.leftShin.position, rig.leftFoot.position);
        // Choose a walking-cycle distance from leg length, limited to 0.65–1.5
        // metres.
        strideLength = Mathf.Clamp(legLength * 1.2f, .65f, 1.5f);

        // Find all Animators inside this character, including inactive child
        // objects.
        animators = GetComponentsInChildren<Animator>(true);
        // Make one space per Animator to remember whether it was enabled.
        animatorStates = new bool[animators.Length];
        // Make another space per Animator to remember its whole-model movement
        // setting.
        rootMotionStates = new bool[animators.Length];
        // Keep the walking clip so later updates can choose a walking pose from
        // it.
        walkClip = walk;
        // Keep the standing clip for relaxed poses and breathing while stopped.
        idleClip = idle;

        // A humanoid avatar tells Unity which model bones are its arms, legs
        // and other human body parts. Both clips must use that same kind of
        // human animation before this script can blend them on the model.
        useClips =
            // Require an Animator before asking it to play compatible human
            // movements.
            rig.animator != null &&
            // Require an avatar, which maps model bones to human body parts.
            rig.animator.avatar != null &&
            // Check that Unity considers this avatar mapping usable.
            rig.animator.avatar.isValid &&
            // Require a human-body mapping rather than another kind of model.
            rig.animator.avatar.isHuman &&
            // Require both the walking and standing clips before trying to mix
            // them.
            walk != null && idle != null &&
            // Require both clips to contain the human-body animation this model
            // can use.
            walk.isHumanMotion && idle.isHumanMotion;

        // If the walking clip describes travel, averageSpeed.magnitude gives
        // its speed. Multiply by clip duration to get distance per cycle,
        // allowing for the 65% walking blend.
        if (useClips && walk.averageSpeed.magnitude > .1f)
            // Calculate a walking-cycle travel distance from the clip rather
            // than leg length.
            strideLength = Mathf.Clamp(
                // Multiply clip speed by duration and allow for the reduced
                // walking amount.
                walk.averageSpeed.magnitude * walk.length * ClipWalkWeight,
                // Keep this clip-based cycle distance between 0.55 and 1.8
                // metres.
                .55f, 1.8f);

        // Remember that this character's animation setup is complete.
        initialized = true;
        // Start updating its body now only if this component is enabled and
        // active.
        if (isActiveAndEnabled)
            // Pause competing animations and apply the first standing pose.
            TakeControl();
    }

    // Let this script move the character's body. First pause the normal
    // Animators, saving their settings so we can restore them afterwards.
    private void TakeControl()
    {
        // Skip setup if unready, already moving the bones, or paused for seated
        // drumming.
        if (!initialized || ownsBones || suspended)
            // Leave the current animation arrangement unchanged in those cases.
            return;

        // Visit each Animator so its settings can be saved before changing
        // them.
        for (int i = 0; i < animators.Length; i++)
        {
            // Remember whether this Animator was turned on before this script
            // took over.
            animatorStates[i] = animators[i].enabled;
            // Remember whether its clips were allowed to move the whole
            // character.
            rootMotionStates[i] = animators[i].applyRootMotion;
            // Stop clips moving the whole character; IntroSequence already
            // moves it.
            animators[i].applyRootMotion = false;
            // Pause this Animator so its normal animations do not fight these
            // body movements.
            animators[i].enabled = false;
        }

        // Start with standing movement and no old foot contacts.
        ownsBones = true;
        // Measure future walking distance from the character's current room
        // position.
        previousPosition = transform.position;
        // Begin with standing movement until actual walking is detected.
        walkingWeight = 0f;
        // Forget any old left-foot contact when starting body movement again.
        leftPlant = default;
        // Forget any old right-foot contact as well.
        rightPlant = default;

        // Only build clip playback if the model and both clips passed the
        // checks.
        if (useClips)
        {
            // Build Unity's connections for mixing idle and walk clips.
            // Manual here means this script chooses the clip times instead
            // of letting Unity advance them on a separate animation clock.
            rig.animator.enabled = true;
            // Create the set of clip-playback connections for this character.
            graph = PlayableGraph.Create(name + " standing locomotion");
            // Let this script choose clip times instead of a separate animation
            // clock.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            // Create a mixer with two inputs: standing and walking.
            mixer = AnimationMixerPlayable.Create(graph, 2);
            // Create the object that reads poses from the standing clip.
            idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
            // Create the matching object for the walking clip.
            walkPlayable = AnimationClipPlayable.Create(graph, walkClip);
            // Stop the standing clip advancing by itself; we will set its time
            // explicitly.
            idlePlayable.SetSpeed(0);
            // Stop the walking clip advancing by itself for the same reason.
            walkPlayable.SetSpeed(0);

            // Ask Unity to adjust the walking feet. Connect idle to input 0
            // and walking to input 1, then send the mix to this model.
            walkPlayable.SetApplyFootIK(true);
            // Connect standing to the mixer's first input, numbered zero.
            graph.Connect(idlePlayable, 0, mixer, 0);
            // Connect walking to its second input, numbered one.
            graph.Connect(walkPlayable, 0, mixer, 1);
            // Create an output so the combined pose can reach the character
            // model.
            var output = AnimationPlayableOutput.Create(
                // Use these playback connections and send the output to this
                // character's Animator.
                graph, "Standing pose", rig.animator);
            // Choose the mixer as the source of the body pose shown on the
            // model.
            output.SetSourcePlayable(mixer);
            // Enable the playback connections so they can apply the chosen
            // poses.
            graph.Play();
        }

        // Apply a standing pose now so the model does not wait in a T-pose.
        EvaluatePose(0f, 0f);
    }

    // Put all saved body parts back before making a fresh pose.
    // This stops small position and rotation changes building up each frame.
    private void RestoreBindPose()
    {
        // Visit each saved starting pose before making a new body pose.
        foreach (var bone in bindPose)
            // Put that body part back at its saved parent-relative position and
            // turn.
            bone.Restore();
    }

    // Stop this script from moving the body and restore the saved pose and
    // Animator settings. Another script can then animate the character.
    private void GiveUpControl()
    {
        // Check whether this script is currently moving the character's body
        // parts.
        if (!ownsBones)
            // Do nothing if there is no standing/walking animation control to
            // release.
            return;

        // Check whether clip-playback connections were actually created.
        if (graph.IsValid())
            // Remove those connections so clips can no longer overwrite another
            // pose.
            graph.Destroy();
        // Put the body parts back before another script starts moving them.
        RestoreBindPose();

        // Visit each Animator whose earlier settings were saved.
        for (int i = 0; i < animators.Length; i++)
            // Only restore an Animator that still exists on the character.
            if (animators[i] != null)
            {
                // Restore its earlier setting for whether animation may move
                // the whole model.
                animators[i].applyRootMotion = rootMotionStates[i];
                // Turn it on or leave it off according to its original enabled
                // state.
                animators[i].enabled = animatorStates[i];
            }
        // Remember that this script is no longer moving the character's body
        // parts.
        ownsBones = false;
    }

    // Stop the standing and walking movements before the character sits.
    // This lets ProceduralDrummer move the body into the drumming pose.
    public void SuspendForPerformance()
    {
        // Block standing and walking updates before seated drumming begins.
        suspended = true;
        // Restore the saved body pose and let the drummer move those body parts
        // instead.
        GiveUpControl();
        // Reset the last room position so seated placement will not count as a
        // step.
        previousPosition = transform.position;
    }

    // Allow standing and walking again after the drummer restores the body.
    public void ResumeAfterPerformance()
    {
        // Allow standing and walking updates after the drummer restores the
        // body.
        suspended = false;
        // Restart them now only if this character's component is active and
        // enabled.
        if (isActiveAndEnabled)
            // Prepare the Animators and apply the initial standing pose again.
            TakeControl();
    }

    // Unity calls this when the component becomes active. Resume body
    // movement only if setup has finished and seated drumming is not using it.
    private void OnEnable()
    {
        // Only resume if body setup finished and seated drumming is not using
        // the body.
        if (initialized && !suspended)
            // Let this script begin moving the body parts again.
            TakeControl();
    }

    // Unity calls this when the component becomes inactive.
    // The => is a short way to call GiveUpControl as this method's only action.
    private void OnDisable()
        // Run GiveUpControl as this method's single action when the component
        // turns off.
        => GiveUpControl();

    // Remove the temporary playback connections when this component is removed.
    private void OnDestroy()
    {
        // Check whether there are clip-playback connections to clean up.
        if (graph.IsValid())
            // Destroy those connections when this component is removed.
            graph.Destroy();
    }

    // Unity calls LateUpdate after the usual movement updates each frame.
    // Measure actual floor-level travel so the legs respond to movement,
    // rather than continuing to walk when the character has stopped.
    private void LateUpdate()
    {
        // Skip this frame if unready, seated, or not currently allowed to move
        // the body.
        if (!initialized || suspended || !ownsBones)
            // Leave the character's current body pose untouched in those
            // states.
            return;

        // dt is the time since the previous frame. delta is how far the whole
        // character moved. ProjectOnPlane removes vertical movement, so moving
        // up or down does not count as walking across the floor.
        // magnitude reads the length of that movement in metres.
        float dt = Time.deltaTime;
        // Calculate how far the character moved across the floor since the last
        // update.
        Vector3 delta = Vector3.ProjectOnPlane(
            // Subtract its old room position, then remove the up/down part of
            // that movement.
            transform.position - previousPosition, Vector3.up);
        // Keep this room position for the next frame's distance comparison.
        previousPosition = transform.position;
        // Read the movement's length in metres to decide how far the legs
        // should step.
        float distance = delta.magnitude;

        // Treat a large sudden move as placement or teleporting, not a step.
        // Clear old floor contacts too, so feet do not stay at the old place.
        if (distance > Mathf.Max(.5f, dt * 5f))
        {
            // If the musician is moved straight to the drum seat,
            // do not count that movement as walking.
            distance = 0f;
            // Release the left foot from any remembered contact at the previous
            // location.
            leftPlant = default;
            // Release the right foot from its old contact too.
            rightPlant = default;
        }
        // Update the body using this frame's elapsed time and accepted travel
        // distance.
        EvaluatePose(dt, distance);
    }

    // Show walking while the character travels, and relaxed standing when it stops.
    // dt: seconds since this code last ran.
    // distance: how far the character moved during that time.
    private void EvaluatePose(float dt, float distance)
    {
        // Change smoothly between standing and walking instead of snapping.
        // Distance divided by time gives speed. Above the small speed limit,
        // aim for walkingWeight 1; otherwise aim for 0. The change takes about
        // 0.22 seconds. The tiny time check avoids dividing by nearly zero.
        float speed = dt > .00001f ? distance / dt : 0f;
        // Aim for full walking above the small speed limit; otherwise aim for
        // standing.
        float target = speed > .025f ? 1f : 0f;
        // Change the walking amount gradually so stopping and starting look
        // smooth.
        walkingWeight = Mathf.MoveTowards(walkingWeight, target, dt / .22f);

        // Move through the walking cycle according to distance travelled.
        // Repeat wraps phase back to 0 after 1. Breathing time keeps increasing
        // even when the character stands still.
        phase = Mathf.Repeat(phase + distance / strideLength, 1f);
        // Keep breathing time moving forward even while the character stands
        // still.
        idleTime += dt;

        // Use clip poses only if both clips and the model were compatible
        // during setup.
        if (useClips)
        {
            // Remember the model object's placement before applying clips.
            // Choose a standing moment using elapsed time and a walking
            // moment using travel progress. % repeats the standing clip.
            Vector3 p = rig.animator.transform.localPosition;
            // Save the model object's turn so applying clips cannot change its
            // placement.
            Quaternion r = rig.animator.transform.localRotation;
            // Choose a standing-clip moment using elapsed time; % loops back at
            // the end.
            idlePlayable.SetTime(idleTime % idleClip.length);
            // Choose a walking-clip moment using the distance-based
            // walking-cycle progress.
            walkPlayable.SetTime(phase * walkClip.length);

            // Keep some relaxed standing mixed in to soften the walking clip.
            // The two amounts add to 1. For example, a walking amount of 0.65
            // leaves 0.35 of the standing pose.
            float clipWeight = walkingWeight * ClipWalkWeight;
            // Give standing the amount left after subtracting the walking
            // amount from one.
            mixer.SetInputWeight(0, 1f - clipWeight);
            // Give walking its calculated amount, so the two clip amounts add
            // up to one.
            mixer.SetInputWeight(1, clipWeight);
            // Apply this mix at the chosen clip times without advancing another
            // clock.
            graph.Evaluate(0f);

            // Keep any clip movement from shifting or turning the whole model.
            // IntroSequence still decides where the character goes.
            rig.animator.transform.SetLocalPositionAndRotation(p, r);
            // Finish this update because the clips have already supplied the
            // body pose.
            return;
        }

        // Without suitable clips, calculate the body movements below.
        // Begin from the saved pose, not last frame's changed pose.
        RestoreBindPose();

        // Find up, sideways and forward for this body. The line between the
        // thighs gives sideways. Cross gives a direction at right angles to
        // sideways and up: forward. normalized keeps a direction but makes its
        // length 1, so later numbers control how far the movement goes.
        Vector3 up = Vector3.up;
        // Find which direction goes from the character's left side to its right
        // side.
        Vector3 side = Vector3.ProjectOnPlane(
            // Use the thigh positions, remove height difference, then set the
            // direction length to one.
            rig.rightThigh.position - rig.leftThigh.position, up).normalized;
        // Find forward as the direction at right angles to sideways and room
        // up.
        Vector3 forward = Vector3.Cross(side, up).normalized;

        // Make gentle repeated breathing and a small standing weight shift.
        // Sin produces a smooth repeating value between -1 and 1. Maya uses
        // a slightly slower breathing rate. Walking reduces the idle shift.
        float breath = Mathf.Sin(
            // Use a slower breathing rate for Maya and the other rate for
            // Mika/Ren.
            idleTime * (style == StandingStyle.Maya ? 1.35f : 1.5f));
        // Add a slow standing weight shift, reducing it as walking becomes
        // stronger.
        float restShift = Mathf.Sin(idleTime * .48f) * (1f - walkingWeight);

        // Sin/Cos use angle units called radians. 2 * PI is one full turn.
        // Convert the 0-to-1 walking cycle into that range for repeated steps.
        float cycle = phase * Mathf.PI * 2f;

        // Keep the hips slightly lowered, with gentle breathing and walking
        // movement. In the up direction: start a little lower, add breathing,
        // lower more when walking, then add a tiny rise/fall twice per cycle.
        // In the side direction: add a small step shift and idle weight shift.
        rig.hips.position += up * (
            // Lower the hips slightly, add breathing, and lower them more while
            // walking.
            -.005f + breath * .0015f - walkingWeight * .014f +
            // Add a very small up/down movement that repeats twice in one
            // walking cycle.
            Mathf.Cos(cycle * 2f) * walkingWeight * .003f) +
            // Start the sideways movement using the current step and walking
            // amount.
            side * (Mathf.Sin(cycle) * walkingWeight * .004f +
                // Add the standing weight shift along that same sideways
                // direction.
                restShift * .004f);

        // Let the torso tilt a little with breathing and turn gently with
        // steps. AngleAxis makes a rotation around the given direction.
        // Multiplying rotations combines these turns with the saved pose.
        rig.spine.rotation = Quaternion.AngleAxis(breath * .4f, side) *
            // Add a gentle torso turn around the character's up direction.
            Quaternion.AngleAxis(
                // Combine step-related turning with the slower standing weight
                // shift.
                Mathf.Sin(cycle) * walkingWeight * .8f + restShift * .35f,
                // Turn around up and combine it with the body's saved spine
                // rotation.
                up) * rig.spine.rotation;

        // Alternate the legs by putting the right leg half a cycle ahead.
        // Give each leg its own saved foot position and floor-contact record.
        AnimateLeg(
            // Use the left thigh, shin and foot as this leg's connected body
            // parts.
            rig.leftThigh, rig.leftShin, rig.leftFoot,
            // Use its saved resting pose and current step, and update its
            // left-floor-contact record.
            leftFootRest, leftFootRotation, phase, up, forward, ref leftPlant);
        // Calculate the right leg separately so it can alternate with the left.
        AnimateLeg(
            // Use the right leg's thigh, shin and foot rather than the left
            // references.
            rig.rightThigh, rig.rightShin, rig.rightFoot,
            // Use the right foot's own saved resting position and turn.
            rightFootRest, rightFootRotation,
            // Put the right step half a cycle ahead and update its own
            // floor-contact record.
            Mathf.Repeat(phase + .5f, 1f), up, forward, ref rightPlant);

        // Swing the arms in opposite directions to balance the steps.
        AnimateArm(
            // Use the left upper arm, forearm and hand for this arm movement.
            rig.leftUpperArm, rig.leftForearm, rig.leftHand,
            // Point outward to the left and swing this arm opposite the right
            // arm.
            -side, up, forward, -Mathf.Cos(cycle));
        // Calculate the right arm with its own body-part references and
        // opposite swing.
        AnimateArm(
            // Use the right upper arm, forearm and hand from this character.
            rig.rightUpperArm, rig.rightForearm, rig.rightHand,
            // Point outward to the right and use the opposite part of the swing
            // cycle.
            side, up, forward, Mathf.Cos(cycle));
    }

    // Make one leg step, then put its foot back down.
    // thigh, shin and foot are that leg's three connected body parts.
    // rest and rotation are the saved foot position/turn relative to the
    // hips' parent. step is progress from 0 to 1 through its walking cycle.
    // up/forward give the body directions. plant remembers floor contact;
    // ref lets this method update that same record for the next frame.
    private void AnimateLeg(
        // Receive this leg's body parts and its saved resting foot position.
        Transform thigh, Transform shin, Transform foot, Vector3 rest,
        // Receive its saved foot turn, step progress and the body's up/forward
        // directions.
        Quaternion rotation, float step, Vector3 up, Vector3 forward,
        // Update this foot's shared contact record so the next frame remembers
        // it.
        ref FootPlant plant)
    {
        // During the first half, keep the foot down while the body passes it.
        // z moves from ahead of the body to behind it; lift stays zero.
        float z, lift;
        // Keep the foot down during the first half of its walking cycle.
        if (step < .5f)
        {
            // Move the foot from ahead to behind the body as the body passes
            // over it.
            z = Mathf.Lerp(.25f, -.25f, step * 2f);
            // Use no upward lift while this foot is in the ground half of the
            // step.
            lift = 0f;
        }
        // Use the second half of the cycle to lift the foot and return it
        // forward.
        else
        {
            // During the second half, lift the foot and bring it forward.
            // t changes this half-cycle into a 0-to-1 amount. SmoothStep makes
            // forward movement less abrupt; Sin lifts then lowers it by 4.5 cm.
            float t = (step - .5f) * 2f;
            // Bring the foot from behind to ahead with a slower start and
            // finish.
            z = Mathf.Lerp(-.25f, .25f, Mathf.SmoothStep(0, 1, t));
            // Raise then lower the returning foot, reaching at most 4.5
            // centimetres of lift.
            lift = Mathf.Sin(t * Mathf.PI) * .045f;
        }

        // Start at the resting foot position in the room. Add the forward and
        // upward step movements. walkingWeight reduces both to zero on stop.
        Vector3 target = poseFrame.TransformPoint(rest) +
            // Add the step's forward/lift offsets, reducing them to zero as
            // walking stops.
            walkingWeight * (forward * (z * strideLength) + up * lift);

        // Once walking is fully blended in, hold a down foot at one room
        // position to reduce sliding. Save that position at first contact.
        // When this phase ends, release it so the foot can step again.
        bool stance = step < .5f && walkingWeight >= .99f;
        // Remember a floor contact only when this foot has just entered its
        // held-down phase.
        if (stance && !plant.planted)
            // Save the intended contact position in the room for this part of
            // the step.
            plant.position = target;
        // Use that saved room position while the foot should remain down.
        if (stance)
            // Keep aiming at the saved contact instead of sliding the foot with
            // the body.
            target = plant.position;
        // Remember whether this foot is held down so the next frame can detect
        // a new contact.
        plant.planted = stance;

        // Bend the knee toward the target, then keep the foot facing in its
        // saved direction relative to the hips' parent.
        SolveLeg(thigh, shin, foot, target, forward);
        // Restore the foot's saved turn relative to the hips' parent after
        // bending the knee.
        foot.rotation = poseFrame.rotation * rotation;
    }

    // Let one arm hang beside the body with a gentle walking swing.
    // upper, forearm and hand are the character's arm bones, not controllers.
    // outward points away from the ribs; up/forward describe body directions.
    // swing goes between -1 and 1, opposite to the other arm's swing.
    private void AnimateArm(
        // Receive this character's arm bones and the direction away from its
        // ribs.
        Transform upper, Transform forearm, Transform hand, Vector3 outward,
        // Receive the body's up/forward directions and this arm's swing amount.
        Vector3 up, Vector3 forward, float swing)
    {
        // Use the existing character-specific swing amount. Reduce it while
        // stopping, and add a very small breathing movement.
        float armSwing = style == StandingStyle.Mika ? .16f : .13f;
        // Reduce the arm swing as the character gradually returns to standing.
        float a = swing * walkingWeight;
        // Add a tiny forward/back breathing movement to keep the arm from
        // looking frozen.
        float breath = Mathf.Sin(idleTime * 1.5f) * .004f;

        // Aim the upper arm mostly down (-up), slightly away from the ribs,
        // and forward/back with the step. FromToRotation finds the turn from
        // the current upper-arm direction to that wanted direction.
        Vector3 upperDirection = (
            // Point the upper arm mostly down and slightly away from the body.
            -up + outward * .16f +
            // Add forward/back movement and set the resulting direction length
            // to one.
            forward * (-.055f + a * armSwing + breath)).normalized;
        // Find the turn needed to aim the upper arm in the calculated
        // direction.
        upper.rotation = Quaternion.FromToRotation(
            // Turn from its current upper-arm direction, keeping the rest of
            // its rotation.
            forearm.position - upper.position, upperDirection) * upper.rotation;

        // Aim the forearm a little farther forward to keep the elbow bent.
        // Use the hand position to find the current forearm direction, then
        // turn the forearm toward its wanted direction without stretching it.
        Vector3 lowerDirection = (
            // Point the forearm mostly down and slightly outward from the body.
            -up + outward * .09f +
            // Aim farther forward to keep the elbow bent, including walking and
            // breathing.
            forward * (.29f + a * armSwing * .85f + breath)).normalized;
        // Find the turn needed to point the forearm in its calculated
        // direction.
        forearm.rotation = Quaternion.FromToRotation(
            // Use the elbow-to-hand direction as the current forearm direction.
            hand.position - forearm.position, lowerDirection) *
            // Combine the calculated turn with the forearm's existing rotation.
            forearm.rotation;
    }

    // Bend the knee so the foot reaches a target without changing leg length.
    // a is the thigh, b the shin, c the foot. goal is the wanted foot position
    // in the room. bend is the direction the knee should point toward.
    private static void SolveLeg(
        // Receive thigh a, shin b, foot c, the target foot position and
        // preferred knee direction.
        Transform a, Transform b, Transform c, Vector3 goal, Vector3 bend)
    {
        // Measure the two leg sections. If either has almost no length, leave
        // the leg alone rather than divide by a number close to zero later.
        Vector3 start = a.position;
        // Measure the upper leg from its hip joint to its knee joint; call this
        // l1.
        float l1 = Vector3.Distance(start, b.position),
            // Measure the lower leg from knee to foot; call this l2.
            l2 = Vector3.Distance(b.position, c.position);
        // Check for either leg section being too short to use safely in these
        // calculations.
        if (l1 < .00001f || l2 < .00001f)
            // Leave this leg alone rather than risk dividing by a length close
            // to zero.
            return;

        // Keep the target within the leg's reach. delta points from hip to
        // target. Clamp limits its distance using the two section lengths.
        Vector3 delta = goal - start;
        // Choose a target distance that the two leg sections can actually
        // reach.
        float distance = Mathf.Clamp(
            // Keep it just inside the shortest and longest distances allowed by
            // those lengths.
            delta.magnitude, Mathf.Abs(l1-l2) + .0001f, l1+l2-.0001f);
        // Keep the hip-to-target direction but make its length one for later
        // positioning.
        Vector3 axis = delta.normalized;

        // The hip, knee and foot form a triangle. First find where the knee
        // lies along the hip-to-foot line (along). Then find how far it bends
        // away from that line, toward bend. plane stores this side direction.
        // Sqrt finds the triangle height; Max avoids a negative square root
        // from small rounding errors.
        Vector3 plane = Vector3.ProjectOnPlane(bend, axis).normalized;
        // Use the triangle's three lengths to find how far along the
        // hip-to-foot line the knee lies.
        float along = (l1*l1-l2*l2+distance*distance)/(2f*distance);
        // Start the knee position at the hip, then move along that line by the
        // calculated amount.
        Vector3 knee = start + axis*along +
            // Add the triangle's sideways height to place the bent knee away
            // from that line.
            plane*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));

        // Turn the thigh toward the calculated knee, then turn the shin
        // toward the reachable foot position. Only rotations change here.
        a.rotation = Quaternion.FromToRotation(
            // Turn the thigh from its current direction toward the calculated
            // knee position.
            b.position-start, knee-start)*a.rotation;
        // Next turn the shin so its end approaches the reachable foot position.
        b.rotation = Quaternion.FromToRotation(
            // Use the updated knee-to-foot direction and combine the turn with
            // its existing rotation.
            c.position-b.position, start+axis*distance-b.position)*b.rotation;
    }
}
