using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    [Header("Configuración de Seguimiento")]
    public float offsetX = 4f;
    public float offsetY = 2f;
    public float smoothTime = 0.25f;

    private Vector3 velocity = Vector3.zero;

    // Cambiado a FixedUpdate para ir al ritmo de las físicas del Player
    void FixedUpdate()
    {
        if (player != null)
        {
            Vector3 targetPosition = new Vector3(player.position.x + offsetX, player.position.y + offsetY, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
    }
}