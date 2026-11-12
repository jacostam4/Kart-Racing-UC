using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// ============================================================================
//  KART FIESTA: capa visual estilo arcade para la carrera.
//
//  - No hay que arrastrarlo a ninguna escena: se agrega solo cuando se carga
//    una escena que tenga carrera (KartTimeTrial).
//  - No cambia cómo se maneja ni las reglas de la carrera; solo agrega cosas
//    que se VEN encima del juego.
//  - Para apagar una mejora, cambia su "true" por "false" en los interruptores.
//  - Para quitarlo todo, borra este archivo.
// ============================================================================
public sealed class KartFiesta : MonoBehaviour
{
    // ---------------- INTERRUPTORES ----------------
    private const bool ColoresVivos = true;      // Colores más saturados y brillo (bloom)
    private const bool CuentaRegresiva = true;   // "3 · 2 · 1 · ¡YA!" gigante en el centro
    private const bool NombreDelRival = true;    // Globo con el nombre encima del kart rival
    private const bool LineasDeVelocidad = true; // Rayas en los bordes al usar nitro
    private const bool Confeti = true;           // Lluvia de confeti al llegar a la meta
    private const bool Velocimetro = true;       // Velocidad en km/h abajo a la derecha
    private const bool Puesto = true;            // 1º, 2º o 3º grande abajo a la derecha
    // -----------------------------------------------

    private KartTimeTrial race;
    private KartMovement player;
    private KartStartLights lights;
    private Camera view;
    private Volume colorVolume;

    private float countdownStart = -1;
    private float goTime = -1;
    private bool confettiStarted;
    private readonly List<Piece> confetti = new();
    private GUIStyle bigText;
    private GUIStyle tagText;
    private GUIStyle hudText;
    private int lastPlace = -1;
    private float placeChangedAt;

    private static readonly Color[] Party =
    {
        new(1f, 0.27f, 0.27f), new(1f, 0.62f, 0.1f), new(1f, 0.88f, 0.15f),
        new(0.3f, 0.86f, 0.35f), new(0.2f, 0.65f, 1f), new(0.78f, 0.4f, 1f)
    };

    private struct Piece
    {
        public Vector2 position;
        public Vector2 velocity;
        public float angle;
        public float spin;
        public Color color;
        public Vector2 size;
    }

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
        if (found != null && found.GetComponent<KartFiesta>() == null)
            found.gameObject.AddComponent<KartFiesta>();
    }

    // ---------- Preparación ----------
    private void OnEnable()
    {
        race = GetComponent<KartTimeTrial>();
        player = FindAnyObjectByType<KartMovement>();
        lights = FindAnyObjectByType<KartStartLights>();
        view = Camera.main;

        if (lights != null)
        {
            lights.CountdownStarted += OnCountdown;
            lights.GreenLight += OnGo;
        }
        if (ColoresVivos)
            CreateColorVolume();
    }

    private void OnDisable()
    {
        if (lights != null)
        {
            lights.CountdownStarted -= OnCountdown;
            lights.GreenLight -= OnGo;
        }
        if (colorVolume != null)
        {
            Destroy(colorVolume.sharedProfile);
            Destroy(colorVolume.gameObject);
        }
    }

    private void Start()
    {
        // Por si el semáforo arrancó antes de que este script se agregara.
        if (player != null && !player.CanDrive && countdownStart < 0)
            countdownStart = Time.time;
    }

    private void OnCountdown()
    {
        countdownStart = Time.time;
        goTime = -1;
        confettiStarted = false;
        confetti.Clear();
    }

    private void OnGo() => goTime = Time.time;

    // ---------- 1. Colores vivos ----------
    private void CreateColorVolume()
    {
        var holder = new GameObject("Kart Fiesta Colores");
        holder.transform.SetParent(transform, false);
        colorVolume = holder.AddComponent<Volume>();
        colorVolume.isGlobal = true;
        colorVolume.priority = 50;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(28f);
        color.contrast.Override(8f);
        color.postExposure.Override(0.15f);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.6f);
        bloom.threshold.Override(1.05f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        colorVolume.sharedProfile = profile;
    }

    // ---------- Animación del confeti ----------
    private void Update()
    {
        if (Confeti && !confettiStarted && race != null && race.Finished && race.Place == 1)
        {
            confettiStarted = true;
            for (int i = 0; i < 160; i++)
            {
                confetti.Add(new Piece
                {
                    position = new Vector2(Random.Range(0f, Screen.width), Random.Range(-Screen.height * 0.6f, 0f)),
                    velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(120f, 260f)),
                    angle = Random.Range(0f, 360f),
                    spin = Random.Range(-360f, 360f),
                    color = Party[i % Party.Length],
                    size = new Vector2(Random.Range(7f, 13f), Random.Range(12f, 20f))
                });
            }
        }

        for (int i = 0; i < confetti.Count; i++)
        {
            Piece piece = confetti[i];
            piece.position += piece.velocity * Time.deltaTime;
            piece.position.x += Mathf.Sin(Time.time * 3 + i) * 30 * Time.deltaTime;
            piece.angle += piece.spin * Time.deltaTime;
            if (piece.position.y > Screen.height + 20)
                piece.position.y = -20;
            confetti[i] = piece;
        }
    }

    // ---------- Dibujo en pantalla ----------
    private void OnGUI()
    {
        GUI.depth = -10; // se dibuja encima del HUD original
        if (bigText == null)
        {
            bigText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tagText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
            tagText.normal.textColor = Color.white;
            hudText = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
        }

        if (LineasDeVelocidad)
            DrawSpeedLines();
        if (NombreDelRival)
            foreach (var other in KartRival.All)
                DrawRivalTag(other);
        if (Velocimetro || Puesto)
            DrawPlaceAndSpeed();
        if (CuentaRegresiva)
            DrawCountdown();
        if (Confeti)
            DrawConfetti();
    }

    // ---------- 2. Cuenta regresiva ----------
    private void DrawCountdown()
    {
        string text;
        Color color;
        float progress;

        if (goTime > 0 && Time.time - goTime < 1.1f)
        {
            text = "¡YA!";
            color = new Color(0.3f, 0.95f, 0.35f);
            progress = (Time.time - goTime) / 1.1f;
        }
        else if (countdownStart > 0 && goTime < 0)
        {
            // El semáforo del juego tarda 2,25 s en ponerse verde: lo partimos en 3.
            float t = Time.time - countdownStart;
            int step = Mathf.Clamp(Mathf.FloorToInt(t / 0.75f), 0, 2);
            text = (3 - step).ToString();
            color = step == 0 ? new Color(1f, 0.3f, 0.3f) : step == 1 ? new Color(1f, 0.65f, 0.15f) : new Color(1f, 0.9f, 0.2f);
            progress = Mathf.Clamp01((t - step * 0.75f) / 0.75f);
        }
        else
        {
            return;
        }

        // Efecto "pop": empieza grande y se acomoda; al final se desvanece un poco.
        float pop = 1 + 0.7f * Mathf.Pow(1 - Mathf.Clamp01(progress * 3), 3);
        bigText.fontSize = Mathf.RoundToInt(Screen.height * 0.2f * pop);
        color.a = 1 - Mathf.Clamp01((progress - 0.75f) * 4);
        var area = new Rect(0, Screen.height * 0.18f, Screen.width, Screen.height * 0.4f);
        Outlined(area, text, bigText, color, Mathf.Max(3, Screen.height / 180));
    }

    // ---------- 3. Nombre del rival ----------
    private void DrawRivalTag(KartRival rival)
    {
        if (rival == null || view == null)
            return;

        Vector3 head = rival.transform.position + Vector3.up * 1.7f;
        Vector3 screen = view.WorldToScreenPoint(head);
        float distance = Vector3.Distance(view.transform.position, head);
        if (screen.z <= 0 || distance > 60)
            return;

        float alpha = Mathf.Clamp01((60 - distance) / 15);
        float scale = Mathf.Clamp(14 / distance, 0.6f, 1.3f) * Screen.height / 650f;
        string name = rival.DisplayName;
        tagText.fontSize = Mathf.RoundToInt(15 * scale);
        float width = (name.Length * 10 + 26) * scale;
        float height = 26 * scale;
        var box = new Rect(screen.x - width / 2, Screen.height - screen.y - height, width, height);

        Color old = GUI.color;
        GUI.color = new Color(1f, 0.35f, 0.25f, 0.9f * alpha);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.Label(box, name, tagText);
        GUI.color = old;
    }

    // ---------- 6 y 7. Puesto y velocímetro ----------
    private static readonly Color PanelColor = new(0.12f, 0.16f, 0.21f, 0.92f);
    private static readonly Color Gold = new(1f, 0.78f, 0.12f);
    private static readonly Color Silver = new(0.78f, 0.84f, 0.92f);
    private static readonly Color Bronze = new(0.86f, 0.5f, 0.22f);
    private static readonly Color Ink = new(0.07f, 0.09f, 0.13f);

    private void DrawPlaceAndSpeed()
    {
        if (race == null || player == null)
            return;

        // Todo se dibuja en "unidades" que crecen con el tamaño de la pantalla.
        float u = Mathf.Clamp(Screen.height / 650f, 0.55f, 1.7f);
        float width = 236 * u;
        float height = (Puesto && Velocimetro ? 186 : Puesto ? 112 : 92) * u;
        float x = Screen.width - width - 16 * u;
        float y = Screen.height - height - 16 * u;
        Color old = GUI.color;

        // Panel oscuro de fondo (mismo estilo que el del nitro) con una franja naranja arriba.
        Box(new Rect(x, y, width, height), PanelColor);
        Box(new Rect(x, y, width, 4 * u), new Color(1f, 0.48f, 0.17f));
        float top = y + 12 * u;

        if (Puesto)
        {
            int place = race.Place;
            if (place != lastPlace)
            {
                lastPlace = place;
                placeChangedAt = Time.time; // para el efecto "pop" cuando cambias de puesto
            }
            float pop = 1 + 0.25f * Mathf.Clamp01(1 - (Time.time - placeChangedAt) * 3);
            Color medal = place == 1 ? Gold : place == 2 ? Silver : Bronze;

            // Placa de color con el número del puesto.
            float badge = 84 * u;
            var badgeRect = new Rect(x + 14 * u, top, badge, badge);
            Box(badgeRect, medal);
            Label(badgeRect, $"{place}º", Mathf.RoundToInt(52 * u * pop), Ink, TextAnchor.MiddleCenter);

            // Texto al lado: "PUESTO" y "de 3".
            float textX = badgeRect.xMax + 14 * u;
            Label(new Rect(textX, top + 6 * u, 120 * u, 24 * u), "PUESTO", Mathf.RoundToInt(15 * u), new Color(0.76f, 0.82f, 0.89f), TextAnchor.MiddleLeft);
            Label(new Rect(textX, top + 30 * u, 120 * u, 40 * u), $"de {race.Racers}", Mathf.RoundToInt(30 * u), Color.white, TextAnchor.MiddleLeft);
            top += badge + 12 * u;
        }

        if (Velocimetro)
        {
            float speed = Mathf.Abs(player.Speed);
            int kmh = Mathf.RoundToInt(speed * 3.6f);
            float fill = Mathf.Clamp01(speed / 23f); // 23 = velocidad máxima con nitro
            Color bar = player.IsBoosting ? new Color(0.35f, 0.92f, 1f) :
                Color.Lerp(new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.35f, 0.25f), fill);

            Label(new Rect(x + 14 * u, top - 4 * u, 120 * u, 44 * u), kmh.ToString(), Mathf.RoundToInt(38 * u), bar, TextAnchor.MiddleLeft);
            Label(new Rect(x + 104 * u, top + 6 * u, 70 * u, 30 * u), "km/h", Mathf.RoundToInt(16 * u), new Color(0.76f, 0.82f, 0.89f), TextAnchor.MiddleLeft);
            if (player.IsBoosting)
                Label(new Rect(x + width - 94 * u, top + 6 * u, 80 * u, 30 * u), "NITRO", Mathf.RoundToInt(15 * u), bar, TextAnchor.MiddleRight);

            // Barra de velocidad: gris de fondo y de color según qué tan rápido vas.
            var track = new Rect(x + 14 * u, top + 44 * u, width - 28 * u, 12 * u);
            Box(track, new Color(0.26f, 0.32f, 0.38f));
            Box(new Rect(track.x, track.y, track.width * fill, track.height), bar);
        }

        GUI.color = old;
    }

    private static void Box(Rect area, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void Label(Rect area, string text, int size, Color color, TextAnchor anchor)
    {
        hudText.fontSize = size;
        hudText.alignment = anchor;
        hudText.normal.textColor = color;
        GUI.color = Color.white;
        GUI.Label(area, text, hudText);
    }

    // ---------- 4. Líneas de velocidad ----------
    private void DrawSpeedLines()
    {
        if (player == null || !player.IsBoosting)
            return;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color old = GUI.color;
        var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        float radius = Mathf.Max(Screen.width, Screen.height) * 0.42f;
        Random.State saved = Random.state;
        Random.InitState(Mathf.FloorToInt(Time.time * 20)); // parpadeo rápido
        for (int i = 0; i < 34; i++)
        {
            float angle = Random.Range(0f, 360f);
            float length = Random.Range(radius * 0.25f, radius * 0.6f);
            float start = radius * Random.Range(0.75f, 1f);
            GUI.matrix = oldMatrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = new Color(1, 1, 1, Random.Range(0.25f, 0.6f));
            GUI.DrawTexture(new Rect(center.x + start, center.y - 1.5f, length, 3), Texture2D.whiteTexture);
        }
        Random.state = saved;
        GUI.matrix = oldMatrix;
        GUI.color = old;
    }

    // ---------- 5. Confeti ----------
    private void DrawConfetti()
    {
        if (confetti.Count == 0)
            return;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color old = GUI.color;
        foreach (var piece in confetti)
        {
            GUI.matrix = oldMatrix;
            GUIUtility.RotateAroundPivot(piece.angle, piece.position);
            GUI.color = piece.color;
            GUI.DrawTexture(new Rect(piece.position - piece.size / 2, piece.size), Texture2D.whiteTexture);
        }
        GUI.matrix = oldMatrix;
        GUI.color = old;
    }

    // Texto con borde oscuro para que se lea sobre cualquier fondo.
    private static void Outlined(Rect area, string text, GUIStyle style, Color color, int thickness)
    {
        Color old = style.normal.textColor;
        style.normal.textColor = new Color(0.05f, 0.05f, 0.1f, color.a);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0)
                    GUI.Label(new Rect(area.x + x * thickness, area.y + y * thickness, area.width, area.height), text, style);
        style.normal.textColor = color;
        GUI.Label(area, text, style);
        style.normal.textColor = old;
    }
}
