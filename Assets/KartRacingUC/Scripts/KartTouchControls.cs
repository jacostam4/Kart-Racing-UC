using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class KartTouchControls : MonoBehaviour
{
    private KartMovement kart;
    private bool left, right;
    private readonly List<Action> releases = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isMobilePlatform || FindAnyObjectByType<KartTouchControls>() != null)
            return;
        var player = FindAnyObjectByType<KartMovement>();
        if (player != null)
            player.gameObject.AddComponent<KartTouchControls>();
    }

    private void Start()
    {
        kart = GetComponent<KartMovement>();
        if (EventSystem.current == null)
            new GameObject("Touch Events", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvas = new GameObject("Touch Controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1;
        var root = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        var safe = Screen.safeArea;
        root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        root.offsetMin = root.offsetMax = Vector2.zero;
        Hold(root, "◀", Vector2.zero, new Vector2(72, 79), pressed => { left = pressed; Steer(); });
        Hold(root, "▶", Vector2.zero, new Vector2(184, 79), pressed => { right = pressed; Steer(); });
        Hold(root, "GAS", Vector2.right, new Vector2(-74, 79), pressed => kart.TouchAccelerate = pressed);
        Hold(root, "FRENO", Vector2.right, new Vector2(-185, 79), pressed => kart.TouchBrake = pressed);
        Hold(root, "REV", Vector2.right, new Vector2(-296, 79), pressed => kart.TouchReverse = pressed);
        Tap(root, "NITRO", Vector2.right, new Vector2(-74, 181), kart.RequestBoost);
        Tap(root, "REINICIAR", Vector2.one, new Vector2(-70, -142), kart.RequestReset);
        Tap(root, "PILOTOS", Vector2.one, new Vector2(-183, -142), () => SceneManager.LoadScene("CharacterSelection"));
    }

    private void Steer() => kart.TouchSteering = (right ? 1 : 0) - (left ? 1 : 0);

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            foreach (var release in releases)
                release();
    }

    private void Hold(Transform root, string label, Vector2 anchor, Vector2 position, Action<bool> apply)
    {
        var button = MakeButton(root, label, anchor, position);
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        var pointers = new HashSet<int>();
        Add(EventTriggerType.PointerDown, data =>
        {
            pointers.Add(((PointerEventData)data).pointerId);
            apply(true);
        });
        Add(EventTriggerType.PointerUp, Release);
        Add(EventTriggerType.PointerExit, Release);
        releases.Add(() => { pointers.Clear(); apply(false); });

        void Release(BaseEventData data)
        {
            pointers.Remove(((PointerEventData)data).pointerId);
            apply(pointers.Count > 0);
        }

        void Add(EventTriggerType type, Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => action(data));
            trigger.triggers.Add(entry);
        }
    }

    private void Tap(Transform root, string label, Vector2 anchor, Vector2 position, Action action) =>
        MakeButton(root, label, anchor, position).onClick.AddListener(() => action());

    private static Button MakeButton(Transform root, string label, Vector2 anchor, Vector2 position)
    {
        var item = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = item.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(100, anchor.y == 1 ? 54 : 86);
        var image = item.GetComponent<Image>();
        image.color = label == "GAS" || label == "NITRO" ? new Color(0.79f, 0.32f, 0.10f, 0.92f) : new Color(0.08f, 0.13f, 0.20f, 0.88f);
        var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(rect, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 19;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        text.raycastTarget = false;
        return item.GetComponent<Button>();
    }
}
