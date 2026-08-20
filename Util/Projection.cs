using UnityEngine;

namespace Peeker.Util
{
    public static class Projection
    {
        /// <summary>
        /// World point -> GUI space (top-left origin, Screen pixels).
        /// False if the point is at or behind the camera plane.
        /// </summary>
        public static bool WorldToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            gui = default;

            // OpenGL-convention clip space: w == -z_eye, so w > 0 means "in front".
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            Vector4 clip = vp * new Vector4(world.x, world.y, world.z, 1f);
            if (clip.w <= 0.0001f) return false;

            float inv  = 1f / clip.w;
            float ndcX = clip.x * inv;   // -1 left .. +1 right
            float ndcY = clip.y * inv;   // -1 bottom .. +1 top

            gui = new Vector2(
                (ndcX * 0.5f + 0.5f) * Screen.width,
                (1f - (ndcY * 0.5f + 0.5f)) * Screen.height);
            return true;
        }
    }
}