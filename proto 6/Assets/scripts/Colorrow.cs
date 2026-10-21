using System.Collections.Generic;
using UnityEngine;

// A full-width bar split into one block per color. Pass through your color, die on any other.
// Rows can slide sideways (wrapping around), which is where the mouse steering earns its keep.
public class ColorRow : MonoBehaviour
{
    [Tooltip("A 1x1 unit square sprite with ColorBlock and a BoxCollider2D.")]
    [SerializeField] ColorBlock blockPrefab;
    [SerializeField] float thickness = 0.4f;
    // [SerializeField] int passGrowth = 2;

    [SerializeField] AudioClip deathSound;
    [SerializeField] AudioClip successSound;

    readonly List<ColorBlock> blocks = new List<ColorBlock>();
    float left, width, laneWidth, slideSpeed, offset;
    int lanes;
    // bool passed;

    public void Build(float leftEdge, float rowWidth, float slide)
    {
        lanes = GameColors.Count;
        left = leftEdge;
        width = rowWidth;
        laneWidth = rowWidth / lanes;
        slideSpeed = slide;

        // Shuffle which color goes in which lane.
        int[] order = new int[lanes];
        for (int i = 0; i < lanes; i++) order[i] = i;
        for (int i = lanes - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        // Sliding rows need a second copy of the pattern so the wrap is seamless.
        int count = slideSpeed != 0f ? lanes * 2 : lanes;
        for (int i = 0; i < count; i++)
        {
            ColorBlock b = Instantiate(blockPrefab, transform);
            b.Init(this, order[i % lanes]);
            b.transform.localScale = new Vector3(laneWidth, thickness, 1f);
            blocks.Add(b);
        }

        if (slideSpeed != 0f) offset = Random.Range(0f, width);
        Layout();
    }

    void Update()
    {
        if (slideSpeed == 0f) return;
        offset += slideSpeed * Time.deltaTime;
        Layout();
    }

    void Layout()
    {
        bool sliding = blocks.Count > lanes;
        float y = transform.position.y;
        for (int i = 0; i < blocks.Count; i++)
        {
            float x = sliding
                ? left - width + laneWidth * 0.5f + Mathf.Repeat(i * laneWidth + offset, width * 2f)
                : left + laneWidth * 0.5f + i * laneWidth;
            blocks[i].transform.position = new Vector3(x, y, 0f);
        }
    }

        public void OnBlockHit(ColorBlock block, SnakeController snake)
    {
        if (snake.CurrentColor != block.ColorIndex)
            snake.Die();
    }
}