using UnityEngine;

/*
 * La clase Player gestiona las entradas de usuario y las delega a un componente
 * Shinobi. De este modo se separan completamente las lecturas de teclado y la
 * lógica de movimiento/comportamiento del personaje.
 */
public class Player : MonoBehaviour
{
    [Tooltip("Referencia al componente Shinobi que controla el personaje del jugador")]
    [SerializeField] private Shinobi shinobi;

    private void Awake()
    {
        // Si no se ha asignado desde el inspector, lo intentamos obtener del propio GameObject
        if (shinobi == null)
        {
            shinobi = GetComponent<Shinobi>();
        }

        if (shinobi == null)
        {
            Debug.LogError("Player: no se encontró un componente Shinobi asociado.");
        }
    }

    private void Update()
    {
        if (shinobi == null) return;

        // Recolectar inputs de movimiento y acciones
        float moveX      = Input.GetAxisRaw("Horizontal");
        float moveZ      = Input.GetAxisRaw("Vertical");
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        bool punchPress  = Input.GetKeyDown(KeyCode.P);
        bool kickPress   = Input.GetKeyDown(KeyCode.K);
        bool kunaiPress  = Input.GetKeyDown(KeyCode.O);
        bool blockHeld   = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // Pasar las entradas a Shinobi
        shinobi.ProcessInput(moveX, moveZ,
                             jumpPressed,
                             punchPress,
                             kickPress,
                             kunaiPress,
                             blockHeld);
    }
}
