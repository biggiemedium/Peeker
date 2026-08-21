using UnityEngine;

namespace Peeker.HUD.Elements
{
    [HudElement("Watermark",
        Description = "Client name and version.",
        X = 6f, Y = 6f)]
    public class WatermarkElement : HudElement
    {
        private const float PaddingX = 6f;
        private const float PaddingY = 3f;

        private string Text
        {
            get { return "Peeker <color=#7FD5FF>1.0.0</color>"; }
        }

        public override Vector2 Measure()
        {
            Vector2 text = HudRenderer.MeasureText(Text);
            return new Vector2(text.x + PaddingX * 2f, text.y + PaddingY * 2f);
        }

        public override void Render()
        {
            Rect b = Position.Bounds;
            HudRenderer.DrawRect(b, new Color(0f, 0f, 0f, 0.45f));
            HudRenderer.DrawText(new Vector2(b.x + PaddingX, b.y + PaddingY), Text, Color.white);
        }
    }
}