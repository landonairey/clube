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
    public class ArrivalCutscene : MonoBehaviour, IGuiDrawer
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
        private const float RidePitch = 22f;
        private const float DriveOffSeconds = 6f;

        // GL743: the ride waits under the loading line until frames come evenly (the world is still settling just after the
        // player is placed), for at most this long; a slow frame never skips more of the animation than the longest step.
        private const float CalmFrameSeconds = 0.05f;
        private const int CalmFrames = 20;
        private const float MostSettlingSeconds = 8f;
        private const float LongestStep = 0.12f;

        // GL744: metres outside the family's parcel the cart may drop the player, nearest first, the spacing of the places
        // tried along its sides, and the most the ground there may differ.
        private static readonly float[] DropOutside = { 6f, 10f, 14f };
        private const float DropSpacing = 4f;
        private const float DropMaxRise = 4f;

        // GL766: metres round the drop kept free of trees, and how high that is checked. GL765: the eye's height, the steps of
        // the look towards the parcel's arrow, and the most the player is left looking up at it.
        private const float DropClearHalfWidth = 3f;
        private const float DropClearHeight = 44f;
        private const float DropEyeHeight = 1.6f;
        private const float SightStep = 2f;
        private const float MostLookUp = 35f;

        private PlayerController player;
        private PlayerSpawn spawn;
        private WorldView worldView;
        private ArrivalCart cart;
        private Transform head;
        private Vector3 headBase;
        private float standingEye;
        private float yaw;
        private float endPitch;
        private Vector3 stop;
        private Vector3 heading;

        private Phase phase = Phase.Waiting;
        private float phaseTime;
        private float sinceStart;
        private float sinceFall;
        private float settling;
        private int calmFrames;
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

        /// <summary>
        /// Hooks scene loads once per play session (GL588). <c>AfterSceneLoad</c> alone runs only for the first scene, so the
        /// game scene loaded after the tutorial never checked <see cref="GameSession.ArrivesByCart"/>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
            {
                Bootstrap();
            }
        }

        /// <summary>Starts the cutscene in a freshly loaded game scene after the tutorial.</summary>
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
            // The player is still switched off here: PlayerCameraToggle.Start switches it on after scene load (GL619).
            PlayerController found = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (found != null && found.TryGetComponent(out PlayerSpawn _))
            {
                new GameObject("Arrival cutscene").AddComponent<ArrivalCutscene>();
            }
        }

        private void Awake()
        {
            IsPlaying = true;
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
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
                if (!spawn.IsPlaced || !Settled())
                {
                    return;
                }
                Begin();
            }
            float dt = Mathf.Min(Time.deltaTime, LongestStep);
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

        // True once the family's parcel is granted (where the player landed) and frames come evenly, or the wait is up.
        private bool Settled()
        {
            settling += Time.unscaledDeltaTime;
            if (settling >= MostSettlingSeconds)
            {
                return true;
            }
            PlayerParcels parcels = player.GetComponent<PlayerParcels>();
            if (parcels != null && parcels.enabled && (parcels.Parcels == null || parcels.Parcels.Owned.Count == 0))
            {
                return false;
            }
            calmFrames = Time.unscaledDeltaTime <= CalmFrameSeconds ? calmFrames + 1 : 0;
            return calmFrames >= CalmFrames;
        }

        // GL744: the cart stops just outside the parcel, facing it, so the first thing to do is walk onto the land.
        private void ChooseStop()
        {
            stop = player.transform.position;
            yaw = player.transform.eulerAngles.y;
            PlayerParcels parcels = player.GetComponent<PlayerParcels>();
            MapSession session = MapSession.Current;
            if (parcels == null || !parcels.enabled || parcels.Parcels == null || worldView == null || session == null || session.Map == null)
            {
                return;
            }
            LandParcels land = parcels.Parcels;
            Vector3 local = worldView.transform.InverseTransformPoint(stop);
            Vector2Int parcel = land.ParcelAt(local);
            // The land's own height: the player may have been set down on top of one of the parcel's trees.
            float here = session.Map.Height(local.x, local.z);
            Vector3 tip = worldView.transform.InverseTransformPoint(ParcelArrowSpot.TipOver(worldView, land, parcel, here));
            var points = new System.Collections.Generic.List<Vector2>();
            foreach (float margin in DropOutside)
            {
                points.AddRange(land.PointsAround(parcel, new Vector2(local.x, local.z), margin, DropSpacing));
            }
            TreeField trees = worldView.Trees;
            int best = ArrivalDropSpot.Pick(points.Count,
                i => Mathf.Abs(session.Map.Height(points[i].x, points[i].y) - here) <= DropMaxRise && !IsWet(points[i]),
                i => GroundIsClear(trees, points[i], session.Map.Height(points[i].x, points[i].y)),
                i => ViewIsClear(trees, points[i], session.Map.Height(points[i].x, points[i].y), tip));
            if (best < 0)
            {
                // Nowhere round the parcel to stop: on the land itself, under any tree the player was set down on.
                stop = worldView.transform.TransformPoint(new Vector3(local.x, here, local.z));
                player.Teleport(stop + Vector3.up * 0.05f);
                return;
            }
            Vector2 point = points[best];
            float height = session.Map.Height(point.x, point.y);
            stop = worldView.transform.TransformPoint(new Vector3(point.x, height, point.y));
            Vector2 toArrow = new Vector2(tip.x - point.x, tip.z - point.y);
            yaw = Mathf.Atan2(toArrow.x, toArrow.y) * Mathf.Rad2Deg;
            // GL765: they get up looking at the arrow over their land.
            endPitch = -Mathf.Min(MostLookUp, Mathf.Atan2(tip.y - height - DropEyeHeight, toArrow.magnitude) * Mathf.Rad2Deg);
            player.Teleport(stop + Vector3.up * 0.05f);
        }

        // GL766: no tree over the place the cart stops and the player lands.
        private static bool GroundIsClear(TreeField trees, Vector2 point, float height)
        {
            var size = new Vector3(DropClearHalfWidth * 2f, DropClearHeight, DropClearHalfWidth * 2f);
            return trees == null || !trees.AnyPartIn(new Bounds(new Vector3(point.x, height + DropClearHeight * 0.5f, point.y), size));
        }

        // GL765: no tree between the eye of someone standing there and the parcel's arrow.
        private static bool ViewIsClear(TreeField trees, Vector2 point, float height, Vector3 tip)
        {
            if (trees == null)
            {
                return true;
            }
            var eye = new Vector3(point.x, height + DropEyeHeight, point.y);
            int steps = Mathf.CeilToInt(Vector3.Distance(eye, tip) / SightStep);
            for (int i = 1; i < steps; i++)
            {
                if (trees.AnyPartIn(new Bounds(Vector3.Lerp(eye, tip, i / (float)steps), Vector3.one)))
                {
                    return false;
                }
            }
            return true;
        }

        // GL766: the land's own height under a world position, for the cart and the fall to tell the ground from a tree.
        private System.Func<Vector3, float> LandHeight()
        {
            MapSession session = MapSession.Current;
            if (session == null || session.Map == null || worldView == null)
            {
                return null;
            }
            return at =>
            {
                Vector3 local = worldView.transform.InverseTransformPoint(at);
                return worldView.transform.TransformPoint(new Vector3(local.x, session.Map.Height(local.x, local.z), local.z)).y;
            };
        }

        private static bool IsWet(Vector2 point)
        {
            MapSession session = MapSession.Current;
            System.Func<float, float, bool> wet = session != null && session.Map != null ? SettlementWriter.WetOf(session.Map.Water) : null;
            return wet != null && wet(point.x, point.y);
        }

        // The player is placed by the parcel: that's where the cart will stop. Take the player out of the controller's hands.
        private void Begin()
        {
            ChooseStop();
            heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            headBase = head.localPosition;
            standingEye = headBase.y;
            player.InputLocked = true;
            player.enabled = false;
            player.Body.enabled = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // GL743: the bounded map has no TradeCart, and without a material the cart was left out altogether.
            Material material = FindFirstObjectByType<TradeCart>() is TradeCart trade ? trade.LowPolyMaterial
                : FindFirstObjectByType<MapSettlements>() is MapSettlements settlements ? settlements.LowPolyMaterial : null;
            if (material != null && worldView != null)
            {
                cart = new ArrivalCart(material, worldView, stop, heading, LandHeight());
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
            // GL743: lying back with the head propped up, looking along the cart's bed, rocked by the ruts.
            float sway = Mathf.Sin(sinceStart * 3.1f) + 0.5f * Mathf.Sin(sinceStart * 7.3f);
            Look(OnBed(BedBack), LyingEye, RidePitch + sway * 1.5f, Mathf.Sin(sinceStart * 2.3f) * 2f);
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
            Look(OnBed(BedBack - 0.75f * slide * slide), LyingEye, RidePitch - 20f * slide, 8f * slide);
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
            Look(at, eyeHeight, Mathf.Lerp(-8f, endPitch, righting), Mathf.Lerp(LyingRoll, 0f, righting));
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
            head.localRotation = Quaternion.Euler(endPitch, 0f, 0f);
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

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
        }

        int IGuiDrawer.GuiDepth => -200;

        void IGuiDrawer.DrawGui()
        {
            float black = phase == Phase.Waiting ? 1f : ArrivalTimeline.Black(sinceStart);
            if (black <= 0f && phase != Phase.Ride)
            {
                return;
            }
            // Over the HUDs and the pause popup.
            Color previous = GUI.color;
            if (black > 0f && !LoadCurtainView.IsUp)
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
