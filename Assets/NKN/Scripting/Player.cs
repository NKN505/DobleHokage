using UnityEngine;

/*
 * La clase Player gestiona las entradas de usuario (teclado en este caso) y delega
 * en el componente Shinobi las acciones de movimiento, salto y ataque. Este
 * enfoque separa claramente la lógica de control de la lógica de comportamiento
 * del personaje, permitiendo que la misma clase Shinobi pueda ser reutilizada
 * tanto por jugadores como por enemigos controlados por IA.
 */
[DisallowMultipleComponent]
[RequireComponent(typeof(Shinobi))]
public class Player : MonoBehaviour
{
    [Tooltip("Referencia al componente Shinobi que controla el personaje del jugador")]
    [SerializeField] private Shinobi shinobi;

    private void Awake()
    {
        // Si no se ha asignado desde el inspector, intentamos obtenerlo del mismo GameObject
        if (shinobi == null)
        {
            shinobi = GetComponent<Shinobi>();
        }

        if (shinobi == null)
        {
            Debug.LogError("Player: No se encontró un componente Shinobi asociado.");
        }
    }

    private void Update()
    {
        if (shinobi == null) return;

        // Lectura de entradas de movimiento
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        shinobi.SetMovement(moveX, moveZ);

        // Estado de bloqueo (pulsando Alt izquierdo o derecho)
        bool isBlocking = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        shinobi.SetBlocking(isBlocking);

        // Salto
        if (Input.GetKeyDown(KeyCode.Space))
        {
            shinobi.Jump();
        }

        // Ataques
        if (Input.GetKeyDown(KeyCode.P))
        {
            shinobi.Punch();
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            shinobi.Kick();
        }
        else if (Input.GetKeyDown(KeyCode.O))
        {
            shinobi.ThrowKunai();
        }
    }
}
