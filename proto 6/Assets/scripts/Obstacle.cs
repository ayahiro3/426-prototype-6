using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [SerializeField] AudioClip deathSound;

    void OnTriggerEnter2D(Collider2D other)
    {
        SnakeController snake = other.GetComponent<SnakeController>();

        if (snake != null && snake.IsAlive)
        {
            if (deathSound != null)
                AudioSource.PlayClipAtPoint(deathSound, transform.position);

            snake.Die();
        }
    }
}
