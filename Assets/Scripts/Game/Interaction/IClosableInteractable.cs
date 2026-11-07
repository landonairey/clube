namespace Clube.Game
{
    /// <summary>
    /// An <see cref="IInteractable"/> that can't always be used (GL404: a shopkeeper away from the counter): while it isn't
    /// <see cref="IsAvailable"/>, <see cref="PlayerInteractor"/> shows its prompt as a notice, without the Interact key,
    /// and pressing the key does nothing.
    /// </summary>
    public interface IClosableInteractable : IInteractable
    {
        bool IsAvailable { get; }
    }
}
