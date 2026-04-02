using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Ajustes de Movimiento")]
    public float forwardSpeed = 5f;
    [HideInInspector] public float currentSpeed; // Para que la cámara lea la velocidad real

    [Header("Física de Capa Progresiva")]
    public float stopFallDamping = 8f;   // Qué tan rápido frena la caída
    public float liftDuration = 0.2f;    // Cuánto dura el impulso progresivo hacia arriba
    public float baseJumpForce = 5f;     // Fuerza mínima de salto si no hay inercia
    public float fallInertiaFactor = 1.2f; // Factor para calcular el impulso final según la caída

    [Header("Impulso Inicial")]
    public float initialBurstForce = 15f; // Fuerza con la que saldrá disparado al iniciar el juego

    [Header("Física de Rebote en Suelo")]
    [Range(0f, 1f)]
    public float groundBounceFactor = 0.7f; // Cuánta fuerza de caída conserva al rebotar (0.7 = 70%)

    private bool isPlaying = false;
    private bool isDead = false;
    private bool canUseBlower = true; // Candado para bloquear el soplahojas al tocar el suelo
    private bool isExecutingGlide = false; // Candado para evitar spam
    private bool isTouchingFloor = false;

    [Header("UI Elements")]
    public GameObject startButton; // <--- ¡AQUÍ ESTÁ DE VUELTA!
    public GameObject retryButton;
    public GameObject gameOverText;

    private CameraController camController;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.simulated = false; // Sin físicas al inicio

        // Buscamos la cámara principal para mandarle la señal del impacto
        camController = Camera.main.GetComponent<CameraController>();

        currentSpeed = forwardSpeed;
    }

    void Update()
    {
        // Calculamos la velocidad real combinada en X e Y para el zoom de la cámara
        currentSpeed = rb.linearVelocity.magnitude;

        if (isPlaying && !isDead)
        {
            // Solo puede usar el propulsor si NO ha tocado el suelo y está cayendo
            if (canUseBlower && rb.linearVelocity.y < -0.1f && !isExecutingGlide)
            {
                if ((Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) ||
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    StartCoroutine(ExecuteGlideRoutine());
                }
            }

            // GAME OVER REAL: Cuando se queda quieto en el suelo después de perder el vuelo
            if (!canUseBlower && isTouchingFloor && rb.linearVelocity.magnitude < 0.5f)
            {
                Die();
            }
        }
    }

    void FixedUpdate()
    {
        if (isPlaying && !isDead)
        {
            // SI PUEDE USAR EL SOPLAHOJAS: Mantiene el avance constante (está volando)
            if (canUseBlower)
            {
                if (rb.linearVelocity.x < forwardSpeed)
                {
                    rb.linearVelocity = new Vector2(forwardSpeed, rb.linearVelocity.y);
                }
            }
            // SI YA TOCÓ EL SUELO: No forzamos velocidad. 
            // Las físicas de Unity y la fricción harán que se arrastre y se detenga solo.
            else
            {
                // Solo nos aseguramos de que no vaya hacia ATRÁS por algún rebote raro
                if (rb.linearVelocity.x < 0f)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
        }
    }

    // Corrutina que replica la parábola en "U" de Mario World
    private System.Collections.IEnumerator ExecuteGlideRoutine()
    {
        isExecutingGlide = true;
        float currentYVelocity = rb.linearVelocity.y;

        // 1. Calcular la fuerza del salto basada en la caída
        float downwardInertia = Mathf.Abs(currentYVelocity);
        float finalJumpForce = baseJumpForce + (downwardInertia * fallInertiaFactor);

        // 2. PARTE 1 DE LA "U": Frenado progresivo
        float dampingTimer = 0f;
        while (dampingTimer < 0.15f)
        {
            float newY = Mathf.Lerp(rb.linearVelocity.y, 0f, Time.deltaTime * stopFallDamping);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);

            dampingTimer += Time.deltaTime;
            yield return null;
        }

        // 3. PARTE 2 DE LA "U": Impulso suave y progresivo hacia arriba
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

        float currentLiftTimer = 0f;
        while (currentLiftTimer < liftDuration)
        {
            rb.AddForce(Vector2.up * (finalJumpForce / liftDuration) * Time.deltaTime, ForceMode2D.Force);

            currentLiftTimer += Time.deltaTime;
            yield return null;
        }

        isExecutingGlide = false;
    }

    public void StartGame()
    {
        startButton.SetActive(false); // <--- Apagamos el botón al iniciar
        isPlaying = true;
        rb.simulated = true;

        // Impulso inicial diagonal del cañonazo
        rb.AddForce(new Vector2(forwardSpeed, initialBurstForce), ForceMode2D.Impulse);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isTouchingFloor = true;

            // Bloquear el soplahojas para siempre en esta partida
            canUseBlower = false;

            // Activar efectos de cámara lenta y zoom si caía con suficiente fuerza
            if (rb.linearVelocity.y < -3f && camController != null)
            {
                camController.TriggerImpactEffect();
            }

            // REBOTE POR INERCIA: Conserva un porcentaje de la velocidad de caída y la manda hacia arriba
            float bounceForce = Mathf.Abs(rb.linearVelocity.y) * groundBounceFactor;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceForce);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isTouchingFloor = false;
        }
    }

    private void Die()
    {
        isDead = true;
        isPlaying = false;

        // Frenado total
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;

        gameOverText.SetActive(true);
        retryButton.SetActive(true);
    }

    public void RetryGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}