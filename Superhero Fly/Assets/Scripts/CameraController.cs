using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform player;
    private PlayerController playerScript;
    private Camera cam;

    [Header("Seguimiento")]
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(3, 1, -10);

    [Header("Zoom por Velocidad")]
    public float minSize = 5f; // Zoom normal
    public float maxSize = 8f; // Zoom out a máxima velocidad
    public float speedThreshold = 15f; // A qué velocidad se alcanza el zoom out máximo
    public float zoomSpeed = 2f;

    [Header("Efecto de Impacto")]
    public float impactZoom = 3.5f; // Zoom in al golpear
    private bool isImpactEffect = false;
    private float targetZoom;

    void Start()
    {
        cam = GetComponent<Camera>();
        playerScript = player.GetComponent<PlayerController>();
        targetZoom = minSize;
    }

    void LateUpdate()
    {
        // 1. Movimiento fluido de la cámara
        Vector3 targetPosition = player.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // 2. Control del Zoom
        if (!isImpactEffect)
        {
            // Calcula el zoom basado en la velocidad del jugador
            float speedPercent = Mathf.InverseLerp(playerScript.forwardSpeed, speedThreshold, playerScript.currentSpeed);
            targetZoom = Mathf.Lerp(minSize, maxSize, speedPercent);
        }

        // Aplicar el zoom suavemente
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSpeed * Time.deltaTime);
    }

    // Animación de impacto que llamaremos desde el jugador o enemigos
    public void TriggerImpactEffect()
    {
        if (!isImpactEffect)
        {
            StartCoroutine(ImpactRoutine());
        }
    }

    private System.Collections.IEnumerator ImpactRoutine()
    {
        isImpactEffect = true;
        targetZoom = impactZoom; // Zoom in agresivo
        Time.timeScale = 0.3f; // Cámara lenta al 30%

        // Espera 0.3 segundos de tiempo REAL (en cámara lenta serían casi 1 seg)
        yield return new WaitForSecondsRealtime(0.3f);

        Time.timeScale = 1f; // Regresa el tiempo a la normalidad
        isImpactEffect = false; // Regresa el zoom a la normalidad según la velocidad
    }
}