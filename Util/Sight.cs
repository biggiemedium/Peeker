using GameNetcodeStuff;
using UnityEngine;

namespace Peeker.Util
{
    /// <summary>
    /// Line-of-sight tests taken from the local player's eye.
    ///
    /// <c>PlayerControllerB.HasLineOfSightToPosition</c> runs the same linecast,
    /// but it stores the result in <c>PlayerControllerB.hit</c> — a shared field
    /// the interact code reads later in the same frame — so borrowing it from a
    /// mod quietly clobbers whatever the player was hovering over.
    /// </summary>
    public static class Sight
    {
        /// <summary>
        /// True when nothing solid sits between the player's eye and the point.
        /// Answers true when the round isn't up yet, so callers gate on the
        /// entity manager rather than on this.
        /// </summary>
        public static bool Clear(PlayerControllerB player, Vector3 point)
        {
            if (player == null)
                return false;

            StartOfRound round = StartOfRound.Instance;
            if (round == null)
                return true;

            // playerEye is the vanilla origin for visibility checks; the camera
            // is only a fallback for the frames before the rig is wired up.
            Transform eye = player.playerEye != null
                ? player.playerEye
                : (player.gameplayCamera != null ? player.gameplayCamera.transform : player.transform);

            return !Physics.Linecast(eye.position, point, out _,
                round.collidersRoomDefaultAndFoliage, QueryTriggerInteraction.Ignore);
        }
    }
}
