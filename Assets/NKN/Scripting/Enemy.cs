using UnityEngine;

/// <summary>
/// Controla la IA de un enemigo en un beat 'em up. Persigue al jugador, ataca cuando
/// está en rango y puede bloquearse de manera reactiva en función de las acciones del jugador.
/// Delega toda la lógica de movimiento y ataques en el componente Shinobi.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Shinobi))]
public class Enemy : MonoBehaviour
{
    #region Configuración
    [Header("Objetivo")]
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";

    [Header("Comportamiento de movimiento")]
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float stopDistance   = 1.0f;
    [SerializeField] private float chaseSpeed     = 1.0f;

    [Header("Ataques")]
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float kickChance     = 0.3f;
    [SerializeField] private float kunaiChance    = 0.15f;

    [Header("Bloqueo Reactivo")]
    [SerializeField] private float blockChance    = 0.7f;
    #endregion

    #region Estado
    private Shinobi shinobi;
    private Shinobi playerShinobi;
    private float lastAttackTime = -999f;
    #endregion

    private void Awake()
    {
        shinobi = GetComponent<Shinobi>();
    }

    private void Start()
    {
        // Si no se ha asignado desde el inspector, buscar por tag
        if (target == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                target        = playerObj.transform;
                playerShinobi = playerObj.GetComponent<Shinobi>();
            }
            else
            {
                Debug.LogWarning($"Enemy: no se encontró ningún objeto con tag '{playerTag}'");
            }
        }
        else
        {
            playerShinobi = target.GetComponent<Shinobi>();
        }
    }

    private void Update()
    {
        if (target == null) return;

        // Calcular dirección y distancia horizontal hacia el jugador
        Vector3 toTarget     = target.position - transform.position;
        float horizontalDist = new Vector2(toTarget.x, toTarget.z).magnitude;

        float moveX = 0f;
        float moveZ = 0f;
        bool punchPressed = false;
        bool kickPressed  = false;
        bool kunaiPressed = false;

        if (horizontalDist > attackDistance)
        {
            // Correr hacia el jugador
            Vector3 dir = toTarget.normalized;
            moveX = dir.x * chaseSpeed;
            moveZ = dir.z * chaseSpeed;
        }
        else if (horizontalDist > stopDistance)
        {
            // Acercarse más lentamente
            Vector3 dir = toTarget.normalized;
            moveX = dir.x * chaseSpeed * 0.5f;
            moveZ = dir.z * chaseSpeed * 0.5f;
        }
        else
        {
            // Detenerse y atacar aleatoriamente
            moveX = 0f;
            moveZ = 0f;

            if (!shinobi.IsAttacking && Time.time - lastAttackTime >= attackCooldown)
            {
                float roll = Random.value;
                if (roll < kunaiChance)
                {
                    kunaiPressed = true;
                }
                else if (roll < kunaiChance + kickChance)
                {
                    kickPressed = true;
                }
                else
                {
                    punchPressed = true;
                }
                lastAttackTime = Time.time;
            }
        }

        // Bloqueo reactivo: si el jugador ataca y está cerca, bloquear con cierta probabilidad
        bool blockHeld = false;
        if (playerShinobi != null && playerShinobi.IsAttacking)
        {
            float distToPlayer = Vector3.Distance(transform.position, target.position);
            if (distToPlayer < attackDistance * 1.5f && Random.value < blockChance)
            {
                blockHeld = true;
            }
        }

        // No queremos saltar con la IA enemiga, así que jumpPressed siempre es false
        shinobi.ProcessInput(moveX, moveZ,
                             false,           // jumpPressed
                             punchPressed,
                             kickPressed,
                             kunaiPressed,
                             blockHeld);
    }

    #region Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
    #endregion
}
