namespace Clube.Game
{
    /// <summary>
    /// Something usable that is the terrain itself, with no object of its own: a boulder or an unclaimed stump (prototype,
    /// backlog 2026-11-24b item 1). <see cref="PlayerInteractor.TargetIsGround"/> is true for it, so a held tool still
    /// digs or mines it with Attack while E (and, for a claim, Place with no tool in hand) does its own thing.
    /// </summary>
    public interface IGroundInteractable : IInteractable
    {
    }
}
