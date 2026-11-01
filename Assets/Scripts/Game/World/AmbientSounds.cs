using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The world's background sound (GL272): a breeze that never stops, stronger high up and under heavy cloud; leaves
    /// rustling, as loud as the trees round the listener are thick; and birdsong now and then, from a little way off, by
    /// day and most of all at dawn (the dawn chorus), never at night. The clips are made by *Clube → Audio → Generate sound
    /// effects*. Plays while the player's sound effects are on (<see cref="PlayerSounds.EffectsOn"/>).
    /// </summary>
    public class AmbientSounds : MonoBehaviour
    {
        [SerializeField]
        private PlayerSounds sounds;

        [SerializeField]
        private AudioClip windLoop;

        [SerializeField]
        private AudioClip leavesLoop;

        [SerializeField]
        private AudioClip[] birdCalls = new AudioClip[0];

        [Tooltip("The breeze's volume on open low ground, and its most high up or under heavy cloud.")]
        [SerializeField]
        private Vector2 windVolume = new Vector2(0.12f, 0.35f);

        [Tooltip("The leaves' volume among thick trees.")]
        [SerializeField, Range(0f, 1f)]
        private float leavesVolume = 0.18f;

        [Tooltip("Seconds between bird calls by day, at random within this range; a third of it at dawn.")]
        [SerializeField]
        private Vector2 birdSeconds = new Vector2(4f, 14f);

        [SerializeField, Range(0f, 1f)]
        private float birdVolume = 0.35f;

        private AudioSource wind;
        private AudioSource leaves;
        private AudioSource birds;
        private DayNightCycle dayNight;
        private CloudLayer clouds;
        private WorldView world;
        private TreeCover cover;
        private TreeField coverOf;
        private float nextBird;
        private float treeLevel;

        private void Awake()
        {
            dayNight = GetComponent<DayNightCycle>();
            clouds = GetComponent<CloudLayer>();
            world = GetComponent<WorldView>();
            wind = Loop("Wind", windLoop);
            leaves = Loop("Leaves", leavesLoop);
            var birdObject = new GameObject("Birdsong");
            birdObject.transform.SetParent(transform, false);
            birds = birdObject.AddComponent<AudioSource>();
            birds.playOnAwake = false;
            birds.spatialBlend = 1f;
            birds.rolloffMode = AudioRolloffMode.Linear;
            birds.minDistance = 8f;
            birds.maxDistance = 70f;
            nextBird = Time.time + 3f;
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void Update()
        {
            Camera listener = Camera.main;
            bool on = sounds == null || sounds.EffectsOn;
            if (listener == null || !on)
            {
                Silence();
                return;
            }
            Vector3 at = listener.transform.position;
            // The player's ambient volume (GL276).
            float gain = SoundSettings.Gain(AudioChannel.Ambient);
            float daylight = dayNight != null ? dayNight.Daylight : 1f;

            // The breeze: louder up high (from 60 m to 160 m) and under heavy cloud.
            float height = Mathf.InverseLerp(60f, 160f, at.y);
            float cloud = clouds != null && clouds.isActiveAndEnabled ? Mathf.InverseLerp(0.3f, 0.8f, clouds.Coverage) : 0f;
            Play(wind, gain * Mathf.Lerp(windVolume.x, windVolume.y, Mathf.Max(height, cloud * 0.7f)));

            // Leaves: as thick as the trees round the listener, eased so walking into a wood swells it gently.
            float trees = TreesAround(at);
            treeLevel = Mathf.MoveTowards(treeLevel, trees, Time.deltaTime * 0.5f);
            Play(leaves, gain * leavesVolume * treeLevel);

            // Birds: by day, more at dawn, none in the dark; a call from somewhere round about, up in the trees.
            if (Time.time >= nextBird)
            {
                bool dawn = dayNight != null && ToolCooldown.IsDawn(dayNight.Hour);
                float wait = Random.Range(birdSeconds.x, birdSeconds.y) * (dawn ? 0.35f : 1f) / Mathf.Lerp(0.6f, 1.2f, trees);
                nextBird = Time.time + wait;
                if (daylight > 0.35f && birdCalls.Length > 0)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    birds.transform.position = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(10f, 30f) + Vector3.up * Random.Range(3f, 10f);
                    birds.pitch = Random.Range(0.9f, 1.15f);
                    birds.PlayOneShot(birdCalls[Random.Range(0, birdCalls.Length)], gain * birdVolume * Mathf.InverseLerp(0.35f, 0.8f, daylight));
                }
            }
        }

        // How thick the trees round a point are, 0-1, from the trees the world grows there.
        private float TreesAround(Vector3 point)
        {
            if (world == null || world.Trees == null)
            {
                return 0f;
            }
            if (cover == null || coverOf != world.Trees)
            {
                cover = new TreeCover(world.Trees, 18f);
                coverOf = world.Trees;
            }
            Vector3 local = world.transform.InverseTransformPoint(point);
            return cover.At(local.x, local.z);
        }

        private static void Play(AudioSource source, float volume)
        {
            if (source.clip == null)
            {
                return;
            }
            source.volume = volume;
            if (!source.isPlaying)
            {
                // Each loop starts somewhere along itself, so two never line up.
                source.time = Random.Range(0f, source.clip.length);
                source.Play();
            }
        }

        private void Silence()
        {
            if (wind != null && wind.isPlaying)
            {
                wind.Stop();
            }
            if (leaves != null && leaves.isPlaying)
            {
                leaves.Stop();
            }
        }
    }
}
