using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The arrival at the family parcel (GL583), played in the game scene after the tutorial throws the player out: the screen
    /// opens from black on the player lying in the back of a trade cart, looking up at the sky; the cart reaches the parcel and
    /// tips them out, then drives off; they land in the dirt with a thud and a jolt, lie rolled on their side, and get up.
    /// </summary>
    /// <remarks>
    /// Started by <see cref="Bootstrap"/> when <see cref="GameSession.ArrivesByCart"/> is set (never for a loaded game, so a load
    /// just places the player at the parcel). While it plays the <see cref="PlayerController"/> is switched off and its input
    /// locked, and <see cref="IsPlaying"/> stops saving; the cart's looks and path are <see cref="ArrivalCart"/>, its timings
    /// <see cref="ArrivalTimeline"/>. Space or Enter skips to standing at the parcel.
    /// </remarks>
    public class ArrivalCutscene : MonoBehaviour
    {
        private enum Phase
        {
            Waiting,
            Ride,
            Tip,
            Fall,
            Lie,
            Rise,
            DriveOff,
        }

        // Eye height above the ground when lying, and how far above the bed's top the eye lies in the cart.
        private const float LyingEye = 0.3f;
        private const float EyeOnBed = 1.05f;
        private const float BedBack = -0.35f;
        private const float LyingRoll = 80f;
        private const float DriveOffSeconds = 6f;

        private PlayerController player;
        private PlayerSpawn spawn;
        private WorldView worldView;
        private ArrivalCart cart;
        private Transform head;
        private Vector3 headBase;
        private float standingEye;
        private float yaw;
        private Vector3 stop;
        private Vector3 heading;

        private Phase phase = Phase.Waiting;
        private float phaseTime;
        private float sinceStart;
        private float sinceFall;
        private float cartAlong;
        private float cartSpeed;
        private float cartTip;

        // The eye, free of the cart while falling and lying.
        private Vector3 eye;
        private Vector3 fallVelocity;
        private float roll;
        private float pitch;

        private static AudioClip thud;

        /// <summary>True while the player is still in the cutscene (saving waits for it).</summary>
        public static bool IsPlaying { get; private set; }

        /// <summary>Starts the cutscene in a freshly loaded game scene after the tutorial.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!GameSession.ArrivesByCart)
            {
                return;
            }
            // Whatever happens, only the first scene after the tutorial plays it.
            GameSession.ArrivesByCart = false;
            if (!GameSession.IsActive)
            {
                return;
            }
            PlayerController found = FindFirstObjectByType<PlayerController>();
            if (found != null && found.TryGetComponent(out PlayerSpawn _))
            {
                new GameObject("Arrival cutscene").AddComponent<ArrivalCutscene>();
            }
        }

        private void Awake()
        {
            IsPlaying = true;
            player = FindFirstObjectByType<PlayerController>();
            spawn = player != null ? player.GetComponent<PlayerSpawn>() : null;
            worldView = FindFirstObjectByType<WorldView>();
            head = player != null ? player.Head : null;
        }

        private void OnDestroy()
        {
            IsPlaying = false;
            cart?.Destroy();
        }

        private void Update()
        {
            if (player == null || spawn == null || head == null)
            {
                Destroy(gameObject);
                return;
            }
            if (phase == Phase.Waiting)
            {
                // Black until the ground under the parcel is there.
                if (!spawn.IsPlaced)
                {
                    return;
                }
                Begin();
            }
            float dt = Time.deltaTime;
            phaseTime += dt;
            sinceStart += dt;
            if (phase != Phase.DriveOff && Skipped())
            {
                Finish(stop);
                return;
            }
            switch (phase)
            {
                case Phase.Ride:
                    UpdateRide();
                    break;
                case Phase.Tip:
                    UpdateTip();
                    break;
                case Phase.Fall:
                    UpdateFall(dt);
                    break;
                case Phase.Lie:
                    UpdateLie();
                    break;
                case Phase.Rise:
                    UpdateRise();
                    break;
                case Phase.DriveOff:
                    break;
            }
            DriveCart(dt);
        }

        // The player is placed on the parcel: that's where the cart will stop. Take the player out of the controller's hands.
        private void Begin()
        {
            stop = player.transform.position;
            yaw = player.transform.eulerAngles.y;
            heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            headBase = head.localPosition;
            standingEye = headBase.y;
            player.InputLocked = true;
            player.enabled = false;
            player.Body.enabled = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Material material = FindFirstObjectByType<TradeCart>() is TradeCart trade ? trade.LowPolyMaterial : null;
            if (material != null && worldView != null)
            {
                cart = new ArrivalCart(material, worldView, stop, heading);
                cartAlong = -ArrivalTimeline.ApproachMetres;
                cart.Place(cartAlong, 0f, true);
                SetPhase(Phase.Ride);
                UpdateRide();
            }
            else
            {
                // No cart to ride (a scene without a trade cart): just fall onto the parcel.
                eye = stop + Vector3.up * 1.2f;
                fallVelocity = Vector3.zero;
                roll = 0f;
                pitch = -90f;
                SetPhase(Phase.Fall);
            }
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private void UpdateRide()
        {
            cartAlong = ArrivalTimeline.RideAlong(phaseTime);
            cart.Place(cartAlong, 0f, true);
            // Lying on their back, looking at the sky, rocked by the ruts.
            float sway = Mathf.Sin(sinceStart * 3.1f) + 0.5f * Mathf.Sin(sinceStart * 7.3f);
            Look(OnBed(BedBack), LyingEye, -90f + sway * 1.5f, Mathf.Sin(sinceStart * 2.3f) * 2f);
            if (phaseTime >= ArrivalTimeline.RideSeconds)
            {
                SetPhase(Phase.Tip);
            }
        }

        private void UpdateTip()
        {
            float s = Mathf.Clamp01(phaseTime / ArrivalTimeline.TipSeconds);
            cartTip = ArrivalTimeline.TipDegrees * ArrivalTimeline.Smooth(s / 0.6f);
            cart.Place(0f, cartTip, false);
            // The load slides down the tilted bed towards the back.
            float slide = Mathf.Clamp01((s - 0.45f) / 0.55f);
            Look(OnBed(BedBack - 0.75f * slide * slide), LyingEye, -90f + 15f * slide, 8f * slide);
            if (s >= 1f)
            {
                eye = OnBed(BedBack - 0.75f);
                fallVelocity = cart.Bed.TransformDirection(Vector3.back) * 2.2f;
                roll = 8f;
                pitch = -75f;
                SetPhase(Phase.Fall);
            }
        }

        private void UpdateFall(float dt)
        {
            fallVelocity += Vector3.down * 9.81f * dt;
            eye += fallVelocity * dt;
            roll = Mathf.MoveTowards(roll, 25f, 40f * dt);
            pitch = Mathf.MoveTowards(pitch, -55f, 60f * dt);
            Look(eye, LyingEye, pitch, roll);
            float ground = GroundAt(eye);
            if (eye.y <= ground + LyingEye)
            {
                eye.y = ground + LyingEye;
                PlayThud(eye);
                SetPhase(Phase.Lie);
            }
        }

        private void UpdateLie()
        {
            // The jolt of landing, then the camera rolls onto its side as if lying on the ground.
            float settle = ArrivalTimeline.Smooth(phaseTime / 0.7f);
            float jolt = ArrivalTimeline.Jolt(phaseTime);
            Vector3 at = eye + Vector3.up * (0.07f * jolt);
            Look(at, LyingEye, Mathf.Lerp(-55f, -8f, settle) + 5f * jolt, Mathf.Lerp(25f, LyingRoll, settle) + 3f * jolt);
            if (phaseTime >= ArrivalTimeline.LieSeconds)
            {
                SetPhase(Phase.Rise);
            }
        }

        private void UpdateRise()
        {
            float s = Mathf.Clamp01(phaseTime / ArrivalTimeline.RiseSeconds);
            // The view rights itself first, and rises to standing height as it does.
            float righting = ArrivalTimeline.Smooth(s / 0.7f);
            float rising = ArrivalTimeline.Smooth((s - 0.25f) / 0.75f);
            float eyeHeight = Mathf.Lerp(LyingEye, standingEye, rising);
            Vector3 at = new Vector3(eye.x, GroundAt(eye) + eyeHeight, eye.z);
            Look(at, eyeHeight, Mathf.Lerp(-8f, 0f, righting), Mathf.Lerp(LyingRoll, 0f, righting));
            if (s >= 1f)
            {
                Finish(new Vector3(eye.x, GroundAt(eye), eye.z));
            }
        }

        // The cart tips back flat and drives off once the player is out of it.
        private void DriveCart(float dt)
        {
            if (cart == null)
            {
                return;
            }
            if (phase >= Phase.Fall)
            {
                sinceFall += dt;
                float back = Mathf.Clamp01(sinceFall / 0.9f);
                cartTip = ArrivalTimeline.TipDegrees * (1f - ArrivalTimeline.Smooth(back));
                cartSpeed = Mathf.MoveTowards(cartSpeed, 5f, 2.5f * dt * (back > 0.5f ? 1f : 0f));
                cartAlong += cartSpeed * dt;
                cart.Place(cartAlong, cartTip, cartSpeed > 0.1f);
                if (phase == Phase.DriveOff && phaseTime >= DriveOffSeconds)
                {
                    Destroy(gameObject);
                }
            }
        }

        private Vector3 OnBed(float z)
        {
            return cart.Bed.TransformPoint(new Vector3(0f, EyeOnBed, z));
        }

        // The ground's height under a point, from the cart's ray when there is one.
        private float GroundAt(Vector3 at)
        {
            if (cart != null)
            {
                return cart.GroundHeight(at);
            }
            var ray = new Ray(at + Vector3.up * 30f, Vector3.down);
            return worldView != null && worldView.Raycast(ray, out Vector3 hit) ? hit.y : stop.y;
        }

        // Puts the view at a world point, the head at a height above the feet, pitched and rolled.
        private void Look(Vector3 eyeAt, float eyeHeight, float pitchDegrees, float rollDegrees)
        {
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localPosition = new Vector3(headBase.x, eyeHeight, headBase.z);
            player.transform.position = eyeAt - Vector3.up * eyeHeight;
            head.localRotation = Quaternion.Euler(pitchDegrees, 0f, rollDegrees);
        }

        private static bool Skipped()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        // Stands the player at a point on the ground and hands control back. The cart drives off (or is removed, when skipped).
        private void Finish(Vector3 feet)
        {
            bool skipped = phase != Phase.Rise;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localPosition = headBase;
            head.localRotation = Quaternion.identity;
            player.Body.enabled = true;
            player.Teleport(new Vector3(feet.x, feet.y + 0.05f, feet.z));
            player.InputLocked = false;
            // Enabling it reads the heading and pitch back from the transforms set above, and locks the cursor again.
            player.enabled = true;
            IsPlaying = false;
            if (skipped || cart == null)
            {
                Destroy(gameObject);
                return;
            }
            SetPhase(Phase.DriveOff);
        }

        private static void PlayThud(Vector3 at)
        {
            if (thud == null)
            {
                float[] samples = SoundSynth.Thud(583);
                thud = AudioClip.Create("Arrival thud", samples.Length, 1, SoundSynth.SampleRate, false);
                thud.SetData(samples, 0);
            }
            AudioSource.PlayClipAtPoint(thud, at, SoundSettings.Gain(AudioChannel.Effects));
        }

        private void OnGUI()
        {
            float black = phase == Phase.Waiting ? 1f : ArrivalTimeline.Black(sinceStart);
            if (black <= 0f && phase != Phase.Ride)
            {
                return;
            }
            // Over the HUDs and the pause popup.
            GUI.depth = -200;
            Color previous = GUI.color;
            if (black > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, black);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            if (IsPlaying && phase != Phase.Waiting)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.Label(new Rect(Screen.width - 220f, Screen.height - 30f, 210f, 24f), "Space: skip");
            }
            GUI.color = previous;
        }
    }
}
