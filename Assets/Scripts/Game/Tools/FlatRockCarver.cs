using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Turns a surface rock chiselled flat on top into the Flat Rock worktop (GL88): after each chisel
    /// hit (<see cref="PlayerToolUser.Chiselled"/>) it looks around the hit for a level patch of square
    /// rock open to the sky, at least 3 × 3 samples (<see cref="FlatTop"/>), and gives that patch the
    /// Flat Rock material, which <see cref="StumpWorktops"/> opens as a station.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class FlatRockCarver : MonoBehaviour
    {
        // How far around a hit, in samples, a top is looked for.
        private const int Margin = 4;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("Materials a Flat Rock can be cut into (the surface rocks, stone).")]
        [SerializeField]
        private List<VoxelMaterial> hosts = new List<VoxelMaterial>();

        [Tooltip("What the top becomes: a worktop material.")]
        [SerializeField]
        private VoxelMaterial flatRock;

        private readonly List<Vector3Int> top = new List<Vector3Int>();
        private PlayerToolUser tools;
        private string notice;
        private float noticeUntil;

        /// <summary>Raised when a Flat Rock is made, with its top samples.</summary>
        public event System.Action<IReadOnlyList<Vector3Int>> Made;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            tools.Chiselled += OnChiselled;
        }

        private void OnDisable()
        {
            tools.Chiselled -= OnChiselled;
        }

        private void OnChiselled(VoxelReach reach, ChiselResult result)
        {
            if (worldView == null || flatRock == null || result.Squared == 0)
            {
                return;
            }
            World world = worldView.World;
            if (!FlatTop.Find(world, reach.Box.MinSample, reach.Box.MaxSample, Margin, worldView.Config.IsoLevel, IsHost, top))
            {
                return;
            }
            foreach (Vector3Int sample in top)
            {
                world.SetMaterial(sample, flatRock.Id);
            }
            notice = $"You've made a {flatRock.DisplayName}: a worktop (E to use)";
            noticeUntil = Time.time + 4f;
            Made?.Invoke(top);
        }

        private bool IsHost(byte material)
        {
            foreach (VoxelMaterial host in hosts)
            {
                if (host != null && host.Id == material)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnGUI()
        {
            if (notice == null || Time.time >= noticeUntil)
            {
                return;
            }
            var area = new Rect(Screen.width * 0.5f - 220f, 100f, 440f, 30f);
            InventoryGridGui.Panel(area);
            GUI.Label(area, notice, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
