namespace Clube.Game
{
    /// <summary>
    /// An <see cref="IInteractable"/> that only tells the player something (GL179: "You don't own this land"):
    /// <see cref="PlayerInteractor"/> shows its prompt without the Interact key, since pressing it does nothing.
    /// </summary>
    public interface INoticeOnly : IInteractable
    {
    }
}
