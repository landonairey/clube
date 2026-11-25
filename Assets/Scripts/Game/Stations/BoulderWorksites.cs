using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Boulders as worksites (prototype, backlog 2026-11-24 items 8 and 9): a boulder is claimed before it can be used, and
    /// a claimed one opens its screen (<see cref="BoulderPanel"/>) on E. The claim is in the terrain (<see cref="WorksiteClaim"/>:
    /// cracks and a worked top), so it is saved with the world; this only remembers what it has read, until a claim is made.
    /// Added beside <see cref="BoulderBashing"/>, which aims and works the boulder.
    /// </summary>
    [RequireComponent(typeof(BoulderBashing))]
    public class BoulderWorksites : MonoBehaviour
    {
        /// <summary>The material of a claimed boulder's cracks, found in the registry by name.</summary>
        public const string CrackName = "Boulder crack";

        /// <summary>The lighter stone of a claimed boulder's working surface, found in the registry by name.</summary>
        public const string WorkedName = "Worked stone";

        // Read claims, by sample: cleared when a claim is made, or when it grows large.
        private const int MaxRemembered = 50000;

        private readonly Dictionary<Vector3Int, bool> claimed = new Dictionary<Vector3Int, bool>();
        private readonly List<Vector3Int> piece = new List<Vector3Int>();
        private BoulderBashing bashing;
        private UnclaimedWorksite notice;
        private BoulderSite site;

        /// <summary>Raised when a boulder is claimed.</summary>
        public event System.Action Claimed;

        private void Awake()
        {
            bashing = GetComponent<BoulderBashing>();
            notice = new UnclaimedWorksite("boulder", ClaimAimed);
            site = new BoulderSite(this);
        }

        /// <summary>The crack and worked-stone materials, or false when the registry has none (then every boulder counts as claimed).</summary>
        public bool TryGetMarks(out VoxelMaterial crack, out VoxelMaterial worked)
        {
            MaterialRegistry registry = bashing.World != null && bashing.World.IsReady ? bashing.World.Config.Materials : null;
            crack = registry != null ? registry.Find(CrackName) : null;
            worked = registry != null ? registry.Find(WorkedName) : null;
            return crack != null && worked != null;
        }

        /// <summary>Whether the boulder holding this sample has been claimed.</summary>
        public bool IsClaimed(Vector3Int sample)
        {
            if (!TryGetMarks(out VoxelMaterial crack, out VoxelMaterial worked))
            {
                return true;
            }
            if (claimed.TryGetValue(sample, out bool known))
            {
                return known;
            }
            if (claimed.Count > MaxRemembered)
            {
                claimed.Clear();
            }
            WorldView view = bashing.World;
            WorksiteClaim.FindBoulder(view.World, sample, view.Config.IsoLevel, bashing.IsBoulderRock, piece);
            bool result = WorksiteClaim.IsClaimed(view.World, piece, crack.Id, worked.Id);
            foreach (Vector3Int part in piece)
            {
                claimed[part] = result;
            }
            claimed[sample] = result;
            return result;
        }

        /// <summary>
        /// What the crosshair is on, for <see cref="PlayerInteractor"/>: a claimed boulder to use, an unclaimed one to
        /// claim, or null (no boulder, or the Flat Rock, which is a worktop of its own).
        /// </summary>
        public IInteractable Find()
        {
            if (!bashing.TryGetAimedRock(out Vector3Int sample, out bool flatRock) || flatRock)
            {
                return null;
            }
            site.At = bashing.World.transform.TransformPoint(bashing.World.World.Grid.SampleToWorld(sample));
            return IsClaimed(sample) ? site : notice;
        }

        // Claims the boulder under the crosshair: a few thin cracks on the side facing the player and a lighter middle of its top.
        private void ClaimAimed()
        {
            if (!bashing.TryGetAimedRock(out Vector3Int sample, out bool flatRock) || flatRock || IsClaimed(sample)
                || !TryGetMarks(out VoxelMaterial crack, out VoxelMaterial worked))
            {
                return;
            }
            WorldView view = bashing.World;
            WorksiteClaim.FindBoulder(view.World, sample, view.Config.IsoLevel, bashing.IsBoulderRock, piece);
            // The cracks face the claimer (backlog 2026-11-24b item 6).
            Vector3 toward = view.transform.InverseTransformDirection(transform.position - view.transform.TransformPoint(view.World.Grid.SampleToWorld(sample)));
            WorksiteClaim.ClaimBoulder(view.World, piece, view.Config.IsoLevel, crack.Id, worked.Id, new Vector2(toward.x, toward.z));
            claimed.Clear();
            Claimed?.Invoke();
        }

        /// <summary>A claimed boulder under the crosshair: E opens its screen, and tools still mine it (backlog 2026-11-24b item 1).</summary>
        private sealed class BoulderSite : IGroundInteractable
        {
            private readonly BoulderWorksites owner;

            public BoulderSite(BoulderWorksites owner)
            {
                this.owner = owner;
            }

            /// <summary>Where the boulder is, in world space: the screen closes when the player walks away.</summary>
            public Vector3 At { get; set; }

            public string Prompt => "Use boulder";

            public void Interact(PlayerInteractor by)
            {
                BoulderPanel panel = owner.GetComponent<BoulderPanel>();
                if (panel != null)
                {
                    panel.Open(At);
                }
            }
        }
    }
}
