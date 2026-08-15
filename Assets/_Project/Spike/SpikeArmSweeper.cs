using UnityEngine;

namespace Contraption.Spike
{
    /// <summary>
    /// Drives the hinged arm back and forth into both of its limits, so the checkpoint exercises
    /// the "motor fighting a limit" case continuously rather than once.
    /// </summary>
    public sealed class SpikeArmSweeper : MonoBehaviour
    {
        private const float SweepPeriodSeconds = 1.6f;

        private HingeJoint2D _hinge;
        private float _motorSpeed;
        private float _maxMotorTorque;
        private float _nextFlipTime;
        private int _direction = 1;

        public void Initialise(HingeJoint2D hinge)
        {
            _hinge = hinge;
            _motorSpeed = Mathf.Abs(hinge.motor.motorSpeed);
            _maxMotorTorque = hinge.motor.maxMotorTorque;
            _nextFlipTime = Time.fixedTime + SweepPeriodSeconds;
        }

        // Physics writes belong in FixedUpdate (docs/CONVENTIONS.md).
        private void FixedUpdate()
        {
            if (_hinge == null)
            {
                return;
            }

            if (Time.fixedTime >= _nextFlipTime)
            {
                _direction = -_direction;
                _nextFlipTime = Time.fixedTime + SweepPeriodSeconds;
            }

            _hinge.motor = new JointMotor2D
            {
                motorSpeed = _motorSpeed * _direction,
                maxMotorTorque = _maxMotorTorque
            };
        }
    }
}
