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
    // ===== Abilities Flags (Set by Gems) =====
    public bool canDoubleJump = false;
    public bool canWallJump = false;
    public bool canDash = false;
    public bool canGravity = false;// Flag for gravity unlock
    public bool isPowerupActive = false;
    public bool hasUnlimitedGravity = false; 
    public bool canFlipGravity = true; // Control gravity flip cooldown

    // NOTE: This should be UNCHECKED in the Inspector for single double jump functionality.
    public bool enableUnlimitedAirJumps = false;

    // ===== Events for other systems =====
    public event Action<bool> OnGroundedChanged;
    public event Action<Transform> OnAttachedToPlatform;
    public event Action OnDetachedFromPlatform;
    public event Action<Vector2> OnVelocityChanged;
    public event Action<Vector2, JumpKind> OnJumpPerformed;

    public enum JumpKind { Ground, Wall, Air }

    // ===== References =====
    [Header("References")]
    public Animator animator;
    public PlayerStateMachine stateMachine;
    public PlayerAudioManager audioManager;
    public Transform graphicsRoot;    // visuals child
    public Camera targetCamera;       // optional: rotate with gravity

    Rigidbody2D rb;
    Collider2D col;

    // Fallback visual target (Not fully implemented in the code, but kept for structure)
    Transform visual;

    // ===== Collision / Contacts =====
    [Header("Collision")]
    public LayerMask groundMask = ~0;
    public float probeInset = 0.03f;
    public float groundCheckDist = 0.5f; // Increased for reliability
    public float wallCheckDist = 0.22f;

    [Header("Wall Detection Options")]
    public bool requireInputIntoWall = true;

    // ===== Movement =====
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float accel = 60f;
    public float decel = 70f;
    [Range(0f, 1f)] public float airControlPercent = 0.6f;
    public float maxAirSpeed = 8.5f;

    // Runtime multipliers (Power-Ups)
    float speedMult = 1f;
    float jumpMult = 1f;

    // ===== Jump =====
    [Header("Jump")]
    public float jumpSpeed = 12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float lowJumpGravityMult = 2.0f;
    public float fallGravityMult = 2.5f;

    [Header("Jump Tuning")]
    public float jumpCutMultiplier = 0.5f;
    public float jumpReleaseGrace = 0.02f;

    // ===== Walls =====
    [Header("Walls")]
    public float wallSlideMaxSpeed = 2.5f;
    public float wallJumpLateral = 8f;
    public float wallJumpVertical = 12f;
    public float wallJumpLockTime = 0.12f;
    public float wallDetachGrace = 0.06f;

    // ===== Dash =====
    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashTime = 0.18f;
    public float dashCooldown = 0.35f;
    public AudioClip dashClip;

    bool isDashing = false;
    float dashEndTime = -999f;
    float nextDashTime = -999f;
    Vector2 dashDir;

    // ===== Gravity / Rotation =====
    [Header("Gravity")]
    public float rotationSlerpTime = 0.2f;
    public float gravityMagnitude = 7.2f;
    public float flipCooldown = 1f;
    public bool rotateBodyWithGravity = true;

    // ===== SFX Cooldowns =====
    [Header("SFX Cooldowns")]
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
    float rawMoveScalar;
    int lastMoveSign = 1;
    Vector2 platformVelocity;
    Transform attachedPlatform;

    bool jumpCutApplied = false;
    float lastJumpTime = -999f;

    float lastLandTime = -999f;
    float lastSlideSfx = -999f;

    float lastLeftWallTime = -999f, lastRightWallTime = -999f;

    // --- CRITICAL DOUBLE JUMP STATE ---
    private bool hasUsedAirJump = false;
    // ----------------------------------

    int facing = 1;

    // Animator hashes (remain the same)
    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int AnimIsWallSliding = Animator.StringToHash("IsWallSliding");
    static readonly int AnimVAlongUp = Animator.StringToHash("VAlongUp");

    // ===== Public exposure for other systems (remains the same) =====
    public Vector2 GravityDir => gravityDir;
    public Vector2 UpAxis => upAxis;
    public Vector2 RightAxis => rightAxis;
    public int FacingSign => facing;
    public bool IsGrounded => grounded;
    public bool IsWallSlidingNow => IsWallSliding();
    public float MoveInputScalar => rawMoveScalar;
    public Transform AttachedPlatform => attachedPlatform;

#if ENABLE_INPUT_SYSTEM
    [Header("Input (New Input System)")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference dashAction;
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (!stateMachine) stateMachine = GetComponent<PlayerStateMachine>();
        if (!audioManager) audioManager = GetComponent<PlayerAudioManager>();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        Physics2D.queriesStartInColliders = false;
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        moveAction?.action?.Enable();
        jumpAction?.action?.Enable();
        dashAction?.action?.Enable();
#endif
    }
    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        moveAction?.action?.Disable();
        jumpAction?.action?.Disable();
        dashAction?.action?.Disable();
#endif
    }

    void Update()
    {
        HandleGravityFlipHotkeys(); // Checks Arrow Keys FIRST
        ReadInput();                // Reads WASD
        UpdateAnimatorParams();

        stateMachine?.Tick(
            grounded,
            IsWallSliding(),
            Vector2.Dot(rb.velocity, upAxis),
            Mathf.Abs(Vector2.Dot(rb.velocity, rightAxis))
        );

        UpdateFacing();
    }

    void FixedUpdate()
    {
        UpdateContacts(); // first

        if (isDashing)
        {
            ApplyDashMotion();
            OnVelocityChanged?.Invoke(rb.velocity);
            jumpingThisFrame = false;
            return;
        }

        HandleHorizontal();
        TryJumpWithCoyoteAndBuffer();
        TryAirJumpWithLimit(); // Uses the new limit logic
        ApplyVariableJumpGravity();
        HandleWallSlideAndWallJump();

        if (attachedPlatform == null) platformVelocity = Vector2.zero;

        OnVelocityChanged?.Invoke(rb.velocity);
        jumpingThisFrame = false;
    }

    // ---------- Gravity flip (arrows) ----------
    void HandleGravityFlipHotkeys()
    {
        if (!canFlipGravity || !canGravity) return; // Must have ability & be off cooldown

        Vector2 newDir = Vector2.zero;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) newDir = Vector2.left;
        if (Input.GetKeyDown(KeyCode.RightArrow)) newDir = Vector2.right;
        if (Input.GetKeyDown(KeyCode.UpArrow)) newDir = Vector2.up;
        if (Input.GetKeyDown(KeyCode.DownArrow)) newDir = Vector2.down;

        if (newDir != Vector2.zero && newDir != gravityDir)
        {
            SetGravity(newDir);
        }
    }

    void SetGravity(Vector2 dir)
    {
        dir = dir.normalized;
        gravityDir = dir;

        Physics2D.gravity = gravityDir * gravityMagnitude;

        if (rotateBodyWithGravity)
        {
            RequestGravityVector(gravityDir, rotationSlerpTime);
        }

        // Apply Cooldown
        canFlipGravity = false;
        Invoke(nameof(ResetFlip), flipCooldown);
    }
    void ResetFlip() => canFlipGravity = true;

    // ---------- Input (FIXED: Uses A/D for movement only, handles flip compensation) ----------
    void ReadInput()
    {
        float x = 0f;

#if ENABLE_INPUT_SYSTEM
        if (moveAction && moveAction.action != null) x = moveAction.action.ReadValue<Vector2>().x;
        else
#endif
        {
            // Explicitly check A/D keys for horizontal movement
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
        }

        // --- CRITICAL FIX: Invert Horizontal Input When Upside Down ---
        if (gravityDir == Vector2.up)
        {
            x = -x; // Invert the input scalar for intuitive movement
        }
        // -----------------------------------------------------------

        // Apply Wall Jump Lock
        if (Time.time < wallJumpLockUntil) x = 0f;

        rawMoveScalar = Mathf.Clamp(x, -1f, 1f);
        moveInput = rawMoveScalar * rightAxis;

        if (Mathf.Abs(rawMoveScalar) > 0.05f)
            lastMoveSign = rawMoveScalar > 0 ? 1 : -1;

        // Jump Input Check
        bool jumpDown = false;
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) jumpDown = jumpAction.action.WasPressedThisFrame();
        else
#endif
        { jumpDown = Input.GetKeyDown(KeyCode.Space); }

        if (jumpDown) lastJumpPressedTime = Time.time;

        // Dash Input Check
        bool dashPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (dashAction && dashAction.action != null) dashPressed = dashAction.action.WasPressedThisFrame();
        else
#endif
        { dashPressed = Input.GetKeyDown(KeyCode.LeftShift); }

        if (dashPressed && canDash) TryStartDash();
    }

    bool JumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) return jumpAction.action.IsPressed();
#endif
        return Input.GetKey(KeyCode.Space);
    }

    // ---------- Contacts (Remains the same) ----------
    void UpdateContacts()
    {
        var b = col.bounds;
        Vector2 center = b.center;
        Vector2 ext = b.extents;

        Vector2 down = gravityDir;
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

        if (grounded != wasGrounded)
        {
            OnGroundedChanged?.Invoke(grounded);
            if (grounded && !wasGrounded)
            {
                // landing edge → FORCE clear wall state & wall memory (fix sticky wall-slide)
                wallLeft = wallRight = false;
                lastLeftWallTime = lastRightWallTime = -999f;
                animator?.SetBool(AnimIsWallSliding, false);
                TryLandSfx();

                // CRITICAL FIX: Reset air jump usage when landing
                hasUsedAirJump = false;
            }
        }

        // wall rays (left/right)
        Vector2 midLeft = center - right * (ext.x - probeInset);
        Vector2 midRight = center + right * (ext.x - probeInset);

        bool hitLeft = Physics2D.Raycast(midLeft, -right, wallCheckDist, groundMask);
        bool hitRight = Physics2D.Raycast(midRight, right, wallCheckDist, groundMask);

        // hysteresis times
        if (hitLeft) lastLeftWallTime = Time.time;
        if (hitRight) lastRightWallTime = Time.time;

        // grace-based contact flags
        wallLeft = (Time.time - lastLeftWallTime) < wallDetachGrace;
        wallRight = (Time.time - lastRightWallTime) < wallDetachGrace;

        // Ground beats wall contact
        if (grounded) { wallLeft = wallRight = false; }
    }

    bool IsWallSliding()
    {
        if (grounded || isDashing) return false;

        bool touching = wallLeft || wallRight;
        bool movingDown = Vector2.Dot(rb.velocity, gravityDir) > 0.01f;

        bool movingToward =
            (wallLeft && Vector2.Dot(moveInput, -rightAxis) > 0f) ||
            (wallRight && Vector2.Dot(moveInput, rightAxis) > 0f);

        bool inputOk = requireInputIntoWall ? movingToward : true;

        return touching && movingDown && inputOk;
    }

    // ---------- Movement (Remains the same) ----------
    void HandleHorizontal()
    {
        float vRight = Vector2.Dot(rb.velocity, rightAxis);
        float vUp = Vector2.Dot(rb.velocity, upAxis);

        float target = (moveSpeed * speedMult) * Mathf.Sign(Vector2.Dot(moveInput, rightAxis)) * Mathf.Abs(moveInput.magnitude);
        float a = grounded ? accel : (accel * airControlPercent);
        float d = grounded ? decel : (decel * airControlPercent);

        float newRight = (Mathf.Abs(target) > 0.01f)
            ? Mathf.MoveTowards(vRight, target, a * Time.fixedDeltaTime)
            : Mathf.MoveTowards(vRight, 0f, d * Time.fixedDeltaTime);

        if (!grounded) newRight = Mathf.Clamp(newRight, -maxAirSpeed * Mathf.Max(1f, speedMult), maxAirSpeed * Mathf.Max(1f, speedMult));

        rb.velocity = newRight * rightAxis + vUp * upAxis + platformVelocity;
    }

    // ---------- Ground/Coyote Jump (Remains the same) ----------
    void TryJumpWithCoyoteAndBuffer()
    {
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if ((canCoyote && buffered) && !jumpingThisFrame && !isDashing)
        {
            jumpingThisFrame = true;
            lastJumpPressedTime = -999f;

            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + (jumpSpeed * jumpMult * upAxis);

            lastJumpTime = Time.time;
            jumpCutApplied = false;

            // hasUsedAirJump is reset in UpdateContacts on landing.

            audioManager?.PlayJump();
            OnJumpPerformed?.Invoke(rb.velocity, JumpKind.Ground);
            OnGroundedChanged?.Invoke(false);
        }
    }

    // ---------- Air Jump with Limit (Remains the same) ----------
    void TryAirJumpWithLimit()
    {
        // 1. Check if the ability is unlocked AND if the air jump hasn't been used yet
        if (!canDoubleJump || hasUsedAirJump) return;

        // 2. Safety check (must be in the air)
        if (grounded || isDashing) return;

        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (buffered)
        {
            lastJumpPressedTime = -999f;

            // CRITICAL FIX: Consume the single air jump
            hasUsedAirJump = true;

            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            // Reset vertical velocity to apply full jump force
            rb.velocity = vRight * rightAxis + (jumpSpeed * jumpMult * upAxis);

            lastJumpTime = Time.time;
            jumpCutApplied = false;

            audioManager?.PlayJump();
            OnJumpPerformed?.Invoke(rb.velocity, JumpKind.Air);
        }
    }
    // --------------------------------------------------------------------------

    // ---------- Variable jump height / better fall (Remains the same) ----------
    void ApplyVariableJumpGravity()
    {
        if (isDashing) return;

        float vUp = Vector2.Dot(rb.velocity, upAxis);
        bool rising = vUp > 0.01f;

        if (rising)
        {
            if (!JumpHeld() && (Time.time - lastJumpTime) > jumpReleaseGrace)
            {
                rb.velocity += gravityDir * (Physics2D.gravity.magnitude * (lowJumpGravityMult - 1f) * Time.fixedDeltaTime);
            }
        }
        else
        {
            rb.velocity += gravityDir * (Physics2D.gravity.magnitude * (fallGravityMult - 1f) * Time.fixedDeltaTime);
        }

        if (rising && !JumpHeld() && !jumpCutApplied && (Time.time - lastJumpTime) > jumpReleaseGrace)
        {
            float maxUp = (jumpSpeed * jumpMult) * jumpCutMultiplier;
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            float vUpClamped = Mathf.Min(vUp, maxUp);
            rb.velocity = vRight * rightAxis + vUpClamped * upAxis;
            jumpCutApplied = true;
        }

        if (!rising) jumpCutApplied = false;
    }

    // ---------- Wall slide & wall jump (Remains the same) ----------
    void HandleWallSlideAndWallJump()
    {
        if (!IsWallSliding()) return;

        float vDown = Vector2.Dot(rb.velocity, gravityDir);
        float vRight = Vector2.Dot(rb.velocity, rightAxis);

        float clampedDown = Mathf.Min(vDown, Mathf.Abs(wallSlideMaxSpeed));
        rb.velocity = vRight * rightAxis + clampedDown * gravityDir;

        if (Time.time - lastSlideSfx >= slideSfxCooldown)
        {
            audioManager?.PlaySlide();
            lastSlideSfx = Time.time;
        }
        if (canWallJump)
        {
            bool jumpPressed = (Time.time - lastJumpPressedTime) <= 0.05f;
            if (jumpPressed)
            {
                lastJumpPressedTime = -999f;

                Vector2 away = wallLeft ? rightAxis : (wallRight ? -rightAxis : Vector2.zero);
                Vector2 j = away * wallJumpLateral + upAxis * (wallJumpVertical * jumpMult);
                rb.velocity = j;
                wallJumpLockUntil = Time.time + wallJumpLockTime;

                audioManager?.PlayJump();
                OnJumpPerformed?.Invoke(rb.velocity, JumpKind.Wall);
            }
        }
    }

    // ---------- Dash (bidirectional) (Remains the same) ----------
    void TryStartDash()
    {
        if (!canDash) return;
        if (isDashing) return;
        if (Time.time < nextDashTime) return;

        // Direction cascade: input -> current horiz velocity -> last non-zero input -> facing
        float sign = 0f;
        // ... (Dash direction logic remains the same) ...

        // 1) current input
        if (Mathf.Abs(rawMoveScalar) > 0.05f)
            sign = Mathf.Sign(rawMoveScalar);

        // 2) horizontal velocity
        if (Mathf.Abs(sign) < 0.5f)
        {
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            if (Mathf.Abs(vRight) > 0.05f)
                sign = Mathf.Sign(vRight);
        }

        // 3) last non-zero input
        if (Mathf.Abs(sign) < 0.5f)
            sign = lastMoveSign;

        // 4) facing fallback
        if (Mathf.Abs(sign) < 0.5f)
            sign = facing;

        dashDir = (sign > 0f ? rightAxis : -rightAxis);

        isDashing = true;
        dashEndTime = Time.time + dashTime;
        nextDashTime = Time.time + dashCooldown;
        platformVelocity = Vector2.zero;

        if (audioManager && dashClip)
        {
            var src = audioManager.GetComponent<AudioSource>();
            if (src) src.PlayOneShot(dashClip);
        }
    }

    void ApplyDashMotion()
    {
        rb.velocity = dashDir * (dashSpeed * Mathf.Max(1f, speedMult));

        if (Time.time >= dashEndTime)
        {
            isDashing = false;
            float vUp = Vector2.Dot(rb.velocity, upAxis);
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + vUp * upAxis;
        }
    }

    // ---------- Facing / flipping (CRITICAL FIX) ----------
    void UpdateFacing()
    {
        if (!graphicsRoot) return;

        // 1. Preserve the current gravity rotation on the graphicsRoot.
        Quaternion originalRotation = graphicsRoot.localRotation;

        // 2. Temporarily reset rotation to world identity 
        //    to calculate localScale.x correctly along the screen's horizontal axis.
        graphicsRoot.localRotation = Quaternion.identity;

        // 3. Update facing based on raw horizontal input
        float dirInput = rawMoveScalar;
        if (Mathf.Abs(dirInput) > 0.05f)
        {
            facing = dirInput > 0f ? 1 : -1;
        }
        else if (IsWallSliding())
        {
            if (wallLeft) facing = -1;
            if (wallRight) facing = 1;
        }

        // 4. Apply the horizontal flip based on 'facing' sign
        var ls = graphicsRoot.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = facing > 0 ? absX : -absX;
        graphicsRoot.localScale = ls;

        // 5. Restore the gravity rotation
        graphicsRoot.localRotation = originalRotation;
    }

    // ---------- Animator (Remains the same) ----------
    void UpdateAnimatorParams()
    {
        if (!animator) return;
        float speedAlongRight = Mathf.Abs(Vector2.Dot(rb.velocity, rightAxis));
        float vAlongUp = Vector2.Dot(rb.velocity, upAxis);
        animator.SetFloat(AnimSpeed, speedAlongRight);
        animator.SetBool(AnimIsGrounded, grounded);
        animator.SetBool(AnimIsWallSliding, grounded ? false : IsWallSliding());
        animator.SetFloat(AnimVAlongUp, vAlongUp);
    }

    // ---------- Platform attach / land SFX (Remains the same) ----------
    void OnCollisionEnter2D(Collision2D c)
    {
        TryAttachToPlatform(c);
        if (grounded)
        {
            OnGroundedChanged?.Invoke(true);
            TryLandSfx();
        }
    }
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
            if (Vector2.Dot(contact.normal, gravityDir) < -0.6f) // standing
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

    // ---------- Gravity hook (Modified for simplicity) ----------
    public void RequestGravityVector(Vector2 newGravityDir, float rotateDuration)
    {
        newGravityDir = newGravityDir.normalized;
        StopAllCoroutines();

        if (rotateBodyWithGravity)
        {
            StartCoroutine(SmoothReorient(newGravityDir, Mathf.Max(0.01f, rotateDuration)));
        }
        else
        {
            gravityDir = newGravityDir;
            Physics2D.gravity = newGravityDir * gravityMagnitude;
        }
    }
    IEnumerator SmoothReorient(Vector2 newGravityDir, float duration)
    {
        gravityDir = newGravityDir;
        Physics2D.gravity = newGravityDir * gravityMagnitude;

        // Graphics Rotation Logic (Smooth Slerp)
        if (graphicsRoot)
        {
            Quaternion start = graphicsRoot.rotation;
            Quaternion target = Quaternion.FromToRotation(graphicsRoot.up, -newGravityDir) * graphicsRoot.rotation;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                graphicsRoot.rotation = Quaternion.Slerp(start, target, t / duration);
                yield return null;
            }
            graphicsRoot.rotation = target;
        }

        // Camera Rotation Logic (Smooth Slerp)
        if (targetCamera)
        {
            Quaternion cs = targetCamera.transform.rotation;
            Quaternion ct = Quaternion.FromToRotation(targetCamera.transform.up, -newGravityDir) * cs;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                targetCamera.transform.rotation = Quaternion.Slerp(cs, ct, t / duration);
                yield return null;
            }
            targetCamera.transform.rotation = ct;
        }

        // Reproject velocity
        Vector2 v = rb.velocity;
        float vRight = Vector2.Dot(v, rightAxis);
        float vUp = Vector2.Dot(v, upAxis);
        rb.velocity = vRight * rightAxis + vUp * upAxis;
    }

    // ---------- Power-Up API (Remains the same) ----------
    public void ApplySpeedMultiplier(float multiplier, float duration)
    {
        StopCoroutine(nameof(CoSpeedBoost));
        StartCoroutine(CoSpeedBoost(multiplier, duration));
    }
    IEnumerator CoSpeedBoost(float multiplier, float duration)
    {
        speedMult = Mathf.Max(0.01f, multiplier);
        yield return new WaitForSeconds(duration);
        speedMult = 1f;
    }

    public void ApplyJumpMultiplier(float multiplier, float duration)
    {
        StopCoroutine(nameof(CoJumpBoost));
        StartCoroutine(CoJumpBoost(multiplier, duration));
    }
    IEnumerator CoJumpBoost(float multiplier, float duration)
    {
        jumpMult = Mathf.Max(0.01f, multiplier);
        yield return new WaitForSeconds(duration);
        jumpMult = 1f;
    }
}