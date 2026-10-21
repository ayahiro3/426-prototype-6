using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Spawns color rows and pickups above the camera as the snake climbs,
// cleans up what falls off the bottom, and restarts on click after death.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] SnakeController snake;
    [SerializeField] ColorRow rowPrefab;
    [SerializeField] Pickup applePrefab;
    [SerializeField] Pickup colorSwitchPrefab;

    [Header("Rows")]
    [SerializeField] float firstRowDistance = 7f;
    [SerializeField] float startRowGap = 6f;
    [SerializeField] float minRowGap = 3.5f;
    [Tooltip("How much the gap shrinks with each row.")]
    [SerializeField] float gapShrinkPerRow = 0.08f;
    [Tooltip("Rows before they start sliding sideways.")]
    [SerializeField] int rowsBeforeSliding = 3;
    [SerializeField] float minSlideSpeed = 0.8f;
    [SerializeField] float maxSlideSpeed = 3f;
    [Tooltip("Rows it takes after sliding starts to reach max slide speed.")]
    [SerializeField] int rowsToMaxSlide = 20;

    [Header("Pickups (placed in the gaps between rows)")]
    [Range(0f, 1f)]
    [SerializeField] float colorSwitchChance = 0.7f;
    [SerializeField] int applesPerGap = 2;
    [SerializeField] float sideMargin = 0.6f;
    [SerializeField] Obstacle obstaclePrefab;
    [SerializeField] int obstaclesPerGap = 2;

    readonly List<GameObject> spawned = new List<GameObject>();
    float nextRowY;
    int rowsSpawned;
    bool waitingForRestart;
    Camera cam;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    void Start()
    {
        nextRowY = snake.HeadPosition.y + firstRowDistance;
    }

    void Update()
    {
        if (waitingForRestart)
        {
            if (InputHelper.ClickedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        float camY = cam.transform.position.y;
        float top = camY + cam.orthographicSize;
        float bottom = camY - cam.orthographicSize;

        // Build a little past the top of the screen so nothing pops in.
        while (nextRowY < top + 3f) SpawnSection();

        // Clean up anything well below the screen.
        spawned.RemoveAll(go => go == null);
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i].transform.position.y < bottom - 3f)
            {
                Destroy(spawned[i]);
                spawned.RemoveAt(i);
            }
        }
    }

    public void OnSnakeDeathFinished() => waitingForRestart = true;

    void SpawnSection()
    {
        float halfWidth = cam.orthographicSize * cam.aspect;
        float camX = cam.transform.position.x;

        // The row.
        ColorRow row = Instantiate(rowPrefab, new Vector3(0f, nextRowY, 0f), Quaternion.identity);
        row.Build(camX - halfWidth, halfWidth * 2f, PickSlideSpeed());
        spawned.Add(row.gameObject);

        // The gap above it.
        float gap = Mathf.Max(minRowGap, startRowGap - rowsSpawned * gapShrinkPerRow);
        float gapStart = nextRowY + 0.8f;
        float gapEnd = nextRowY + gap - 0.8f;
        float mid = (gapStart + gapEnd) * 0.5f;

        if (Random.value < colorSwitchChance)
        {
            Pickup cs = Instantiate(colorSwitchPrefab, new Vector3(RandomX(camX, halfWidth), mid, 0f), Quaternion.identity);
            cs.SetColor(GameColors.RandomExcept(snake.CurrentColor));
            spawned.Add(cs.gameObject);
        }

        for (int i = 0; i < applesPerGap; i++)
        {
            float y = Random.Range(gapStart, gapEnd);
            if (Mathf.Abs(y - mid) < 0.6f) y += 1f; // keep apples off the color switch
            Pickup a = Instantiate(applePrefab, new Vector3(RandomX(camX, halfWidth), y, 0f), Quaternion.identity);
            spawned.Add(a.gameObject);
        }

        for (int i = 0; i < obstaclesPerGap; i++)
        {
             float y = Random.Range(gapStart, gapEnd);

            Obstacle o = Instantiate(
            obstaclePrefab,
            new Vector3(RandomX(camX, halfWidth), y, 0f),
            Quaternion.identity
            );

            spawned.Add(o.gameObject);
        }

        nextRowY += gap;
        rowsSpawned++;
    }

    float PickSlideSpeed()
    {
        if (rowsSpawned < rowsBeforeSliding) return 0f;
        float t = Mathf.Clamp01((rowsSpawned - rowsBeforeSliding) / (float)rowsToMaxSlide);
        float speed = Mathf.Lerp(minSlideSpeed, maxSlideSpeed, t) * Random.Range(0.7f, 1f);
        return Random.value < 0.5f ? speed : -speed;
    }

    float RandomX(float camX, float halfWidth) =>
        Random.Range(camX - halfWidth + sideMargin, camX + halfWidth - sideMargin);
}