using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A short notice for each kind of item the player collects ("+3 Copper ore"), counted up
    /// while more of it keeps coming and fading a moment after the last (first pass of U2).
    /// Listens to the <see cref="PlayerInventory"/> when there is one (with "Inventory full:
    /// Copper ore" for what didn't fit), otherwise to <see cref="PlayerToolUser.Collected"/>.
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
        private GUIStyle style;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.Added += OnCollected;
                inventory.Rejected += OnRejected;
            }
            else
            {
                tools.Collected += OnCollected;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Added -= OnCollected;
                inventory.Rejected -= OnRejected;
            }
            else
            {
                tools.Collected -= OnCollected;
            }
        }

        private void OnCollected(ItemDefinition item)
        {
            Note(item, full: false);
        }

        private void OnRejected(ItemDefinition item)
        {
            Note(item, full: true);
        }

        private void Note(ItemDefinition item, bool full)
        {
            foreach (Notice notice in notices)
            {
                if (notice.Item == item && notice.Full == full)
                {
                    notice.Count++;
                    notice.LastTime = Time.time;
                    return;
                }
            }
            notices.Add(new Notice { Item = item, Full = full, Count = 1, LastTime = Time.time });
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
                string text = notice.Full
                    ? $"Inventory full: {notice.Item.DisplayName}"
                    : $"+{notice.Count} {notice.Item.DisplayName}";
                GUI.Label(new Rect(0f, y, Screen.width, LineHeight), text, style);
                y += LineHeight;
            }
            GUI.color = previous;
        }

        private sealed class Notice
        {
            public ItemDefinition Item;
            public bool Full;
            public int Count;
            public float LastTime;
        }
    }
}
