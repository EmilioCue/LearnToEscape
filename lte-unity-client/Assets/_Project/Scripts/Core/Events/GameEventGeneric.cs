using System.Collections.Generic;
using UnityEngine;

namespace LearnToEscape.Core.Events
{
    public abstract class GameEvent<T> : ScriptableObject
    {
        private readonly List<IGameEventListener<T>> _listeners = new();

        public void Raise(T value)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i].OnEventRaised(value);
        }

        public void RegisterListener(IGameEventListener<T> listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(IGameEventListener<T> listener)
        {
            if (listener != null)
                _listeners.Remove(listener);
        }
    }
}
