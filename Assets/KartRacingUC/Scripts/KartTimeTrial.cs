using System;
using UnityEngine;

public sealed class KartTimeTrial : MonoBehaviour
{
    [SerializeField] private KartMovement kart;
    [SerializeField] private KartStartLights lights;
    [SerializeField] private Transform[] checkpoints;
    [SerializeField] private Transform finish;
    [SerializeField] private KartRival rival;

    private const int TotalLaps = 3;
    private const float GateHalfWidth = 4.15f;
    private Vector3 previousPosition;
    private int nextCheckpoint;
    private int completedLaps;
    private double raceStart;
    private double lapStart;
    private double finalTime;
    private double bestLap;
    private bool running;
    private bool finished;
    private KartNitroBottle[] bottles;
    private int lastBottleA = -1;
    private int lastBottleB = -1;
    private string rivalName = "Gabriel";
    public void SetRivalName(string name) => rivalName = name;

    private GUIStyle caption;
    private GUIStyle value;
    private GUIStyle title;

    private void OnEnable()
    {
        bottles = GetComponentsInChildren<KartNitroBottle>(true);
        lights.CountdownStarted += Prepare;
        lights.GreenLight += Begin;
        Prepare();
    }

    private void OnDisable()
    {
        lights.CountdownStarted -= Prepare;
        lights.GreenLight -= Begin;
    }

    private void Prepare()
    {
        running = false;
        finished = false;
        nextCheckpoint = 0;
        completedLaps = 0;
        finalTime = 0;
        bestLap = double.PositiveInfinity;
        HideBottles();
        previousPosition = kart.transform.position;
    }

    private void Begin()
    {
        raceStart = Time.timeAsDouble;
        lapStart = raceStart;
        previousPosition = kart.transform.position;
        running = true;
        SpawnBottles();
    }

    private void LateUpdate()
    {
        if (!running)
            return;

        Vector3 position = kart.transform.position;
        Transform gate = nextCheckpoint == checkpoints.Length ? finish : checkpoints[nextCheckpoint];
        if (Crossed(gate, previousPosition, position))
        {
            if (nextCheckpoint == checkpoints.Length)
                CompleteLap();
            else
                nextCheckpoint++;
        }
        previousPosition = position;
    }

    private static bool Crossed(Transform gate, Vector3 from, Vector3 to)
    {
        float before = Vector3.Dot(from - gate.position, gate.forward);
        float after = Vector3.Dot(to - gate.position, gate.forward);
        if (before >= 0 || after < 0)
            return false;

        Vector3 crossing = Vector3.Lerp(from, to, -before / (after - before)) - gate.position;
        return Mathf.Abs(Vector3.Dot(crossing, gate.right)) <= GateHalfWidth && Mathf.Abs(crossing.y) < 3;
    }

    private void CompleteLap()
    {
        double now = Time.timeAsDouble;
        bestLap = Math.Min(bestLap, now - lapStart);
        lapStart = now;
        nextCheckpoint = 0;
        completedLaps++;
        if (completedLaps < TotalLaps)
        {
            SpawnBottles();
            return;
        }

        finalTime = now - raceStart;
        running = false;
        finished = true;
        HideBottles();
    }

    private void HideBottles()
    {
        foreach (var bottle in bottles)
            bottle.SetAvailable(false);
    }

    private void SpawnBottles()
    {
        HideBottles();
        if (bottles.Length < 2)
            return;

        int first;
        int second;
        do
        {
            first = UnityEngine.Random.Range(0, bottles.Length);
            second = UnityEngine.Random.Range(0, bottles.Length - 1);
            if (second >= first)
                second++;
        }
        while (bottles.Length > 2 &&
            ((first == lastBottleA && second == lastBottleB) ||
             (first == lastBottleB && second == lastBottleA)));

        lastBottleA = first;
        lastBottleB = second;
        bottles[first].SetAvailable(true);
        bottles[second].SetAvailable(true);
    }
    private void OnGUI()
    {
        if (caption == null)
        {
            caption = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            caption.normal.textColor = new Color(0.76f, 0.82f, 0.89f);
            value = new GUIStyle(caption) { fontSize = 24 };
            value.normal.textColor = Color.white;
            title = new GUIStyle(value) { fontSize = 29 };
        }

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        float scale = Mathf.Clamp(Screen.height / 650f, 0.55f, 1.7f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.color = new Color(0.12f, 0.16f, 0.21f, 0.90f);
        GUI.DrawTexture(new Rect(16, 16, 448, 80), Texture2D.whiteTexture);
        GUI.color = oldColor;

        DrawMetric(30, "VUELTA", $"{Mathf.Min(completedLaps + 1, TotalLaps)} / {TotalLaps}");
        DrawMetric(144, "TIEMPO TOTAL", Format(finished ? finalTime : running ? Time.timeAsDouble - raceStart : 0));
        DrawMetric(302, "MEJOR VUELTA", double.IsPositiveInfinity(bestLap) ? "--:--.---" : Format(bestLap));

        DrawNitro(scale, oldColor);

        if (finished)
        {
            float x = (Screen.width / scale - 310) * 0.5f;
            float y = (Screen.height / scale - 116) * 0.5f;
            GUI.color = new Color(0.12f, 0.16f, 0.21f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, 310, 116), Texture2D.whiteTexture);
            GUI.color = oldColor;
            string result = rival == null ? "Carrera completada" :
                rival.Finished && rival.FinishTime < finalTime ? $"{rivalName} ganó" : "¡Ganaste!";
            GUI.Label(new Rect(x + 19, y + 13, 280, 42), result, title);
            GUI.Label(new Rect(x + 20, y + 67, 270, 26), "Pulsa R para volver a la parrilla", caption);
        }
        else if (running && rival != null)
        {
            string rivalStatus = rival.Finished ? $"{rivalName} llegó a la meta" : $"{rivalName} · vuelta {rival.Lap} / {TotalLaps}";
            GUI.Label(new Rect(28, 101, 280, 27), rivalStatus, caption);
        }
        else if (!running)
        {
            GUI.Label(new Rect(28, 101, 250, 27), "Espera la luz verde", caption);
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawNitro(float scale, Color color)
    {
        float y = Screen.height / scale - 92;
        GUI.color = new Color(0.12f, 0.16f, 0.21f, 0.90f);
        GUI.DrawTexture(new Rect(16, y, 250, 76), Texture2D.whiteTexture);
        GUI.color = new Color(0.26f, 0.32f, 0.38f);
        GUI.DrawTexture(new Rect(30, y + 37, 220, 12), Texture2D.whiteTexture);
        GUI.color = kart.IsBoosting ? new Color(0.34f, 0.92f, 1) : new Color(0.10f, 0.63f, 0.94f);
        GUI.DrawTexture(new Rect(30, y + 37, 220 * kart.Nitro / 100, 12), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.Label(new Rect(30, y + 9, 220, 24), $"NITRO  {Mathf.CeilToInt(kart.Nitro)}%     BOTELLAS  {kart.Bottles}", caption);
        GUI.Label(new Rect(30, y + 49, 220, 24), "E  ·  Impulso", caption);
    }
    private void DrawMetric(float x, string label, string text)
    {
        GUI.Label(new Rect(x, 24, 155, 24), label, caption);
        GUI.Label(new Rect(x, 47, 155, 34), text, value);
    }

    private static string Format(double seconds)
    {
        int milliseconds = (int)(seconds * 1000);
        return $"{milliseconds / 60000:00}:{milliseconds / 1000 % 60:00}.{milliseconds % 1000:000}";
    }
}




