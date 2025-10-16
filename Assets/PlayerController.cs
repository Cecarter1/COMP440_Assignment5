using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // ===== Events for other systems =====
    public event Action<bool> OnGroundedChanged;
    public event Action<Transform> OnAttachedToPlatform;
    public event Action OnDetachedFromPlatform;
    public event Action<Vector2> OnVelocityChanged;

    // ===== References =====
    [Header("References")]
    public Animator animator;
    public PlayerStateMachine stateMachine;
    public PlayerAudioManager audioManager;
    public Transform graphicsRoot;      // child with sprite/animator
    public Camera targetCamera;         // optional rotate with gravity

    Rigidbody2D rb;
    Collider2D col;

    // ===== Collision / Contacts =====
    [Header("Collision")]
    public LayerMask groundMask = ~0;
    public float probeInset = 0.03f;        // small inset so rays start just inside bounds
    public float groundCheckDist = 0.16f;   // 0.14–0.20 good range
    public float wallCheckDist = 0.22f;     // 0.2–0.3 good range

    // ===== Movement =====
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float accel = 60f;
    public float decel = 70f;
    [Range(0f, 1f)] public float airControlPercent = 0.6f;
    public float maxAirSpeed = 8.5f;

    // ===== Jump =====
    [Header("Jump")]
    public float jumpSpeed = 13.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float lowJumpGravityMult = 3.0f; // stronger gravity when released early (short hop)
    public float fallGravityMult = 3.5f;    // stronger gravity when falling

    [Header("Jump Tuning")]
    public float jumpCutMultiplier = 0.5f;  // cap rising speed on release (0.4–0.6 feels good)
    public float jumpReleaseGrace = 0.02f;  // ignore micro-release right at takeoff

    // ===== Walls =====
    [Header("Walls")]
    public float wallSlideMaxSpeed = 2.5f;
    public float wallJumpLateral = 8f;
    public float wallJumpVertical = 12f;
    public float wallJumpLockTime = 0.12f;

    // ===== Gravity / Rotation =====
    [Header("Gravity")]
    public float rotationSlerpTime = 0.2f;

    // ===== SFX =====
    [Header("SFX")]
    public float landSfxCooldown = 0.20f;
    public float slideSfxCooldown = 0.25f;

    // ===== State =====
    Vector2 gravityDir = Vector2.down;
    Vector2 rightAxis => new Vector2(-gravityDir.y, gravityDir.x).normalized;
    Vector2 upAxis => -gravityDir;

    float lastGroundedTime = -999f, lastJumpPressedTime = -999f;
    bool grounded, wallLeft, wallRight, jumpingThisFrame;
    float wallJumpLockUntil = -999f;
    Vector2 moveInput;
    Vector2 platformVelocity;
    Transform attachedPlatform;

    // jump cut state
    bool jumpCutApplied = false;
    float lastJumpTime = -999f;

    // SFX rate-limit state
    float lastLandTime = -999f;
    float lastSlideSfx = -999f;

    // Facing state (flip sprite)
    int facing = 1; // +1 = facing right, -1 = facing left

    // Animator hashes
    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int AnimIsWallSliding = Animator.StringToHash("IsWallSliding");
    static readonly int AnimVAlongUp = Animator.StringToHash("VAlongUp");
    // (Optional) if you want to drive a facing param in Animator:
    // static readonly int AnimFacing = Animator.StringToHash("Facing");

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New Input System)")]
    public InputActionReference moveAction; // Vector2 (x used)
    public InputActionReference jumpAction; // Button
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (!stateMachine) stateMachine = GetComponent<PlayerStateMachine>();
        if (!audioManager) audioManager = GetComponent<PlayerAudioManager>();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true; // prevent spin

        Physics2D.queriesStartInColliders = false;
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        moveAction?.action?.Enable();
        jumpAction?.action?.Enable();
#endif
    }
    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        moveAction?.action?.Disable();
        jumpAction?.action?.Disable();
#endif
    }

    void Update()
    {
        ReadInput();
        UpdateAnimatorParams();

        stateMachine.Tick(
            grounded,
            IsWallSliding(),
            Vector2.Dot(rb.velocity, upAxis),
            Mathf.Abs(Vector2.Dot(rb.velocity, rightAxis))
        );

        UpdateFacing(); //  flip the Graphics child based on input/wall
    }

    void FixedUpdate()
    {
        UpdateContacts();                 //  important this runs first
        HandleHorizontal();
        TryJumpWithCoyoteAndBuffer();     //  uses lastGroundedTime & buffered press
        ApplyVariableJumpGravity();
        HandleWallSlideAndWallJump();

        if (attachedPlatform == null) platformVelocity = Vector2.zero;

        OnVelocityChanged?.Invoke(rb.velocity);
        jumpingThisFrame = false;
    }

    // -------- Input ----------
    void ReadInput()
    {
        float x = 0f;
#if ENABLE_INPUT_SYSTEM
        if (moveAction && moveAction.action != null) x = moveAction.action.ReadValue<Vector2>().x;
        else
#endif
        { x = Input.GetAxisRaw("Horizontal"); }

        if (Time.time < wallJumpLockUntil) x = 0f;
        moveInput = Mathf.Clamp(x, -1f, 1f) * rightAxis;

        bool jumpDown = false;
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) jumpDown = jumpAction.action.WasPressedThisFrame();
        else
#endif
        { jumpDown = Input.GetKeyDown(KeyCode.Space); }
        if (jumpDown) lastJumpPressedTime = Time.time;
    }
    bool JumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) return jumpAction.action.IsPressed();
#endif
        return Input.GetKey(KeyCode.Space);
    }

    // -------- Contacts (3-ray ground, 2-ray walls) ----------
    void UpdateContacts()
    {
        var b = col.bounds;
        Vector2 center = b.center;
        Vector2 ext = b.extents;

        Vector2 down = gravityDir;  // gravity "down"
        Vector2 up = -gravityDir;
        Vector2 right = rightAxis;

        // ground rays (center/left/right)
        Vector2 baseCenter = center - up * (ext.y - probeInset);
        Vector2 baseLeft = baseCenter - right * (ext.x * 0.7f);
        Vector2 baseRight = baseCenter + right * (ext.x * 0.7f);

        bool g0 = Physics2D.Raycast(baseCenter, down, groundCheckDist, groundMask);
        bool g1 = Physics2D.Raycast(baseLeft, down, groundCheckDist, groundMask);
        bool g2 = Physics2D.Raycast(baseRight, down, groundCheckDist, groundMask);

        bool wasGrounded = grounded;
        grounded = g0 || g1 || g2;
        if (grounded) lastGroundedTime = Time.time;
        if (grounded != wasGrounded) OnGroundedChanged?.Invoke(grounded);

        // wall rays (left/right)
        Vector2 midLeft = center - right * (ext.x - probeInset);
        Vector2 midRight = center + right * (ext.x - probeInset);
        wallLeft = Physics2D.Raycast(midLeft, -right, wallCheckDist, groundMask);
        wallRight = Physics2D.Raycast(midRight, right, wallCheckDist, groundMask);
    }

    bool IsWallSliding()
    {
        if (grounded) return false;

        bool touching = wallLeft || wallRight;
        bool movingDown = Vector2.Dot(rb.velocity, gravityDir) > 0.01f;
        bool movingToward =
            (wallLeft && Vector2.Dot(moveInput, -rightAxis) > 0f) ||
            (wallRight && Vector2.Dot(moveInput, rightAxis) > 0f);

        return touching && movingDown && movingToward;
    }

    // -------- Movement ----------
    void HandleHorizontal()
    {
        float vRight = Vector2.Dot(rb.velocity, rightAxis);
        float vUp = Vector2.Dot(rb.velocity, upAxis);

        float target = moveSpeed * Mathf.Sign(Vector2.Dot(moveInput, rightAxis)) * Mathf.Abs(moveInput.magnitude);
        float a = grounded ? accel : (accel * airControlPercent);
        float d = grounded ? decel : (decel * airControlPercent);

        float newRight = (Mathf.Abs(target) > 0.01f)
            ? Mathf.MoveTowards(vRight, target, a * Time.fixedDeltaTime)
            : Mathf.MoveTowards(vRight, 0f, d * Time.fixedDeltaTime);

        if (!grounded) newRight = Mathf.Clamp(newRight, -maxAirSpeed, maxAirSpeed);

        rb.velocity = newRight * rightAxis + vUp * upAxis + platformVelocity;
    }

    // -------- Jump (coyote + buffer) ----------
    void TryJumpWithCoyoteAndBuffer()
    {
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if ((canCoyote && buffered) && !jumpingThisFrame)
        {
            jumpingThisFrame = true;
            lastJumpPressedTime = -999f;

            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + (jumpSpeed * upAxis);

            // mark jump start for variable-height cut
            lastJumpTime = Time.time;
            jumpCutApplied = false;

            audioManager?.PlayJump();
            OnGroundedChanged?.Invoke(false);
        }
    }

    // -------- Variable jump height + better fall ----------
    void ApplyVariableJumpGravity()
    {
        float vUp = Vector2.Dot(rb.velocity, upAxis);
        bool rising = vUp > 0.01f;

        if (rising)
        {
            // If released during rise (after tiny grace), add low-jump gravity
            if (!JumpHeld() && (Time.time - lastJumpTime) > jumpReleaseGrace)
            {
                rb.velocity += gravityDir * (Physics2D.gravity.magnitude * (lowJumpGravityMult - 1f) * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Falling: heavier gravity for more control
            rb.velocity += gravityDir * (Physics2D.gravity.magnitude * (fallGravityMult - 1f) * Time.fixedDeltaTime);
        }

        // One-time upward velocity cap on early release for crisp short hop
        if (rising && !JumpHeld() && !jumpCutApplied && (Time.time - lastJumpTime) > jumpReleaseGrace)
        {
            float maxUp = jumpSpeed * jumpCutMultiplier; // e.g., 50% of initial up speed
            float clampedUp = Mathf.Min(vUp, maxUp);
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + clampedUp * upAxis;
            jumpCutApplied = true;
        }

        if (!rising) jumpCutApplied = false;
    }

    // -------- Wall slide & wall jump ----------
    void HandleWallSlideAndWallJump()
    {
        if (!IsWallSliding()) return;

        float vDown = Vector2.Dot(rb.velocity, gravityDir);
        float vRight = Vector2.Dot(rb.velocity, rightAxis);

        // Clamp slide speed
        float clampedDown = Mathf.Min(vDown, Mathf.Abs(wallSlideMaxSpeed));
        rb.velocity = vRight * rightAxis + clampedDown * gravityDir;

        // light slide SFX with cooldown
        if (Time.time - lastSlideSfx >= slideSfxCooldown)
        {
            audioManager?.PlaySlide();
            lastSlideSfx = Time.time;
        }

        // Wall jump
        bool jumpPressed = (Time.time - lastJumpPressedTime) <= 0.05f;
        if (jumpPressed)
        {
            lastJumpPressedTime = -999f;
            Vector2 away = wallLeft ? rightAxis : (wallRight ? -rightAxis : Vector2.zero);
            Vector2 j = away * wallJumpLateral + upAxis * wallJumpVertical;
            rb.velocity = j;
            wallJumpLockUntil = Time.time + wallJumpLockTime;
            audioManager?.PlayJump();
        }
    }

    // -------- Facing / flipping the Graphics child ----------
    void UpdateFacing()
    {
        if (!graphicsRoot) return;

        // Prefer input direction; if neutral, face toward wall while sliding; else keep last facing
        float dirInput = Vector2.Dot(moveInput, rightAxis); // right=+, left=-
        if (Mathf.Abs(dirInput) > 0.05f)
        {
            facing = dirInput > 0f ? 1 : -1;
        }
        else if (IsWallSliding())
        {
            if (wallLeft) facing = -1;
            if (wallRight) facing = 1;
        }

        var ls = graphicsRoot.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = facing > 0 ? absX : -absX;  // flip along local X (safe for gravity flips)
        graphicsRoot.localScale = ls;

        // (Optional) if your Animator uses a Facing parameter:
        // if (animator) animator.SetFloat(AnimFacing, facing);
    }

    // -------- Animator ----------
    void UpdateAnimatorParams()
    {
        if (!animator) return;
        float speedAlongRight = Mathf.Abs(Vector2.Dot(rb.velocity, rightAxis));
        float vAlongUp = Vector2.Dot(rb.velocity, upAxis);
        animator.SetFloat(AnimSpeed, speedAlongRight);
        animator.SetBool(AnimIsGrounded, grounded);
        animator.SetBool(AnimIsWallSliding, IsWallSliding());
        animator.SetFloat(AnimVAlongUp, vAlongUp);
    }

    // -------- Platform attach (simple velocity inherit) ----------
    void OnCollisionEnter2D(Collision2D c) { TryAttachToPlatform(c); if (grounded) TryLandSfx(); }
    void OnCollisionStay2D(Collision2D c) { TryAttachToPlatform(c); }
    void OnCollisionExit2D(Collision2D c)
    {
        if (attachedPlatform != null && c.transform == attachedPlatform)
        {
            attachedPlatform = null; platformVelocity = Vector2.zero; OnDetachedFromPlatform?.Invoke();
        }
    }
    void TryAttachToPlatform(Collision2D c)
    {
        foreach (var contact in c.contacts)
        {
            if (Vector2.Dot(contact.normal, gravityDir) < -0.6f)
            {
                var prb = c.rigidbody;
                platformVelocity = prb ? prb.velocity : Vector2.zero;
                if (attachedPlatform != c.transform)
                {
                    attachedPlatform = c.transform;
                    OnAttachedToPlatform?.Invoke(attachedPlatform);
                }
                return;
            }
        }
    }
    void TryLandSfx()
    {
        if (Time.time - lastLandTime >= landSfxCooldown)
        {
            audioManager?.PlayLand();
            lastLandTime = Time.time;
        }
    }

    // -------- Gravity interface ----------
    public void RequestGravityVector(Vector2 newGravityDir, float rotateDuration)
    {
        newGravityDir = newGravityDir.normalized;
        StopAllCoroutines();
        StartCoroutine(SmoothReorient(newGravityDir, Mathf.Max(0.01f, rotateDuration)));
    }
    IEnumerator SmoothReorient(Vector2 newGravityDir, float duration)
    {
        gravityDir = newGravityDir;

        if (graphicsRoot)
        {
            Quaternion start = graphicsRoot.rotation;
            Quaternion target = Quaternion.FromToRotation(graphicsRoot.up, -newGravityDir) * graphicsRoot.rotation;
            float t = 0f; while (t < duration) { t += Time.deltaTime; graphicsRoot.rotation = Quaternion.Slerp(start, target, t / duration); yield return null; }
            graphicsRoot.rotation = target;
        }
        if (targetCamera)
        {
            Quaternion cs = targetCamera.transform.rotation;
            Quaternion ct = Quaternion.FromToRotation(targetCamera.transform.up, -newGravityDir) * cs;
            float t = 0f; while (t < duration) { t += Time.deltaTime; targetCamera.transform.rotation = Quaternion.Slerp(cs, ct, t / duration); yield return null; }
            targetCamera.transform.rotation = ct;
        }
        // reproject velocity to new axes
        Vector2 v = rb.velocity;
        float vRight = Vector2.Dot(v, rightAxis);
        float vUp = Vector2.Dot(v, upAxis);
        rb.velocity = vRight * rightAxis + vUp * upAxis;
    }
}
