using System;
using System.Collections.Generic;
using Peeker.Tracking;
using UnityEngine;
using Core = Peeker.Peeker;

namespace Peeker.HUD.Elements
{
    [HudElement("Entity List",
        Description = "Nearby players, hostiles, passives and scrap.",
        X = 12f, Y = 12f,
        Width = PanelWidth, Height = 60f,
        EnabledByDefault = true)]
    public class EntityListElement : HudElement
    {
        public const float PanelWidth = 260f;

        private const int MaxRows = 28;
        private const int DummyRows = 6;
        private const float LineHeight = 16f;
        private const float PadX = 8f;
        private const float PadY = 6f;

        private static readonly EntityKind[] Order =
        {
            EntityKind.Player, EntityKind.Hostile, EntityKind.Passive, EntityKind.Scrap
        };

        private static readonly Dictionary<EntityKind, Color> Colours =
            new Dictionary<EntityKind, Color>
            {
                { EntityKind.Player,  new Color(0.30f, 0.80f, 1.00f) },
                { EntityKind.Hostile, new Color(1.00f, 0.32f, 0.32f) },
                { EntityKind.Passive, new Color(0.55f, 0.90f, 0.55f) },
                { EntityKind.Scrap,   new Color(1.00f, 0.85f, 0.25f) }
            };

        private readonly List<TrackedEntity> _sorted = new List<TrackedEntity>();
        private bool _hasData;

        private GUIStyle _row;
        private GUIStyle _header;

        protected override void OnEnable()
        {
            Plugin.Log.LogInfo("Entity List on");
        }

        protected override void OnDisable()
        {
            _sorted.Clear();
            _hasData = false;
            Plugin.Log.LogInfo("Entity List off");
        }

        /// <summary>
        /// Collect + sort once per frame instead of inside the Repaint pass, so the
        /// row count is already known when Measure() runs during Layout.
        /// </summary>
        public override void Update()
        {
            _sorted.Clear();
            _hasData = false;

            EntityManager em = Core.Entities;
            if (em == null || !em.InLevel) return;

            foreach (TrackedEntity e in em.All)
                if (e.Alive) _sorted.Add(e);

            _sorted.Sort((a, b) =>
            {
                int ka = Array.IndexOf(Order, a.Kind);
                int kb = Array.IndexOf(Order, b.Kind);
                if (ka != kb) return ka.CompareTo(kb);
                return em.DistanceTo(a).CompareTo(em.DistanceTo(b));
            });

            _hasData = true;
        }

        public override Vector2 Measure()
        {
            int shown = _hasData ? Mathf.Min(_sorted.Count, MaxRows) : DummyRows;
            return new Vector2(PanelWidth, 26f + (shown + 1) * LineHeight);
        }

        public override void Render()
        {
            EntityManager em = Core.Entities;
            if (!_hasData || em == null || !em.InLevel) return;

            EnsureStyles();

            Rect panel = Position.Bounds;
            int shown = Mathf.Min(_sorted.Count, MaxRows);

            HudRenderer.DrawRect(panel, new Color(0f, 0f, 0f, 0.55f));

            float y = panel.y + PadY;

            _header.normal.textColor = Color.white;
            GUI.Label(new Rect(panel.x + PadX, y, panel.width, LineHeight),
                $"ENTITIES  {_sorted.Count}", _header);
            y += LineHeight + 4f;

            _row.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(panel.x + PadX, y, panel.width, LineHeight),
                $"P {em.CountOf(EntityKind.Player)}   " +
                $"H {em.CountOf(EntityKind.Hostile)}   " +
                $"N {em.CountOf(EntityKind.Passive)}   " +
                $"S {em.CountOf(EntityKind.Scrap)}", _row);
            y += LineHeight;

            for (int i = 0; i < shown; i++)
            {
                TrackedEntity e = _sorted[i];
                _row.normal.textColor = Colours[e.Kind];

                string detail = e.Detail;
                string label = detail.Length > 0
                    ? $"{e.Name}  [{detail}]"
                    : e.Name;

                DrawRow(panel, y, label, $"{em.DistanceTo(e):F0}m");
                y += LineHeight;
            }

            if (_sorted.Count > shown)
            {
                _row.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(panel.x + PadX, y, panel.width, LineHeight),
                    $"+{_sorted.Count - shown} more", _row);
            }
        }

        /// <summary>
        /// Placeholder rows so the panel can be positioned from the main menu or
        /// anywhere else the entity manager has nothing to report.
        /// </summary>
        public override void RenderDummy()
        {
            if (_hasData)
            {
                Render();
                return;
            }

            EnsureStyles();

            Rect panel = Position.Bounds;
            HudRenderer.DrawRect(panel, new Color(0f, 0f, 0f, 0.55f));

            float y = panel.y + PadY;

            _header.normal.textColor = Color.white;
            GUI.Label(new Rect(panel.x + PadX, y, panel.width, LineHeight),
                $"ENTITIES  {DummyRows}", _header);
            y += LineHeight + 4f;

            _row.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(panel.x + PadX, y, panel.width, LineHeight),
                "P 2   H 2   N 1   S 1", _row);
            y += LineHeight;

            for (int i = 0; i < DummyRows; i++)
            {
                EntityKind kind = Order[i % Order.Length];
                _row.normal.textColor = Colours[kind];
                DrawRow(panel, y, kind + " " + (i + 1), (8 + i * 7) + "m");
                y += LineHeight;
            }
        }

        private void DrawRow(Rect panel, float y, string label, string distance)
        {
            GUI.Label(new Rect(panel.x + PadX, y, panel.width - 70f, LineHeight), label, _row);
            GUI.Label(new Rect(panel.xMax - 66f, y, 58f, LineHeight), distance, _row);
        }

        private void EnsureStyles()
        {
            if (_row != null) return;

            _row = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }
    }
}