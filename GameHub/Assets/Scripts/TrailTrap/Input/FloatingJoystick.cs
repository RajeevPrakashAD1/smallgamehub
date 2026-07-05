using UnityEngine;
using UnityEngine.EventSystems;

namespace TrailTrap
{
    /// <summary>
    /// Floating joystick: touch anywhere on the zone and the base appears under the finger;
    /// dragging sets Direction (base -> finger). Release = zero = go straight. Pure input+view —
    /// the sim never sees this class, only the Direction that JoystickTurnInput reads.
    /// </summary>
    public sealed class FloatingJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform baseRing;
        [SerializeField] RectTransform knob;
        [SerializeField] float radius = 150f;      // knob travel, in canvas reference px
        [SerializeField] float deadZone = 0.15f;   // fraction of radius that reads as "no input"

        public Vector2 Direction { get; private set; }   // zero when idle or inside dead-zone

        RectTransform _zone;
        int _pointerId = int.MinValue;   // finger that owns the stick; MinValue = none

        void Awake()
        {
            _zone = (RectTransform)transform;
            baseRing.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return;   // a second finger can't steal the stick
            _pointerId = e.pointerId;
            baseRing.gameObject.SetActive(true);
            baseRing.anchoredPosition = LocalPoint(e);
            knob.anchoredPosition = Vector2.zero;
            Direction = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            Vector2 pull = LocalPoint(e) - baseRing.anchoredPosition;
            knob.anchoredPosition = Vector2.ClampMagnitude(pull, radius);
            Direction = pull.magnitude < radius * deadZone ? Vector2.zero : pull.normalized;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            _pointerId = int.MinValue;
            baseRing.gameObject.SetActive(false);
            Direction = Vector2.zero;
        }

        // Screen px -> this zone's local space, so canvas scaling can't skew distances.
        Vector2 LocalPoint(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _zone, e.position, e.pressEventCamera, out Vector2 p);
            return p;
        }
    }
}
