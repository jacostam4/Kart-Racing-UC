using System.Collections.Generic;
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
        // La escena ya trae a Juan como jugador y a Gabriel como rival.
        GameObject chosen = KartFrontEnd.SelectedModel;
        if (chosen == null)
            chosen = juan;

        // Si se entra directo a la escena sin pasar por el menú, se deja como estaba.
        GameObject[] pilots = KartFrontEnd.AllPilots;
        string[] names = KartFrontEnd.AllNames;
        if (pilots == null || pilots.Length == 0)
            return;

        // Rivales = todos los pilotos del menú menos el que eligió el jugador.
        var rivals = new List<int>();
        for (int i = 0; i < pilots.Length; i++)
        {
            if (pilots[i] != null && pilots[i] != chosen)
                rivals.Add(i);
        }

        // Segundo rival: copiamos el kart rival ANTES de cambiarle el piloto,
        // le damos otro puesto en la parrilla y un ritmo un poco distinto.
        if (rivals.Count >= 2)
        {
            var copy = Instantiate(rival.gameObject, rival.transform.parent);
            int index = rivals[1];
            copy.name = names[index] + " Rival";
            copy.GetComponent<KartRival>().Configure(-11.5f, -1.6f, 0.97f);
            copy.GetComponent<KartRival>().DisplayName = FirstName(names[index]);
            copy.GetComponent<KartDriverPresentation>().ReplaceDriver(pilots[index]);
        }

        // Jugador.
        if (chosen != juan)
            player.ReplaceDriver(chosen);

        // Primer rival (el kart que ya estaba en la escena).
        if (rivals.Count >= 1)
        {
            int index = rivals[0];
            if (pilots[index] != gabriel)
                rival.ReplaceDriver(pilots[index]);
            race.SetRivalName(FirstName(names[index]));
        }
    }

    private static string FirstName(string fullName)
    {
        int space = fullName.IndexOf(' ');
        return space > 0 ? fullName.Substring(0, space) : fullName;
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            SceneManager.LoadScene("CharacterSelection");
    }
}
