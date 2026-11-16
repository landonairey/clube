using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a figure's body is doing (GL404-GL406, GL608), which <see cref="FigurePoses"/> turns into joint angles.</summary>
    public enum FigureAction
    {
        Stand,
        Walk,
        Sit,
        Lie,
        Hammer,
        Stoke,
        Sweep,
        Drink,
        Examine,
        Read,
        Whittle,
        Mine,
    }

    /// <summary>
    /// A figure's joints at one moment (GL608), angles in degrees about each joint's own axes: x swings a limb forward
    /// (negative) or back, an elbow bends the forearm forward and a knee the shin back.
    /// </summary>
    public struct FigurePose
    {
        public float HipsHeight;
        public Vector3 Torso;
        public float Breath;
        public Vector3 Head;
        public Vector3 ArmLeft;
        public Vector3 ArmRight;
        public float ElbowLeft;
        public float ElbowRight;
        public Vector3 LegLeft;
        public Vector3 LegRight;
        public float KneeLeft;
        public float KneeRight;
    }

    /// <summary>
    /// The figures' motion made in code (GL608; no clips): idle sway, a walk whose stride follows the speed, sitting,
    /// sleeping and the work swings. Pure: the same action, time and stride always give the same pose.
    /// </summary>
    public static class FigurePoses
    {
        /// <summary>The beat at which the mining swing lands (GL552).</summary>
        public const float MineImpact = 0.7f;

        /// <summary>The walking speed, metres a second, the walk cycle is drawn for.</summary>
        public const float WalkSpeed = 1.4f;

        /// <summary>How fast the step cycle turns at <see cref="WalkSpeed"/>, radians a second.</summary>
        public const float StepRate = 7f;

        /// <summary>Where the mining swing is at a time, 0 to 1: the pick lands as it passes <see cref="MineImpact"/>.</summary>
        public static float MineBeat(float time)
        {
            return Mathf.Repeat(time * 0.7f, 1f);
        }

        /// <summary>True when a prop is in the hand during an action: tools only for their chore, a staff or dagger always.</summary>
        public static bool PropShown(FigureAction action, FigureProp prop)
        {
            if (prop == FigureProp.None || action == FigureAction.Lie)
            {
                return false;
            }
            if (prop == FigureProp.Staff || prop == FigureProp.Dagger)
            {
                return true;
            }
            return action == FigureAction.Hammer || action == FigureAction.Mine || action == FigureAction.Sweep || action == FigureAction.Drink
                || action == FigureAction.Read || action == FigureAction.Whittle;
        }

        /// <summary>
        /// The pose for an action at <paramref name="time"/> seconds. Walking reads <paramref name="step"/>, the step
        /// cycle's angle in radians, and <paramref name="stride"/>, how long the steps are against a normal walk's.
        /// </summary>
        public static FigurePose At(FigureAction action, float time, float step = 0f, float stride = 1f)
        {
            float t = time;
            var pose = new FigurePose { HipsHeight = FigureSkeleton.HipHeight, Breath = 1f };
            switch (action)
            {
                case FigureAction.Walk:
                {
                    float swing = Mathf.Sin(step) * stride, lift = Mathf.Cos(step);
                    pose.LegLeft = new Vector3(swing * 28f - 4f, 0f, 0f);
                    pose.LegRight = new Vector3(-swing * 28f - 4f, 0f, 0f);
                    // The knee bends while its leg comes forward.
                    pose.KneeLeft = 8f + Mathf.Max(0f, -lift) * 40f * stride;
                    pose.KneeRight = 8f + Mathf.Max(0f, lift) * 40f * stride;
                    pose.ArmLeft = new Vector3(-swing * 24f, 0f, -3f);
                    pose.ArmRight = new Vector3(swing * 24f, 0f, 3f);
                    pose.ElbowLeft = 16f + Mathf.Max(0f, swing) * 14f;
                    pose.ElbowRight = 16f + Mathf.Max(0f, -swing) * 14f;
                    pose.Torso = new Vector3(3f * stride, -swing * 5f, 0f);
                    pose.Head = new Vector3(0f, swing * 4f, 0f);
                    pose.HipsHeight += Mathf.Abs(lift) * 0.035f * stride - 0.02f;
                    break;
                }
                case FigureAction.Sit:
                    Sit(ref pose);
                    pose.Head = new Vector3(0f, Mathf.Sin(t * 0.3f) * 15f, 0f);
                    break;
                case FigureAction.Lie:
                    // Asleep: still, breathing slowly.
                    pose.Breath = 1f + Mathf.Sin(t * 1.4f) * 0.04f;
                    pose.Head = new Vector3(-10f, 0f, 0f);
                    pose.ArmLeft = new Vector3(0f, 0f, -8f);
                    pose.ArmRight = new Vector3(0f, 0f, 8f);
                    break;
                case FigureAction.Hammer:
                {
                    // Up slowly, down hard on the anvil.
                    float beat = Mathf.Repeat(t * 0.9f, 1f);
                    float up = beat < 0.75f ? beat / 0.75f : 1f - (beat - 0.75f) / 0.25f;
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 40f + up * 110f, 0.4f, 0f);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 45f, 0.6f, 10f);
                    pose.Torso = new Vector3(10f + (1f - up) * 6f, 0f, 0f);
                    pose.Head = new Vector3(20f, 0f, 0f);
                    pose.KneeLeft = pose.KneeRight = 6f;
                    break;
                }
                case FigureAction.Mine:
                {
                    // Both hands on the pick: up over the shoulder, down hard into the face on the beat, a short recoil.
                    float beat = MineBeat(t);
                    const float Top = 0.55f;
                    float up = beat < Top ? Mathf.SmoothStep(0f, 1f, beat / Top)
                        : beat < MineImpact ? 1f - (beat - Top) / (MineImpact - Top)
                        : Mathf.Sin((beat - MineImpact) / (1f - MineImpact) * Mathf.PI) * 0.12f;
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 60f + up * 100f, 0.3f, -12f);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 55f + up * 90f, 0.3f, 14f);
                    pose.Torso = new Vector3(14f - up * 12f, 0f, 0f);
                    pose.Head = new Vector3(6f - up * 8f, 0f, 0f);
                    pose.LegLeft = new Vector3(-16f, 0f, 0f);
                    pose.LegRight = new Vector3(12f, 0f, 0f);
                    pose.KneeLeft = 18f - up * 6f;
                    pose.KneeRight = 10f;
                    pose.HipsHeight -= 0.02f;
                    break;
                }
                case FigureAction.Stoke:
                {
                    float push = Mathf.Sin(t * 2.2f) * 0.5f + 0.5f;
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 60f + push * 15f, 0.5f, 0f);
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 60f + push * 15f, 0.5f, 0f);
                    pose.Torso = new Vector3(12f + push * 8f, 0f, 0f);
                    break;
                }
                case FigureAction.Sweep:
                {
                    float stroke = Mathf.Sin(t * 3f), shuffle = Mathf.Sin(t * 3.5f);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 40f, 0.5f, -10f);
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 30f, 0.3f, 0f);
                    pose.Torso = new Vector3(12f, stroke * 18f, 0f);
                    pose.LegLeft = new Vector3(shuffle * 14f, 0f, 0f);
                    pose.LegRight = new Vector3(-shuffle * 14f, 0f, 0f);
                    pose.KneeLeft = 6f + Mathf.Max(0f, -shuffle) * 14f;
                    pose.KneeRight = 6f + Mathf.Max(0f, shuffle) * 14f;
                    break;
                }
                case FigureAction.Drink:
                {
                    Sit(ref pose);
                    // A long sip every few seconds, head back.
                    float cycle = Mathf.Repeat(t, 6f);
                    float sip = cycle < 1f ? cycle : cycle < 2.5f ? 1f : cycle < 3.2f ? 1f - (cycle - 2.5f) / 0.7f : 0f;
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 30f + sip * 125f, 0.6f, -sip * 20f);
                    pose.Head = new Vector3(-sip * 25f, 0f, 0f);
                    break;
                }
                case FigureAction.Examine:
                    pose.Torso = new Vector3(24f, 0f, 0f);
                    pose.Head = new Vector3(22f, Mathf.Sin(t * 0.6f) * 25f, 0f);
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 55f - Mathf.Sin(t * 1.3f) * 10f, 0.5f, 0f);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 35f, 0.5f, 0f);
                    pose.KneeLeft = pose.KneeRight = 10f;
                    break;
                case FigureAction.Read:
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 70f, 0.6f, -18f);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 70f, 0.6f, 18f);
                    pose.Head = new Vector3(28f + Mathf.Sin(t * 0.4f) * 3f, 0f, 0f);
                    break;
                case FigureAction.Whittle:
                    Sit(ref pose);
                    Raise(ref pose.ArmLeft, ref pose.ElbowLeft, 55f, 0.6f, 15f);
                    Raise(ref pose.ArmRight, ref pose.ElbowRight, 50f - Mathf.Sin(t * 5f) * 12f, 0.6f, -15f);
                    pose.Head = new Vector3(30f, 0f, 0f);
                    break;
                default:
                    // Standing: a little sway and breath, now and then a glance about.
                    pose.Torso = new Vector3(Mathf.Sin(t * 1.1f) * 1.5f, 0f, Mathf.Sin(t * 0.5f) * 1f);
                    pose.Breath = 1f + Mathf.Sin(t * 1.6f) * 0.015f;
                    pose.Head = new Vector3(0f, Mathf.Sin(t * 0.37f) * 20f, 0f);
                    pose.ArmLeft = new Vector3(2f, 0f, -5f);
                    pose.ArmRight = new Vector3(2f, 0f, 5f);
                    pose.ElbowLeft = pose.ElbowRight = 10f + Mathf.Sin(t * 1.1f) * 2f;
                    pose.KneeLeft = pose.KneeRight = 3f;
                    break;
            }
            return pose;
        }

        // Seated: hips down on the seat, thighs out in front, shins hanging, hands on the knees.
        private static void Sit(ref FigurePose pose)
        {
            pose.HipsHeight = FigureSkeleton.SeatHeight;
            pose.LegLeft = new Vector3(-85f, -6f, 0f);
            pose.LegRight = new Vector3(-85f, 6f, 0f);
            pose.KneeLeft = pose.KneeRight = 80f;
            pose.ArmLeft = new Vector3(-30f, 0f, -6f);
            pose.ArmRight = new Vector3(-30f, 0f, 6f);
            pose.ElbowLeft = pose.ElbowRight = 45f;
        }

        // Lifts a hand forward by an angle from hanging, a share of it taken by the elbow, the arm leaning in or out by roll.
        private static void Raise(ref Vector3 arm, ref float elbow, float angle, float elbowShare, float roll)
        {
            arm = new Vector3(-angle * (1f - elbowShare), 0f, roll);
            elbow = angle * elbowShare;
        }
    }
}
