using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Something a tool can be swung at that isn't ground (GL607): an animal. <see cref="PlayerToolUser"/> finds the one
    /// under the view by its collider and strikes it with whatever tool is held, so no tool needs to know about animals.
    /// </summary>
    public interface IToolTarget
    {
        /// <summary>False once a strike would do nothing (it is dead): the tool then works the ground behind it as usual.</summary>
        bool CanBeStruck { get; }

        /// <summary>A hit with a tool, swung from a place in the scene.</summary>
        void Strike(ToolDefinition tool, Vector3 from);
    }
}
