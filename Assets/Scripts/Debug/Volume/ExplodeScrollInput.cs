using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In Play mode, the mouse wheel pulls the volume tetrahedra apart (scroll up)
    /// or pushes them back together (scroll down), so you can see the separate
    /// pieces that add up to the solid. Ignored while the right mouse button is
    /// held, when the wheel changes the free-fly camera's speed instead.
    /// </summary>
    [RequireComponent(typeof(VoxelVolumeLab))]
    public class ExplodeScrollInput : MonoBehaviour
    {
        [Tooltip("Change in explode distance per scroll notch, in voxel units.")]
        [SerializeField, Range(0.01f, 0.25f)]
        private float stepPerNotch = 0.05f;

        private VoxelVolumeLab volumeLab;

        private void Awake()
        {
            volumeLab = GetComponent<VoxelVolumeLab>();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || mouse.rightButton.isPressed)
            {
                return;
            }

            // Scroll magnitude per notch differs between platforms, so only the direction is used.
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                volumeLab.ExplodeDistance += Mathf.Sign(scroll) * stepPerNotch;
            }
        }
    }
}
