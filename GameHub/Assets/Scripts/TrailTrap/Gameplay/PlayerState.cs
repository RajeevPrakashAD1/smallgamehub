using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Everything that describes a player right now. A value type (struct) so movement can be
    /// a pure function: take a state, return a new one (no shared mutation).
    /// </summary>
    public struct PlayerState
    {
        public Vector2 position;   // sim units
        public float   heading;    // radians; 0 = +X, CCW positive
        public float   speed;      // units/sec (Boost/ramp may change this later)
        public bool    alive;
    }

    /// <summary>
    /// A player's intent for one tick, kept separate from state so it can be sent over the
    /// network later (M4) and so movement stays "state + intent -> next state".
    /// </summary>
    public struct InputFrame
    {
        // Analog steering axis in [-1,1]. Sign = direction (-1 right/CW, +1 left/CCW),
        // magnitude = how hard to turn this tick. MovementStep scales it by turnRate*dt,
        // so |turn| <= 1 keeps the capped turn rate. Filled by an ITurnInput (aim-to-steer).
        public float turn;
    }
}
