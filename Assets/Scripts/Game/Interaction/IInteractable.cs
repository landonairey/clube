namespace Clube.Game
{
    /// <summary>
    /// Something in the world the player can use with Interact (GL9, first pass of I3): a
    /// station now, later pickups (I2), merchants (GL14) and NPCs (NP4). Found by
    /// <see cref="PlayerInteractor"/> through a collider on it or its children.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>What using it does, shown after the key, e.g. "Use furnace".</summary>
        string Prompt { get; }

        void Interact(PlayerInteractor by);
    }
}
