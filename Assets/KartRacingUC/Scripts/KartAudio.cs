using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class KartAudio : MonoBehaviour
{
    private static AudioClip music, engine, brake, boost, impact;
    private KartMovement player;
    private KartRival rival;
    private AudioSource motor, tires, effects;
    private float previousSpeed;
    private float lastImpact = float.NegativeInfinity;
    private bool boosting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        music = Resources.Load<AudioClip>("KartAudio/Hiphop80");
        engine = Resources.Load<AudioClip>("KartAudio/Engine");
        brake = Resources.Load<AudioClip>("KartAudio/Brake");
        boost = Resources.Load<AudioClip>("KartAudio/Boost");
        impact = Resources.Load<AudioClip>("KartAudio/Impact");
        var soundtrack = new GameObject("Hiphop 80 BPM");
        DontDestroyOnLoad(soundtrack);
        var source = soundtrack.AddComponent<AudioSource>();
        source.clip = music;
        source.volume = 0.38f;
        source.loop = true;
        source.Play();
        SceneManager.sceneLoaded -= AddRaceAudio;
        SceneManager.sceneLoaded += AddRaceAudio;
        AddRaceAudio(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AddRaceAudio(Scene scene, LoadSceneMode mode)
    {
        var player = FindAnyObjectByType<KartMovement>();
        if (player != null && player.GetComponent<KartAudio>() == null)
            player.gameObject.AddComponent<KartAudio>();

        foreach (var rival in FindObjectsByType<KartRival>())
            if (rival.GetComponent<KartAudio>() == null)
                rival.gameObject.AddComponent<KartAudio>();
    }

    private void Start()
    {
        player = GetComponent<KartMovement>();
        rival = GetComponent<KartRival>();
        float spatial = player != null ? 0 : 1;
        motor = Source(engine, spatial, true);
        tires = Source(brake, spatial, true);
        if (player != null)
        {
            effects = Source(null, 0, false);
            player.Impact += PlayImpact;
        }
    }

    private AudioSource Source(AudioClip clip, float spatial, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = spatial;
        source.dopplerLevel = 0;
        source.maxDistance = 32;
        source.volume = 0;
        if (loop)
            source.Play();
        return source;
    }

    private void Update()
    {
        float speed = Mathf.Abs(player != null ? player.Speed : rival.Speed);
        float rpm = Mathf.Clamp01(speed / 23);
        bool braking = player != null ? player.IsBraking : previousSpeed - speed > 2 * Time.deltaTime && speed > 2;
        float response = 1 - Mathf.Exp(-10 * Time.deltaTime);
        motor.pitch = Mathf.Lerp(motor.pitch, 0.66f + 1.58f * rpm, response);
        motor.volume = Mathf.Lerp(motor.volume, Mathf.Lerp(0.12f, 0.34f, rpm), response);
        tires.volume = Mathf.Lerp(tires.volume, braking ? Mathf.Lerp(0.12f, 0.44f, rpm) : 0, response);
        previousSpeed = speed;

        if (player == null)
            return;
        if (player.IsBoosting && !boosting)
        {
            effects.volume = 1;
            effects.PlayOneShot(boost, 0.7f);
        }
        boosting = player.IsBoosting;
    }

    private void PlayImpact(Vector3 point, Vector3 normal, float speed)
    {
        if (Time.time - lastImpact < 0.15f)
            return;
        lastImpact = Time.time;
        effects.volume = 1;
        effects.PlayOneShot(impact, Mathf.Clamp(speed / 16, 0.24f, 0.86f));
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Impact -= PlayImpact;
    }
}
