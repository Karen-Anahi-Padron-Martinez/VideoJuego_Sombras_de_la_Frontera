using UnityEngine;

/// <summary>
/// Datos de una cinemática: pasos en orden y la escena que se carga al terminar.
/// La reproduce CutscenePlayer.
/// </summary>
[CreateAssetMenu(fileName = "Cinematica", menuName = "Sombras de la Frontera/Cinemática")]
public class CutsceneData : ScriptableObject
{
    public enum TipoPaso { Negro, Imagen }

    [System.Serializable]
    public class Paso
    {
        public TipoPaso tipo;
        [TextArea(2, 6)] public string texto;
        public Sprite imagen;
        [Tooltip("Segundos de pausa tras escribir el texto; luego avanza solo. 0 = esperar a que el jugador avance.")]
        public float duracion;

        [Header("Opcional")]
        [Tooltip("Sonido de ambiente en loop (crossfade). Vacío = mantener el ambiente actual.")]
        public AudioClip ambiente;
        [Tooltip("Multiplicador de la máquina de escribir. 0 = texto completo de golpe.")]
        public float velocidadTexto = 1f;
        [Tooltip("Tamaño del texto. 0 = tamaño por defecto.")]
        public float tamanoTexto;
        [Tooltip("Duración del fade de este paso. 0 = fade por defecto.")]
        public float duracionFade;
    }

    public string siguienteEscena;
    public Paso[] pasos = new Paso[0];
}
