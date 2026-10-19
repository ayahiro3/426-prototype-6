using UnityEngine;

// One colored piece of a ColorRow. Created by ColorRow; don't place these by hand.
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class ColorBlock : MonoBehaviour
{
    public int ColorIndex { get; private set; }

    ColorRow row;

    public void Init(ColorRow owner, int colorIndex)
    {
        row = owner;
        ColorIndex = colorIndex;
        GetComponent<SpriteRenderer>().color = GameColors.Get(colorIndex);
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var snake = other.GetComponent<SnakeController>();
        if (snake == null || !snake.IsAlive) return;
        row.OnBlockHit(this, snake);
    }
}