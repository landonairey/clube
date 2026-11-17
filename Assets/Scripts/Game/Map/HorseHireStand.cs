using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A horse-hire stand by a settlement's name board (GL728): a hitching rail, a saddled horse tied to it (the cart
    /// horse's body, <see cref="CartHorse"/>) and its groom. Talking to the groom (E) opens <see cref="HorseHirePanel"/>,
    /// the rides to the other stands for <see cref="HorseHire.Fare"/> coins.
    /// </summary>
    public class HorseHireStand : MonoBehaviour, IInteractable
    {
        private static readonly Color RailWood = new Color(0.45f, 0.32f, 0.2f);
        private static readonly Color Saddle = new Color(0.33f, 0.18f, 0.1f);

        private MapHorseHire hire;

        /// <summary>Which of <see cref="MapHorseHire.Stands"/> this is.</summary>
        public int Index { get; private set; }

        /// <summary>The settlement's name.</summary>
        public string SettlementName { get; private set; }

        public string Prompt => $"Hire a horse to another city ({Wallet.Format(HorseHire.Fare)} coins a ride)";

        public void Interact(PlayerInteractor by)
        {
            HorseHirePanel panel = by.GetComponent<HorseHirePanel>();
            if (panel == null)
            {
                panel = by.gameObject.AddComponent<HorseHirePanel>();
            }
            panel.Open(hire, Index);
        }

        /// <summary>
        /// Builds the stand under this object, which stands at the hitching post with its z along the road out of the
        /// settlement and its x away from the road.
        /// </summary>
        public void Build(MapHorseHire owner, int index, string settlementName, Material material, int seed)
        {
            hire = owner;
            Index = index;
            SettlementName = settlementName;

            // The rail: two posts and a bar along the road.
            var rail = new LowPolyMeshBuilder();
            foreach (float along in new[] { -1.3f, 1.3f })
            {
                rail.Box(new Vector3(0f, 0.5f, along), new Vector3(0.14f, 1.3f, 0.14f), Quaternion.identity, RailWood);
            }
            rail.Box(new Vector3(0f, 1.05f, 0f), new Vector3(0.09f, 0.09f, 2.9f), Quaternion.identity, RailWood * 0.9f);
            var railObject = new GameObject("Hitching rail");
            railObject.transform.SetParent(transform, false);
            railObject.AddComponent<MeshFilter>().sharedMesh = rail.Make("Hitching rail");
            railObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var railCollider = railObject.AddComponent<BoxCollider>();
            railCollider.center = new Vector3(0f, 0.6f, 0f);
            railCollider.size = new Vector3(0.2f, 1.2f, 2.8f);

            // The horse, tied alongside the rail on the road's side, with a saddle and a lead to the bar.
            var horseObject = new GameObject("Hired horse");
            horseObject.transform.SetParent(transform, false);
            CartHorse horse = horseObject.AddComponent<CartHorse>();
            horse.Build(material);
            horse.Place(transform.TransformPoint(new Vector3(-0.9f, 0f, 0.1f)), transform.forward, false, 0f);
            var tack = new LowPolyMeshBuilder();
            tack.Box(new Vector3(0f, 1.52f, 0.1f), new Vector3(0.62f, 0.12f, 0.7f), Quaternion.identity, Saddle);
            tack.Box(new Vector3(0f, 1.25f, 0.1f), new Vector3(0.66f, 0.5f, 0.06f), Quaternion.identity, Saddle * 0.8f);
            tack.Box(new Vector3(0.45f, 1.2f, 0.95f), new Vector3(0.9f, 0.025f, 0.025f), Quaternion.Euler(0f, 0f, 15f), Saddle * 0.6f);
            var tackObject = new GameObject("Saddle");
            tackObject.transform.SetParent(horseObject.transform, false);
            tackObject.AddComponent<MeshFilter>().sharedMesh = tack.Make("Saddle");
            tackObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var horseCollider = horseObject.AddComponent<BoxCollider>();
            horseCollider.center = new Vector3(0f, 1.1f, 0f);
            horseCollider.size = new Vector3(0.6f, 1.4f, 2.1f);

            // The groom, on the road's side of the horse, facing the road; what the player talks to.
            var groomObject = new GameObject("Groom");
            groomObject.transform.SetParent(transform, false);
            groomObject.transform.localPosition = new Vector3(-1.9f, 0f, -1.4f);
            groomObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            FigureRig groom = groomObject.AddComponent<FigureRig>();
            groom.Build(FigurePreset.Carter, material, seed);
            groom.Pose = FigureAction.Stand;
            var groomCollider = groomObject.AddComponent<CapsuleCollider>();
            groomCollider.center = new Vector3(0f, 0.9f, 0f);
            groomCollider.height = 1.8f;
            groomCollider.radius = 0.35f;
            // The groom, the horse and the rail all find this stand as what the player looks at (GetComponentInParent).
        }
    }
}
