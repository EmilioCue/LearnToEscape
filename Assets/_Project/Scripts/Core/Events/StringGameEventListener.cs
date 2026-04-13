using UnityEngine;
using UnityEngine.Events;

namespace LearnToEscape.Core.Events
{
    [System.Serializable]
    public class StringUnityEvent : UnityEvent<string> { }

    public class StringGameEventListener : MonoBehaviour, IGameEventListener<string>
    {
        [SerializeField] private StringGameEvent _event;
        [SerializeField] private StringUnityEvent _response;

        private void OnEnable()
        {
            if (_event != null)
                _event.RegisterListener(this);
        }

        private void OnDisable()
        {
            if (_event != null)
                _event.UnregisterListener(this);
        }

        public void OnEventRaised(string value) => _response?.Invoke(value);
    }
}
