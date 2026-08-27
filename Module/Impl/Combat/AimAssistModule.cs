using System;
using GameNetcodeStuff;
using HarmonyLib;
using Peeker.Settings;
using Peeker.Tracking;
using Peeker.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Peeker.Module.Impl.Combat
{
    /// <summary>Which tracked kinds AimAssist is allowed to lock on to.</summary>
    public enum AimTargets
    {
        Hostiles,
        Players,
        Both
    }

    /// <summary>Tie-break used when more than one candidate passes the gates.</summary>
    public enum AimPriority
    {
        Crosshair,
        Distance
    }

    /// <summary>
    /// Steers the local player's view onto the best entity that
    /// <see cref="EntityManager"/> is already tracking, so this module never runs
    /// a scene sweep of its own.
    ///
    /// The game splits looking across two places: yaw lives on
    /// <c>thisPlayerBody</c>, and pitch lives in the private <c>cameraUp</c>
    /// accumulator which is copied onto <c>gameplayCamera.localEulerAngles.x</c>.
    /// Both are rewritten from <c>PlayerControllerB.LateUpdate</c>, which is why
    /// this hooks the tail of LateUpdate — a postfix on Update would be stomped
    /// by look input later in the same frame.
    /// </summary>
    public class AimAssistModule : Module
    {
        internal static bool Active;

        // The Harmony hook is static, so it needs a way back to the live
        // instance's settings. The registry only ever constructs one of these.
        private static AimAssistModule _instance;

        // cameraUp is private on PlayerControllerB, so it can't be assigned
        // directly. Bound once as a field ref rather than reflected per frame.
        private static readonly AccessTools.FieldRef<PlayerControllerB, float> CameraUp =
            AccessTools.FieldRefAccess<PlayerControllerB, float>("cameraUp");

        // Matches the clamp inside CalculateNormalLookingInput. Writing outside
        // it would be snapped back the moment the player moves the mouse.
        private const float PitchClamp = 80f;

        private static Texture2D _px;

        private readonly Setting<AimTargets> _targets;
        private readonly Setting<AimPriority> _priority;
        private readonly Setting<float> _range;
        private readonly Setting<float> _fov;
        private readonly Setting<float> _speed;
        private readonly Setting<float> _aimHeight;
        private readonly Setting<bool> _requireLineOfSight;
        private readonly Setting<bool> _sticky;
        private readonly Setting<bool> _holdToAim;
        private readonly Setting<bool> _showTarget;
        private readonly Setting<Color> _markerColour;

        // Written by the patch, read by OnGUI on the same frame.
        private TrackedEntity _target;
        private Vector3 _aimPoint;

        // The sticky lock is keyed on the transform, not the TrackedEntity: the
        // manager throws its entity objects away and rebuilds them on every
        // rescan, so holding the reference would drop the lock once a second.
        private int _stickyId;

        public AimAssistModule() : base("AimAssist", Key.X, ModuleCategory.Combat)
        {
            _targets = Register(Setting.Of("Targets", AimTargets.Hostiles)
                .WithDescription("Which tracked entities are eligible for a lock."));

            _priority = Register(Setting.Of("Priority", AimPriority.Crosshair)
                .WithDescription("Crosshair takes the smallest angle off centre; Distance takes the nearest."));

            _range = Register(Setting.Range("Range", 45f, 5f, 150f)
                .WithDescription("Maximum distance to a target, in metres."));

            _fov = Register(Setting.Range("FOV", 70f, 5f, 180f)
                .WithDescription("Full width of the cone around the crosshair that a target must sit inside."));

            _speed = Register(Setting.Range("Speed", 10f, 1f, 30f)
                .WithDescription("How hard the view is pulled onto the target. Low values look human."));

            _aimHeight = Register(Setting.Range("Aim Height", 0.8f, 0f, 1f)
                .WithDescription("Point on the target to aim at, as a fraction of its height. 0 is the feet."));

            _requireLineOfSight = Register(Setting.Of("Require Line Of Sight", true)
                .WithDescription("Ignore targets with geometry between them and your eye."));

            _sticky = Register(Setting.Of("Sticky Target", true)
                .WithDescription("Hold the current target until it dies or leaves the cone, instead of re-picking every frame."));

            _holdToAim = Register(Setting.Of("Hold To Aim", false)
                .WithDescription("Only steer while the right mouse button is held. Targeting still runs, so the marker stays live."));

            _showTarget = Register(Setting.Of("Show Target", true)
                .WithDescription("Draw a marker on the locked target."));

            // The predicate is handed this setting's own value (Java's design),
            // but the lambda is free to ignore it and read a sibling instead.
            _markerColour = Register(Setting.Of("Marker Color", new Color(1f, 0.32f, 0.32f))
                .WithVisibility(_ => _showTarget.Value)
                .WithDescription("Colour of the lock marker."));

            _instance = this;
        }

        // Tells Module which patch container to apply on enable / unpatch on
        // disable. Without this the postfix below is never installed.
        protected override Type[] PatchTypes => new[] { typeof(AimAssistPatch) };

        protected override void OnEnabled()
        {
            Active = true;
            Plugin.Log.LogInfo("AimAssist on");
        }

        protected override void OnDisabled()
        {
            Active = false;
            _target = null;
            _stickyId = 0;
            Plugin.Log.LogInfo("AimAssist off");
        }

        public override void Update()
        {
            // the patch drives everything so the writes land in the same frame
            // the game reads them
        }

        public override void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!_showTarget.Value || _target == null || !_target.Alive) return;

            EntityManager entities = global::Peeker.Peeker.Entities;
            if (entities == null || !entities.InLevel) return;

            if (!Projection.WorldToGui(entities.Camera, _aimPoint, out Vector2 gui)) return;

            DrawMarker(gui, _markerColour.Value);
        }

        // ---- Aiming ---------------------------------------------------------

        // runs at the tail of PlayerControllerB.LateUpdate
        internal static void Steer(PlayerControllerB player)
        {
            AimAssistModule self = _instance;
            if (self == null || player == null)
                return;

            self._target = null;

            Camera camera = player.gameplayCamera;
            if (camera == null || !self.CanAim(player))
            {
                self._stickyId = 0;
                return;
            }

            TrackedEntity target = self.PickTarget(player, camera);
            if (target == null)
            {
                self._stickyId = 0;
                return;
            }

            self._target = target;
            self._stickyId = target.Transform.GetInstanceID();
            self._aimPoint = AimPointOf(target, self._aimHeight.Value);

            if (!self._holdToAim.Value || IsAimHeld())
                self.ApplyLook(player, camera, self._aimPoint);
        }

        private void ApplyLook(PlayerControllerB player, Camera camera, Vector3 aimPoint)
        {
            Vector3 direction = aimPoint - camera.transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            direction.Normalize();

            // Framerate independent, and clamped so a long frame can't overshoot
            // into a visible snap.
            float t = Mathf.Clamp01(Time.deltaTime * _speed.Value);

            // Yaw is owned by thisPlayerBody. Only Y is touched: the body also
            // carries pitch and roll from ladders and special animations.
            Transform body = player.thisPlayerBody;
            if (body != null)
            {
                float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                Vector3 euler = body.eulerAngles;
                body.eulerAngles = new Vector3(euler.x, Mathf.LerpAngle(euler.y, desiredYaw, t), euler.z);
            }

            // Pitch has to go through cameraUp. Writing the camera transform
            // alone would be undone next frame, because look input rebuilds the
            // camera angle from that field every tick. Positive is downward,
            // hence the negated Y.
            float desiredPitch = Mathf.Asin(Mathf.Clamp(-direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(Mathf.LerpAngle(CameraUp(player), desiredPitch, t), -PitchClamp, PitchClamp);
            CameraUp(player) = pitch;

            Vector3 cameraEuler = camera.transform.localEulerAngles;
            camera.transform.localEulerAngles = new Vector3(pitch, cameraEuler.y, cameraEuler.z);
        }

        // ---- Target selection ----------------------------------------------

        private TrackedEntity PickTarget(PlayerControllerB player, Camera camera)
        {
            EntityManager entities = global::Peeker.Peeker.Entities;
            if (entities == null || !entities.InLevel)
                return null;

            Vector3 eye = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            float range = _range.Value;
            float halfFov = _fov.Value * 0.5f;
            bool byAngle = _priority.Value == AimPriority.Crosshair;

            TrackedEntity best = null;
            float bestScore = float.MaxValue;

            foreach (TrackedEntity entity in entities.All)
            {
                if (!Eligible(entity))
                    continue;

                Vector3 point = AimPointOf(entity, _aimHeight.Value);
                Vector3 offset = point - eye;

                float distance = offset.magnitude;
                if (distance < 0.001f || distance > range)
                    continue;

                float angle = Vector3.Angle(forward, offset);
                if (angle > halfFov)
                    continue;

                if (_requireLineOfSight.Value && !Sight.Clear(player, point))
                    continue;

                // A still-valid sticky target wins outright, so the lock only
                // moves once the current one actually stops qualifying.
                if (_sticky.Value && entity.Transform.GetInstanceID() == _stickyId)
                    return entity;

                float score = byAngle ? angle : distance;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = entity;
            }

            return best;
        }

        private bool Eligible(TrackedEntity entity)
        {
            if (entity == null || !entity.Alive)
                return false;

            switch (_targets.Value)
            {
                case AimTargets.Hostiles:
                    return entity.Kind == EntityKind.Hostile;
                case AimTargets.Players:
                    return entity.Kind == EntityKind.Player;
                default:
                    return entity.Kind == EntityKind.Hostile || entity.Kind == EntityKind.Player;
            }
        }

        private bool CanAim(PlayerControllerB player)
        {
            if (player.isPlayerDead || !player.isPlayerControlled)
                return false;

            // Anything where the game has taken the camera off the player: it is
            // animating the view itself and fighting that just looks broken.
            if (player.inSpecialInteractAnimation || player.isClimbingLadder || player.inShockingMinigame)
                return false;

            if (player.isTypingChat || player.inTerminalMenu || player.isFreeCamera)
                return false;

            if (player.quickMenuManager != null && player.quickMenuManager.isMenuOpen)
                return false;

            return !global::Peeker.Peeker.MenuOpen;
        }

        private static Vector3 AimPointOf(TrackedEntity entity, float heightFraction)
        {
            return entity.Position + Vector3.up * (entity.Height * heightFraction);
        }

        private static bool IsAimHeld()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        // ---- Marker ---------------------------------------------------------

        private static void DrawMarker(Vector2 centre, Color colour)
        {
            const float Half = 11f;
            const float Arm = 5f;
            const float Thickness = 2f;

            EnsurePixel();

            Color previous = GUI.color;
            GUI.color = colour;

            float left = centre.x - Half;
            float right = centre.x + Half;
            float top = centre.y - Half;
            float bottom = centre.y + Half;

            // Four corner brackets rather than a closed box, so the target itself
            // stays readable underneath.
            Bar(left, top, Arm, Thickness);
            Bar(left, top, Thickness, Arm);

            Bar(right - Arm, top, Arm, Thickness);
            Bar(right - Thickness, top, Thickness, Arm);

            Bar(left, bottom - Thickness, Arm, Thickness);
            Bar(left, bottom - Arm, Thickness, Arm);

            Bar(right - Arm, bottom - Thickness, Arm, Thickness);
            Bar(right - Thickness, bottom - Arm, Thickness, Arm);

            GUI.color = previous;
        }

        private static void Bar(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), _px);
        }

        private static void EnsurePixel()
        {
            if (_px != null) return;

            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
            _px.hideFlags = HideFlags.HideAndDontSave;   // survives scene loads
        }

        /// <summary>
        /// LateUpdate is the frame's last word on where the player is looking:
        /// it calls PlayerLookInput, which rewrites both the body yaw and the
        /// camera pitch. Running after it is the only way a steer survives.
        /// </summary>
        [HarmonyPatch(typeof(PlayerControllerB))]
        private static class AimAssistPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("LateUpdate")]
            private static void Postfix(PlayerControllerB __instance)
            {
                if (!Active)
                    return;

                StartOfRound round = StartOfRound.Instance;
                if (round == null || __instance != round.localPlayerController)
                    return;

                Steer(__instance);
            }
        }
    }
}
