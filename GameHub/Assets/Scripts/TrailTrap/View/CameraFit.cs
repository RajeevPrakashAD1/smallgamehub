using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: keeps the whole arena on screen on any device (mobile-first rule).
    /// Fit policy: the camera zooms so config.arenaHalfSize (+ margin) always fits —
    /// extra screen space on wider/taller devices shows only cosmetic background,
    /// never gameplay info, so every player sees 100% of the arena.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFit : MonoBehaviour
    {
        [SerializeField] SimConfig config;
        [SerializeField] float margin = 1f;   // world units of breathing room outside the walls

        Camera _cam;

        void Awake() => _cam = GetComponent<Camera>();

        // Every frame, so window resizes / device rotation / Inspector retunes apply instantly.
        // Cost is one divide and a max — nothing.
        void LateUpdate()
        {
            Vector2 need = config.arenaHalfSize + Vector2.one * margin;
            _cam.orthographicSize = Mathf.Max(need.y, need.x / _cam.aspect);
        }
    }
}
