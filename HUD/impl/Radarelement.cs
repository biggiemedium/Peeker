using System.Collections.Generic;
using Peeker.Tracking;
using UnityEngine;
using Core = Peeker.Peeker;

namespace Peeker.HUD.Elements
{
    /// <summary>
    /// Square top-down radar, in the spirit of the ship monitor: the local player
    /// sits at the centre and everything the <see cref="EntityManager"/> is already
    /// tracking is plotted around them, hostiles as red dots.
    ///
    /// Cost per frame is one flatten of the camera basis plus two dot products per
    /// tracked entity - no scene sweeps (the entity manager owns those), no trig,
    /// no per-frame allocation. The blip list keeps its capacity between frames and
    /// the dot texture is built once for the session.
    /// </summary>
    [HudElement("Radar",
        Description = "Top-down radar. Hostiles are red dots, plotted relative to where you are facing.",
        X = 12f, Y = 320f,
        Width = DefaultSize, Height = DefaultSize)]
    public class RadarElement : HudElement
    {
        public const float DefaultSize = 180f;

        // ---- tunables ---------------------------------------------------
        // Plain fields rather than Setting<T>: HudElement is not a SettingsHolder,
        // so there is nothing to bind them to yet. When the menu learns about HUD
        // elements these become Register(Setting.Range(...)) one for one.

        /// <summary>Radius of the level the radar covers, in metres.</summary>
        public float Range = 40f;

        /// <summary>Side length of the square, in pixels.</summary>
        public float Size = DefaultSize;

        /// <summary>True: up is where you are looking. False: up is world north (+Z).</summary>
        public bool RotateWithView = true;

        /// <summary>Plot teammates as well, in blue.</summary>
        public bool ShowTeammates = false;

        /// <summary>Dim and shrink entities that are on another floor.</summary>
        public bool FadeByHeight = true;

        /// <summary>Draw the hostile count and the range readout.</summary>
        public bool ShowLabels = true;

        // ---- constants --------------------------------------------------

        // Hard ceiling on plotted dots. A full level never gets near this; it only
        // exists so a runaway spawn cannot turn the radar into a thousand draws.
        private const int MaxBlips = 64;

        private const float DotSize = 7f;
        private const float SelfDotSize = 5f;

        // Vertical slack treated as "same floor", and how far past it the fade runs
        // out. Interior levels stack roughly 4-5m apart.
        private const float SameFloorSlack = 2.5f;
        private const float FloorFadeSpan = 10f;

        private static readonly Color Background = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Frame = new Color(1f, 1f, 1f, 0.30f);
        private static readonly Color Grid = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color HostileColour = new Color(1.00f, 0.25f, 0.25f);
        private static readonly Color TeammateColour = new Color(0.30f, 0.80f, 1.00f);
        private static readonly Color SelfColour = new Color(1f, 1f, 1f, 0.95f);

        private struct Blip
        {
            public Vector2 Offset;   // pixels from the centre of the square
            public Color Colour;
            public float Size;
        }

        private readonly List<Blip> _blips = new List<Blip>(MaxBlips);
        private bool _hasData;
        private int _hostileCount;

        private static Texture2D _dot;
        private GUIStyle _label;

        protected override void OnEnable()
        {
            Plugin.Log.LogInfo("Radar on");
        }

        protected override void OnDisable()
        {
            _blips.Clear();
            _hasData = false;
            _hostileCount = 0;
            Plugin.Log.LogInfo("Radar off");
        }

        public override Vector2 Measure()
        {
            return new Vector2(Size, Size);
        }

        /// <summary>
        /// All of the maths happens here, once per frame, so the Repaint pass is
        /// nothing but texture draws - and a frame that repaints twice does not pay
        /// for the projection twice.
        /// </summary>
        public override void Update()
        {
            _blips.Clear();
            _hasData = false;
            _hostileCount = 0;

            EntityManager em = Core.Entities;
            if (em == null || !em.InLevel) return;

            Vector3 origin = em.LocalPlayer.transform.position;

            Vector3 forward, right;
            Basis(em.Camera.transform, out forward, out right);

            float half = Size * 0.5f;
            float scale = half / Mathf.Max(1f, Range);
            float rangeSq = Range * Range;

            // Dots are drawn from their centre, so this is how far a centre may sit
            // from the middle before the dot pokes out through the frame.
            float limit = half - DotSize * 0.5f - 1f;

            IReadOnlyList<TrackedEntity> all = em.All;

            // Indexed loop over All rather than foreach over OfKind(): that iterator
            // allocates an enumerator, and this runs every frame.
            for (int i = 0; i < all.Count; i++)
            {
                TrackedEntity e = all[i];
                if (e == null || !e.Alive) continue;

                Color colour;
                if (e.Kind == EntityKind.Hostile) colour = HostileColour;
                else if (e.Kind == EntityKind.Player && ShowTeammates) colour = TeammateColour;
                else continue;

                Vector3 d = e.Position - origin;

                // Flat distance: something two floors up is still "20m away" on a
                // top-down radar, and its height only changes how it is drawn.
                if (d.x * d.x + d.z * d.z > rangeSq) continue;

                if (e.Kind == EntityKind.Hostile) _hostileCount++;
                if (_blips.Count >= MaxBlips) continue;

                // forward.y and right.y are zero, so these two dots are already the
                // top-down projection - d never has to be flattened separately.
                float x = (d.x * right.x + d.z * right.z) * scale;
                float y = (d.x * forward.x + d.z * forward.z) * scale;

                float alpha = 1f;
                float size = DotSize;

                if (FadeByHeight)
                {
                    float t = Mathf.Clamp01((Mathf.Abs(d.y) - SameFloorSlack) / FloorFadeSpan);
                    alpha = Mathf.Lerp(1f, 0.35f, t);
                    size = Mathf.Lerp(DotSize, DotSize * 0.65f, t);
                }

                colour.a = alpha;

                _blips.Add(new Blip
                {
                    // GUI y grows downward, so forward has to be negated.
                    Offset = new Vector2(
                        Mathf.Clamp(x, -limit, limit),
                        Mathf.Clamp(-y, -limit, limit)),
                    Colour = colour,
                    Size = size
                });
            }

            _hasData = true;
        }

        public override void Render()
        {
            if (!_hasData) return;

            Rect box = Position.Bounds;
            Vector2 centre = box.center;

            DrawFrame(box);

            for (int i = 0; i < _blips.Count; i++)
            {
                Blip b = _blips[i];
                DrawDot(centre + b.Offset, b.Size, b.Colour);
            }

            DrawSelf(centre);

            if (ShowLabels) DrawLabels(box, _hostileCount);
        }

        /// <summary>
        /// Edit mode runs on the menu screen where there is no level, so the frame
        /// gets stand-in blips rather than being an empty square you cannot judge
        /// the placement of.
        /// </summary>
        public override void RenderDummy()
        {
            if (_hasData)
            {
                Render();
                return;
            }

            Rect box = Position.Bounds;
            Vector2 centre = box.center;
            float half = Size * 0.5f;

            DrawFrame(box);

            // Fixed fractions of the box so the preview does not jitter frame to
            // frame the way Random would.
            DrawDot(centre + new Vector2(half * 0.35f, -half * 0.50f), DotSize, HostileColour);
            DrawDot(centre + new Vector2(-half * 0.60f, half * 0.20f), DotSize, HostileColour);
            DrawDot(centre + new Vector2(half * 0.15f, half * 0.65f), DotSize * 0.7f,
                new Color(HostileColour.r, HostileColour.g, HostileColour.b, 0.45f));

            DrawSelf(centre);

            if (ShowLabels) DrawLabels(box, 2);
        }

        // ---- drawing ----------------------------------------------------

        private void DrawFrame(Rect box)
        {
            HudRenderer.DrawRect(box, Background);

            Vector2 c = box.center;

            // Cross through the middle plus the half-range square: all the scale
            // reference a square radar needs.
            HudRenderer.DrawRect(new Rect(box.x, c.y, box.width, 1f), Grid);
            HudRenderer.DrawRect(new Rect(c.x, box.y, 1f, box.height), Grid);

            float quarter = box.width * 0.25f;
            HudRenderer.DrawBorder(
                new Rect(box.x + quarter, box.y + quarter, box.width * 0.5f, box.height * 0.5f),
                Grid);

            HudRenderer.DrawBorder(box, Frame);
        }

        private void DrawSelf(Vector2 centre)
        {
            // Tick pointing whichever way the radar treats as "up", so the display
            // still reads when it is not the view being rotated.
            HudRenderer.DrawRect(
                new Rect(centre.x - 1f, centre.y - SelfDotSize * 2f, 2f, SelfDotSize * 1.6f),
                new Color(1f, 1f, 1f, 0.45f));

            DrawDot(centre, SelfDotSize, SelfColour);
        }

        private static void DrawDot(Vector2 centre, float size, Color colour)
        {
            Texture2D dot = DotTexture;
            Color prev = GUI.color;

            // Dark backing so a red dot stays readable over a light patch of HUD.
            float outer = size + 2f;
            GUI.color = new Color(0f, 0f, 0f, colour.a * 0.55f);
            GUI.DrawTexture(
                new Rect(centre.x - outer * 0.5f, centre.y - outer * 0.5f, outer, outer), dot);

            GUI.color = colour;
            GUI.DrawTexture(
                new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), dot);

            GUI.color = prev;
        }

        private void DrawLabels(Rect box, int hostiles)
        {
            EnsureStyles();

            _label.alignment = TextAnchor.UpperLeft;
            _label.normal.textColor = hostiles > 0
                ? HostileColour
                : new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(new Rect(box.x + 5f, box.y + 3f, 60f, 14f), hostiles.ToString(), _label);

            _label.alignment = TextAnchor.LowerRight;
            _label.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(new Rect(box.xMax - 65f, box.yMax - 17f, 60f, 14f),
                Mathf.RoundToInt(Range) + "m", _label);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        // ---- basis ------------------------------------------------------

        /// <summary>
        /// Flattened camera basis: one normalize per frame, and the caller gets to
        /// treat both vectors as pure XZ.
        /// </summary>
        private void Basis(Transform cam, out Vector3 forward, out Vector3 right)
        {
            if (!RotateWithView)
            {
                forward = Vector3.forward;
                right = Vector3.right;
                return;
            }

            Vector3 f = cam.forward;
            forward = new Vector3(f.x, 0f, f.z);

            // Looking straight up or down flattens forward to nothing; the camera's
            // up vector points along the view direction there.
            if (forward.sqrMagnitude < 1e-4f)
            {
                Vector3 u = cam.up;
                forward = new Vector3(u.x, 0f, u.z);
            }

            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
                right = Vector3.right;
                return;
            }

            forward.Normalize();
            right = new Vector3(forward.z, 0f, -forward.x);   // cross(up, forward)
        }

        // ---- dot texture ------------------------------------------------

        /// <summary>
        /// A soft-edged circle, built once and kept for the session. Worth it over a
        /// 1x1 white pixel: square blips do not read as a radar, and this still costs
        /// one bilinear quad per dot.
        /// </summary>
        private static Texture2D DotTexture
        {
            get
            {
                if (_dot != null) return _dot;

                const int res = 32;
                const float r = res * 0.5f;

                _dot = new Texture2D(res, res, TextureFormat.ARGB32, false);
                _dot.filterMode = FilterMode.Bilinear;
                _dot.wrapMode = TextureWrapMode.Clamp;
                _dot.hideFlags = HideFlags.HideAndDontSave;   // survives scene loads

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);

                        // A pixel of feather at the rim kills the staircase edge
                        // without needing a bigger texture.
                        float a = Mathf.Clamp01(r - 0.5f - d);
                        _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                _dot.Apply();
                return _dot;
            }
        }
    }
}
