using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private CameraController camController;

    [Header("Movimiento")]
    public float forwardSpeed = 5f;
    [HideInInspector] public float currentSpeed;

    [Header("Planeo")]
    public float glideFallSpeed = -1.2f;
    public float glideTransitionSpeed = 2f;

    [Header("Física de la U")]
    public float stopFallDamping = 8f;     // Qué tan rápido frena al fondo de la U
    public float liftDuration = 0.25f;     // Duración del impulso hacia arriba
    public float baseJumpForce = 5f;
    public float fallInertiaFactor = 1.2f;

    [Header("Castigo por spam / picada corta")]
    public float minDiveDistance = 1.5f;   // Distancia mínima para activar la U completa
    public float diveCooldown = 0.4f;      // Cooldown entre usos

    [Header("Lanzamiento")]
    public float initialBurstForce = 15f;

    [Header("Rebote en suelo")]
    [Range(0f, 1f)] public float groundBounceFactor = 0.7f;

    [Header("Desaceleración en suelo")]
    public float groundDrag = 18f;

    [Header("UI")]
    public GameObject startButton;
    public GameObject retryButton;
    public GameObject gameOverText;

    private bool isPlaying, isDead, canUseBlower, isTouchingFloor;
    private bool isExecutingGlide = false;
    private float cooldownTimer = 0f;

    // Para medir la picada
    private bool isDiving = false;
    private float diveStartY = 0f;
    private float diveDistance = 0f;

    private HUDController hudController;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.simulated = false;
        camController = Camera.main.GetComponent<CameraController>();
        canUseBlower = true;

        hudController = FindObjectOfType<HUDController>();
    }

    void Update()
    {
        currentSpeed = rb.linearVelocity.magnitude;
        if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

        if (!isPlaying || isDead) return; // ← ya no incluye !canUseBlower

        if (canUseBlower)
        {
            TrackDive();
            CheckGlideInput();
        }

        // Ahora este chequeo siempre se ejecuta mientras isPlaying && !isDead
        if (!canUseBlower && isTouchingFloor && rb.linearVelocity.magnitude < 0.5f)
            Die();
    }

    void FixedUpdate()
    {
        if (!isPlaying || isDead) return;

        if (canUseBlower)
        {
            // Velocidad horizontal mínima solo mientras vuela
            if (rb.linearVelocity.x < forwardSpeed)
                rb.linearVelocity = new Vector2(forwardSpeed, rb.linearVelocity.y);

            // Planeo suave cuando no hay corrutina activa ni picada
            if (!isExecutingGlide && !isDiving)
            {
                rb.gravityScale = 0;
                float smoothY = Mathf.Lerp(
                    rb.linearVelocity.y, glideFallSpeed,
                    glideTransitionSpeed * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, smoothY);
            }
        }
        else if (isTouchingFloor)
        {
            // Freno progresivo solo cuando está en suelo y sin propulsor
            // No interfiere con la corrutina porque canUseBlower ya es false
            float newX = Mathf.MoveTowards(
                rb.linearVelocity.x, 0f, groundDrag * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    // Detecta si el jugador está en picada (presionando) y mide distancia
    private void TrackDive()
    {
        bool pressing =
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);

        if (pressing && !isExecutingGlide)
        {
            if (!isDiving)
            {
                isDiving = true;
                diveStartY = rb.position.y;
                diveDistance = 0f;
                rb.gravityScale = 4f; // picada
            }
            diveDistance = Mathf.Abs(rb.position.y - diveStartY);
        }
        else if (!pressing && isDiving)
        {
            isDiving = false;
        }
    }

    private void CheckGlideInput()
    {
        bool released =
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame);

        if (!released || isExecutingGlide || cooldownTimer > 0) return;

        cooldownTimer = diveCooldown;
        StartCoroutine(ExecuteGlideRoutine());
    }

    private System.Collections.IEnumerator ExecuteGlideRoutine()
    {
        isExecutingGlide = true;
        isDiving = false;

        float energy = Mathf.Clamp01(diveDistance / minDiveDistance);
        float currentYVelocity = rb.linearVelocity.y;

        if (diveDistance < minDiveDistance)
        {
            // Picada corta: mini rebote que sigue perdiendo altura
            float miniForce = baseJumpForce * energy * 0.4f;

            float t = 0f;
            while (t < 0.1f)
            {
                rb.gravityScale = 0;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                    Mathf.Lerp(rb.linearVelocity.y, miniForce, Time.deltaTime * 10f));
                t += Time.deltaTime;
                yield return null;
            }

            // Suelta al planeo normal — ya baja solo
            rb.gravityScale = 1f;
            isExecutingGlide = false;
            yield break;
        }

        // --- U COMPLETA (igual que el código viejo que funcionaba) ---

        // Calcular fuerza según inercia de caída
        float downwardInertia = Mathf.Abs(currentYVelocity);
        float finalJumpForce = baseJumpForce + (downwardInertia * fallInertiaFactor);

        // FASE 1: Frenado progresivo (fondo de la U)
        float dampingTimer = 0f;
        while (dampingTimer < 0.15f)
        {
            rb.gravityScale = 0;
            float newY = Mathf.Lerp(rb.linearVelocity.y, 0f, Time.deltaTime * stopFallDamping);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
            dampingTimer += Time.deltaTime;
            yield return null;
        }

        // FASE 2: Impulso suave hacia arriba (subida de la U)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.gravityScale = 0;

        float liftTimer = 0f;
        while (liftTimer < liftDuration)
        {
            rb.AddForce(Vector2.up * (finalJumpForce / liftDuration) * Time.deltaTime,
                        ForceMode2D.Force);
            liftTimer += Time.deltaTime;
            yield return null;
        }

        // Al terminar, vuelve al planeo (FixedUpdate lo toma desde acá)
        rb.gravityScale = 0;
        isExecutingGlide = false;
    }

    public void StartGame()
    {
        startButton.SetActive(false);
        isPlaying = true;
        canUseBlower = true;
        rb.simulated = true;
        rb.AddForce(new Vector2(forwardSpeed, initialBurstForce), ForceMode2D.Impulse);

        hudController?.OnGameStart(rb.position.x);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Floor")) return;

        isTouchingFloor = true;
        canUseBlower = false;
        isExecutingGlide = false;
        isDiving = false;
        StopAllCoroutines();

        rb.gravityScale = 2f;

        if (rb.linearVelocity.y < -3f && camController != null)
            camController.TriggerImpactEffect();

        // Cada rebote conserva menos energía — efecto de Burrito Bison
        float bounce = Mathf.Abs(rb.linearVelocity.y) * groundBounceFactor;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounce);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Floor")) isTouchingFloor = false;
    }

    private void Die()
    {
        isDead = true;
        isPlaying = false;
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;
        gameOverText.SetActive(true);
        retryButton.SetActive(true);

        hudController?.OnGameOver();
    }

    public void RetryGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}