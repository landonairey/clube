using System.Collections.Generic;
using Clube.Core;
using Unity.Profiling;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A felled limb on its way down (GL344): the limb's mesh tips over about the cut, 90° towards the fall direction,
    /// speeding up as it goes, while the cut drops to the ground under it. It passes through anything in the way (other
    /// trees included). Just before it lies flat a big plume of dust goes up, with leaves thrown out of it (GL469), and the
    /// limb becomes voxels again
    /// (<see cref="TreeFelling.Land"/>); the mesh stays a moment longer, under the dust, while the chunks it landed in
    /// rebuild, then goes.
    /// </summary>
    public class FallingLimb : MonoBehaviour
    {
        // Seconds the mesh stays after the limb is voxels again, hiding the frames before the landed chunks are drawn.
        private const float Overlap = 0.7f;

        // Seconds before it lands that the dust starts, so the cloud is already thick when the mesh turns to voxels (GL469).
        private const float DustLead = 0.25f;

        private WorldView worldView;
        private FelledLimb limb;
        private TreeMaterials trees;
        private Vector3Int direction;
        private Material dustMaterial;
        private Color dustColour;
        private Material burstMaterial;
        private bool dustStarted;
        private System.Action<List<Vector3Int>, List<Vector3Int>> onLandedCallback;
        private Transform body;
        private Mesh mesh;
        private Vector3 pivotStart;
        private float pivotEnd;
        // GL724: samples it lands further out, clear of the stump, and that distance in the world view's space.
        private int outward;
        private Vector3 slide;
        private float duration;
        private float elapsed;
        private bool landed;
        private Quaternion fullTurn;
        // The limb's voxels still being taken out (a slice a frame); it falls once they're gone.
        private LimbRemoval removal;
        private float removalBudget;
        // Its voxels being put back down after it hit the ground, and where they went.
        private IEnumerator<int> landing;
        private readonly List<Vector3Int> landedWood = new List<Vector3Int>();
        private readonly List<Vector3Int> landedLeaves = new List<Vector3Int>();

        // A falling limb's main-thread time (taking out its voxels, landing) in the profiler, per frame.
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("FallingLimb.Update");

        /// <summary>Starts the fall. <paramref name="origin"/> is where the mesh's corner sits, in the world view's space.</summary>
        public void Begin(
            WorldView view, FelledLimb felled, TreeMaterials treeMaterials, Mesh limbMesh, Vector3 origin, Material[] materials,
            Vector3Int fallDirection, Material dust, Color dustTint, Material burst, System.Action<List<Vector3Int>, List<Vector3Int>> onLanded = null,
            LimbRemoval removing = null, float removalMilliseconds = 3f)
        {
            removal = removing;
            removalBudget = removalMilliseconds;
            worldView = view;
            limb = felled;
            trees = treeMaterials;
            direction = fallDirection;
            dustMaterial = dust;
            dustColour = dustTint;
            burstMaterial = burst;
            onLandedCallback = onLanded;
            mesh = limbMesh;

            WorldGrid grid = view.World.Grid;
            pivotStart = grid.SampleToWorld(limb.Pivot);
            transform.localPosition = pivotStart;
            transform.localRotation = Quaternion.identity;

            body = new GameObject("Mesh").transform;
            body.SetParent(transform, false);
            body.localPosition = origin - pivotStart;
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = body.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;

            if (removal == null || removal.Step(removalBudget))
            {
                removal = null;
                PlanFall();
            }
        }

        // Where and how long it falls. Reads the ground round the stump, so only once the limb is out of the world.
        private void PlanFall()
        {
            WorldGrid grid = worldView.World.Grid;
            WorldView view = worldView;

            // Tipped over: what pointed up points along the fall direction.
            Vector3 along = direction;
            fullTurn = Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, along));
            // It lands a voxel or two clear of the stump's edge (GL724), sliding out as it tips.
            outward = TreeFelling.ClearOfStump(view.World, limb, direction, trees, view.Config.IsoLevel, TreeFelling.GapFor(limb.Pivot));
            slide = (Vector3)direction * (outward * grid.VoxelSize);
            // The cut comes down to the ground under where the limb will lie, half its thickness up.
            pivotEnd = GroundUnder(pivotStart + slide) + grid.VoxelSize;
            float drop = Mathf.Max(0f, pivotStart.y - pivotEnd);
            // As long as falling the drop would take, and never too quick to see.
            duration = Mathf.Max(1.4f, Mathf.Sqrt(2f * drop / 9.81f) * 1.3f);
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                Tick();
            }
        }

        private void Tick()
        {
            if (worldView == null || limb == null)
            {
                Destroy(gameObject);
                return;
            }
            if (removal != null)
            {
                // Still standing where the tree was, over the voxels being taken out beneath it.
                if (!removal.Step(removalBudget))
                {
                    return;
                }
                removal = null;
                PlanFall();
            }
            elapsed += Time.deltaTime;
            if (landed)
            {
                if (landing != null)
                {
                    StepLanding(removalBudget);
                    return;
                }
                if (elapsed > duration + Overlap)
                {
                    Destroy(gameObject);
                }
                return;
            }
            // Tipping speeds up like a falling tree: slow off the stump, fast at the end.
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            transform.localRotation = Quaternion.Slerp(Quaternion.identity, fullTurn, eased);
            transform.localPosition = new Vector3(pivotStart.x + slide.x * eased, Mathf.Lerp(pivotStart.y, pivotEnd, eased), pivotStart.z + slide.z * eased);
            if (!dustStarted && elapsed >= duration - DustLead)
            {
                Dust();
            }
            if (t >= 1f)
            {
                Land();
            }
        }

        // Puts it back as voxels, a slice a frame under the dust (a big crown's landing once stalled a frame by 80 ms); the
        // mesh stays until it's all down.
        private void Land()
        {
            landed = true;
            landedWood.Clear();
            landedLeaves.Clear();
            landing = TreeFelling.LandSteps(
                worldView.World, limb, direction, trees, worldView.Config.IsoLevel, landedWood: landedWood, landedLeaves: landedLeaves,
                outward: outward).GetEnumerator();
            if (!dustStarted)
            {
                Dust();
            }
            Burst();
            StepLanding(removalBudget);
        }

        private void StepLanding(double milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.Elapsed.TotalMilliseconds < milliseconds)
            {
                if (!landing.MoveNext())
                {
                    landing = null;
                    // The overlap counts from here: the landed chunks still have to be drawn.
                    elapsed = Mathf.Min(elapsed, duration);
                    onLandedCallback?.Invoke(landedWood, landedLeaves);
                    return;
                }
            }
        }

        // A big cloud along where the limb will lie: soft puffs that billow out and up and fade. Thick from the start and
        // wider than the limb, so it hides the mesh becoming voxels (GL469).
        private void Dust()
        {
            dustStarted = true;
            if (dustMaterial == null)
            {
                return;
            }
            WorldGrid grid = worldView.World.Grid;
            Vector3 size = (Vector3)(limb.Max - limb.Min) * grid.VoxelSize;
            float length = Mathf.Max(size.y, 1f);
            float width = Mathf.Max(Mathf.Max(size.x, size.z), 1f);
            Vector3 along = direction;
            Vector3 centre = LandedCut() + along * length * 0.5f;
            // A big tree raises a big cloud, a twig a small one.
            float scale = Mathf.Clamp(size.magnitude / 5f, 0.5f, 1.8f);

            var dust = new GameObject("Felling dust");
            dust.transform.SetParent(worldView.transform, false);
            dust.transform.localPosition = centre;
            dust.transform.localRotation = Quaternion.LookRotation(along);
            var particles = dust.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f * scale, 3.2f * scale);
            main.startColor = dustColour;
            main.gravityModifier = -0.04f;
            main.maxParticles = 400;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(length * 12f, 30f, 320f)) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width + scale, 1.5f * scale, length + scale);
            ParticleSystem.SizeOverLifetimeModule growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.VelocityOverLifetimeModule rise = particles.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rise.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            rise.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMaterial;
            particles.Play();
        }

        // Where the cut lies once the limb is down, in the world view's space.
        private Vector3 LandedCut()
        {
            return new Vector3(pivotStart.x + slide.x, pivotEnd, pivotStart.z + slide.z);
        }

        // Leaves flung up out of the dust, and splinters for the wood that shattered past the cap (GL468, GL469).
        private void Burst()
        {
            int leafSamples = 0;
            foreach (FelledSample sample in limb.Solid)
            {
                leafSamples += sample.IsWood ? 0 : 1;
            }
            WorldGrid grid = worldView.World.Grid;
            Vector3 size = (Vector3)(limb.Max - limb.Min) * grid.VoxelSize;
            float length = Mathf.Max(size.y, 1f);
            float width = Mathf.Max(Mathf.Max(size.x, size.z), 1f);
            Vector3 along = direction;
            int leaves = leafSamples == 0 ? 0 : Mathf.Clamp(leafSamples / 10, 12, 120);
            int splinters = limb.WoodLost == 0 ? 0 : Mathf.Clamp(limb.WoodLost / 2, 6, 60);
            FellingBurst.Spawn(worldView.transform, LandedCut() + along * length * 0.5f, along, length, width, leaves, splinters, burstMaterial);
        }

        // The ground (solid, not tree) under a point in the world view's space, searched down from it.
        private float GroundUnder(Vector3 point)
        {
            World world = worldView.World;
            WorldGrid grid = world.Grid;
            Vector3Int sample = grid.WorldToNearestSample(point);
            float iso = worldView.Config.IsoLevel;
            for (int y = sample.y; y > sample.y - 400; y--)
            {
                var at = new Vector3Int(sample.x, y, sample.z);
                float? density = world.GetDensity(at);
                if (!density.HasValue)
                {
                    break;
                }
                if (density.Value >= iso && !trees.IsTree(world.GetMaterial(at) ?? 0))
                {
                    return grid.SampleToWorld(at).y;
                }
            }
            return point.y;
        }

        private void OnDestroy()
        {
            // Never leave half a tree standing, or half of one lying (unless the world itself is going).
            if (worldView != null && worldView.World != null)
            {
                removal?.Finish();
                if (landing != null)
                {
                    StepLanding(double.PositiveInfinity);
                }
            }
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }
}
