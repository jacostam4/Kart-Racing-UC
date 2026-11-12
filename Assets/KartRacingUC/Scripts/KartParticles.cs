using UnityEngine;

public sealed class KartParticles : MonoBehaviour
{
    [SerializeField] private KartMovement kart;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem exhaust;

    private ParticleSystem sparks;
    private KartSkidMarks skidMarks;
    private float lastImpact = float.NegativeInfinity;

    private void OnEnable()
    {
        if (skidMarks == null)
            skidMarks = new KartSkidMarks(kart);
        if (sparks == null)
            sparks = CreateSparks();
        kart.Impact += EmitSparks;
    }

    private void OnDisable()
    {
        kart.Impact -= EmitSparks;
        skidMarks?.Break();
    }

    private void OnDestroy()
    {
        skidMarks?.Dispose();
        if (sparks != null)
            Destroy(sparks.gameObject);
    }

    private void Update()
    {
        float speed = Mathf.Abs(kart.Speed);
        var dustEmission = dust.emission;
        dustEmission.rateOverTime = speed > 1 ? speed * (0.9f + Mathf.Abs(kart.Steering)) : 0;
        var exhaustEmission = exhaust.emission;
        exhaustEmission.rateOverTime = 4 + speed * 0.45f;
    }

    private void LateUpdate()
    {
        if (kart.IsBraking)
            skidMarks.Draw();
        else
            skidMarks.Break();
    }

    private ParticleSystem CreateSparks()
    {
        var emitter = new GameObject("Barrier Sparks");
        emitter.transform.SetParent(transform, false);
        var system = emitter.AddComponent<ParticleSystem>();
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.75f;
        main.maxParticles = 240;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.38f, 0.08f), 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dust.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.13f;
        renderer.lengthScale = 1.5f;
        return system;
    }

    private void EmitSparks(Vector3 point, Vector3 normal, float speed)
    {
        if (Time.time - lastImpact < 0.15f)
            return;
        lastImpact = Time.time;

        int count = Mathf.Clamp(Mathf.RoundToInt(speed * 3), 10, 38);
        for (int i = 0; i < count; i++)
        {
            var velocity = (normal + Vector3.up * 0.45f + Random.insideUnitSphere * 0.85f).normalized;
            var particle = new ParticleSystem.EmitParams
            {
                position = point + normal * 0.045f,
                velocity = velocity * Random.Range(2.2f, 5.8f),
                startLifetime = Random.Range(0.16f, 0.40f),
                startSize = Random.Range(0.04f, 0.09f),
                startColor = new Color32(255, (byte)Random.Range(166, 246), 65, 255)
            };
            sparks.Emit(particle, 1);
        }
    }
}
