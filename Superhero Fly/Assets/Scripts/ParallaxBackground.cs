using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public GameObject cam;
    public float parallaxFactor; // 0 = fondo fijo, 1 = se mueve con cámara

    [Header("Asignar los Objetos Padres")]
    public Transform backgroundLeft;
    public Transform backgroundCenter;
    public Transform backgroundRight;

    [Header("Ajuste Manual")]
    public float width = 20f; // El ancho de tus imágenes/contenedores

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 1. Movimiento Parallax (Visual)
        float distX = cam.transform.position.x * parallaxFactor;
        float distY = cam.transform.position.y * parallaxFactor;

        transform.position = new Vector3(startPos.x + distX, startPos.y + distY, transform.position.z);

        // 2. Reciclaje basado en la posición REAL de la cámara en el mundo
        float camX = cam.transform.position.x;

        // Si la cámara pasa el centro del grupo hacia la derecha
        if (camX > backgroundCenter.position.x + (width / 2))
        {
            // Movemos el bloque de la izquierda a la derecha
            backgroundLeft.position = new Vector3(backgroundRight.position.x + width, backgroundLeft.position.y, backgroundLeft.position.z);

            // Rotamos las referencias en el script
            Transform temp = backgroundLeft;
            backgroundLeft = backgroundCenter;
            backgroundCenter = backgroundRight;
            backgroundRight = temp;
        }
        // Si la cámara pasa el centro del grupo hacia la izquierda
        else if (camX < backgroundCenter.position.x - (width / 2))
        {
            // Movemos el bloque de la derecha a la izquierda
            backgroundRight.position = new Vector3(backgroundLeft.position.x - width, backgroundRight.position.y, backgroundRight.position.z);

            // Rotamos las referencias en el script
            Transform temp = backgroundRight;
            backgroundRight = backgroundCenter;
            backgroundCenter = backgroundLeft;
            backgroundLeft = temp;
        }
    }
}