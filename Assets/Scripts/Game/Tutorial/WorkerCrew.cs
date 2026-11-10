using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>Other Mining Corp hands swinging picks at the mountain wall in the tutorial (GL531): looping miner figures.</summary>
    public static class WorkerCrew
    {
        private static readonly float[] Xs = { 35f, 41f, 47f, 57f, 63f, 67f };

        /// <summary>Puts the workers at the foot of the north wall (map space, under <paramref name="map"/>).</summary>
        public static void Build(Transform map, Material material)
        {
            var crew = new GameObject("Mining crew");
            crew.transform.SetParent(map, false);
            float z = CompanyWork.Fields.yMax - 0.8f;
            for (int i = 0; i < Xs.Length; i++)
            {
                var go = new GameObject("Worker " + (i + 1));
                go.transform.SetParent(crew.transform, false);
                go.transform.localPosition = new Vector3(Xs[i], CompanyWork.Ground, z + (i % 2) * 0.5f);
                go.transform.localRotation = Quaternion.identity;
                var figure = go.AddComponent<KeeperFigure>();
                figure.Build(ShopKind.Mining, material, 600 + i * 7);
                figure.Pose = KeeperPose.Mine;
            }
        }
    }
}
