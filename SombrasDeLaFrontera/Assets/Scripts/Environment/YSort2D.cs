using UnityEngine;

/// <summary>
/// Gestiona la profundidad 2D (Top-Down Y-Sorting):
/// - Los objetos con menor Y (más al sur/abajo en pantalla) se dibujan por DELANTE.
/// - Los objetos con mayor Y (más al norte/arriba en pantalla) se dibujan por DETRÁS.
/// - Para obstáculos con volumen (árboles, cactus, camionetas, arbustos, rocas):
///   El orden se calcula desde su base inferior (bounds.min.y), garantizando que
///   cualquier enemigo (serpiente, alacrán, coyote, perro) o Mateo que pase
///   por detrás o dentro del obstáculo quede naturalmente CUBIERTO por este.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class YSort2D : MonoBehaviour
{
    [Tooltip("Si es true, calcula el orden una sola vez al iniciar (para árboles, rocas, arbustos, cactus, etc.)")]
    public bool isStatic = false;

    [Tooltip("Si es true, calcula el orden desde la base inferior del sprite (sr.bounds.min.y)")]
    public bool useBottomBounds = false;

    [Tooltip("Offset vertical adicional")]
    public float yOffset = 0f;

    [Tooltip("Base para asegurar números de orden positivos")]
    public int baseOrder = 5000;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        if (!isStatic)
        {
            UpdateSortingOrder();
        }
    }

    public void UpdateSortingOrder()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        float sortY;
        if (useBottomBounds)
        {
            // Base inferior del sprite (donde el obstáculo toca la arena)
            sortY = sr.bounds.min.y + yOffset;
        }
        else
        {
            // Posición de los pies/centro de la criatura móvil
            sortY = transform.position.y + yOffset;
        }

        sr.sortingOrder = baseOrder - Mathf.RoundToInt(sortY * 100f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateSortingOrder();
    }
#endif
}
