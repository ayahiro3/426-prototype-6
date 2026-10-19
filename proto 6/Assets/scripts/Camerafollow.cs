using UnityEngine;

// Put on the Main Camera. Follows the snake upward only, never back down.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [Tooltip("Where the snake head sits on screen. 0 = bottom edge, 1 = top edge.")]
    [Range(0f, 1f)]
    [SerializeField] float headScreenHeight = 0.3f;

    Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (target == null) return;
        float desiredY = target.position.y + cam.orthographicSize * (1f - 2f * headScreenHeight);
        if (desiredY > transform.position.y)
            transform.position = new Vector3(transform.position.x, desiredY, transform.position.z);
    }
}