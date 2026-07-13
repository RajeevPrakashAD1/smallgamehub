using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Dev-only session starter (M5): draws Host/Join buttons until a session is running.
    /// Replaces GameManager.autoHost so a second instance (an MPPM virtual player) can join
    /// instead of hosting. A real mobile menu replaces this in a later milestone.
    /// </summary>
    public sealed class NetBootstrap : MonoBehaviour
    {
        void OnGUI()
        {
            if (NetKit.Session.IsRunning) return;

            const float w = 220f, h = 90f;
            float x = (Screen.width - w) * 0.5f;
            if (GUI.Button(new Rect(x, Screen.height * 0.35f, w, h), "Host"))
                NetKit.Session.StartHost();
            if (GUI.Button(new Rect(x, Screen.height * 0.35f + h + 20f, w, h), "Join"))
                NetKit.Session.StartClient();
        }
    }
}
