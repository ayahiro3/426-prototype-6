using UnityEngine;

public enum PickupType { Apple, ColorSwitch }

// Apples: neutral, always grow you.
// Color switches: change your color, and grow you more if it's a NEW color (rewards switching).
// Tip: use a different shape per type (e.g. small circle for apples, diamond for color switches).
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class Pickup : MonoBehaviour
{
    [SerializeField] PickupType type = PickupType.Apple;
    [SerializeField] Color appleColor = new Color(0.92f, 0.92f, 0.92f);
    [SerializeField] int appleGrowth = 1;
    [Tooltip("Growth for switching to a different color.")]
    [SerializeField] int switchGrowth = 2;

    [Header("Idle animation")]
    [SerializeField] float pulseAmount = 0.1f;
    [SerializeField] float pulseSpeed = 4f;
    [SerializeField] float spinSpeed = 90f;

    [SerializeField] AudioClip pickupSound;

    public PickupType Type => type;
    public int ColorIndex { get; private set; }

    SpriteRenderer sr;
    Vector3 baseScale;
    float phase;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<Collider2D>().isTrigger = true;
        baseScale = transform.localScale;
        phase = Random.value * Mathf.PI * 2f;
        if (type == PickupType.Apple) sr.color = appleColor;
    }

    public void SetColor(int colorIndex)
    {
        ColorIndex = colorIndex;
        sr.color = GameColors.Get(colorIndex);
    }

    void Update()
    {
        transform.localScale = baseScale * (1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed + phase));
        if (type == PickupType.ColorSwitch)
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var snake = other.GetComponent<SnakeController>();
        if (snake == null || !snake.IsAlive) return;

        if (type == PickupType.Apple)
        {
            snake.Grow(appleGrowth);
        }
        else if (snake.CurrentColor != ColorIndex)
        {
            snake.SetColor(ColorIndex);
            snake.Grow(switchGrowth);
        }

       Debug.Log("Pickup sound code reached!");

    if (pickupSound != null)
    {
        Debug.Log("Sound is assigned!");
        AudioSource.PlayClipAtPoint(pickupSound, transform.position);
    }
    else
    {
        Debug.LogWarning("Sound is missing!");
    }
        Destroy(gameObject);
    }
}