using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// One job: produce a turn axis for a player, device-agnostic. Takes the current state
    /// because aim-to-steer needs the player's heading to know which way to turn. Everything
    /// downstream only sees the resulting float, so the device never leaks past here.
    /// </summary>
    public interface ITurnInput
    {
        float Read(in PlayerState state);   // [-1,1]; -1 right(CW), 0 straight, +1 left(CCW)
    }

    /// <summary>Desktop dev input that matches the mobile model: steer toward the mouse cursor.</summary>
    public sealed class MouseAimTurnInput : ITurnInput
    {
        readonly Camera _cam;
        readonly float _band;   // smoothing band in radians

        public MouseAimTurnInput(Camera cam, float smoothBandDeg = 15f)
        {
            _cam = cam;
            _band = smoothBandDeg * Mathf.Deg2Rad;
        }

        public float Read(in PlayerState p)
        {
            Vector2 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            return SteerMath.TurnToward(p.heading, world - p.position, _band);   // aim player -> cursor
        }
    }

    /// <summary>Mobile joystick: steer toward the direction the thumb is pulling.</summary>
    public sealed class JoystickTurnInput : ITurnInput
    {
        readonly FloatingJoystick _joy;
        readonly float _band;   // smoothing band in radians

        public JoystickTurnInput(FloatingJoystick joy, float smoothBandDeg = 15f)
        {
            _joy = joy;
            _band = smoothBandDeg * Mathf.Deg2Rad;
        }

        public float Read(in PlayerState p)
            => SteerMath.TurnToward(p.heading, _joy.Direction, _band);
    }

    /// <summary>Relative hold-left/right keys. Dev fallback only (mobile is the real target).</summary>
    public sealed class KeyboardTurnInput : ITurnInput
    {
        readonly KeyCode _left, _right;

        public KeyboardTurnInput(KeyCode left, KeyCode right) { _left = left; _right = right; }

        public float Read(in PlayerState _)
            => (Input.GetKey(_left) ? 1f : 0f) - (Input.GetKey(_right) ? 1f : 0f);
    }
}
