using System.Collections;
using UnityEngine;

public sealed class KartStartLights : MonoBehaviour
{
    [SerializeField] private KartMovement kart;
    [SerializeField] private Renderer[] lenses;
    [SerializeField] private Light[] lamps;

    private MaterialPropertyBlock properties;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private Coroutine countdown;
    public event System.Action CountdownStarted;
    public event System.Action GreenLight;

    private void OnEnable()
    {
        properties = new MaterialPropertyBlock();
        kart.ReturnedToGrid += Restart;
    }

    private void Start() => Restart();

    private void OnDisable()
    {
        kart.ReturnedToGrid -= Restart;
        if (countdown != null)
            StopCoroutine(countdown);
        countdown = null;
    }

    private void Restart()
    {
        if (countdown != null)
            StopCoroutine(countdown);

        kart.CanDrive = false;
        CountdownStarted?.Invoke();
        countdown = StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        Show(0);
        yield return new WaitForSeconds(1.4f);
        Show(1);
        yield return new WaitForSeconds(0.85f);
        Show(2);
        kart.CanDrive = true;
        GreenLight?.Invoke();
        yield return new WaitForSeconds(2);
        Show(-1);
        countdown = null;
    }

    private void Show(int active)
    {
        for (int index = 0; index < lenses.Length; index++)
        {
            bool lit = index == active;
            Color color = lenses[index].sharedMaterial.GetColor(BaseColor);
            lenses[index].GetPropertyBlock(properties);
            properties.SetColor(BaseColor, lit ? color : color * 0.23f);
            properties.SetColor(EmissionColor, lit ? color * 3 : Color.black);
            lenses[index].SetPropertyBlock(properties);
            lamps[index].enabled = lit;
        }
    }
}



