using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The meteor event (GL736, first pass of WE1): a glowing rock falls out of the sky along a <see cref="MeteorPlan"/>,
    /// and where it lands a crater is cut with a large brush edit and lined with lumps of magnetite, the richest iron ore
    /// the game has. <see cref="Call"/> brings one down at a place; the debug menu calls it at the player. Nothing yet
    /// calls it by chance (WE1 proper: how rare, the sound, the sky), so it is only ever called on purpose.
    /// </summary>
    public class MeteorEvent : MonoBehaviour
    {
        private const string OreName = "Magnetite";

        private WorldView worldView;
        private MeteorPlan plan;
        private float age;
        private float azimuth;
        private GameObject rock;

        /// <summary>True while a meteor is falling.</summary>
        public bool IsFalling => rock != null;

        /// <summary>Strikes counted since the world loaded (tests and the lab).</summary>
        public int Landed { get; private set; }

        /// <summary>The meteor event of the open scene, made on first use; null when the scene has no world.</summary>
        public static MeteorEvent Find()
        {
            var found = FindAnyObjectByType<MeteorEvent>();
            if (found != null)
            {
                return found;
            }
            var view = FindAnyObjectByType<WorldView>();
            if (view == null)
            {
                return null;
            }
            found = new GameObject("Meteor Event").AddComponent<MeteorEvent>();
            found.worldView = view;
            return found;
        }

        /// <summary>
        /// Brings a meteor down on a spot: the surface under (x, z), searched from a little over <paramref name="near"/>.
        /// False when no terrain is loaded there or one is already falling.
        /// </summary>
        public bool Call(Vector3 target)
        {
            if (rock != null || worldView == null || !worldView.IsReady
                || !GroundProbe.TryGround(worldView, target.x, target.z, target.y, out float ground))
            {
                return false;
            }
            azimuth = Random.value * 360f;
            plan = new MeteorPlan(new Vector3(target.x, ground, target.z), azimuth);
            age = 0f;
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Meteor";
            Destroy(rock.GetComponent<Collider>());
            rock.transform.SetParent(transform, true);
            rock.transform.localScale = Vector3.one * 3f;
            rock.transform.position = plan.Start;
            Tint(rock, new Color(1f, 0.55f, 0.15f));
            return true;
        }

        private void Update()
        {
            if (rock == null)
            {
                return;
            }
            age += Time.deltaTime;
            float t = age / MeteorPlan.FallSeconds;
            // Gathers speed as it falls.
            rock.transform.position = plan.At(t * t);
            if (t >= 1f)
            {
                Destroy(rock);
                rock = null;
                Land();
            }
        }

        private void Land()
        {
            Vector3 at = plan.Impact;
            worldView.ApplyBrush(at + Vector3.up, new BrushSettings(MeteorPlan.CraterRadius), BrushOperation.Remove);
            VoxelMaterial ore = worldView.Config.Materials != null ? worldView.Config.Materials.Find(OreName) : null;
            if (ore != null)
            {
                for (int i = 0; i < MeteorPlan.Lumps; i++)
                {
                    float radius = i == 0 ? 1.5f : 0.9f;
                    worldView.ApplyBrush(at + MeteorPlan.LumpOffset(i, azimuth), new BrushSettings(radius, material: ore.Id), BrushOperation.Add);
                }
            }
            Landed++;
        }

        private static void Tint(GameObject target, Color colour)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colour);
            block.SetColor("_EmissionColor", colour * 4f);
            target.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
