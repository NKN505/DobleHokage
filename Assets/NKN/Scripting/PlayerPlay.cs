using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class PlayerPlay : MonoBehaviour
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

    #region Estado
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

    // 👉 expone el estado de ataque para que JoninAI pueda leerlo
    public bool IsAttacking => isAttacking;
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

    #region Unity Methods
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider>();

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.position = new Vector3(col.bounds.center.x, col.bounds.min.y + 0.01f, col.bounds.center.z);
            groundCheck = gc.transform;
        }

        // === IGNORAR COLISIONES CON EL ENEMY ===
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
        inputX = Input.GetAxisRaw("Horizontal");
        inputZ = Input.GetAxisRaw("Vertical");

        CheckGround();

        // === BLOQUEO ===
        isBlocking = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // --- SALTO ---
        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking && !isDropKicking && !isBlocking)
        {
            if (CanJump())
                PerformJump(jumpCount + 1);
        }

        // === ATAQUES ===
        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            if (Input.GetKeyDown(KeyCode.P))
                HandlePunchInput();
            else if (Input.GetKeyDown(KeyCode.K))
            {
                if (isGrounded)
                    StartCoroutine(DoAttack(ANIM_TrigKickGround, kickLock, false));
                else
                    StartCoroutine(DoDropKick(ANIM_TrigKickAir, kickLock));
            }
            else if (Input.GetKeyDown(KeyCode.O))
                StartCoroutine(DoAttack(ANIM_TrigKunai, kunaiLock, true));
        }

        if (punchStage > 0 && Time.time - lastPunchTime > comboResetTime)
            ResetPunchCombo();

        // === Animaciones ===
        bool moving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputZ) > 0.01f;
        bool canRun = moving && isGrounded && !isAttacking && !isDropKicking && !isBlocking;

        anim.SetBool(ANIM_IsRunning, canRun);
        anim.SetBool(ANIM_IsGrounded, isGrounded);
        anim.SetBool(ANIM_IsDropKicking, isDropKicking);
        anim.SetBool(ANIM_IsFlipping, isFlipping);
        anim.SetBool(ANIM_IsBlocking, isBlocking);

        if (isDropKicking && isGrounded)
            isDropKicking = false;
    }

    private void FixedUpdate()
    {
        MoveCharacter();
        ApplyExtraGravity();
    }
    #endregion

    #region Métodos Auxiliares
    private void MoveCharacter()
    {
        if (!isAttacking && !isDropKicking && !isBlocking)
        {
            Vector3 move = new Vector3(inputX, 0f, inputZ).normalized * moveSpeed;
            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            if (Mathf.Abs(inputX) > 0.01f)
            {
                Vector3 facing = new Vector3(Mathf.Sign(inputX), 0f, 0f);
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }
        else if (isAttacking || isBlocking)
        {
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
