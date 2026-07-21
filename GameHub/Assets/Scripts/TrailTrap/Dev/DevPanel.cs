using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Dev-only cheat panel: a corner DEV button that expands into DevFlags checkboxes.
    /// Draws only in the editor and development builds — release players never see it.
    /// Flags apply where the sim runs (the host/server decides deaths).
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        bool _open;

        void OnGUI()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;

            const float w = 170f, h = 60f, pad = 10f;
            if (GUI.Button(new Rect(pad, pad, w, h), "DEV"))
                _open = !_open;
            if (!_open) return;

            DevFlags.NoDeath = GUI.Toggle(new Rect(pad, pad + h + 8f, w * 2f, 40f),
                                          DevFlags.NoDeath, " No death");
        }
    }
}
