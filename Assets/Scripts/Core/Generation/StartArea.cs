using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The land the player starts on (2026-10-13): a square of the world that generation makes sure
    /// has what the first loop needs, at least a few trees (<see cref="TreeField"/>) and a clay
    /// deposit (<see cref="LayerTable"/>), adding them where the seed gave none. Part of
    /// <see cref="TerrainSettings"/>; the world labs set it to the parcel the player lands in.
    /// </summary>
    [Serializable]
    public class StartArea
    {
        [Tooltip("Make sure the start area has the trees and clay below.")]
        [SerializeField]
        private bool enabled;

        [Tooltip("The area's corner with the lowest x and z, in metres from the world origin.")]
        [SerializeField]
        private Vector2 min = new Vector2(-32f, 0f);

        [Tooltip("Width of the square area, in metres (a parcel is 32 m at 4 m chunks).")]
        [SerializeField, Min(4f)]
        private float size = 32f;

        [Tooltip("Fewest trees rooted in the area; more are grown on its flattest spots when the seed gave fewer.")]
        [SerializeField, Range(0, 8)]
        private int minTrees = 2;

        [Tooltip("Make sure a clay deposit reaches into the area; one is put in its middle when none does.")]
        [SerializeField]
        private bool clay = true;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>The square, in x and z metres (Rect's y is z).</summary>
        public Rect Area
        {
            get => new Rect(min, new Vector2(Mathf.Max(4f, size), Mathf.Max(4f, size)));
            set
            {
                min = value.min;
                size = Mathf.Max(4f, value.width);
            }
        }

        public int MinTrees
        {
            get => enabled ? minTrees : 0;
            set => minTrees = Mathf.Clamp(value, 0, 8);
        }

        public bool Clay
        {
            get => enabled && clay;
            set => clay = value;
        }
    }
}
