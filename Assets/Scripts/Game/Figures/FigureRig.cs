using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>How much of a figure is drawn (GL615), by its distance from the camera (<see cref="FigureLod"/>).</summary>
    public enum FigureDetail
    {
        /// <summary>Every part, casting shadows.</summary>
        Full,

        /// <summary>Every part, no shadows.</summary>
        NoShadow,

        /// <summary>One mesh of the figure standing: no moving limbs, no shadow.</summary>
        Far,
    }

    /// <summary>
    /// A low-poly person in the scene (GL608; replaces the blocky FigureRig of GL404): hangs <see cref="FigureMeshes"/>'
    /// parts on <see cref="FigureSkeleton"/>'s joints and turns them each frame by <see cref="FigurePoses"/> for the
    /// <see cref="Pose"/> being shown. Every person in the game is one of these, dressed by a <see cref="FigurePreset"/>:
    /// shopkeepers, the company's workers and crew lead, the adventurer, the thief, the carter, the menu's miner and the
    /// player's shadow. The origin is between the feet, facing +z; <see cref="Hand"/> is the right palm, where props go.
    /// </summary>
    public class FigureRig : MonoBehaviour
    {
        /// <summary>The beat at which the mining swing lands.</summary>
        public const float MineImpact = FigurePoses.MineImpact;

        private Transform body;
        private Transform hips;
        private Transform torso;
        private Transform head;
        private Transform armLeft;
        private Transform armRight;
        private Transform forearmLeft;
        private Transform forearmRight;
        private Transform legLeft;
        private Transform legRight;
        private Transform shinLeft;
        private Transform shinRight;
        private Transform hand;
        private Material material;
        private GameObject prop;
        private bool propAlways;
        private float phase;
        private float step;
        private FigureDetail detail;
        private bool shadowOnly;
        private GameObject far;
        private Mesh farMesh;

        /// <summary>
        /// How much of the figure is drawn (GL615): a crowd's cost is its renderers (11 a figure, each drawn again for
        /// depth and for every shadow cascade), so a figure further off drops its shadow, and a far one is a single mesh
        /// of itself standing, which no longer moves its limbs. Set by <see cref="FigureLod"/> from the camera's distance.
        /// </summary>
        public FigureDetail Detail
        {
            get => detail;
            set
            {
                if (detail != value)
                {
                    detail = value;
                    ApplyDetail();
                }
            }
        }

        /// <summary>The pose being shown.</summary>
        public FigureAction Pose { get; set; }

        /// <summary>How fast the figure is moving while it walks, metres a second: sets the pace and length of its steps.</summary>
        public float Speed { get; set; } = FigurePoses.WalkSpeed;

        /// <summary>The look the figure was built with.</summary>
        public FigureLook Look { get; private set; }

        /// <summary>The right palm: held things are parented here, -y along the forearm and +z out of the fist.</summary>
        public Transform Hand => hand;

        /// <summary>Where the mining swing is, 0 to 1: the pick lands on the face as it passes <see cref="MineImpact"/> (GL552).</summary>
        public float MineBeat => FigurePoses.MineBeat(Time.time + phase);

        /// <summary>Builds the keeper of a kind of shop, in a material for vertex-coloured models.</summary>
        public void Build(ShopKind kind, Material material, int seed)
        {
            Build(FigureLooks.Of(kind), material, seed);
        }

        /// <summary>Builds a preset as one person wears it: the seed picks the height, build and colours.</summary>
        public void Build(FigurePreset preset, Material material, int seed)
        {
            Build(FigureLooks.Varied(preset, seed), material, seed);
        }

        /// <summary>Builds a look as given; the seed only sets the figure's own timing, so a crowd doesn't move as one.</summary>
        public void Build(FigureLook look, Material material, int seed)
        {
            if (body != null)
            {
                Destroy(body.gameObject);
            }
            Look = look;
            this.material = material;
            phase = (float)new System.Random(seed).NextDouble() * 10f;
            FigureParts parts = FigureMeshes.For(look);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * look.Height;
            hips = Part("Hips", body, new Vector3(0f, FigureSkeleton.HipHeight, 0f), parts.Pelvis);
            legLeft = Part("Leg L", hips, FigureSkeleton.Hip(look.Build, -1), parts.Thigh);
            legRight = Part("Leg R", hips, FigureSkeleton.Hip(look.Build, 1), parts.Thigh);
            shinLeft = Part("Shin L", legLeft, new Vector3(0f, -FigureSkeleton.ThighLength, 0f), parts.Shin);
            shinRight = Part("Shin R", legRight, new Vector3(0f, -FigureSkeleton.ThighLength, 0f), parts.Shin);
            torso = Part("Torso", hips, Vector3.zero, parts.Torso);
            head = Part("Head", torso, new Vector3(0f, FigureSkeleton.NeckHeight, 0f), parts.Head);
            armLeft = Part("Arm L", torso, FigureSkeleton.Shoulder(look.Build, -1), parts.UpperArm);
            armRight = Part("Arm R", torso, FigureSkeleton.Shoulder(look.Build, 1), parts.UpperArm);
            forearmLeft = Part("Forearm L", armLeft, new Vector3(0f, -FigureSkeleton.UpperArmLength, 0f), parts.Forearm);
            forearmRight = Part("Forearm R", armRight, new Vector3(0f, -FigureSkeleton.UpperArmLength, 0f), parts.Forearm);
            hand = Part("Hand", forearmRight, FigureSkeleton.HandSocket, null);
            Hold(FigureProps.For(look.Prop));
            propAlways = false;
            // Something to look at for E: the whole figure, turning with it when it lies down.
            var capsule = body.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            Show(Pose, phase);
            ClearFar();
            ApplyDetail();
            if (GetComponent<FigureLod>() == null)
            {
                gameObject.AddComponent<FigureLod>();
            }
        }

        private void OnDestroy()
        {
            ClearFar();
        }

        private void ClearFar()
        {
            if (far != null)
            {
                Destroy(far);
                far = null;
            }
            if (farMesh != null)
            {
                Destroy(farMesh);
                farMesh = null;
            }
        }

        // Full: every part, with shadows. NoShadow: every part. Far: one mesh of the figure standing, no shadow.
        private void ApplyDetail()
        {
            if (body == null || shadowOnly)
            {
                return;
            }
            bool isFar = detail == FigureDetail.Far;
            if (isFar && far == null)
            {
                BuildFar();
            }
            if (far != null)
            {
                far.SetActive(isFar);
            }
            var shadows = detail == FigureDetail.Full ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (MeshRenderer renderer in body.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (far != null && renderer.gameObject == far)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    continue;
                }
                renderer.enabled = !isFar;
                renderer.shadowCastingMode = shadows;
            }
        }

        // The whole figure as one mesh, standing, in the body's own frame (so it lies down and turns with it).
        private void BuildFar()
        {
            Show(FigureAction.Stand, 0f);
            var pieces = new List<CombineInstance>();
            foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && (prop == null || filter.gameObject != prop))
                {
                    pieces.Add(new CombineInstance { mesh = filter.sharedMesh, transform = body.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            farMesh = new Mesh { name = "Figure far" };
            farMesh.CombineMeshes(pieces.ToArray(), true, true);
            far = new GameObject("Far");
            far.transform.SetParent(body, false);
            far.AddComponent<MeshFilter>().sharedMesh = farMesh;
            far.AddComponent<MeshRenderer>().sharedMaterial = material;
            Show(Pose, Time.time + phase);
        }

        /// <summary>Puts a thing in the right hand in place of the look's prop (null empties the hand).</summary>
        public void Hold(Mesh mesh)
        {
            propAlways = true;
            if (prop != null)
            {
                Destroy(prop);
                prop = null;
            }
            if (mesh != null && hand != null)
            {
                prop = Part("Prop", hand, Vector3.zero, mesh).gameObject;
            }
        }

        /// <summary>Makes the figure cast its shadow without being drawn or looked at: the player's own body (GL608).</summary>
        public void CastShadowOnly()
        {
            shadowOnly = true;
            ClearFar();
            FigureLod lod = GetComponent<FigureLod>();
            if (lod != null)
            {
                Destroy(lod);
            }
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }

        /// <summary>
        /// Lays the figure on its back along <paramref name="along"/> (head that way), its middle over <paramref name="centre"/>
        /// (world positions), or stands it back up when <paramref name="lie"/> is false.
        /// </summary>
        public void LieAlong(bool lie, Vector3 centre, Vector3 along)
        {
            if (body == null)
            {
                return;
            }
            if (!lie)
            {
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                return;
            }
            Vector3 up = transform.parent != null ? transform.parent.up : Vector3.up;
            body.rotation = Quaternion.LookRotation(up, along);
            body.position = centre - along.normalized * (0.88f * Look.Height) + up * 0.24f;
        }

        /// <summary>Shows an action as it is at a time, at once: what <c>Update</c> does each frame, and what tools call to pose a figure.</summary>
        public void Show(FigureAction action, float time)
        {
            if (body == null)
            {
                return;
            }
            float stride = Mathf.Clamp(Speed / FigurePoses.WalkSpeed, 0.4f, 1.4f);
            FigurePose pose = FigurePoses.At(action, time, step, stride);
            hips.localPosition = new Vector3(0f, pose.HipsHeight, 0f);
            torso.localRotation = Quaternion.Euler(pose.Torso);
            torso.localScale = new Vector3(1f, 1f, pose.Breath);
            head.localRotation = Quaternion.Euler(pose.Head);
            armLeft.localRotation = Quaternion.Euler(pose.ArmLeft);
            armRight.localRotation = Quaternion.Euler(pose.ArmRight);
            forearmLeft.localRotation = Quaternion.Euler(-pose.ElbowLeft, 0f, 0f);
            forearmRight.localRotation = Quaternion.Euler(-pose.ElbowRight, 0f, 0f);
            legLeft.localRotation = Quaternion.Euler(pose.LegLeft);
            legRight.localRotation = Quaternion.Euler(pose.LegRight);
            shinLeft.localRotation = Quaternion.Euler(pose.KneeLeft, 0f, 0f);
            shinRight.localRotation = Quaternion.Euler(pose.KneeRight, 0f, 0f);
            if (prop != null)
            {
                prop.SetActive(propAlways ? action != FigureAction.Lie : FigurePoses.PropShown(action, Look.Prop));
            }
        }

        private void Update()
        {
            if (detail == FigureDetail.Far && !shadowOnly)
            {
                return;
            }
            if (Pose == FigureAction.Walk)
            {
                step += Time.deltaTime * FigurePoses.StepRate * Mathf.Clamp(Speed / FigurePoses.WalkSpeed, 0.5f, 2.5f);
            }
            Show(Pose, Time.time + phase);
        }

        private Transform Part(string name, Transform parent, Vector3 at, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return go.transform;
        }
    }
}
