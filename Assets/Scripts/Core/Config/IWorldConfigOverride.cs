namespace Clube.Core
{
    /// <summary>
    /// Changes a world's play copy of its config before the world starts (GL203): a component beside a
    /// <c>WorldView</c> that implements it is asked once, in the view's Awake, so e.g. a new game's seed
    /// shapes the first chunks rather than regenerating them. The config asset itself is never changed.
    /// </summary>
    public interface IWorldConfigOverride
    {
        void Apply(WorldConfig playCopy);
    }
}
