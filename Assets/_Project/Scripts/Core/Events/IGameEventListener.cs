namespace LearnToEscape.Core.Events
{
    public interface IGameEventListener<T>
    {
        void OnEventRaised(T value);
    }
}
