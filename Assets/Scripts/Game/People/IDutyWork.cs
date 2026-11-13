using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Work a resident does out in the world (GL615), named by a <see cref="Clube.Core.Duty.Work"/>: felling, digging,
    /// hunting. One is made for a villager each time a duty with that work begins (<see cref="DutyWorks"/>), so it can
    /// remember what it is in the middle of. The villager does the walking and the posing; the work says where to stand
    /// and what a stroke does.
    /// </summary>
    public interface IDutyWork
    {
        /// <summary>
        /// Looks for the next thing to do. True with where to stand and what to face (in the world's own metres);
        /// false when there is nothing to do just now. Asked again after every stroke and while walking there, so the
        /// place can move (a tunnel's face, an animal).
        /// </summary>
        bool Seek(Villager worker, out Vector3 stand, out Vector3 face);

        /// <summary>One stroke, made standing where <see cref="Seek"/> last said.</summary>
        void Stroke(Villager worker);
    }
}
