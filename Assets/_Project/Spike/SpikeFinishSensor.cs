using UnityEngine;

namespace Contraption.Spike
{
    /// <summary>
    /// Trigger at the end of the course. Only the cargo reaching it counts as a win — driving
    /// an empty chassis across the line is not success.
    /// </summary>
    public sealed class SpikeFinishSensor : MonoBehaviour
    {
        public bool CargoArrived { get; private set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<SpikeCargo>() != null)
            {
                CargoArrived = true;
            }
        }
    }
}
