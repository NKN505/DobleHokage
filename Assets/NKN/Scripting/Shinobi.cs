using System.Collections;
using UnityEngine;

/*
 * La clase Shinobi encapsula toda la lógica de movimiento y ataques que antes
 * residía en PlayerPlay. La idea es centralizar el control del personaje
 * (animaciones, físicas y colisiones) y exponer métodos públicos que puedan
 * ser invocados por controladores externos. De este modo la clase Player
 * únicamente se ocupa de gestionar entradas de teclado y trasladarlas a
 * llamadas sobre Shinobi, mientras que las inteligencias artificiales (Enemy)
 * también pueden reutilizar los mismos movimientos llamando a los métodos
 * públicos adecuados.
 */
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class Shinobi : MonoBehaviour
{
    #region Configuración
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
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

    [Header("Rotación del Kunai")]
    [SerializeField] private Vector3 kunaiRotationOffset = Vector3.zero;

    [Header("Combo")]
    [SerializeField] private float comboResetTime = 1.5f;
    #endregion

    #region Componentes
    private Rigidbody rb;
    private Animator anim;
    private Collider col;
    #endregion

    #region Estado interno
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

    // Expone el estado de ataque para que controladores externos puedan consultarlo
    public bool IsAttacking => isAttacking;
    #endregion

    #region Hashes de Animator
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

    #region Unity Methods
    private void Awake()
    {
        // Recupera los componentes necesarios
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider>();

        // Configura algunas propiedades del rigidbody para evitar rotaciones no deseadas
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;

        // Si no hay groundCheck asignado, se crea automáticamente en la base del collider
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.position = new Vector3(col.bounds.center.x, col.bounds.min.y + 0.01f, col.bounds.center.z);
            groundCheck = gc.transform;
        }

        // Ignora colisiones con el objeto etiquetado como Enemy (útil para juegos beat em up)
        GameObject enemyObj = GameObject.FindGameObjectWithTag("Enemy");
        if (enemyObj != null)
        {
            Collider enemyCol = enemyObj.GetComponent<Collider>();
            if (enemyCol != null)
                Physics.IgnoreCollision(col, enemyCol, true);
        }
    }

    private void Update()
    {
        // Se ejecutan tareas que requieren consultar el estado cada frame.
        CheckGround();

        // Si hay un combo de puñetazos activo, se controla su reseteo
        if (punchStage > 0 && Time.time - lastPunchTime > comboResetTime)
            ResetPunchCombo();

        // Actualiza los parámetros del Animator para reflejar el estado actual
        bool moving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputZ) > 0.01f;
        bool canRun = moving && isGrounded && !isAttacking && !isDropKicking && !isBlocking;

        anim.SetBool(ANIM_IsRunning, canRun);
        anim.SetBool(ANIM_IsGrounded, isGrounded);
        anim.SetBool(ANIM_IsDropKicking, isDropKicking);
        anim.SetBool(ANIM_IsFlipping, isFlipping);
        anim.SetBool(ANIM_IsBlocking, isBlocking);

        // Si el dropkick ha terminado y hemos tocado suelo, reseteamos su estado
        if (isDropKicking && isGrounded)
            isDropKicking = false;
    }

    private void FixedUpdate()
    {
        // Aplicar el movimiento y la gravedad adicional en FixedUpdate para una física estable
        MoveCharacter();
        ApplyExtraGravity();
    }
    #endregion

    #region API pública para controladores externos
    /// <summary>
    /// Establece la dirección de movimiento deseada. Debe llamarse cada frame desde un controlador externo (p.ej. Player o Enemy).
    /// </summary>
    public void SetMovement(float x, float z)
    {
        inputX = Mathf.Clamp(x, -1f, 1f);
        inputZ = Mathf.Clamp(z, -1f, 1f);
    }

    /// <summary>
    /// Activa o desactiva el bloqueo. Cuando se está bloqueando se detienen los ataques y el movimiento.
    /// </summary>
    public void SetBlocking(bool block)
    {
        isBlocking = block;
    }

    /// <summary>
    /// Solicita un salto. Comprueba internamente si se puede saltar (coyote time, número de saltos, ataque en curso).
    /// </summary>
    public void Jump()
    {
        if (isAttacking || isDropKicking || isBlocking)
            return;
        if (CanJump())
        {
            PerformJump(jumpCount + 1);
        }
    }

    /// <summary>
    /// Solicita lanzar un puñetazo. Gestiona internamente el combo de hasta tres golpes.
    /// </summary>
    public void Punch()
    {
        if (isAttacking || isDropKicking || isBlocking)
            return;
        HandlePunchInput();
    }

    /// <summary>
    /// Solicita ejecutar una patada. Si el personaje está en suelo se realiza la patada de suelo;
    /// si está en el aire se realiza una patada voladora (dropkick).
    /// </summary>
    public void Kick()
    {
        if (isAttacking || isDropKicking || isBlocking)
            return;
        if (isGrounded)
        {
            StartCoroutine(DoAttack(ANIM_TrigKickGround, kickLock, false));
        }
        else
        {
            StartCoroutine(DoDropKick(ANIM_TrigKickAir, kickLock));
        }
    }

    /// <summary>
    /// Solicita lanzar un kunai. Sólo se permite si no se está atacando ni bloqueando.
    /// </summary>
    public void ThrowKunai()
    {
        if (isAttacking || isDropKicking || isBlocking)
            return;
        StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
    }
    #endregion

    #region Métodos privados (lógica interna)
    /// <summary>
    /// Mueve al personaje en función de la entrada establecida. Maneja la rotación según la dirección de avance.
    /// </summary>
    private void MoveCharacter()
    {
        // Si no se está atacando, dropkicking o bloqueando, movemos con la velocidad indicada.
        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            Vector3 move = new Vector3(inputX, 0f, inputZ).normalized * moveSpeed;
            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            // Cambiar la orientación según el eje X (mirar hacia la izquierda o derecha)
            if (Mathf.Abs(inputX) > 0.01f)
            {
                Vector3 facing = new Vector3(Mathf.Sign(inputX), 0f, 0f);
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }
        // Si se ataca o se está bloqueando se detiene la velocidad horizontal
        else if (isAttacking || isBlocking)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    /// <summary>
    /// Aplica gravedad adicional para que los saltos sean más rápidos al caer.
    /// </summary>
    private void ApplyExtraGravity()
    {
        if (!isGrounded && rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Physics.gravity * 1.5f, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Rutina genérica para ataques que no dependan del estado del aire (puñetazos, kunai, patadas de suelo).
    /// </summary>
    private IEnumerator DoAttack(int triggerHash, float lockTime, bool doThrowKunai, bool allowMovement = false)
    {
        // Si no se permite movimiento, marcamos el estado de ataque
        if (!allowMovement) isAttacking = true;

        // Lanzamos la animación
        anim.SetTrigger(triggerHash);

        // Si se trata de un kunai, instanciamos el proyectil
        if (doThrowKunai) SpawnKunai();

        // Esperamos el tiempo de bloqueo
        yield return new WaitForSeconds(lockTime);

        // Finaliza el estado de ataque si procede
        if (!allowMovement) isAttacking = false;
    }

    /// <summary>
    /// Rutina específica para la patada aérea. Marca el estado de dropkick.
    /// </summary>
    private IEnumerator DoDropKick(int triggerHash, float lockTime)
    {
        isDropKicking = true;
        anim.SetTrigger(triggerHash);

        yield return new WaitForSeconds(lockTime);

        isDropKicking = false;
    }

    /// <summary>
    /// Gestiona la lógica del combo de puñetazos. Incrementa la etapa del combo y reproduce la animación.
    /// </summary>
    private void HandlePunchInput()
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

    /// <summary>
    /// Coroutine que bloquea al personaje durante el tiempo que dure el puñetazo.
    /// </summary>
    private IEnumerator PunchRoutine(float lockTime)
    {
        isAttacking = true;
        yield return new WaitForSeconds(lockTime);
        isAttacking = false;

        if (punchStage >= 3)
            ResetPunchCombo();
    }

    /// <summary>
    /// Restaura el combo de puñetazos al estado inicial.
    /// </summary>
    private void ResetPunchCombo()
    {
        punchStage = 0;
        anim.SetInteger(ANIM_PunchStage, 0);
    }

    /// <summary>
    /// Instancia y lanza un kunai desde el firePoint en la dirección en que mira el personaje.
    /// </summary>
    private void SpawnKunai()
    {
        if (kunaiPrefab == null || firePoint == null) return;

        // Determina la orientación del kunai en función de hacia dónde mira el personaje
        float facing = Mathf.Sign(transform.forward.x);

        // Rotación base que se ajusta según la dirección
        Vector3 baseRotation = new Vector3(0f, 180f, 90f);
        Vector3 finalRotation = new Vector3(
            baseRotation.x,
            baseRotation.y * facing,
            baseRotation.z * facing
        );

        Quaternion prefabRot = Quaternion.Euler(finalRotation);

        // Instancia el kunai
        GameObject k = Instantiate(kunaiPrefab, firePoint.position, prefabRot);

        // Asigna la velocidad al Rigidbody del kunai si lo tiene
        if (k.TryGetComponent(out Rigidbody krb))
        {
            Vector3 forwardDir = transform.forward.normalized;
            krb.linearVelocity = forwardDir * kunaiSpeed;
        }
    }

    /// <summary>
    /// Comprueba si el personaje está tocando el suelo mediante esfera y rayo, y actualiza
    /// las variables de salto y estado según corresponda.
    /// </summary>
    private void CheckGround()
    {
        bool sphereGround = Physics.CheckSphere(groundCheck.position, groundRadius, groundLayer, QueryTriggerInteraction.Ignore);
        float rayLength = 0.35f;
        bool rayGround = Physics.Raycast(groundCheck.position, Vector3.down, rayLength, groundLayer, QueryTriggerInteraction.Ignore);

        bool groundedNow = sphereGround || rayGround;

        if (groundedNow)
            lastTimeGrounded = Time.time;

        // Si acabamos de aterrizar y la velocidad vertical es descendente, reseteamos saltos y estados
        if (groundedNow && !wasGrounded && rb.linearVelocity.y <= 0f)
        {
            jumpCount = 0;
            isDropKicking = false;
            isFlipping = false;
        }

        isGrounded = groundedNow;
        wasGrounded = groundedNow;
    }

    /// <summary>
    /// Determina si es posible realizar un salto adicional. Considera el coyote time para el primer salto.
    /// </summary>
    private bool CanJump()
    {
        if (jumpCount >= maxJumps) return false;

        if (jumpCount == 0)
        {
            bool withinCoyote = (Time.time - lastTimeGrounded) <= coyoteTime;
            return isGrounded || withinCoyote;
        }

        return true;
    }

    /// <summary>
    /// Ejecuta el salto ajustando la velocidad vertical del rigidbody y lanzando las animaciones correspondientes.
    /// </summary>
    private void PerformJump(int jumpIndex)
    {
        // Eliminamos la velocidad vertical anterior
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
