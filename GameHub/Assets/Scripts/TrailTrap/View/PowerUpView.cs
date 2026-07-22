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
            _dot = ProcSprites.MakeDot(64);
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

    }
}
