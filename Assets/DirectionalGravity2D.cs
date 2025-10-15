using UnityEngine;

public class DirectionalGravity2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Jump (Mario-style, clamped)")]
    public float jumpImpulse = 7f;           // initial push along upDir
    public float maxJumpHoldTime = 0.22f;    // how long holding Space adds boost
    public float holdAcceleration = 35f;     // accel added while holding
    public float maxJumpSpeed = 10f;         // HARD CAP on speed along upDir

    // “Better jump” feel using gravityScale (per-object, safer than AddForce)
    public float baseGravityScale = 1.0f;
    public float lowJumpMultiplier = 2.0f;   // stronger pull when key released early
    public float fallMultiplier = 2.5f;      // stronger pull when falling

    [Header("Gravity")]
    public float gravityMagnitude = 9.81f;   // global magnitude
    public float flipCooldown = 1f;

    [Header("Refs")]
    public Rigidbody2D rb;

    private Vector2 gDir = Vector2.down; // current gravity direction (unit)
    private bool canFlip = true;
    private bool isGrounded = false;

    // Jump state
    private bool jumpHeld = false;
    private bool isJumping = false;
    private float jumpHoldTimer = 0f;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = baseGravityScale;
        ApplyGravity(gDir);
    }

    void Update()
    {
        // Read non-physics input here only
        if (canFlip)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SetGravity(Vector2.left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) SetGravity(Vector2.right);
            if (Input.GetKeyDown(KeyCode.UpArrow)) SetGravity(Vector2.up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) SetGravity(Vector2.down);
        }

        jumpHeld = Input.GetKey(KeyCode.Space);
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            isJumping = true;
            jumpHoldTimer = 0f;
            isGrounded = false; // will be set true again on collision
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isJumping = false;
        }
    }

    void FixedUpdate()
    {
        Vector2 v = rb.velocity;

        // Movement mapping (depends on gravity axis)
        if (gDir == Vector2.left || gDir == Vector2.right)
        {
            int moveY = 0;
            if (Input.GetKey(KeyCode.W)) moveY += 1; // +Y
            if (Input.GetKey(KeyCode.S)) moveY -= 1; // -Y
            v.y = moveY * moveSpeed;
        }
        else
        {
            int moveX = 0;
            if (Input.GetKey(KeyCode.A)) moveX -= 1; // -X
            if (Input.GetKey(KeyCode.D)) moveX += 1; // +X
            v.x = moveX * moveSpeed;
        }

        Vector2 upDir = -gDir;

        // Start jump impulse (once)
        if (isJumping && jumpHoldTimer == 0f)
        {
            // Replace along-up component with jumpImpulse, keep orthogonal velocity
            float alongUp = Vector2.Dot(v, upDir);
            Vector2 vOrtho = v - alongUp * upDir;
            v = vOrtho + upDir * Mathf.Min(jumpImpulse, maxJumpSpeed);
        }

        // Hold-to-rise (boost) while under cap
        if (isJumping && jumpHeld && jumpHoldTimer < maxJumpHoldTime)
        {
            float upSpeed = Vector2.Dot(v, upDir);
            if (upSpeed < maxJumpSpeed)
            {
                float add = holdAcceleration * Time.fixedDeltaTime;
                float newUp = Mathf.Min(upSpeed + add, maxJumpSpeed);
                v += upDir * (newUp - upSpeed);
            }
            jumpHoldTimer += Time.fixedDeltaTime;
        }
        else
        {
            // stop boosting if timer expired or key not held
            isJumping = false;
        }

        // Better jump feel via gravityScale (per-object, stable)
        float upVel = Vector2.Dot(v, upDir);
        if (upVel > 0f)
        {
            // going up
            rb.gravityScale = jumpHeld ? baseGravityScale : baseGravityScale * lowJumpMultiplier;
        }
        else
        {
            // falling
            rb.gravityScale = baseGravityScale * fallMultiplier;
        }

        rb.velocity = v;
    }

    // Grounding: contact with normal ~ upDir counts as ground
    void OnCollisionStay2D(Collision2D collision)
    {
        foreach (var c in collision.contacts)
        {
            if (Vector2.Dot(c.normal, -gDir) > 0.6f)
            {
                isGrounded = true;
                // Reset jump state on land
                isJumping = false;
                jumpHoldTimer = 0f;
                return;
            }
        }
        isGrounded = false;
    }

    void OnCollisionExit2D(Collision2D collision) => isGrounded = false;

    // ---------- Gravity control ----------
    void SetGravity(Vector2 dir)
    {
        if (dir == Vector2.zero) return;
        gDir = dir.normalized;
        ApplyGravity(gDir);

        // Optional sprite orientation
        float z = (gDir == Vector2.down) ? 0f :
                  (gDir == Vector2.up) ? 180f :
                  (gDir == Vector2.left) ? 90f : -90f;
        transform.rotation = Quaternion.Euler(0, 0, z);

        canFlip = false;
        Invoke(nameof(ResetFlip), flipCooldown);

        // Cancel any active boost on flip
        isJumping = false;
        jumpHoldTimer = 0f;
    }

    void ApplyGravity(Vector2 dir) => Physics2D.gravity = dir * gravityMagnitude;
    void ResetFlip() => canFlip = true;
}
