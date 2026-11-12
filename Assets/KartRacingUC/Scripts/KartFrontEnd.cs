using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class KartFrontEnd : MonoBehaviour
{
    [SerializeField] private bool selector;
    [SerializeField] private GameObject[] pilots;

    public static int SelectedPilot { get; private set; }
    private static readonly string[] Names = { "Juan Acosta", "Gabriel Moreno" };
    private static readonly Color Navy = new(0.035f, 0.065f, 0.105f);
    private static readonly Color Panel = new(0.075f, 0.125f, 0.19f);
    private static readonly Color Accent = new(1f, 0.48f, 0.17f);
    private static readonly Color Pale = new(0.87f, 0.91f, 0.96f);
    private readonly Image[] cards = new Image[2];
    private readonly Text[] cardNames = new Text[2];
    private readonly RenderTexture[] previews = new RenderTexture[2];
    private Font font;
    private bool loading;

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var events = new GameObject("Event System", typeof(EventSystem));
        events.transform.SetParent(transform, false);
        events.GetComponent<EventSystem>().sendNavigationEvents = false;
        events.AddComponent<InputSystemUIInputModule>();
        Image background = ImageAt(canvas.transform, "Background", 0, 0, 1, 1, Navy);
        ImageAt(background.transform, "Top accent", 0, 0.984f, 1, 1, Accent);
        if (selector)
            BuildSelector(background.transform);
        else
            BuildHome(background.transform);
    }

    private void Update()
    {
        var keys = Keyboard.current;
        if (keys == null || loading)
            return;
        if (selector)
        {
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame)
                Select(0);
            else if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame)
                Select(1);
            if (keys.escapeKey.wasPressedThisFrame)
                Load("MainMenu");
            else if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)
                Load("TestKart");
        }
        else if (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame)
        {
            Load("CharacterSelection");
        }
    }

    private void BuildHome(Transform root)
    {
        ImageAt(root, "Title panel", 0.13f, 0.30f, 0.87f, 0.74f, Panel);
        Label(root, "KART RACING UC", 0.15f, 0.47f, 0.85f, 0.68f, 72, FontStyle.Bold, Pale);
        Label(root, "Carreras arcade · Contrarreloj de 3 vueltas", 0.16f, 0.39f, 0.84f, 0.47f, 23, FontStyle.Normal, Pale);
        ButtonAt(root, "ESPACIO  ·  EMPEZAR", 0.33f, 0.22f, 0.67f, 0.32f, Accent,
            () => Load("CharacterSelection"));
    }

    private void BuildSelector(Transform root)
    {
        Label(root, "Elige a tu piloto", 0.12f, 0.83f, 0.88f, 0.96f, 52, FontStyle.Bold, Pale);
        for (int i = 0; i < Names.Length; i++)
        {
            int choice = i;
            float left = 0.17f + i * 0.34f;
            cards[i] = ImageAt(root, Names[i], left, 0.27f, left + 0.32f, 0.80f, Panel);
            var card = cards[i].transform;
            var button = cards[i].gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Select(choice));

            var preview = new GameObject("Pilot preview", typeof(RectTransform), typeof(RawImage));
            preview.transform.SetParent(card, false);
            SetRect(preview.GetComponent<RectTransform>(), 0.02f, 0.21f, 0.98f, 0.98f);
            var image = preview.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = Preview(i);
            cardNames[i] = Label(card, Names[i], 0.02f, 0.02f, 0.98f, 0.20f, 25, FontStyle.Bold, Pale);
        }

        ButtonAt(root, "ESC  ·  VOLVER", 0.16f, 0.10f, 0.39f, 0.20f, Panel, () => Load("MainMenu"));
        ButtonAt(root, "ENTER  ·  CORRER", 0.61f, 0.10f, 0.84f, 0.20f, Accent, () => Load("TestKart"));
        Select(SelectedPilot);
    }

    private Texture Preview(int index)
    {
        Vector3 origin = new(index * 25, -100, 0);
        var model = Instantiate(pilots[index], origin, Quaternion.identity, transform);
        model.name = Names[index] + " Preview";
        var animator = model.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);

        var target = new RenderTexture(400, 320, 24);
        previews[index] = target;
        var camera = new GameObject(Names[index] + " Portrait", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(transform, false);
        Vector3 focus = bounds.center + Vector3.up * bounds.size.y * 0.24f;
        camera.transform.position = focus + new Vector3(0.26f, 0.09f, 1) * Mathf.Max(1.5f, bounds.size.y * 1.25f);
        camera.transform.LookAt(focus);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(0.35f, bounds.size.y * 0.34f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Panel;
        camera.farClipPlane = 6;
        camera.targetTexture = target;
        return target;
    }

    private void Select(int index)
    {
        SelectedPilot = index;
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].color = i == index ? Accent : Panel;
            cardNames[i].color = i == index ? Navy : Pale;
        }
    }

    private void Load(string scene)
    {
        if (loading)
            return;
        loading = true;
        SceneManager.LoadScene(scene);
    }

    private void OnDestroy()
    {
        foreach (var preview in previews)
        {
            if (preview != null)
                Destroy(preview);
        }
    }

    private Image ImageAt(Transform parent, string name, float left, float bottom, float right, float top, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        SetRect(item.GetComponent<RectTransform>(), left, bottom, right, top);
        var image = item.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text Label(Transform parent, string text, float left, float bottom, float right, float top,
        int size, FontStyle weight, Color color)
    {
        var item = new GameObject(text, typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        SetRect(item.GetComponent<RectTransform>(), left, bottom, right, top);
        var label = item.GetComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = weight;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return label;
    }

    private void ButtonAt(Transform parent, string label, float left, float bottom, float right, float top,
        Color background, UnityEngine.Events.UnityAction click)
    {
        var image = ImageAt(parent, label, left, bottom, right, top, background);
        var button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(click);
        Label(image.transform, label, 0, 0, 1, 1, 22, FontStyle.Bold, background == Accent ? Navy : Pale);
    }

    private static void SetRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

