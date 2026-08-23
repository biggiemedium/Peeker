using UnityEngine;
using UnityEngine.UI;

namespace Peeker.UI.Internal
{
    /// <summary>Sine-wave alpha oscillation, standing in for the design's <c>peekerPulse</c> CSS keyframes.</summary>
    public class Pulse : MonoBehaviour
    {
        public float PeriodSeconds = 2.4f;
        public float MinAlpha = 0.35f;
        public float MaxAlpha = 1f;

        private Graphic _graphic;

        private void Awake() => _graphic = GetComponent<Graphic>();

        private void Update()
        {
            if (_graphic == null) return;
            float t = (Mathf.Sin((Time.unscaledTime / PeriodSeconds) * Mathf.PI * 2f - Mathf.PI / 2f) + 1f) * 0.5f;
            Color c = _graphic.color;
            c.a = Mathf.Lerp(MinAlpha, MaxAlpha, t);
            _graphic.color = c;
        }
    }
}
