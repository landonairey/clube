using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Short notices about the player's items (first pass of U2): "+3 Copper ore" as items
    /// come in, "Inventory full: Copper ore" for what didn't fit, "-12 Stone" when dropping
    /// and "Can't drop Pickaxe" when the selected item can't go into the ground. Counts add
    /// up while more of the same keeps coming, and each fades a moment after the last.
    /// Listens to the <see cref="PlayerInventory"/> when there is one, otherwise to
    /// <see cref="PlayerToolUser.Collected"/>, and to the <see cref="ItemDropper"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class PickupNotice : MonoBehaviour
    {
        private const float LineHeight = 22f;

        [Tooltip("Seconds a notice stays after the last item of its kind.")]
        [SerializeField, Min(0.1f)]
        private float showSeconds = 2.5f;

        [Tooltip("Seconds it takes to fade out at the end.")]
        [SerializeField, Min(0f)]
        private float fadeSeconds = 0.5f;

        private readonly List<Notice> notices = new List<Notice>();
        private PlayerToolUser tools;
        private PlayerInventory inventory;
        private ItemDropper dropper;
        private GUIStyle style;

        private enum Kind
        {
            Added,
            Full,
            Dropped,
            CantDrop,
        }

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            inventory = GetComponent<PlayerInventory>();
            dropper = GetComponent<ItemDropper>();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.Added += OnAdded;
                inventory.Rejected += OnRejected;
            }
            else
            {
                tools.Collected += OnAdded;
            }
            if (dropper != null)
            {
                dropper.Dropped += OnDropped;
                dropper.Refused += OnRefused;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Added -= OnAdded;
                inventory.Rejected -= OnRejected;
            }
            else
            {
                tools.Collected -= OnAdded;
            }
            if (dropper != null)
            {
                dropper.Dropped -= OnDropped;
                dropper.Refused -= OnRefused;
            }
        }

        private void OnAdded(ItemDefinition item)
        {
            Note(item, Kind.Added, 1);
        }

        private void OnRejected(ItemDefinition item)
        {
            Note(item, Kind.Full, 1);
        }

        private void OnDropped(ItemDefinition item, int count)
        {
            Note(item, Kind.Dropped, count);
        }

        private void OnRefused(ItemDefinition item)
        {
            Note(item, Kind.CantDrop, 1);
        }

        private void Note(ItemDefinition item, Kind kind, int count)
        {
            foreach (Notice notice in notices)
            {
                if (notice.Item == item && notice.Kind == kind)
                {
                    notice.Count += count;
                    notice.LastTime = Time.time;
                    return;
                }
            }
            notices.Add(new Notice { Item = item, Kind = kind, Count = count, LastTime = Time.time });
        }

        private void OnGUI()
        {
            notices.RemoveAll(notice => Time.time - notice.LastTime > showSeconds);
            if (notices.Count == 0)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            Color previous = GUI.color;
            float y = Screen.height * 0.5f + 48f;
            foreach (Notice notice in notices)
            {
                float left = showSeconds - (Time.time - notice.LastTime);
                GUI.color = new Color(1f, 1f, 1f, fadeSeconds > 0f ? Mathf.Clamp01(left / fadeSeconds) : 1f);
                GUI.Label(new Rect(0f, y, Screen.width, LineHeight), Text(notice), style);
                y += LineHeight;
            }
            GUI.color = previous;
        }

        private static string Text(Notice notice)
        {
            string name = notice.Item.DisplayName;
            switch (notice.Kind)
            {
                case Kind.Full:
                    return $"Inventory full: {name}";
                case Kind.Dropped:
                    return $"-{notice.Count} {name}";
                case Kind.CantDrop:
                    return $"Can't drop {name}";
                default:
                    return $"+{notice.Count} {name}";
            }
        }

        private sealed class Notice
        {
            public ItemDefinition Item;
            public Kind Kind;
            public int Count;
            public float LastTime;
        }
    }
}
