using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace Peeker.Tracking
{
    public enum EntityKind { Player, Hostile, Passive, Scrap }

    public class TrackedEntity
    {
        public EntityKind Kind;
        public string Name;
        public Transform Transform;
        
        // TODO: Dynamically get enemy hitbox size
        public float Height = 2f;
        public float Radius = 0.5f;

        public PlayerControllerB Player;
        public EnemyAI Enemy;
        public GrabbableObject Item;

        public bool Alive => Transform != null;
        public Vector3 Position => Transform.position;

        public string Detail
        {
            get
            {
                if (Player != null) return $"{Player.health} hp";
                if (Item != null) return $"${Item.scrapValue}";
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Single source of truth for everything alive in the level.
    /// Modules read from here instead of each running their own
    /// FindObjectsOfType sweep.
    /// </summary>
    public class EntityManager
    {
        private const float ScanInterval = 1f;

        private readonly List<TrackedEntity> _entities = new List<TrackedEntity>();
        private float _nextScan;

        public PlayerControllerB LocalPlayer { get; private set; }
        public Camera Camera => LocalPlayer != null ? LocalPlayer.gameplayCamera : null;
        public bool InLevel => LocalPlayer != null && Camera != null;

        public IReadOnlyList<TrackedEntity> All => _entities;

        public IEnumerable<TrackedEntity> OfKind(EntityKind kind)
        {
            foreach (var e in _entities)
                if (e.Kind == kind && e.Alive)
                    yield return e;
        }

        public int CountOf(EntityKind kind)
        {
            int n = 0;
            foreach (var e in _entities)
                if (e.Kind == kind && e.Alive) n++;
            return n;
        }

        public float DistanceTo(TrackedEntity e)
        {
            if (LocalPlayer == null || !e.Alive) return float.MaxValue;
            return Vector3.Distance(LocalPlayer.transform.position, e.Position);
        }

        /// <summary>Call once per frame. Cheap except on scan ticks.</summary>
        public void Update()
        {
            var round = StartOfRound.Instance;
            LocalPlayer = round != null ? round.localPlayerController : null;

            if (LocalPlayer == null)
            {
                _entities.Clear();
                _nextScan = 0f;
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                Rescan(round);
            }
            else
            {
                _entities.RemoveAll(e => !e.Alive);
            }
        }

        private void Rescan(StartOfRound round)
        {
            _entities.Clear();

            if (round.allPlayerScripts != null)
            {
                foreach (var p in round.allPlayerScripts)
                {
                    if (p == null || p == LocalPlayer) continue;
                    if (!p.isPlayerControlled || p.isPlayerDead) continue;

                    _entities.Add(new TrackedEntity
                    {
                        Kind = EntityKind.Player,
                        Name = p.playerUsername,
                        Transform = p.transform,
                        Height = 2f,
                        Player = p
                    });
                }
            }

            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.isEnemyDead) continue;

                var type = e.enemyType;
                _entities.Add(new TrackedEntity
                {
                    Kind = IsPassive(type) ? EntityKind.Passive : EntityKind.Hostile,
                    Name = type != null ? type.enemyName : e.GetType().Name,
                    Transform = e.transform,
                    Height = 2f,
                    Enemy = e
                });
            }

            foreach (var g in Object.FindObjectsOfType<GrabbableObject>())
            {
                if (g == null || g.isHeld || g.isPocketed) continue;
                if (g.itemProperties == null || !g.itemProperties.isScrap) continue;

                _entities.Add(new TrackedEntity
                {
                    Kind = EntityKind.Scrap,
                    Name = g.itemProperties.itemName,
                    Transform = g.transform,
                    Height = 0.5f,
                    Item = g
                });
            }
        }
        
        private static void FitBounds(TrackedEntity e, GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            e.Height = Mathf.Max(0.3f, b.max.y - go.transform.position.y);
            e.Radius = Mathf.Max(0.2f, Mathf.Max(b.extents.x, b.extents.z));
        }

        // Lethal Company has no "passive" flag. Daytime enemies are the
        // closest thing — Manticoils, Locusts, Tulip Snakes. Circuit Bees
        // are daytime but will absolutely kill you, so they're excluded.
        private static bool IsPassive(EnemyType type)
        {
            if (type == null) return false;
            if (!type.isDaytimeEnemy) return false;
            return type.enemyName != null &&
                   type.enemyName.IndexOf("Bee", System.StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}