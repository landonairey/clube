using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's own body as the sun sees it (GL608): a <see cref="FigureRig"/> in the player's look that only casts a
    /// shadow, so looking down or behind shows a person's shadow walking, standing and crouching with the
    /// player, while the first-person view stays clear. Added by <see cref="PlayerController"/>; no scene needs editing.
    /// </summary>
    public class PlayerShadowBody : MonoBehaviour
    {
        private const string ShaderName = "Clube/Low Poly";

        private PlayerController player;
        private FigureRig rig;
        private float standingHeight;

        private void Start()
        {
            player = GetComponent<PlayerController>();
            Shader shader = Shader.Find(ShaderName);
            if (player == null || shader == null)
            {
                enabled = false;
                return;
            }
            standingHeight = player.Body.height;
            var go = new GameObject("Shadow body");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, player.Body.center.y - standingHeight * 0.5f, 0f);
            rig = go.AddComponent<FigureRig>();
            FigureLook look = FigureLooks.Of(FigurePreset.Player);
            look.Height = standingHeight / FigureSkeleton.Tall;
            rig.Build(look, new Material(shader) { name = "Player shadow" }, 0);
            rig.CastShadowOnly();
        }

        private void LateUpdate()
        {
            if (rig == null)
            {
                return;
            }
            float speed = player.HorizontalVelocity.magnitude;
            bool walking = speed > 0.3f && player.IsGrounded;
            rig.Speed = speed;
            rig.Pose = walking ? FigureAction.Walk : FigureAction.Stand;
            // Crouching and sliding lower the capsule: the shadow squats with it.
            rig.transform.localScale = new Vector3(1f, Mathf.Clamp01(player.Body.height / standingHeight), 1f);
        }
    }
}
