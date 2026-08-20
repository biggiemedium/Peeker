using System.Collections.Generic;
using Peeker;
using Peeker.Module;
using Peeker.Tracking;
using UnityEngine;
using UnityEngine.InputSystem;

public class HUDModule : Module
{
    private const int MaxRows = 28;

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

    private GUIStyle _row;
    private GUIStyle _header;
    private Texture2D _bg;
    private readonly List<TrackedEntity> _sorted = new List<TrackedEntity>();

    public HUDModule() : base("HUD", Key.H, ModuleCategory.Visual) { }

    protected override void OnEnabled()  => Plugin.Log.LogInfo("HUD on");
    protected override void OnDisabled() => Plugin.Log.LogInfo("HUD off");

    public override void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        var em = Peeker.Peeker.Entities;
        if (em == null || !em.InLevel) return;

        EnsureStyles();

        _sorted.Clear();
        foreach (var e in em.All)
            if (e.Alive) _sorted.Add(e);

        _sorted.Sort((a, b) =>
        {
            int ka = System.Array.IndexOf(Order, a.Kind);
            int kb = System.Array.IndexOf(Order, b.Kind);
            if (ka != kb) return ka.CompareTo(kb);
            return em.DistanceTo(a).CompareTo(em.DistanceTo(b));
        });

        int shown = Mathf.Min(_sorted.Count, MaxRows);
        float lineH = 16f;
        float w = 260f;
        float h = 26f + (shown + 1) * lineH;
        var panel = new Rect(12f, 12f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(panel, _bg);
        GUI.color = Color.white;

        float y = panel.y + 6f;

        _header.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 8f, y, w, lineH),
            $"ENTITIES  {_sorted.Count}", _header);
        y += lineH + 4f;

        _row.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(panel.x + 8f, y, w, lineH),
            $"P {em.CountOf(EntityKind.Player)}   " +
            $"H {em.CountOf(EntityKind.Hostile)}   " +
            $"N {em.CountOf(EntityKind.Passive)}   " +
            $"S {em.CountOf(EntityKind.Scrap)}", _row);
        y += lineH;

        for (int i = 0; i < shown; i++)
        {
            var e = _sorted[i];
            _row.normal.textColor = Colours[e.Kind];

            string detail = e.Detail;
            string label = detail.Length > 0
                ? $"{e.Name}  [{detail}]"
                : e.Name;

            GUI.Label(new Rect(panel.x + 8f, y, w - 70f, lineH), label, _row);
            GUI.Label(new Rect(panel.xMax - 66f, y, 58f, lineH),
                $"{em.DistanceTo(e):F0}m", _row);
            y += lineH;
        }

        if (_sorted.Count > shown)
        {
            _row.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(panel.x + 8f, y, w, lineH),
                $"+{_sorted.Count - shown} more", _row);
        }
    }

    private void EnsureStyles()
    {
        if (_bg == null)
        {
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, Color.white);
            _bg.Apply();
        }

        if (_row == null)
        {
            _row = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }
    }
}