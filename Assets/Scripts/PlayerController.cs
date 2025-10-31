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
    public event Action<Vector2, JumpKind> OnJumpPerformed;

    public enum JumpKind { Ground, Wall, Air }

    // ===== References =====
    [Header("References")]
    public Animator animator;
    public PlayerStateMachine stateMachine;
    public PlayerAudioManager audioManager;
    public Transform graphicsRoot;      // visuals child
    public Camera targetCamera;         // optional: rotate with gravity

    Rigidbody2D rb;
    Collider2D col;

    // Fallback visual target if graphicsRoot is not assigned  (Change #1)
    Transform visual;

    // ===== Collision / Contacts =====
    [Header("Collision")]
    public LayerMask groundMask = ~0;
    public float probeInset = 0.03f;
    public float groundCheckDist = 0.16f;
    public float wallCheckDist = 0.22f;

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

    // ===== Unlimited Air Jump =====
    [Header("Air Jump (Unlimited)")]
    public bool enableUnlimitedAirJumps = true;

    // ===== Walls =====
    [Header("Walls")]
    public float wallSlideMaxSpeed = 2.5f;
    public float wallJumpLateral = 8f;
    public float wallJumpVertical = 12f;
    public float wallJumpLockTime = 0.12f;

    // ===== Dash =====
    [Header("Dash")]
    public bool enableDash = true;
    public float dashSpeed = 18f;
    public float dashTime = 0.18f;
    public float dashCooldown = 0.35f;
    public AudioClip dashClip; // optional SFX

    bool isDashing = false;
    float dashEndTime = -999f;
    float nextDashTime = -999f;
    Vector2 dashDir;

    // ===== Gravity / Rotation (MERGED FROM gravityscript) =====
    [Header("Gravity Switch")]
    [Tooltip("World gravity magnitude set on flip.")]
    public float gravityMagnitude = 7.2f;   // was 9.81f → lighter feel
    [Tooltip("Seconds before you can flip gravity again.")]
    public float flipCooldown = 1f;
    [Tooltip("Rotate character visuals with gravity flips.")]
    public bool rotateBodyWithGravity = true;
    [Tooltip("Slerp time used by SmoothReorient")]
    public float rotationSlerpTime = 0.2f;

    bool canFlipGravity = true;

    // ===== State =====
    Vector2 gravityDir = Vector2.down;
    Vector2 rightAxis => new Vector2(-gravityDir.y, gravityDir.x).normalized; // axis orthogonal to gravity
    Vector2 upAxis => -gravityDir; // up is opposite gravity

    float lastGroundedTime = -999f, lastJumpPressedTime = -999f;
    bool grounded, wallLeft, wallRight, jumpingThisFrame;
    float wallJumpLockUntil = -999f;
    Vector2 moveInput;             // world-space along rightAxis
    float rawMoveScalar;           // -1..1 along rightAxis (we remap keys per gravity)
    int lastMoveSign = 1;          // remembers last non-zero input direction
    Vector2 platformVelocity;
    Transform attachedPlatform;

    bool jumpCutApplied = false;
    float lastJumpTime = -999f;

    float lastLandTime = -999f;
    float lastSlideSfx = -999f;

    int facing = 1; // +1 right, -1 left

    // Animator hashes
    static readonly int AnimSpeed = Animator.StringToHash("Speed");
    static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int AnimIsWallSliding = Animator.StringToHash("IsWallSliding");
    static readonly int AnimVAlongUp = Animator.StringToHash("VAlongUp");

    // ===== Public exposure for other systems =====
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
    public InputActionReference moveAction; // Vector2 (x) — will be overridden by gravity-aware WASD below
    public InputActionReference jumpAction; // Button
    public InputActionReference dashAction; // Button
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

        // (Change #1) Fallback visual if graphicsRoot is not set
        visual = graphicsRoot ? graphicsRoot : transform;

        // Initialize world gravity and visual orientation to match internal gravityDir
        Physics2D.gravity = gravityDir * gravityMagnitude;

        if (rotateBodyWithGravity && visual)
        {
            // For 2D, look "forward" on Z, and set Up to -gravityDir
            visual.rotation = Quaternion.LookRotation(Vector3.forward, -gravityDir);
        }

        if (targetCamera && rotateBodyWithGravity)
        {
            targetCamera.transform.rotation = Quaternion.LookRotation(Vector3.forward, -gravityDir);
        }
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
        // --- Gravity flipping via arrow keys (borrowed behavior) ---
        HandleGravityFlipHotkeys();

        ReadInput();
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
        TryJumpWithCoyoteAndBuffer(); // ground/coyote jump
        TryUnlimitedAirJump();        // unlimited mid-air taps (your existing toggle)
        ApplyVariableJumpGravity();
        HandleWallSlideAndWallJump();

        if (attachedPlatform == null) platformVelocity = Vector2.zero;

        OnVelocityChanged?.Invoke(rb.velocity);
        jumpingThisFrame = false;
    }

    // ---------- Gravity flip (arrows) ----------
    void HandleGravityFlipHotkeys()
    {
        if (!canFlipGravity) return;

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

        // world gravity (matches gravityscript behavior)
        Physics2D.gravity = gravityDir * gravityMagnitude;

        // rotate character visuals smoothly (keeps collider stable, like your current setup)
        if (rotateBodyWithGravity)
        {
            // reuse your existing smooth reorient coroutine via this wrapper
            RequestGravityVector(gravityDir, rotationSlerpTime);
        }

        // flip cooldown
        canFlipGravity = false;
        Invoke(nameof(ResetFlip), flipCooldown);
    }
    void ResetFlip() => canFlipGravity = true;

    void ReadInput()
    {
        float scalar = 0f;

        if (gravityDir == Vector2.left || gravityDir == Vector2.right)
        {
            bool w = Input.GetKey(KeyCode.W);
            bool s = Input.GetKey(KeyCode.S);

            if (w) scalar += (gravityDir == Vector2.right ? +1f : -1f);
            if (s) scalar += (gravityDir == Vector2.right ? -1f : +1f);
            // A/D intentionally ignored here
        }
        else
        {
            // Up/Down gravity: horizontal A/D only
            bool a = Input.GetKey(KeyCode.A);
            bool d = Input.GetKey(KeyCode.D);

            if (a) scalar -= 1f;
            if (d) scalar += 1f;

            // FIX: when gravity is UP, invert so:
            //  A = move negative X (left), D = move positive X (right)
            if (gravityDir == Vector2.up)
                scalar = -scalar;
        }

        rawMoveScalar = Mathf.Clamp(scalar, -1f, 1f);
        if (Time.time < wallJumpLockUntil) rawMoveScalar = 0f;

        moveInput = rawMoveScalar * rightAxis;

        if (Mathf.Abs(rawMoveScalar) > 0.05f)
            lastMoveSign = rawMoveScalar > 0 ? 1 : -1;

        bool jumpDown = false;
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) jumpDown = jumpAction.action.WasPressedThisFrame();
        else
#endif
        { jumpDown = Input.GetKeyDown(KeyCode.Space); }
        if (jumpDown) lastJumpPressedTime = Time.time;

        bool dashPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (dashAction && dashAction.action != null) dashPressed = dashAction.action.WasPressedThisFrame();
        else
#endif
        { dashPressed = Input.GetKeyDown(KeyCode.LeftShift); }

        if (dashPressed && enableDash) TryStartDash();
    }

    bool JumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (jumpAction && jumpAction.action != null) return jumpAction.action.IsPressed();
#endif
        return Input.GetKey(KeyCode.Space);
    }

    // ---------- Contacts ----------
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
        if (grounded != wasGrounded) OnGroundedChanged?.Invoke(grounded);

        // wall rays (left/right)
        Vector2 midLeft = center - right * (ext.x - probeInset);
        Vector2 midRight = center + right * (ext.x - probeInset);
        wallLeft = Physics2D.Raycast(midLeft, -right, wallCheckDist, groundMask);
        wallRight = Physics2D.Raycast(midRight, right, wallCheckDist, groundMask);
    }

    bool IsWallSliding()
    {
        if (grounded || isDashing) return false;

        bool touching = wallLeft || wallRight;
        bool movingDown = Vector2.Dot(rb.velocity, gravityDir) > 0.01f;
        bool movingToward =
            (wallLeft && Vector2.Dot(moveInput, -rightAxis) > 0f) ||
            (wallRight && Vector2.Dot(moveInput, rightAxis) > 0f);

        return touching && movingDown && movingToward;
    }

    // ---------- Movement ----------
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

    // ---------- Ground/Coyote Jump ----------
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

            audioManager?.PlayJump();
            OnJumpPerformed?.Invoke(rb.velocity, JumpKind.Ground);
            OnGroundedChanged?.Invoke(false);
        }
    }

    // ---------- Unlimited Air Jump ----------
    void TryUnlimitedAirJump()
    {
        if (!enableUnlimitedAirJumps) return;
        if (grounded || isDashing) return;

        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        if (buffered)
        {
            lastJumpPressedTime = -999f;

            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + (jumpSpeed * jumpMult * upAxis);

            lastJumpTime = Time.time;
            jumpCutApplied = false;

            audioManager?.PlayJump();
            OnJumpPerformed?.Invoke(rb.velocity, JumpKind.Air);
        }
    }

    // ---------- Variable jump height / better fall ----------
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
            float vUpClamped = Mathf.Min(vUp, maxUp);
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            rb.velocity = vRight * rightAxis + vUpClamped * upAxis;
            jumpCutApplied = true;
        }

        if (!rising) jumpCutApplied = false;
    }

    // ---------- Wall slide & wall jump ----------
    void HandleWallSlideAndWallJump()
    {
        if (!IsWallSliding()) return;

        float vDown = Vector2.Dot(rb.velocity, gravityDir);
        float vRight = Vector2.Dot(rb.velocity, rightAxis);

        float clampedDown = Mathf.Min(vDown, Mathf.Abs(wallSlideMaxSpeed));
        rb.velocity = vRight * rightAxis + clampedDown * gravityDir;

        //        if (Time.time - lastSlideSfx >= slideSfxCooldown)
        //      {
        //          audioManager?.PlaySlide();
        //          lastSlideSfx = Time.time;
        //      }

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

    // ---------- Dash (bidirectional) ----------
    void TryStartDash()
    {
        if (!enableDash) return;
        if (isDashing) return;
        if (Time.time < nextDashTime) return;

        float sign = 0f;

        if (Mathf.Abs(rawMoveScalar) > 0.05f)
            sign = Mathf.Sign(rawMoveScalar);

        if (Mathf.Abs(sign) < 0.5f)
        {
            float vRight = Vector2.Dot(rb.velocity, rightAxis);
            if (Mathf.Abs(vRight) > 0.05f)
                sign = Mathf.Sign(vRight);
        }

        if (Mathf.Abs(sign) < 0.5f)
            sign = lastMoveSign;

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

    // ---------- Facing / flipping ----------
    void UpdateFacing()
    {
        if (!graphicsRoot) return;

        float dirInput = rawMoveScalar;
        if (Math.Abs(dirInput) > 0.05f)
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
        ls.x = facing > 0 ? absX : -absX;
        graphicsRoot.localScale = ls;
    }

    // ---------- Animator ----------
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

    // ---------- Platform attach / land SFX ----------
    void OnCollisionEnter2D(Collision2D c)
    {
        // Run platform attachment check
        TryAttachToPlatform(c);

        if (grounded)
        {
            // Explicitly fire the event to trigger the Animator transition (Fall -> Idle)
            OnGroundedChanged?.Invoke(true);

            // Run the land sound effect logic
            
        }
    }
    void OnCollisionStay2D(Collision2D c) { TryAttachToPlatform(c); }
    void OnCollisionExit2D(Collision2D c)
    {
        if (attachedPlatform != null && c.transform == attachedPlatform)
        {
            attachedPlatform = null;
            platformVelocity = Vector2.zero;
            OnDetachedFromPlatform?.Invoke();
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
    //    void TryLandSfx()
    //    {
    //        if (Time.time - lastLandTime >= landSfxCooldown)
    //        {
    //            audioManager?.PlayLand();
    //            lastLandTime = Time.time;
    //        }
    //    }

    // ---------- Gravity hook (already present) ----------
    public void RequestGravityVector(Vector2 newGravityDir, float rotateDuration)
    {
        newGravityDir = newGravityDir.normalized;
        StopAllCoroutines();
        StartCoroutine(SmoothReorient(newGravityDir, Mathf.Max(0.01f, rotateDuration)));
    }

    // (Change #2) Robust world-up aiming for visuals/camera
    IEnumerator SmoothReorient(Vector2 newGravityDir, float duration)
    {
        gravityDir = newGravityDir;

        Quaternion startVis = visual ? visual.rotation : Quaternion.identity;
        Quaternion targetVis = Quaternion.LookRotation(Vector3.forward, -newGravityDir);

        Quaternion startCam = targetCamera ? targetCamera.transform.rotation : Quaternion.identity;
        Quaternion targetCam = Quaternion.LookRotation(Vector3.forward, -newGravityDir);

        float t = 0f;

        if (duration <= 0.011f)
        {
            if (visual && rotateBodyWithGravity) visual.rotation = targetVis;
            if (targetCamera && rotateBodyWithGravity) targetCamera.transform.rotation = targetCam;
        }
        else
        {
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);

                if (visual && rotateBodyWithGravity)
                    visual.rotation = Quaternion.Slerp(startVis, targetVis, u);

                if (targetCamera && rotateBodyWithGravity)
                    targetCamera.transform.rotation = Quaternion.Slerp(startCam, targetCam, u);

                yield return null;
            }
            if (visual && rotateBodyWithGravity) visual.rotation = targetVis;
            if (targetCamera && rotateBodyWithGravity) targetCamera.transform.rotation = targetCam;
        }

        // Reproject velocity onto new axes so momentum feels consistent
        Vector2 v = rb.velocity;
        float vRight = Vector2.Dot(v, rightAxis);
        float vUp = Vector2.Dot(v, upAxis);
        rb.velocity = vRight * rightAxis + vUp * upAxis;
    }

    // ---------- Power-Up API ----------
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
