using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// GL725: how much of a cart or vessel on its way off the map is still here. "As they pass outside the buildable
    /// area they can no longer be interacted with" (its colliders are off, so nothing can be opened, struck or stood on),
    /// "and as these pass beyond the map border, they fade away": low-poly parts dither out (<c>_Fade</c> on the
    /// Low Poly shader), anything else (the crates) drops out halfway.
    /// </summary>
    public class OffMapPresence : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private readonly List<Collider> colliders = new List<Collider>();
        private readonly List<Renderer> fading = new List<Renderer>();
        private readonly List<Renderer> others = new List<Renderer>();
        private readonly List<bool> colliderWas = new List<bool>();
        private readonly List<GameObject> roots = new List<GameObject>();
        private MaterialPropertyBlock block;
        private bool interactable = true;
        private float fade = 1f;
        private bool gathered;

        /// <summary>False once it is past the free ground.</summary>
        public bool Interactable => interactable;

        /// <summary>1 whole, 0 gone.</summary>
        public float Fade => fade;

        /// <summary>Takes in the parts of another object that goes with this one (a cart's horse).</summary>
        public void Include(GameObject part)
        {
            Gather();
            Collect(part);
        }

        /// <summary>Sets how present it is; does nothing when nothing changed.</summary>
        public void Set(bool canInteract, float seen)
        {
            seen = Mathf.Clamp01(seen);
            Gather();
            if (canInteract != interactable)
            {
                interactable = canInteract;
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = canInteract && colliderWas[i];
                    }
                }
            }
            if (!Mathf.Approximately(seen, fade))
            {
                fade = seen;
                block ??= new MaterialPropertyBlock();
                // Gathered afresh: a figure makes its far mesh only when first seen from afar.
                fading.Clear();
                others.Clear();
                foreach (GameObject root in roots)
                {
                    if (root == null)
                    {
                        continue;
                    }
                    foreach (Renderer part in root.GetComponentsInChildren<Renderer>(true))
                    {
                        Material material = part.sharedMaterial;
                        (material != null && material.HasProperty(FadeId) ? fading : others).Add(part);
                    }
                }
                foreach (Renderer part in fading)
                {
                    if (part == null)
                    {
                        continue;
                    }
                    // Left enabled (figures switch their own parts for distance): at 0 the dither drops every pixel.
                    part.GetPropertyBlock(block);
                    block.SetFloat(FadeId, seen);
                    part.SetPropertyBlock(block);
                }
                foreach (Renderer part in others)
                {
                    if (part != null)
                    {
                        part.enabled = seen > 0.5f;
                    }
                }
            }
        }

        private void Gather()
        {
            if (!gathered)
            {
                gathered = true;
                Collect(gameObject);
            }
        }

        private void Collect(GameObject root)
        {
            roots.Add(root);
            foreach (Collider part in root.GetComponentsInChildren<Collider>(true))
            {
                colliders.Add(part);
                colliderWas.Add(part.enabled);
                part.enabled = part.enabled && interactable;
            }
        }
    }
}
