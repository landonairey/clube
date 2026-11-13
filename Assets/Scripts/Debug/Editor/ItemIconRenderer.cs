using System;
using System.Collections.Generic;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Takes the picture of an inventory icon (GL612): draws a model (a mesh and a material, or a prefab) in a preview
    /// scene of its own, under its own lights, framed by <see cref="IconFraming"/>, and returns a square texture with a
    /// clear background. The picture is taken twice, on black and on white, and the background worked out from the
    /// difference, so it doesn't depend on the render pipeline keeping alpha. Create one, render many, dispose.
    /// </summary>
    public sealed class ItemIconRenderer : IDisposable
    {
        private const string HeldItemMaterial = "Assets/Materials/HeldItem.mat";
        private const string ChunkShader = "Clube/ItemIconChunk";
        private const int Oversample = 4;

        private readonly PreviewRenderUtility preview = new PreviewRenderUtility();
        private readonly Material vertexColoured;
        private readonly Material textured;

        private readonly bool asyncShaders;

        public ItemIconRenderer()
        {
            // A shader still compiling in the background would draw the first picture black.
            asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            vertexColoured = new Material(AssetDatabase.LoadAssetAtPath<Material>(HeldItemMaterial)) { hideFlags = HideFlags.HideAndDontSave };
            textured = new Material(Shader.Find(ChunkShader)) { hideFlags = HideFlags.HideAndDontSave };
            preview.camera.orthographic = true;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.ambientColor = new Color(0.3f, 0.31f, 0.35f);
            // The sun over the camera's left shoulder, and a weak fill from the other side.
            preview.lights[0].intensity = 0.88f;
            preview.lights[0].color = new Color(1f, 0.97f, 0.92f);
            preview.lights[0].transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            preview.lights[1].intensity = 0.25f;
            preview.lights[1].transform.rotation = Quaternion.Euler(20f, -110f, 0f);
        }

        /// <summary>The picture of a model, <paramref name="size"/> pixels square; null when there is nothing to draw.</summary>
        public Texture2D Render(ItemIconModel model, int size)
        {
            if (model.IsEmpty)
            {
                return null;
            }
            GameObject instance = null;
            try
            {
                var corners = new List<Vector3>();
                Matrix4x4 placed = Matrix4x4.Rotate(model.Turn);
                if (model.Prefab != null)
                {
                    instance = Place(model.Prefab, model.Turn, corners);
                }
                else
                {
                    var box = new Vector3[8];
                    // A long tool is framed on its head end only; the handle runs out of the picture.
                    Bounds bounds = model.Mesh.bounds;
                    float kept = bounds.size.y * Mathf.Clamp(model.Keep, 0.1f, 1f);
                    bounds.SetMinMax(new Vector3(bounds.min.x, bounds.max.y - kept, bounds.min.z), bounds.max);
                    IconFraming.Corners(bounds, placed, box, 0);
                    corners.AddRange(box);
                }
                if (corners.Count == 0)
                {
                    return null;
                }
                IconFraming framing = IconFraming.Fit(corners, model.View);
                Camera camera = preview.camera;
                camera.transform.SetPositionAndRotation(framing.Position, framing.Rotation);
                camera.orthographicSize = framing.OrthographicSize;
                camera.nearClipPlane = framing.NearClip;
                camera.farClipPlane = framing.FarClip;

                Material material = null;
                if (model.Mesh != null)
                {
                    material = model.Albedo != null ? textured : vertexColoured;
                    material.SetColor("_Tint", model.Tint);
                    if (model.Albedo != null)
                    {
                        material.SetTexture("_MainTex", model.Albedo);
                        material.SetFloat("_Tiling", 1f / Mathf.Max(0.01f, model.TextureZoom));
                        material.SetFloat("_Vivid", model.Vividness);
                    }
                }
                int pixels = size * Oversample;
                Color[] onBlack = Shoot(model.Mesh, placed, material, Color.black, pixels);
                Color[] onWhite = Shoot(model.Mesh, placed, material, Color.white, pixels);
                return Compose(onBlack, onWhite, pixels, size);
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        // Puts a prefab in the preview scene, its lights and effects off, and collects the corners of what it draws.
        private GameObject Place(GameObject prefab, Quaternion turn, List<Vector3> corners)
        {
            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, turn);
            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
            }
            var box = new Vector3[8];
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                {
                    continue;
                }
                IconFraming.Corners(filter.sharedMesh.bounds, filter.transform.localToWorldMatrix, box, 0);
                corners.AddRange(box);
            }
            return instance;
        }

        private Color[] Shoot(Mesh mesh, Matrix4x4 placed, Material material, Color background, int pixels)
        {
            preview.camera.backgroundColor = background;
            preview.BeginStaticPreview(new Rect(0f, 0f, pixels, pixels));
            if (mesh != null)
            {
                preview.DrawMesh(mesh, placed, material, 0);
            }
            preview.Render(true);
            Texture2D shot = preview.EndStaticPreview();
            Color[] colours = shot.GetPixels();
            UnityEngine.Object.DestroyImmediate(shot);
            return colours;
        }

        // A pixel that is the same on black and on white is the model; one that follows the background is empty.
        private static Texture2D Compose(Color[] onBlack, Color[] onWhite, int pixels, int size)
        {
            var result = new Color[size * size];
            int block = pixels / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int dy = 0; dy < block; dy++)
                    {
                        int row = (y * block + dy) * pixels + x * block;
                        for (int dx = 0; dx < block; dx++)
                        {
                            Color black = onBlack[row + dx], white = onWhite[row + dx];
                            float cover = 1f - Mathf.Clamp01(((white.r - black.r) + (white.g - black.g) + (white.b - black.b)) / 3f);
                            // On black the colour is already multiplied by its cover.
                            r += black.r;
                            g += black.g;
                            b += black.b;
                            a += cover;
                        }
                    }
                    result[y * size + x] = a > 1e-4f ? new Color(r / a, g / a, b / a, a / (block * block)) : Color.clear;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(result);
            texture.Apply();
            return texture;
        }

        public void Dispose()
        {
            ShaderUtil.allowAsyncCompilation = asyncShaders;
            preview.Cleanup();
            UnityEngine.Object.DestroyImmediate(vertexColoured);
            UnityEngine.Object.DestroyImmediate(textured);
        }
    }
}
