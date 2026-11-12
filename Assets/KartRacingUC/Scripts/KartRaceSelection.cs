using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class KartRaceSelection : MonoBehaviour
{
    [SerializeField] private KartDriverPresentation player;
    [SerializeField] private KartDriverPresentation rival;
    [SerializeField] private KartTimeTrial race;
    [SerializeField] private GameObject juan;
    [SerializeField] private GameObject gabriel;

    private void Start()
    {
        if (KartFrontEnd.SelectedPilot == 0)
            return;

        player.ReplaceDriver(gabriel);
        rival.ReplaceDriver(juan);
        race.SetRivalName("Juan");
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SceneManager.LoadScene("CharacterSelection");
    }
}
