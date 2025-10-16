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

    // ===== Animator (optional) =====
    [Header("Animator (Optional)")]
    public Animator animator;
    public string AnimSpeed = "Speed";
    public string AnimVertical = "VSpeed";
    public string AnimGrounded = "Grounded";
    public string AnimSliding = "Sliding";

    // ===== External systems (optional) =====
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

    // --- Directional gravity additions ---
    public float gravityMagnitude = 9.81f;
    public float gravityFlipCooldown = 0.8f;
    public bool gravityInputEnabled = true;
    public bool rotateWithGravity = true;

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
    bool canFlipGravity = true;

    // jump cut state
    bool jumpCutApplied = false;
    float lastJumpTime = -999f;

    // SFX rate-limit state
    float lastLandTime = -999f;
    float lastSlideSfx = -999f;

    // Facing state (flip sprite)
    int facing = 1; // +1 = face right, -1 = face left

    // ===== Input (legacy or New Input System) =====
#if ENABLE_INPUT_SYSTEM
    [Header("Input System (Optional)")]
    public InputActionReference moveAction; // Vector2
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

        // Initialize world gravity based on current gravityDir
        Physics2D.gravity = gravityDir * gravityMagnitude;
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

        // Arrow-key gravity flips (isolated mechanic)
        if (gravityInputEnabled && canFlipGravity)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SetGravity(Vector2.left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) SetGravity(Vector2.right);
            if (Input.GetKeyDown(KeyCode.UpArrow)) SetGravity(Vector2.up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) SetGravity(Vector2.down);
        }

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
        HandleJumpWithCoyoteBufferAndWalls();
        HandleJumpVariableHeight();
        HandleWallSlide();
        HandlePlatformAttachment();
    }

    // -------- Input ----------
    bool jumpDown;
    void ReadInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveAction && moveAction.action != null)
            moveInput = moveAction.action.ReadValue<Vector2>();
        else
#endif
        {
            // Legacy WASD mapping (world axes, remapped later to rightAxis)
            moveInput = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f)
            );
        }

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
        Bounds b = col.bounds;
        float inset = probeInset;

        // ground: 3 rays along –Up, centered & +/- along Right
        Vector2 originCenter = (Vector2)b.center - upAxis * (b.extents.y - inset);
        Vector2 originLeft = originCenter - rightAxis * (b.extents.x - inset);
        Vector2 originRight = originCenter + rightAxis * (b.extents.x - inset);

        RaycastHit2D gC = Physics2D.Raycast(originCenter, gravityDir, groundCheckDist, groundMask);
        RaycastHit2D gL = Physics2D.Raycast(originLeft, gravityDir, groundCheckDist, groundMask);
        RaycastHit2D gR = Physics2D.Raycast(originRight, gravityDir, groundCheckDist, groundMask);

        bool wasGrounded = grounded;
        grounded = gC.collider || gL.collider || gR.collider;

        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (!wasGrounded)
            {
                TryLandSfx();
                OnGroundedChanged?.Invoke(true);
            }
        }
        else
        {
            if (wasGrounded) OnGroundedChanged?.Invoke(false);
        }

        // walls: 2 rays along +/- Right
        Vector2 wOriginCenter = (Vector2)b.center;
        wallLeft = Physics2D.Raycast(wOriginCenter, -rightAxis, wallCheckDist, groundMask);
        wallRight = Physics2D.Raycast(wOriginCenter, rightAxis, wallCheckDist, groundMask);
    }

    bool IsWallSliding()
    {
        // sliding if touching wall, moving down along gravity, and pushing into that wall
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

        // Project input onto Right axis (we only move along Right in this controller)
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
        }
    }

    // -------- Jump height control (dynamic) ----------
    void HandleJumpVariableHeight()
    {
        float vUp = Vector2.Dot(rb.velocity, upAxis);

        // If rising and player released soon after takeoff, cut the upward speed
        if (vUp > 0f)
        {
            bool earlyRelease = !JumpHeld() && (Time.time - lastJumpTime) > jumpReleaseGrace;
            if (earlyRelease && !jumpCutApplied)
            {
                // Apply "jump cut" once
                float cut = vUp * (1f - Mathf.Clamp01(jumpCutMultiplier));
                rb.velocity -= upAxis * cut;
                jumpCutApplied = true;
            }
        }

        // Adjust gravity scale for better feel
        if (vUp > 0f)
        {
            // rising
            rb.gravityScale = JumpHeld() ? 1f : lowJumpGravityMult;
        }
        else
        {
            // falling
            rb.gravityScale = fallGravityMult;
        }
    }

    // -------- Walls (slide + jump away) ----------
    void HandleWallSlide()
    {
        if (!IsWallSliding()) return;

        float vRight = Vector2.Dot(rb.velocity, rightAxis);
        float vDown = Vector2.Dot(rb.velocity, gravityDir);
        float clampedDown = Mathf.Min(vDown, wallSlideMaxSpeed);
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

    // -------- Combined jump handler ----------
    void HandleJumpWithCoyoteBufferAndWalls()
    {
        jumpingThisFrame = false;

        // If still in wall-jump lock, ignore coyote jump
        if (Time.time < wallJumpLockUntil) return;

        TryJumpWithCoyoteAndBuffer();
    }

    // -------- Platform stick / detach ----------
    void HandlePlatformAttachment()
    {
        // Simple example: if grounded on a moving platform, cache its velocity & parent for visuals
        if (!grounded)
        {
            platformVelocity = Vector2.zero;
            if (attachedPlatform)
            {
                OnDetachedFromPlatform?.Invoke();
                attachedPlatform = null;
            }
            return;
        }

        // Cast straight down to see if we're on a kinematic/moving platform with Rigidbody2D
        Vector2 origin = col.bounds.center;
        RaycastHit2D hit = Physics2D.Raycast(origin, gravityDir, groundCheckDist + 0.05f, groundMask);
        if (hit && hit.rigidbody)
        {
            // approximate platform velocity from its Rigidbody2D
            platformVelocity = hit.rigidbody.velocity;
            if (attachedPlatform != hit.transform)
            {
                attachedPlatform = hit.transform;
                OnAttachedToPlatform?.Invoke(attachedPlatform);
            }
            return;
        }

        // otherwise reset
        platformVelocity = Vector2.zero;
        if (attachedPlatform)
        {
            OnDetachedFromPlatform?.Invoke();
            attachedPlatform = null;
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
            facing = dirInput > 0 ? 1 : -1;
        }
        else if (IsWallSliding())
        {
            facing = wallRight ? 1 : -1;
        }

        // Apply local-scale flip on the graphicsRoot
        Vector3 ls = graphicsRoot.localScale;
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
        animator.SetFloat(AnimVertical, vAlongUp);
        animator.SetBool(AnimGrounded, grounded);
        animator.SetBool(AnimSliding, IsWallSliding());
    }

    void TryLandSfx()
    {
        if (Time.time - lastLandTime >= landSfxCooldown)
        {
            audioManager?.PlayLand();
            lastLandTime = Time.time;
        }
    }

    // New gravity setter that updates Physics2D.gravity and enforces cooldown
    public void SetGravity(Vector2 newDir)
    {
        if (newDir == Vector2.zero || !canFlipGravity) return;
        newDir = newDir.normalized;
        // Smooth reorientation also sets gravityDir and reprojects velocity
        StopAllCoroutines();
        StartCoroutine(SmoothReorient(newDir, Mathf.Max(0.01f, rotationSlerpTime)));
        Physics2D.gravity = newDir * gravityMagnitude;
        canFlipGravity = false;
        Invoke(nameof(ResetGravityFlip), gravityFlipCooldown);
    }
    void ResetGravityFlip() => canFlipGravity = true;

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
            float t = 0f;
            while (t < duration) { t += Time.deltaTime; graphicsRoot.rotation = Quaternion.Slerp(start, target, t / duration); yield return null; }
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
