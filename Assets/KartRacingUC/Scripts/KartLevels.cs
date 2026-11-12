using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================================
//  KART LEVELS: 3 niveles con progresión + sistema de vidas.
//
//  - Nivel 1 (DÍA): rivales tranquilos, 3 vidas.
//  - Nivel 2 (ATARDECER): rivales más rápidos, 3 vidas.
//  - Nivel 3 (NOCHE): rivales al máximo, solo 2 vidas.
//  - Para pasar al siguiente nivel hay que llegar de 1º.
//  - Cada choque fuerte contra la barrera quita una vida. Sin vidas: GAME OVER.
//
//  Se agrega solo a la escena de carrera (no hay que arrastrarlo a nada).
//  Crea dos Canvas: "HUD Canvas" (nivel y vidas) y "Resultado Canvas"
//  (Victoria / Game Over).
// ============================================================================
public sealed class KartLevels : MonoBehaviour
{
    private sealed class Level
    {
        public string Name;
        public string Goal;
        public float RivalPace;   // 1 = velocidad normal de los rivales
        public int Lives;
        public bool ChangeLight;  // false = se deja la luz original de la escena
        public Color Sun;
        public float SunIntensity;
        public float SunHeight;   // grados del sol sobre el horizonte
        public Color Ambient;
        public Color Fog;
        public float FogDensity;
        public bool Night;
    }

    // ---------------- CONFIGURACIÓN DE LOS NIVELES ----------------
    private static readonly Level[] Levels =
    {
        new Level
        {
            Name = "DÍA", Goal = "Calienta motores: los rivales van tranquilos.",
            RivalPace = 0.9f, Lives = 3, ChangeLight = false
        },
        new Level
        {
            Name = "ATARDECER", Goal = "Los rivales aceleran. ¡Cuida tus vidas!",
            RivalPace = 1.0f, Lives = 3, ChangeLight = true,
            Sun = new Color(1f, 0.58f, 0.32f), SunIntensity = 0.9f, SunHeight = 9,
            Ambient = new Color(0.58f, 0.44f, 0.44f), Fog = new Color(0.95f, 0.62f, 0.45f), FogDensity = 0.006f
        },
        new Level
        {
            Name = "NOCHE", Goal = "La gran final: rivales al máximo y solo 2 vidas.",
            RivalPace = 1.07f, Lives = 2, ChangeLight = true, Night = true,
            Sun = new Color(0.55f, 0.65f, 1f), SunIntensity = 0.4f, SunHeight = 38,
            Ambient = new Color(0.2f, 0.23f, 0.38f), Fog = new Color(0.04f, 0.05f, 0.12f), FogDensity = 0.012f
        },
    };
    private const float HardHitSpeed = 5;      // velocidad mínima del choque para perder vida
    private const float SafeSeconds = 1.2f;    // segundos sin perder otra vida después de un choque
    // ----------------------------------------------------------------

    public static int Current { get; set; }   // 0 = nivel 1, 1 = nivel 2, 2 = nivel 3
    public static int Count => Levels.Length;

    private enum Result { None, NextLevel, Champion, Retry, GameOver }

    private static readonly Color Navy = new(0.035f, 0.065f, 0.105f);
    private static readonly Color Panel = new(0.12f, 0.16f, 0.21f, 0.94f);
    private static readonly Color Accent = new(1f, 0.48f, 0.17f);
    private static readonly Color Pale = new(0.87f, 0.91f, 0.96f);
    private static readonly Color HeartOn = new(1f, 0.25f, 0.3f);
    private static readonly Color HeartOff = new(0.3f, 0.34f, 0.4f);

    private KartTimeTrial race;
    private KartMovement player;
    private KartStartLights lights;
    private Font font;

    private int lives;
    private float safeUntil;
    private float hitAt = -10;
    private float bannerAt;
    private Result result;

    private Text[] hearts;
    private Image flash;
    private CanvasGroup banner;
    private GameObject endScreen;
    private Text endTitle;
    private Text endMessage;
    private Text primaryLabel;

    // ---------- Instalación automática ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToRace();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToRace();

    private static void AddToRace()
    {
        var found = FindAnyObjectByType<KartTimeTrial>();
        if (found != null && found.GetComponent<KartLevels>() == null)
            found.gameObject.AddComponent<KartLevels>();
    }

    private Level Now => Levels[Current];

    // ---------- Preparación ----------
    private void OnEnable()
    {
        Current = Mathf.Clamp(Current, 0, Count - 1);
        race = GetComponent<KartTimeTrial>();
        player = FindAnyObjectByType<KartMovement>();
        lights = FindAnyObjectByType<KartStartLights>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        KartRival.Difficulty = Now.RivalPace;
        race.ShowFinishPanel = false; // la pantalla final ahora la dibuja este script con Canvas
        ApplyLook();
        BuildHud();
        BuildEndScreen();

        if (player != null)
            player.Impact += OnImpact;
        if (lights != null)
            lights.CountdownStarted += OnCountdown;
        OnCountdown();
    }

    private void OnDisable()
    {
        if (player != null)
            player.Impact -= OnImpact;
        if (lights != null)
            lights.CountdownStarted -= OnCountdown;
    }

    // Se llama al empezar cada intento (también al pulsar R).
    private void OnCountdown()
    {
        lives = Now.Lives;
        safeUntil = 0;
        result = Result.None;
        bannerAt = Time.time;
        if (endScreen != null)
            endScreen.SetActive(false);
        RefreshHearts();
    }

    // ---------- Vidas ----------
    private void OnImpact(Vector3 contact, Vector3 normal, float speed)
    {
        if (result != Result.None || race.Finished || speed < HardHitSpeed || Time.time < safeUntil)
            return;

        lives--;
        safeUntil = Time.time + SafeSeconds;
        hitAt = Time.time;
        RefreshHearts();

        if (lives <= 0)
        {
            player.CanDrive = false;
            Show(Result.GameOver);
        }
    }

    private void RefreshHearts()
    {
        if (hearts == null)
            return;
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].gameObject.SetActive(i < Now.Lives);
            hearts[i].color = i < lives ? HeartOn : HeartOff;
        }
    }

    // ---------- Cada cuadro ----------
    private void Update()
    {
        // Destello rojo al chocar y corazones que parpadean mientras estás protegido.
        flash.color = new Color(1f, 0.1f, 0.1f, 0.35f * Mathf.Clamp01(1 - (Time.time - hitAt) * 2.5f));
        bool blink = Time.time < safeUntil && Mathf.Repeat(Time.time * 8, 1) < 0.5f;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].enabled = !(blink && i == lives);

        // Cartel del nivel al inicio: se ve 3,5 s y luego se desvanece.
        float shown = Time.time - bannerAt;
        banner.alpha = shown < 3.5f ? 1 : Mathf.Clamp01(1 - (shown - 3.5f) * 2);

        // ¿Terminó la carrera?
        if (result == Result.None && race.Finished)
        {
            if (race.Place == 1)
                Show(Current < Count - 1 ? Result.NextLevel : Result.Champion);
            else
                Show(Result.Retry);
        }

        var keys = Keyboard.current;
        if (result != Result.None && keys != null &&
            (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
            Primary();
    }

    // ---------- Pantalla final ----------
    private void Show(Result value)
    {
        result = value;
        switch (value)
        {
            case Result.NextLevel:
                endTitle.text = "¡NIVEL SUPERADO!";
                endTitle.color = new Color(1f, 0.82f, 0.2f);
                endMessage.text = $"Ganaste el nivel {Current + 1}.\nSigue el nivel {Current + 2}: {Levels[Current + 1].Name}";
                primaryLabel.text = "ENTER  ·  SIGUIENTE NIVEL";
                break;
            case Result.Champion:
                endTitle.text = "¡CAMPEÓN!";
                endTitle.color = new Color(1f, 0.82f, 0.2f);
                endMessage.text = "Ganaste los 3 niveles de Kart Racing UC.";
                primaryLabel.text = "ENTER  ·  MENÚ PRINCIPAL";
                break;
            case Result.Retry:
                endTitle.text = $"QUEDASTE {race.Place}º";
                endTitle.color = Pale;
                endMessage.text = "Necesitas el 1º lugar para pasar al siguiente nivel.";
                primaryLabel.text = "ENTER  ·  REINTENTAR";
                break;
            case Result.GameOver:
                endTitle.text = "GAME OVER";
                endTitle.color = HeartOn;
                endMessage.text = "Te quedaste sin vidas. ¡Inténtalo otra vez!";
                primaryLabel.text = "ENTER  ·  REINTENTAR";
                break;
        }
        endScreen.SetActive(true);
    }

    private void Primary()
    {
        if (result == Result.NextLevel)
        {
            Current++;
            Reload();
        }
        else if (result == Result.Champion)
        {
            GoToMenu();
        }
        else
        {
            Reload();
        }
    }

    private static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    private static void GoToMenu()
    {
        Current = 0;
        SceneManager.LoadScene("MainMenu");
    }

    // ---------- Ambiente de cada nivel ----------
    private void ApplyLook()
    {
        if (!Now.ChangeLight)
            return;

        foreach (var light in FindObjectsByType<Light>())
        {
            if (light.type != LightType.Directional)
                continue;
            light.color = Now.Sun;
            light.intensity *= Now.SunIntensity;
            Vector3 angles = light.transform.eulerAngles;
            light.transform.rotation = Quaternion.Euler(Now.SunHeight, angles.y, 0);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Now.Ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Now.Fog;
        RenderSettings.fogDensity = Now.FogDensity;

        if (Now.Night && Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Now.Fog;
        }
    }

    // ---------- Canvas 1: HUD (nivel + vidas) ----------
    private void BuildHud()
    {
        Transform root = NewCanvas("HUD Canvas", 5);

        flash = NewImage(root, "Destello", 0, 0, 1, 1, Color.clear);

        // Panel arriba a la derecha.
        Image box = NewImage(root, "Nivel y vidas", 0.80f, 0.84f, 0.985f, 0.975f, Panel);
        NewImage(box.transform, "Franja", 0, 0.95f, 1, 1, Accent);
        NewText(box.transform, $"NIVEL {Current + 1}/{Count}  ·  {Now.Name}", 0.06f, 0.55f, 0.96f, 0.92f, 17, Pale, TextAnchor.MiddleLeft);
        hearts = new Text[3];
        for (int i = 0; i < hearts.Length; i++)
            hearts[i] = NewText(box.transform, "♥", 0.04f + i * 0.17f, 0.04f, 0.21f + i * 0.17f, 0.58f, 34, HeartOn, TextAnchor.MiddleCenter);

        // Cartel de presentación del nivel (abajo al centro, entre el nitro y el velocímetro).
        Image card = NewImage(root, "Cartel del nivel", 0.3f, 0.17f, 0.7f, 0.33f, Panel);
        banner = card.gameObject.AddComponent<CanvasGroup>();
        banner.blocksRaycasts = false;
        NewImage(card.transform, "Franja", 0, 0.94f, 1, 1, Accent);
        NewText(card.transform, $"NIVEL {Current + 1}  ·  {Now.Name}", 0.03f, 0.45f, 0.97f, 0.92f, 30, Accent, TextAnchor.MiddleCenter);
        NewText(card.transform, $"{Now.Goal}  Vidas: {Now.Lives}", 0.03f, 0.06f, 0.97f, 0.48f, 17, Pale, TextAnchor.MiddleCenter);
    }

    // ---------- Canvas 2: Resultado (Victoria / Game Over) ----------
    private void BuildEndScreen()
    {
        Transform root = NewCanvas("Resultado Canvas", 20);
        endScreen = root.gameObject;

        NewImage(root, "Oscurecer", 0, 0, 1, 1, new Color(0, 0, 0, 0.45f));
        Image box = NewImage(root, "Resultado", 0.28f, 0.27f, 0.72f, 0.73f, Panel);
        NewImage(box.transform, "Franja", 0, 0.97f, 1, 1, Accent);
        endTitle = NewText(box.transform, "", 0.05f, 0.62f, 0.95f, 0.92f, 52, Pale, TextAnchor.MiddleCenter);
        endMessage = NewText(box.transform, "", 0.07f, 0.38f, 0.93f, 0.62f, 21, Pale, TextAnchor.MiddleCenter);
        primaryLabel = NewButton(box.transform, 0.08f, 0.08f, 0.6f, 0.26f, Accent, Navy, Primary);
        Text menu = NewButton(box.transform, 0.63f, 0.08f, 0.92f, 0.26f, new Color(0.26f, 0.32f, 0.38f), Pale, GoToMenu);
        menu.text = "MENÚ";

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Event System", typeof(EventSystem));
            events.transform.SetParent(transform, false);
            events.AddComponent<InputSystemUIInputModule>();
        }
        endScreen.SetActive(false);
    }

    // ---------- Ayudantes para construir la interfaz ----------
    private Transform NewCanvas(string name, int order)
    {
        var item = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        item.transform.SetParent(transform, false);
        var canvas = item.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = item.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        return item.transform;
    }

    private static Image NewImage(Transform parent, string name, float left, float bottom, float right, float top, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        Anchor(item.GetComponent<RectTransform>(), left, bottom, right, top);
        var image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text NewText(Transform parent, string text, float left, float bottom, float right, float top,
        int size, Color color, TextAnchor anchor)
    {
        var item = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        Anchor(item.GetComponent<RectTransform>(), left, bottom, right, top);
        var label = item.GetComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = color;
        label.alignment = anchor;
        label.raycastTarget = false;
        return label;
    }

    private Text NewButton(Transform parent, float left, float bottom, float right, float top,
        Color background, Color textColor, UnityEngine.Events.UnityAction click)
    {
        Image image = NewImage(parent, "Botón", left, bottom, right, top, background);
        image.raycastTarget = true;
        image.gameObject.AddComponent<Button>().onClick.AddListener(click);
        return NewText(image.transform, "", 0, 0, 1, 1, 18, textColor, TextAnchor.MiddleCenter);
    }

    private static void Anchor(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
