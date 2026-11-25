using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The active quest on the HUD (GL216): under "Active Quest", the first quest of the checklist not done
    /// (<see cref="PlayerQuests"/>), in the top-left corner under the lab HUD box (<see cref="HudLayout"/>). The
    /// field log (Q) has the whole list. The early quests show a sketch of where they happen (GL446) and a tip box
    /// underneath (GL447). Gone once every quest is done.
    /// </summary>
    [RequireComponent(typeof(PlayerQuests))]
    public class QuestHud : MonoBehaviour
    {
        private PlayerQuests quests;
        private PlayerController player;
        private QuestHudView view;

        private void Awake()
        {
            quests = GetComponent<PlayerQuests>();
            player = GetComponent<PlayerController>();
        }

        // The boxes are on the HUD canvas (GL777) and stay there while a screen is open: IMGUI screens draw after the
        // canvas, so an open screen covers them as it covers the rest of the HUD. Until GL883 an IMGUI copy took over under
        // an open screen, and its 65% black blended differently from the canvas's, so the boxes changed shade with the screen.
        private void LateUpdate()
        {
            view ??= new QuestHudView();
            QuestLog.Quest active = quests.Log.Active;
            if (active == null || (player != null && !player.enabled))
            {
                view.Refresh(null, null, null, null, null, 0f);
                return;
            }
            string heading = active.Section == PlayerQuests.MainSection ? "Active Quest" : "Active Quest (side)";
            view.Refresh(active.Id, active.Title, heading, SketchFor(active.Id), PlayerQuests.TipFor(active.Id), HudLayout.TopLeftFree);
        }

        private void OnDestroy()
        {
            view?.Destroy();
        }

        private static Texture2D SketchFor(string questId)
        {
            switch (questId)
            {
                case "hand-axe": return PlaceSketch.Boulder;
                case "gather-wood": return PlaceSketch.Tree;
                case "campfire": return PlaceSketch.Stump;
                default: return null;
            }
        }
    }
}
