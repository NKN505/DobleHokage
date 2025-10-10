using UnityEngine;

/*
 * La clase Enemy controla una IA muy básica para un enemigo en un juego
 * beat 'em up. Al igual que Player, delega toda la lógica de movimiento y
 * ataques en el componente Shinobi, pero sus decisiones se basan en la
 * posición del jugador en lugar de entradas de teclado. Este ejemplo de IA
 * persigue al jugador cuando está lejos y ataca cuando está dentro del
 * rango de ataque. También puede bloquearse si detecta que el jugador
 * está atacando.
 */
[DisallowMultipleComponent]
[RequireComponent(typeof(Shinobi))]
public class Enemy : MonoBehaviour
{
    // Referencia al objetivo a seguir (normalmente el jugador)
    [Tooltip("Referencia al Transform del jugador al que perseguir")] 
    public Transform target;

    // Distancia a la que el enemigo decide atacar en lugar de moverse
    [SerializeField] private float attackDistance = 1.5f;

    // Velocidad de persecución (se utiliza para escalar la entrada de movimiento)
    [SerializeField] private float chaseSpeed = 1.0f;

    private Shinobi shinobi;

    private void Awake()
    {
        shinobi = GetComponent<Shinobi>();
    }

    private void Update()
    {
        // Si no hay objetivo asignado, no hacemos nada
        if (target == null) return;

        // Calculamos la dirección horizontal hacia el objetivo (solo en el eje X/Z)
        Vector3 toTarget = target.position - transform.position;
        // Sólo se considera la distancia en el plano horizontal para el ataque
        float horizontalDist = new Vector2(toTarget.x, toTarget.z).magnitude;

        float moveX = 0f;
        float moveZ = 0f;

        // Si estamos fuera de rango, ajustamos la entrada de movimiento para acercarnos
        if (horizontalDist > attackDistance)
        {
            // Normalizamos la dirección para que la velocidad dependa de chaseSpeed
            Vector3 dir = toTarget.normalized;
            moveX = Mathf.Sign(dir.x) * Mathf.Min(Mathf.Abs(dir.x), 1f) * chaseSpeed;
            moveZ = Mathf.Sign(dir.z) * Mathf.Min(Mathf.Abs(dir.z), 1f) * chaseSpeed;
        }
        else
        {
            // Dentro del rango de ataque no nos movemos más
            moveX = 0f;
            moveZ = 0f;

            // Si no está atacando, iniciamos un ataque simple
            if (!shinobi.IsAttacking)
            {
                shinobi.Punch();
            }
        }

        // Establecemos la dirección de movimiento en Shinobi
        shinobi.SetMovement(moveX, moveZ);

        // Podemos decidir bloquear si el jugador está atacando
        bool shouldBlock = false;
        Shinobi playerShinobi = target.GetComponent<Shinobi>();
        if (playerShinobi != null && playerShinobi.IsAttacking)
        {
            shouldBlock = true;
        }
        shinobi.SetBlocking(shouldBlock);
    }
}
