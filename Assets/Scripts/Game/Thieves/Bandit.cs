using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What makes a resident of a thieves' camp answer to a blow (GL617: "the player can fight back and recover
    /// goods"). The game has no health for people, and this adds none: a thief struck with any tool drops everything he
    /// carries where he stands (bags and a coin pouch, GL611's dropped items) and lies knocked down for a while; then he
    /// gets up and goes back to his duties, and his band leaves the player be for longer.
    /// </summary>
    public class Bandit : MonoBehaviour, IToolTarget
    {
        /// <summary>Real seconds a struck thief stays down.</summary>
        public const float DazeSeconds = 20f;

        private Villager villager;
        private ThievesCamps thieves;

        public Villager Villager => villager;

        /// <summary>Blows taken since he came into the world.</summary>
        public int Blows { get; private set; }

        public bool CanBeStruck => villager != null && !villager.IsDazed;

        public void Setup(Villager who, ThievesCamps camps)
        {
            villager = who;
            thieves = camps;
        }

        public void Strike(ToolDefinition tool, Vector3 from)
        {
            if (!CanBeStruck)
            {
                return;
            }
            Blows++;
            if (thieves != null)
            {
                thieves.Spill(villager);
                thieves.Routed(villager.Site.Settlement.Index);
            }
            villager.Daze(DazeSeconds);
        }
    }
}
