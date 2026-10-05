using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// Switches a lab between the free-fly camera and walking as the player (M7); P toggles.
    /// The player drops onto the ground under the fly camera, facing the same way, and
    /// flying again starts from the player's eyes. While walking, the lab's click tools
    /// (chunk focus, voxel selection, the lab brush) are switched off so the player's own
    /// controls own the mouse; they come back as they were.
    /// </summary>
    /// <remarks>
    /// Works with the player as a plain GameObject, so the lab doesn't depend on
    /// <c>Clube.Game</c>: the player's spawn component places it when it is switched on.
    /// </remarks>
    public class PlayerCameraToggle : MonoBehaviour
    {
        [SerializeField]
        private FreeFlyCamera flyCamera;

        [Tooltip("The player object: switched on to walk, off to fly. Its camera is a child.")]
        [SerializeField]
        private GameObject player;

        [Tooltip("Lab tools switched off while walking, e.g. ChunkFocus, VoxelSelector, TerrainBrushTool.")]
        [SerializeField]
        private Behaviour[] labTools = new Behaviour[0];

        [Tooltip("Start Play mode walking as the player instead of flying.")]
        [SerializeField]
        private bool startAsPlayer;

        private bool[] toolsWereEnabled;
        private bool isPlayer;

        /// <summary>True while walking as the player; false while flying.</summary>
        public bool IsPlayer
        {
            get => isPlayer;
            set
            {
                if (value != isPlayer)
                {
                    Switch(value);
                }
            }
        }

        /// <summary>Whether the toggle can work: it has a fly camera and a player.</summary>
        public bool IsSetUp => flyCamera != null && player != null;

        private void Start()
        {
            if (!IsSetUp)
            {
                UnityEngine.Debug.LogWarning($"{nameof(PlayerCameraToggle)} on '{name}' needs a fly camera and a player.", this);
                return;
            }
            player.SetActive(false);
            if (startAsPlayer)
            {
                Switch(true);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsSetUp && keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                IsPlayer = !IsPlayer;
            }
        }

        private void Switch(bool toPlayer)
        {
            if (!IsSetUp || !Application.isPlaying)
            {
                return;
            }
            isPlayer = toPlayer;
            Transform fly = flyCamera.transform;
            if (toPlayer)
            {
                player.transform.SetPositionAndRotation(fly.position, Quaternion.Euler(0f, fly.eulerAngles.y, 0f));
                flyCamera.gameObject.SetActive(false);
                SetToolsEnabled(false);
                player.SetActive(true);
            }
            else
            {
                Camera eyes = player.GetComponentInChildren<Camera>();
                Transform from = eyes != null ? eyes.transform : player.transform;
                fly.SetPositionAndRotation(from.position, from.rotation);
                player.SetActive(false);
                flyCamera.gameObject.SetActive(true);
                SetToolsEnabled(true);
            }
        }

        private void SetToolsEnabled(bool restore)
        {
            if (!restore)
            {
                toolsWereEnabled = new bool[labTools.Length];
            }
            for (int i = 0; i < labTools.Length; i++)
            {
                if (labTools[i] == null)
                {
                    continue;
                }
                if (restore)
                {
                    labTools[i].enabled = toolsWereEnabled != null && toolsWereEnabled[i];
                }
                else
                {
                    toolsWereEnabled[i] = labTools[i].enabled;
                    labTools[i].enabled = false;
                }
            }
        }
    }
}
