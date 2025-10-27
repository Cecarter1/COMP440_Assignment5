using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Gravity")]
    public float gravityMagnitude = 9.81f;
    public float flipCooldown = 1f;
    [Tooltip("Rotate the collider/body to face gravity when it flips.")]
    public bool rotateBodyWithGravity = true;

    [Header("Wall / Surface Handling")]
    [Tooltip("Dot(normal, -gDir) threshold to count as ground (0.6 ~ within ~53° of 'up').")]
    public float groundDotThreshold = 0.6f;
    [Tooltip("Small push away from non-ground surfaces to prevent sticky contacts.")]
    public float wallSeparationImpulse = 0.05f;
    [Tooltip("If true, assign a 0-friction material at runtime to reduce wall-sticking from friction.")]
    public bool forceZeroFriction = true;

    [Header("Refs")]
    public Rigidbody2D rb;

    private Vector2 gDir = Vector2.down;   // current gravity direction (unit)
    private bool canFlip = true;
    private bool isGrounded = false;
    private Vector2 lastNonGroundNormal = Vector2.zero;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 1f;

        // Prevent walls from knocking us over (physics torque disabled)
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.angularVelocity = 0f;
        rb.rotation = 0f;

        // Keep CapsuleCollider2D vertical in local space (world orientation comes from transform rotation)
        var cap = GetComponent<CapsuleCollider2D>();
        if (cap) cap.direction = CapsuleDirection2D.Vertical;

        if (forceZeroFriction)
        {
            var mat = new PhysicsMaterial2D("NoFrictionRuntime");
            mat.friction = 0f;
            mat.bounciness = 0f;
            rb.sharedMaterial = mat;
        }

        ApplyGravity(gDir);
        ApplyBodyRotationToGravity();
    }

    void Update()
    {
        if (canFlip)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SetGravity(Vector2.left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) SetGravity(Vector2.right);
            if (Input.GetKeyDown(KeyCode.UpArrow)) SetGravity(Vector2.up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) SetGravity(Vector2.down);
        }
    }

    void FixedUpdate()
    {
        Vector2 v = rb.velocity;

        // Move along axis orthogonal to gravity
        if (gDir == Vector2.left || gDir == Vector2.right)
        {
            int moveY = 0;
            if (Input.GetKey(KeyCode.W)) moveY += 1;
            if (Input.GetKey(KeyCode.S)) moveY -= 1;
            v.y = moveY * moveSpeed;
        }
        else
        {
            int moveX = 0;
            if (Input.GetKey(KeyCode.A)) moveX -= 1;
            if (Input.GetKey(KeyCode.D)) moveX += 1;
            v.x = moveX * moveSpeed;
        }

        // Anti-wall-stick: remove velocity into walls and add a tiny separation
        if (lastNonGroundNormal != Vector2.zero)
        {
            float intoWall = Vector2.Dot(v, -lastNonGroundNormal);
            if (intoWall > 0f)
            {
                v -= intoWall * (-lastNonGroundNormal);
                v += lastNonGroundNormal * wallSeparationImpulse;
            }
        }

        rb.velocity = v;
        lastNonGroundNormal = Vector2.zero;

        // Belt & suspenders: ensure physics never spins us
        rb.angularVelocity = 0f;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        bool touchedGround = false;
        foreach (var c in collision.contacts)
        {
            if (Vector2.Dot(c.normal, -gDir) > groundDotThreshold)
                touchedGround = true;
            else
                lastNonGroundNormal = c.normal;
        }
        isGrounded = touchedGround;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }

    // ---------- Gravity control ----------
    void SetGravity(Vector2 dir)
    {
        if (dir == Vector2.zero) return;
        gDir = dir.normalized;
        ApplyGravity(gDir);
        ApplyBodyRotationToGravity();   // rotate collider/body to match gravity

        canFlip = false;
        Invoke(nameof(ResetFlip), flipCooldown);
    }

    void ApplyBodyRotationToGravity()
    {
        if (!rotateBodyWithGravity) return;

        // FreezeRotation stops physics torque, but we can still set transform/rotation manually.
        float z = (gDir == Vector2.down) ? 0f :
                  (gDir == Vector2.up) ? 180f :
                  (gDir == Vector2.left) ? 90f : -90f;

        // Set the rigidbody rotation so the collider rotates too.
        rb.rotation = z;
    }

    void ApplyGravity(Vector2 dir) => Physics2D.gravity = dir * gravityMagnitude;
    void ResetFlip() => canFlip = true;
}
