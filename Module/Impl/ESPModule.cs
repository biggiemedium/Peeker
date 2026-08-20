using Peeker;
using Peeker.Module;
using Peeker.Tracking;
using Peeker.Util;
using UnityEngine;
using UnityEngine.InputSystem;

public class ESPModule : Module
{
    private static Texture2D _px;

    public ESPModule() : base("ESP", Key.G, ModuleCategory.Visual) { }

    protected override void OnEnabled()  => Plugin.Log.LogInfo("ESP on");
    protected override void OnDisabled() => Plugin.Log.LogInfo("ESP off");

    public override void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        var em = global::Peeker.Peeker.Entities;
        if (em == null || !em.InLevel) return;

        EnsurePixel();
        var cam = em.Camera;

        foreach (var e in em.OfKind(EntityKind.Hostile))
            DrawBox(cam, e.Position, e.Height, e.Radius, Color.red);
    }

    private static void EnsurePixel()
    {
        if (_px != null) return;

        _px = new Texture2D(1, 1);
        _px.SetPixel(0, 0, Color.white);
        _px.Apply();
        _px.hideFlags = HideFlags.HideAndDontSave;   // survives scene loads
    }

    private static readonly Vector2[] _corners = new Vector2[8];

    private void DrawBox(Camera cam, Vector3 feet, float height, float radius, Color colour)
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

        Outline(Rect.MinMaxRect(minX, minY, maxX, maxY), colour, 2f);
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