using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put this on the snake HEAD. The head needs a SpriteRenderer, a Rigidbody2D and a
// CircleCollider2D (Is Trigger on). Body segments are spawned from segmentPrefab and need no collider.
// The snake always climbs; the mouse only steers it left and right.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class SnakeController : MonoBehaviour
{
    [Tooltip("Seconds until the snake reaches max climb speed.")]
    [SerializeField] float secondsToMaxSpeed = 90f;
    float runTime;
    [Header("Climbing")]
    [SerializeField] float startClimbSpeed = 3.5f;
    [SerializeField] float maxClimbSpeed = 8f;

    [Header("Steering")]
    [Tooltip("Max sideways speed when chasing the cursor.")]
    [SerializeField] float sideSpeed = 12f;
    [Tooltip("Keeps the head this far inside the screen edges.")]
    [SerializeField] float sideMargin = 0.3f;
    [Tooltip("Roughly how long it takes to catch up to the cursor. Higher = smoother, lazier turns.")]
    [SerializeField] float steerSmoothTime = 0.18f;
    [Tooltip("How fast the head rotates to face where it's going, in degrees per second.")]
    [SerializeField] float headTurnSpeed = 540f;
    float sideVelocity;

    [Header("Body")]
    [SerializeField] SpriteRenderer segmentPrefab;
    [SerializeField] int startLength = 4;
    [SerializeField] float segmentSpacing = 0.35f;
    [Tooltip("How close a body segment has to get to a wrong-color block to be cut off.")]
    [SerializeField] float segmentHitRadius = 0.12f;

    [Header("Color")]
    [SerializeField] int startColor = 0;
    [SerializeField] float colorBlendSpeed = 12f;
    [Tooltip("How fast a color change ripples down the body, in segments per second.")]
    [SerializeField] float colorRippleSpeed = 40f;

    [Header("Hunger")]
    [Tooltip("Seconds between losing a tail segment. Eating an apple resets the timer.")]
    [SerializeField] float shrinkInterval = 2.5f;
    [Tooltip("How long the tail blinks before it drops off.")]
    [SerializeField] float shrinkWarningTime = 0.7f;

    float hungerTimer;

    public int CurrentColor { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public int Length => segments.Count;
    public float ClimbSpeed { get; private set; }
    public Vector2 HeadPosition => rb.position;

    const float PathPointSpacing = 0.05f;

    readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
    readonly List<Vector2> path = new List<Vector2>(); // path[0] is the newest point

    Rigidbody2D rb;
    SpriteRenderer headRenderer;
    Camera cam;
    float targetX;
    int previousColor;
    float colorChangedAt = -999f;

    readonly Collider2D[] hitBuffer = new Collider2D[8];
    ContactFilter2D blockFilter;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        headRenderer = GetComponent<SpriteRenderer>();
        cam = Camera.main;

        // Row blocks are triggers, so the overlap check has to include triggers.
        blockFilter = new ContactFilter2D { useTriggers = true, useLayerMask = false };

        if (cam == null)
            Debug.LogError("SnakeController: no camera tagged MainCamera.");
        else if (!cam.orthographic)
            Debug.LogWarning("SnakeController: camera is Perspective. Set Projection to Orthographic.");
    }

    void Start()
    {
        ClimbSpeed = startClimbSpeed;
        targetX = rb.position.x;
        CurrentColor = previousColor = startColor;
        headRenderer.color = GameColors.Get(CurrentColor);

        // Seed a straight trail below the head so the starting body has somewhere to sit.
        float seedLength = (startLength + 2) * segmentSpacing;
        for (float d = 0; d <= seedLength; d += PathPointSpacing)
            path.Add(rb.position + Vector2.down * d);

        for (int i = 0; i < startLength; i++) AddSegment();
        UpdateSegmentPositions();
    }

    void Update()
    {
        if (!IsAlive) return;
        ReadMouse();
        UpdateColors();
    }
     // Without apples the snake loses a tail segment every shrinkInterval seconds.
    // Out of segments and still starving = game over.
    void UpdateHunger(float dt)
    {
        hungerTimer += dt;
        if (hungerTimer < shrinkInterval) return;

        hungerTimer = 0f;
        if (segments.Count > 0) CutFrom(segments.Count - 1);
        else Die();
    }

    public void FeedApple() => hungerTimer = 0f;

    void FixedUpdate()
    {
        
        if (!IsAlive || cam == null) return;

        float dt = Time.fixedDeltaTime;
        runTime += dt;
        float t = Mathf.Clamp01(runTime / secondsToMaxSpeed);
        ClimbSpeed = Mathf.Lerp(startClimbSpeed, maxClimbSpeed, t * t); // slow start, then speeds up faster and faster

        float halfWidth = cam.orthographicSize * cam.aspect;
        float camX = cam.transform.position.x;
        float clampedTarget = Mathf.Clamp(targetX, camX - halfWidth + sideMargin, camX + halfWidth - sideMargin);

        Vector2 pos = rb.position;
        // Ease toward the cursor: speeds up and slows down smoothly instead of snapping.
        float newX = Mathf.SmoothDamp(pos.x, clampedTarget, ref sideVelocity, steerSmoothTime, sideSpeed, dt);
        Vector2 next = new Vector2(newX, pos.y + ClimbSpeed * dt);

        // Turn the head toward where it's going instead of snapping (sprite should face up).
        Vector2 vel = next - pos;
        float wantAngle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, wantAngle, headTurnSpeed * dt));
        rb.MovePosition(next);

        RecordPath(next);
        CheckSegmentHits();
        UpdateHunger(dt);
        
    }

    void LateUpdate()
    {
        if (IsAlive) UpdateSegmentPositions();
    }

    // ---------- Steering ----------

    bool mouseActive;
    Vector2 firstMouseReading;
    bool haveFirstReading;

    void ReadMouse()
    {
        if (cam == null || !Application.isFocused) return;

        Vector2 m = InputHelper.MouseScreenPosition;

        // Before the cursor has actually moved over the Game view, Unity often reports (0, 0),
        // the bottom-left corner, which would yank the snake to the left. Go straight up until
        // the mouse genuinely moves.
        if (!mouseActive)
        {
            if (!haveFirstReading) { firstMouseReading = m; haveFirstReading = true; return; }
            if (m == Vector2.zero || (m - firstMouseReading).sqrMagnitude < 4f) return;
            mouseActive = true;
        }

        // If the cursor leaves the Game view, keep the last target.
        if (m.x < 0 || m.y < 0 || m.x > Screen.width || m.y > Screen.height) return;

        targetX = cam.ScreenToWorldPoint(m).x;
    }

    // ---------- Body ----------

    void RecordPath(Vector2 headPos)
    {
        if (path.Count == 0 || Vector2.Distance(path[0], headPos) >= PathPointSpacing)
            path.Insert(0, headPos);

        float needed = (segments.Count + 2) * segmentSpacing;
        float total = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            total += Vector2.Distance(path[i - 1], path[i]);
            if (total > needed)
            {
                path.RemoveRange(i + 1, path.Count - i - 1);
                break;
            }
        }
    }

    // Walks back along the trail and drops a segment every segmentSpacing units.
    void UpdateSegmentPositions()
    {
        Vector2 prev = transform.position;
        float walked = 0f;
        float target = segmentSpacing;
        int seg = 0;

        for (int i = 0; i < path.Count && seg < segments.Count; i++)
        {
            Vector2 p = path[i];
            float d = Vector2.Distance(prev, p);
            while (seg < segments.Count && walked + d >= target)
            {
                float t = d > 0f ? (target - walked) / d : 0f;
                segments[seg].transform.position = Vector2.Lerp(prev, p, t);
                seg++;
                target += segmentSpacing;
            }
            walked += d;
            prev = p;
        }

        for (; seg < segments.Count; seg++)
            segments[seg].transform.position = prev;
    }

    // The first segment touching a wrong-color block gets cut off, along with everything behind it.
    void CheckSegmentHits()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            int count = Physics2D.OverlapCircle(segments[i].transform.position, segmentHitRadius, blockFilter, hitBuffer);
            for (int h = 0; h < count; h++)
            {
                var block = hitBuffer[h].GetComponent<ColorBlock>();
                if (block != null && block.ColorIndex != SegmentColorIndex(i))
                {
                    CutFrom(i);
                    return;
                }
            }
        }
    }

    // A segment's color is whatever the ripple has reached, so switching colors right
    // before a row can cost you the tail that hasn't changed yet.
    int SegmentColorIndex(int i)
    {
        float reached = (Time.time - colorChangedAt) * colorRippleSpeed;
        return i < reached ? CurrentColor : previousColor;
    }

    void CutFrom(int index)
    {
        for (int i = segments.Count - 1; i >= index; i--)
        {
            SpriteRenderer seg = segments[i];
            segments.RemoveAt(i);
            seg.color = Color.white;
            StartCoroutine(Pop(seg.transform, destroyAfter: true));
        }
    }

    public void Grow(int amount = 1)
    {
        for (int i = 0; i < amount; i++) AddSegment();
    }

    void AddSegment()
    {
        Vector3 spawnPos = segments.Count > 0
            ? segments[segments.Count - 1].transform.position
            : transform.position;

        SpriteRenderer seg = Instantiate(segmentPrefab, spawnPos, Quaternion.identity);
        seg.color = GameColors.Get(CurrentColor);
        seg.sortingOrder = headRenderer.sortingOrder - 1 - segments.Count;
        segments.Add(seg);
    }

    // ---------- Color ----------

    public void SetColor(int colorIndex)
    {
        if (colorIndex == CurrentColor) return;
        previousColor = CurrentColor;
        CurrentColor = colorIndex;
        colorChangedAt = Time.time;
    }

    // Head changes first, then the new color ripples down the body.
    void UpdateColors()
    {
        Color target = GameColors.Get(CurrentColor);
        Color old = GameColors.Get(previousColor);
        float blend = 1f - Mathf.Exp(-colorBlendSpeed * Time.deltaTime);

        headRenderer.color = Color.Lerp(headRenderer.color, target, blend);

        float reached = (Time.time - colorChangedAt) * colorRippleSpeed;
        for (int i = 0; i < segments.Count; i++)
        {
            Color want = i < reached ? target : old;
            segments[i].color = Color.Lerp(segments[i].color, want, blend);
        }
        if (shrinkInterval - hungerTimer < shrinkWarningTime)
        {
            SpriteRenderer next = segments.Count > 0 ? segments[segments.Count - 1] : headRenderer;
            Color c = next.color;
            c.a = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(Time.time * 18f));
            next.color = c;
        }
    }

    // ---------- Death ----------

    public void Die()
    {
        if (!IsAlive) return;
        IsAlive = false;
        StartCoroutine(DeathRoutine());
    }

    // Wordless game over: flash white, then pop apart from tail to head.
    IEnumerator DeathRoutine()
    {
        var all = new List<SpriteRenderer> { headRenderer };
        all.AddRange(segments);
        Color body = GameColors.Get(CurrentColor);

        for (int f = 0; f < 3; f++)
        {
            foreach (var r in all) r.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            foreach (var r in all) r.color = body;
            yield return new WaitForSeconds(0.08f);
        }

        float popDelay = Mathf.Min(0.03f, 1f / all.Count);
        for (int i = all.Count - 1; i >= 0; i--)
        {
            StartCoroutine(Pop(all[i].transform));
            yield return new WaitForSeconds(popDelay);
        }

        yield return new WaitForSeconds(0.3f);
        if (GameManager.Instance != null) GameManager.Instance.OnSnakeDeathFinished();
    }

    IEnumerator Pop(Transform t, bool destroyAfter = false)
    {
        Vector3 start = t.localScale;
        const float duration = 0.15f;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration;
            float s = k < 0.4f ? Mathf.Lerp(1f, 1.4f, k / 0.4f) : Mathf.Lerp(1.4f, 0f, (k - 0.4f) / 0.6f);
            t.localScale = start * s;
            yield return null;
        }
        t.localScale = Vector3.zero;
        if (destroyAfter) Destroy(t.gameObject);
    }
}