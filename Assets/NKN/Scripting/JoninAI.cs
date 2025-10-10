using System.Collections;
using UnityEngine;

// JoninAI controla a un enemigo que utiliza las mismas animaciones y ataques
// que PlayerPlay, pero decide por sí mismo cuándo moverse, saltar y atacar.
// Un parámetro de inteligencia (0‑100) modula su comportamiento: cuanto mayor
// sea la inteligencia, más acciones tendrá disponibles, más rápido será y más
// agresivo o evasivo podrá ser.  Se utiliza la anotación Range para
// permitir ajustar la inteligencia desde el inspector mediante una barra.
// Obliga a que exista Rigidbody, Animator y Collider para evitar NullReference
// y que el enemigo se quede en T-pose si no se asigna un controlador.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class JoninAI : MonoBehaviour
{
    #region Configuración
    [Header("Movimiento")]
    [SerializeField] private float baseMoveSpeed = 5f;
    [SerializeField] private float zLimit = 2f;

    [Header("Salto")]
    [SerializeField] private float jumpForce = 7.5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private int maxJumps = 2;

    [Header("Ataques")]
    [SerializeField] private float punchLock = 0.35f;
    [SerializeField] private float kickLock = 0.45f;
    [SerializeField] private float kunaiLock = 0.40f;
    [SerializeField] private float dropKickForce = 8f;

    [Header("Kunai")]
    [SerializeField] private GameObject kunaiPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float kunaiSpeed = 12f;

    [Header("Combo")]
    [SerializeField] private float comboResetTime = 1.5f;

    [Header("Inteligencia")]
    // Inteligencia del enemigo (0‑100). Se muestra como barra en el inspector.
    [Range(0, 100)]
    public int intelligence = 50;
    #endregion

    #region Componentes y estado interno
    private Rigidbody rb;
    private Animator anim;
    private Collider col;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isAttacking;
    private bool isDropKicking;
    private bool isFlipping;
    private bool isBlocking;
    private float inputX;
    private float inputZ;
    private float lastTimeGrounded;
    private int jumpCount = 0;
    private int punchStage = 0;
    private float lastPunchTime;
    #endregion

    #region Referencias externas
    // Referencia al jugador para poder seguirlo y reaccionar a sus ataques
    private Transform playerTarget;
    private PlayerPlay playerController;
    #endregion

    #region Animator Hashes
    private static readonly int ANIM_IsRunning = Animator.StringToHash("isRunning");
    private static readonly int ANIM_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int ANIM_TrigJump = Animator.StringToHash("Jump");
    private static readonly int ANIM_TrigFlip = Animator.StringToHash("Flip");
    private static readonly int ANIM_TrigPunch = Animator.StringToHash("Punch");
    private static readonly int ANIM_TrigKunai = Animator.StringToHash("Kunai");
    private static readonly int ANIM_TrigKickGround = Animator.StringToHash("KickGround");
    private static readonly int ANIM_TrigKickAir = Animator.StringToHash("KickAir");
    private static readonly int ANIM_PunchStage = Animator.StringToHash("PunchStage");
    private static readonly int ANIM_IsDropKicking = Animator.StringToHash("isDropKicking");
    private static readonly int ANIM_IsFlipping = Animator.StringToHash("isFlipping");
    private static readonly int ANIM_IsBlocking = Animator.StringToHash("isBlocking");
    #endregion

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider>();
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;

        // Si no existe un groundCheck, lo creamos en tiempo de ejecución
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.position = new Vector3(col.bounds.center.x, col.bounds.min.y + 0.01f, col.bounds.center.z);
            groundCheck = gc.transform;
        }

        // El enemigo ignora colisiones con la capa del jugador para evitar que se quede atascado
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Collider playerCol = playerObj.GetComponent<Collider>();
            if (playerCol != null)
                Physics.IgnoreCollision(col, playerCol, true);

            // Si no tenemos un controlador de animaciones asignado en este Animator, copiamos el del jugador.
            // Esto evita la pose en T cuando el enemigo no tiene controller en el inspector.
            if (anim != null && anim.runtimeAnimatorController == null)
            {
                Animator playerAnim = playerObj.GetComponent<Animator>();
                if (playerAnim != null)
                {
                    anim.runtimeAnimatorController = playerAnim.runtimeAnimatorController;
                }
            }
        }
    }

    private void Start()
    {
        // Buscar el objetivo del jugador en la escena
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerPlay>();
        }
    }

    private void Update()
    {
        // Actualizar información del suelo
        CheckGround();

        // Determinar bloqueo si la inteligencia permite cubrirse
        if (intelligence >= 50 && playerController != null && playerTarget != null)
        {
            // Si el jugador está atacando y está cerca, bloquear
            float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
            if (playerController.IsAttacking && distToPlayer < 2.5f)
            {
                isBlocking = true;
            }
            else
            {
                isBlocking = false;
            }
        }
        else
        {
            isBlocking = false;
        }

        // Decidir movimiento en el plano X/Z hacia el jugador
        Vector3 desiredDir = Vector3.zero;
        if (playerTarget != null)
        {
            Vector3 toPlayer = playerTarget.position - transform.position;
            // Evitar mover en Y; normalizamos en X/Z
            desiredDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (desiredDir.magnitude > 0.1f)
                desiredDir.Normalize();
        }

        // Ajustar velocidad según inteligencia. Usamos un multiplicador lineal entre 1.0 y 1.5
        float speedMultiplier = 1f + (intelligence / 100f) * 0.5f;
        float currentSpeed = baseMoveSpeed * speedMultiplier;

        // Establecer inputs ficticios para reutilizar MoveCharacter()
        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            // Acercarse o alejarse del jugador según inteligencia baja
            if (intelligence < 10 && playerTarget != null)
            {
                // Mantener distancia constante: retrocede si está demasiado cerca del jugador
                float distReal = Vector3.Distance(transform.position, playerTarget.position);
                if (distReal < 3f)
                {
                    desiredDir = -desiredDir;
                }
            }

            inputX = Mathf.Clamp(desiredDir.x, -1f, 1f);
            inputZ = Mathf.Clamp(desiredDir.z, -1f, 1f);
        }
        else
        {
            // Si está atacando, dropkick o bloqueando, no queremos que se mueva
            inputX = 0f;
            inputZ = 0f;
        }

        // Gestionar ataques sólo cuando no está ocupado
        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            float dist = playerTarget != null ? Vector3.Distance(transform.position, playerTarget.position) : Mathf.Infinity;

            // Inteligencia extremadamente baja: sólo ataques a distancia y puñetazos en corto
            if (intelligence < 10)
            {
                if (dist > 3.5f)
                {
                    // Lanzar kunai
                    StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
                }
                else
                {
                    // Realizar un simple puñetazo
                    HandlePunchAI();
                }
            }
            // Inteligencia baja: sin patadas, pero puede saltar y lanzar kunai
            else if (intelligence < 30)
            {
                if (dist > 5f)
                {
                    StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
                }
                else if (dist > 2f && intelligence >= 15 && CanJump() && Random.value < 0.1f)
                {
                    // Ocasionalmente saltar para acercarse o esquivar
                    PerformJump(jumpCount + 1);
                }
                else
                {
                    HandlePunchAI();
                }
            }
            // Inteligencia media: puede usar patadas y salto
            else if (intelligence < 50)
            {
                if (dist > 5f)
                {
                    StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
                }
                else if (dist > 2f && Random.value < 0.4f)
                {
                    // Patada si está en el suelo, dropkick si está en el aire
                    if (isGrounded)
                        StartCoroutine(DoAttack(ANIM_TrigKickGround, kickLock, false));
                    else
                        StartCoroutine(DoDropKick(ANIM_TrigKickAir, kickLock));
                }
                else if (CanJump() && Random.value < 0.2f)
                {
                    PerformJump(jumpCount + 1);
                }
                else
                {
                    HandlePunchAI();
                }
            }
            // Inteligencia media/alta: puede cubrirse, no salta pero usa patadas y kunais
            else if (intelligence <= 80)
            {
                if (dist > 5f)
                {
                    StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
                }
                else if (dist > 2f && Random.value < 0.5f)
                {
                    // Sólo patada desde el suelo
                    if (isGrounded)
                        StartCoroutine(DoAttack(ANIM_TrigKickGround, kickLock, false));
                    else
                        StartCoroutine(DoDropKick(ANIM_TrigKickAir, kickLock));
                }
                else
                {
                    HandlePunchAI();
                }
            }
            // Inteligencia alta: puede usar todos los ataques; solo a 100 intentará esquivar
            else
            {
                if (dist > 5f && Random.value < 0.7f)
                {
                    StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
                }
                else if (dist > 2f && Random.value < 0.6f)
                {
                    // Mezcla de patadas y dropkick
                    if (isGrounded)
                        StartCoroutine(DoAttack(ANIM_TrigKickGround, kickLock, false));
                    else
                        StartCoroutine(DoDropKick(ANIM_TrigKickAir, kickLock));
                }
                else if (intelligence == 100 && CanJump() && Random.value < 0.3f)
                {
                    // Salta ocasionalmente para esquivar
                    PerformJump(jumpCount + 1);
                }
                else
                {
                    HandlePunchAI();
                }
            }
        }

        // Reset del combo de puñetazos si pasó demasiado tiempo
        if (punchStage > 0 && Time.time - lastPunchTime > comboResetTime)
            ResetPunchCombo();

        // Actualizar valores de animación
        bool moving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputZ) > 0.01f;
        bool canRun = moving && isGrounded && !isAttacking && !isDropKicking && !isBlocking;
        anim.SetBool(ANIM_IsRunning, canRun);
        anim.SetBool(ANIM_IsGrounded, isGrounded);
        anim.SetBool(ANIM_IsDropKicking, isDropKicking);
        anim.SetBool(ANIM_IsFlipping, isFlipping);
        anim.SetBool(ANIM_IsBlocking, isBlocking);

        // Si termina el dropkick al tocar el suelo
        if (isDropKicking && isGrounded)
            isDropKicking = false;
    }

    private void FixedUpdate()
    {
        MoveCharacter();
        ApplyExtraGravity();
    }

    #region Métodos Auxiliares
    private void MoveCharacter()
    {
        // La velocidad depende de la inteligencia (calculada en Update)
        float speedMultiplier = 1f + (intelligence / 100f) * 0.5f;
        float currentSpeed = baseMoveSpeed * speedMultiplier;

        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            Vector3 move = new Vector3(inputX, 0f, inputZ).normalized * currentSpeed;
            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            // Rotar para mirar en la dirección de movimiento en el eje X
            if (Mathf.Abs(inputX) > 0.01f)
            {
                Vector3 facing = new Vector3(Mathf.Sign(inputX), 0f, 0f);
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }
        else if (isAttacking || isBlocking)
        {
            // Detener movimiento horizontal mientras ataca o se cubre
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    private void ApplyExtraGravity()
    {
        if (!isGrounded && rb.linearVelocity.y < 0f)
            rb.AddForce(Physics.gravity * 1.5f, ForceMode.Acceleration);
    }

    private IEnumerator DoAttack(int triggerHash, float lockTime, bool doThrowKunai, bool allowMovement = false)
    {
        if (!allowMovement) isAttacking = true;
        anim.SetTrigger(triggerHash);
        if (doThrowKunai) SpawnKunai();
        yield return new WaitForSeconds(lockTime);
        if (!allowMovement) isAttacking = false;
    }

    private IEnumerator DoDropKick(int triggerHash, float lockTime)
    {
        isDropKicking = true;
        anim.SetTrigger(triggerHash);
        yield return new WaitForSeconds(lockTime);
        isDropKicking = false;
    }

    // Gestiona un puñetazo simple o combo
    private void HandlePunchAI()
    {
        punchStage++;
        if (punchStage > 3) punchStage = 1;
        lastPunchTime = Time.time;
        anim.SetInteger(ANIM_PunchStage, punchStage);
        anim.SetTrigger(ANIM_TrigPunch);
        float lockTime = punchLock;
        if (punchStage == 2) lockTime += 0.1f;
        if (punchStage == 3) lockTime += 0.2f;
        StartCoroutine(PunchRoutine(lockTime));
    }

    private IEnumerator PunchRoutine(float lockTime)
    {
        isAttacking = true;
        yield return new WaitForSeconds(lockTime);
        isAttacking = false;
        if (punchStage >= 3)
            ResetPunchCombo();
    }

    private void ResetPunchCombo()
    {
        punchStage = 0;
        anim.SetInteger(ANIM_PunchStage, 0);
    }

    private void SpawnKunai()
    {
        if (kunaiPrefab == null || firePoint == null) return;
        float facing = Mathf.Sign(transform.forward.x);
        Vector3 baseRotation = new Vector3(0f, 180f, 90f);
        Vector3 finalRotation = new Vector3(
            baseRotation.x,
            baseRotation.y * facing,
            baseRotation.z * facing
        );
        Quaternion prefabRot = Quaternion.Euler(finalRotation);
        GameObject k = Instantiate(kunaiPrefab, firePoint.position, prefabRot);
        if (k.TryGetComponent(out Rigidbody krb))
        {
            Vector3 forwardDir = transform.forward.normalized;
            krb.linearVelocity = forwardDir * kunaiSpeed;
        }
    }

    private void CheckGround()
    {
        bool sphereGround = Physics.CheckSphere(groundCheck.position, groundRadius, groundLayer, QueryTriggerInteraction.Ignore);
        float rayLength = 0.35f;
        bool rayGround = Physics.Raycast(groundCheck.position, Vector3.down, rayLength, groundLayer, QueryTriggerInteraction.Ignore);
        bool groundedNow = sphereGround || rayGround;
        if (groundedNow)
            lastTimeGrounded = Time.time;
        if (groundedNow && !wasGrounded && rb.linearVelocity.y <= 0f)
        {
            jumpCount = 0;
            isDropKicking = false;
            isFlipping = false;
        }
        isGrounded = groundedNow;
        wasGrounded = groundedNow;
    }

    private bool CanJump()
    {
        if (intelligence >= 50 && intelligence <= 80)
        {
            // No puede saltar en este rango de inteligencia
            return false;
        }
        if (jumpCount >= maxJumps) return false;
        if (jumpCount == 0)
        {
            bool withinCoyote = (Time.time - lastTimeGrounded) <= coyoteTime;
            return isGrounded || withinCoyote;
        }
        return true;
    }

    private void PerformJump(int jumpIndex)
    {
        // No saltar si la inteligencia está en rango que lo prohíbe
        if (intelligence >= 50 && intelligence <= 80) return;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        if (jumpIndex == 1)
        {
            anim.SetTrigger(ANIM_TrigJump);
            isFlipping = false;
        }
        else if (jumpIndex == 2)
        {
            anim.SetTrigger(ANIM_TrigFlip);
            isFlipping = true;
        }
        jumpCount = Mathf.Clamp(jumpIndex, 0, maxJumps);
    }
    #endregion
}