using Peeker;
using Peeker.Module;
using Peeker.Settings;
using Peeker.Tracking;
using Peeker.Util;
using UnityEngine;
using UnityEngine.InputSystem;

public class ESPModule : Module
{
    private static Texture2D _px;

    private readonly Setting<float> _range;
    private readonly Setting<Color> _boxColour;
    private readonly Setting<float> _thickness;
    private readonly Setting<bool> _fill;
    private readonly Setting<float> _fillOpacity;
    private readonly Setting<bool> _fadeWithDistance;

    public ESPModule() : base("ESP", Key.G, ModuleCategory.Visual)
    {
        _range = Register(Setting.Range("Range", 150f, 0f, 500f))
            .WithDescription("Max distance to detect and draw entities. 0 = unlimited.");

        _boxColour = Register(Setting.Of("Box Color", Color.red))
            .WithDescription("Outline color for hostile entities.");

        _thickness = Register(Setting.Range("Thickness", 2f, 1f, 6f))
            .WithDescription("Outline thickness in pixels.");

        _fill = Register(Setting.Of("Fill", false))
            .WithDescription("Shade the inside of the box.");

        // The predicate is handed this setting's own value (Java's design), but
        // the lambda is free to ignore it and read a sibling setting instead.
        _fillOpacity = Register(Setting.Range("Fill Opacity", 0.15f, 0f, 1f))
            .WithVisibility(_ => _fill.Value)
            .WithDescription("How opaque the fill is.");

        _fadeWithDistance = Register(Setting.Of("Fade With Distance", false))
            .WithDescription("Far entities draw more transparent.");
    }

    protected override void OnEnabled()  => Plugin.Log.LogInfo("ESP on");
    protected override void OnDisabled() => Plugin.Log.LogInfo("ESP off");

    public override void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        var em = global::Peeker.Peeker.Entities;
        if (em == null || !em.InLevel) return;

        EnsurePixel();
        var cam = em.Camera;
        var camPos = cam.transform.position;

        float range = _range.Value;
        float rangeSq = range * range;
        float thickness = _thickness.Value;
        Color baseColour = _boxColour.Value;

        foreach (var e in em.OfKind(EntityKind.Hostile))
        {
            float distSq = (e.Position - camPos).sqrMagnitude;
            if (range > 0f && distSq > rangeSq) continue;

            Color colour = baseColour;

            if (_fadeWithDistance.Value && range > 0f)
            {
                // 1.0 at the camera, 0.25 at the edge of the configured range.
                float t = Mathf.Sqrt(distSq) / range;
                colour.a *= Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(t));
            }

            DrawBox(cam, e.Position, e.Height, e.Radius, colour, thickness);
        }
    }

    private static void EnsurePixel()
    {
        if (_px != null) return;

        _px = new Texture2D(1, 1);
        _px.SetPixel(0, 0, Color.white);
        _px.Apply();
        _px.hideFlags = HideFlags.HideAndDontSave;   // survives scene loads
    }

    private void DrawBox(Camera cam, Vector3 feet, float height, float radius, Color colour, float thickness)
    {
        var camTf  = cam.transform;
        var centre = feet + Vector3.up * (height * 0.5f);

        // Cheap reject before touching corners.
        if (Vector3.Dot(centre - camTf.position, camTf.forward) <= cam.nearClipPlane) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        int valid = 0;

        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3(
                feet.x + ((i & 1) == 0 ? -radius : radius),
                feet.y + ((i & 2) == 0 ? 0f : height),
                feet.z + ((i & 4) == 0 ? -radius : radius));

            if (!Projection.WorldToGui(cam, c, out var p)) continue;

            valid++;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        if (valid < 4) return;                     // straddling the near plane
        if (maxX - minX < 2f || maxY - minY < 2f) return;

        var rect = Rect.MinMaxRect(minX, minY, maxX, maxY);

        if (_fill.Value)
            FillRect(rect, colour, _fillOpacity.Value);

        Outline(rect, colour, thickness);
    }

    private void FillRect(Rect r, Color colour, float opacity)
    {
        var prev = GUI.color;
        GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * opacity);
        GUI.DrawTexture(r, _px);
        GUI.color = prev;
    }

    private void Outline(Rect r, Color colour, float t)
    {
        var prev = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _px);              // top
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _px);       // bottom
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _px);             // left
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _px);      // right
        GUI.color = prev;
    }
}