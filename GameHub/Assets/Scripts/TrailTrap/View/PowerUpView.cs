using System.Collections.Generic;
using UnityEngine;

namespace TrailTrap
{
    /// <summary>
    /// Pure VIEW: one glowing dot per pickup on the board. Pools SpriteRenderers and reads
    /// GameManager.PowerUps.Board every frame; never touches gameplay state.
    /// </summary>
    public sealed class PowerUpView : MonoBehaviour
    {
        [SerializeField] float diameter = 0.8f;
        [SerializeField] float pulse = 0.12f;
        [SerializeField] float pulseSpeed = 3f;

        GameManager _game;
        Sprite _dot;
        readonly List<SpriteRenderer> _pool = new();

        void Awake()
        {
            _game = FindAnyObjectByType<GameManager>();
            _dot = MakeCircleSprite(64);
        }

        void LateUpdate()
        {
            if (_game == null || _game.PowerUps == null) return;
            var board = _game.PowerUps.Board;

            EnsurePool(board.Count);
            float s = diameter * (1f + pulse * Mathf.Sin(Time.time * pulseSpeed));

            for (int i = 0; i < _pool.Count; i++)
            {
                bool used = i < board.Count;
                _pool[i].gameObject.SetActive(used);
                if (!used) continue;

                _pool[i].transform.position = board[i].pos;
                _pool[i].transform.localScale = Vector3.one * s;
                _pool[i].color = ColorFor(board[i].type);
            }
        }

        void EnsurePool(int need)
        {
            while (_pool.Count < need)
            {
                var go = new GameObject("Pickup");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _dot;
                sr.sortingOrder = 10;
                _pool.Add(sr);
            }
        }

        static Color ColorFor(PowerUpType type) => type switch
        {
            PowerUpType.Boost => UiTheme.Instance.boost,
            PowerUpType.Phase => UiTheme.Instance.phase,
            PowerUpType.Eraser => UiTheme.Instance.eraser,
            _ => Color.white,
        };

        // Build a soft white dot once (no image asset needed); each pickup tints a copy.
        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                    float a = Mathf.Clamp01(1f - d / r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
