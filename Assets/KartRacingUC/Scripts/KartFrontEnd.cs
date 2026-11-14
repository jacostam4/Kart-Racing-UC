using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class KartFrontEnd : MonoBehaviour
{
    [SerializeField] private bool selector;
    // Lista de modelos de pilotos. Para agregar un personaje nuevo, arrastra su modelo aquí en el Inspector.
    [SerializeField] private GameObject[] pilots;
    // Opcional: nombres a mostrar. Si se deja vacío, se usa el nombre del archivo sin "Avatar ".
    [SerializeField] private string[] pilotNames;

    private static readonly string[] Hours = { "DÍA", "ATARDECER", "NOCHE" };
    private readonly Image[] hourButtons = new Image[3];
    public static int SelectedHour { get; private set; }

    public static int SelectedPilot { get; private set; }
    // El modelo elegido viaja a la escena de carrera a través de esta variable.
    public static GameObject SelectedModel { get; private set; }
    // Todos los pilotos del menú: la carrera los usa para escoger a los rivales.
    public static GameObject[] AllPilots { get; private set; }
    public static string[] AllNames { get; private set; }
    private string[] Names;
    private static readonly Color Navy = new(0.035f, 0.065f, 0.105f);
    private static readonly Color Panel = new(0.075f, 0.125f, 0.19f);
    private static readonly Color Accent = new(1f, 0.48f, 0.17f);
    private static readonly Color Pale = new(0.87f, 0.91f, 0.96f);
    private Image[] cards = new Image[0];
    private Text[] cardNames = new Text[0];
    private RenderTexture[] previews = new RenderTexture[0];
    private Font font;
    private bool loading;

    private void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Names = BuildNames();
        if (selector)
        {
            AllPilots = pilots;
            AllNames = Names;
        }
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
                Select(Mathf.Max(0, SelectedPilot - 1));
            else if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame)
                Select(Mathf.Min(Names.Length - 1, SelectedPilot + 1));
            if (keys.escapeKey.wasPressedThisFrame)
                Load("MainMenu");
            else if (keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)
                Load("TestKart");
        }
        else
        {
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame)
                SelectHour((SelectedHour + Hours.Length - 1) % Hours.Length);
            else if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame)
                SelectHour((SelectedHour + 1) % Hours.Length);

            if (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame)
                Load("CharacterSelection");
        }
    }

    private void BuildHome(Transform root)
    {
        ImageAt(root, "Title panel", 0.13f, 0.25f, 0.87f, 0.79f, Panel);
        Label(root, "KART RACING UC", 0.15f, 0.54f, 0.85f, 0.74f, 72, FontStyle.Bold, Pale);
        Label(root, "Horario del campeonato", 0.16f, 0.46f, 0.84f, 0.54f, 23, FontStyle.Normal, Pale);
        for (int i = 0; i < Hours.Length; i++)
        {
            int choice = i;
            float left = 0.19f + i * 0.21f;
            hourButtons[i] = ButtonAt(root, Hours[i], left, 0.34f, left + 0.20f, 0.44f,
                Navy, () => SelectHour(choice));
        }

        SelectHour(SelectedHour);
        ButtonAt(root, "ESPACIO  ·  EMPEZAR", 0.33f, 0.17f, 0.67f, 0.27f, Accent,
            () => Load("CharacterSelection"));
    }

    private void SelectHour(int index)
    {
        SelectedHour = index;
        for (int i = 0; i < hourButtons.Length; i++)
        {
            hourButtons[i].color = i == index ? Accent : Navy;
            hourButtons[i].GetComponentInChildren<Text>().color = i == index ? Navy : Pale;
        }
    }

    private void BuildSelector(Transform root)
    {
        Label(root, "Elige a tu piloto", 0.12f, 0.83f, 0.88f, 0.96f, 52, FontStyle.Bold, Pale);
        int count = Names.Length;
        cards = new Image[count];
        cardNames = new Text[count];
        previews = new RenderTexture[count];
        // Las tarjetas se reparten solas según cuántos pilotos haya.
        const float gap = 0.02f;
        float span = count <= 2 ? 0.66f : 0.86f;
        float start = 0.5f - span / 2;
        float width = (span - gap * (count - 1)) / count;
        int nameSize = count <= 3 ? 25 : 20;
        for (int i = 0; i < count; i++)
        {
            int choice = i;
            float left = start + i * (width + gap);
            cards[i] = ImageAt(root, Names[i], left, 0.27f, left + width, 0.80f, Panel);
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
            cardNames[i] = Label(card, Names[i], 0.02f, 0.02f, 0.98f, 0.20f, nameSize, FontStyle.Bold, Pale);
        }

        ButtonAt(root, "ESC  ·  VOLVER", 0.16f, 0.10f, 0.39f, 0.20f, Panel, () => Load("MainMenu"));
        ButtonAt(root, "ENTER  ·  CORRER", 0.61f, 0.10f, 0.84f, 0.20f, Accent, () => Load("TestKart"));
        Select(Mathf.Clamp(SelectedPilot, 0, count - 1));
    }

    private string[] BuildNames()
    {
        var result = new string[pilots.Length];
        for (int i = 0; i < pilots.Length; i++)
        {
            if (pilotNames != null && i < pilotNames.Length && !string.IsNullOrWhiteSpace(pilotNames[i]))
            {
                result[i] = pilotNames[i];
                continue;
            }
            string name = pilots[i] != null ? pilots[i].name : "Piloto " + (i + 1);
            if (name.StartsWith("Avatar "))
                name = name.Substring("Avatar ".Length);
            result[i] = name.Replace(".glb", "");
        }
        return result;
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
        SelectedModel = pilots[index];
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
        if (scene == "TestKart")
            KartLevels.Current = 0; // una partida nueva siempre empieza en el nivel 1
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

    private Image ButtonAt(Transform parent, string label, float left, float bottom, float right, float top,
        Color background, UnityEngine.Events.UnityAction click)
    {
        var image = ImageAt(parent, label, left, bottom, right, top, background);
        var button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(click);
        Label(image.transform, label, 0, 0, 1, 1, 22, FontStyle.Bold, background == Accent ? Navy : Pale);
        return image;
    }

    private static void SetRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}



