using UnityEngine;
using TMPro;
using System.Collections;

public class HUDController : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerController player;
    public Rigidbody2D playerRb;

    [Header("HUD — juego activo")]
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI speedText;

    [Header("Pantalla de inicio")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI instructionText;

    [Header("Pantalla de Game Over")]
    public TextMeshProUGUI finalDistanceText;
    public TextMeshProUGUI recordText;
    public TextMeshProUGUI previousRecordText;

    [Header("Conversión de unidades")]
    [Tooltip("Multiplicador unidades Unity → km/h. Ajustá según escala de tu juego.")]
    public float unitsToKmH = 13f;

    [Header("Suavizado de velocidad")]
    public float smoothSpeed = 8f;

    [Header("Animación de viaje (distancia → game over)")]
    public float travelDuration = 0.7f;
    public AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float scaleOnArrival = 1.4f;
    public float scaleDuration = 0.2f;

    [Header("Fade")]
    public float fadeInDuration = 0.4f;

    // ── Estado interno ──────────────────────────────────────────
    private float startX;
    private float distanceMeters;
    private float displayedSpeed;
    private bool gameStarted;

    private Vector2 distanceOriginalPos;
    private Vector3 distanceOriginalScale;
    private float distanceOriginalFontSize;

    private const string RECORD_KEY = "BestDistance";

    // ── Unity ───────────────────────────────────────────────────
    void Start()
    {
        CacheDistanceDefaults();
        HideGameOverUI();
        ResetDistanceToOrigin();
        SetAlpha(distanceText, 0f);
        SetAlpha(speedText, 0f);
        ShowStartScreen();
    }

    void Update()
    {
        if (!gameStarted) return;
        UpdateDistance();
        UpdateSpeed();
    }

    // ── API pública ─────────────────────────────────────────────
    public void OnGameStart(float startPosX)
    {
        startX = startPosX;
        distanceMeters = 0f;
        displayedSpeed = 0f;
        gameStarted = true;

        SetActive(titleText, false);
        SetActive(instructionText, false);

        ResetDistanceToOrigin();
        StartCoroutine(FadeIn(distanceText));
        StartCoroutine(FadeIn(speedText));
    }

    public void OnGameOver()
    {
        gameStarted = false;

        float previousRecord = PlayerPrefs.GetFloat(RECORD_KEY, 0f);
        bool isNewRecord = distanceMeters > previousRecord;

        if (isNewRecord)
        {
            PlayerPrefs.SetFloat(RECORD_KEY, distanceMeters);
            PlayerPrefs.Save();
        }

        StartCoroutine(FadeOut(speedText));
        StartCoroutine(TravelDistanceText(isNewRecord, previousRecord));
    }

    // ── Pantalla de inicio ──────────────────────────────────────
    private void ShowStartScreen()
    {
        if (titleText != null)
            titleText.text = "FLYING BASTARD";

        if (instructionText != null)
        {
            float record = PlayerPrefs.GetFloat(RECORD_KEY, 0f);
            instructionText.text = record > 0f
                ? $"Récord: {FormatDistance(record)}\nPress start to play"
                : "Press start to play";
        }
    }

    // ── Update helpers ──────────────────────────────────────────
    private void UpdateDistance()
    {
        distanceMeters = Mathf.Max(0f, playerRb.position.x - startX);
        distanceText.text = FormatDistance(distanceMeters);
    }

    private void UpdateSpeed()
    {
        float target = playerRb.linearVelocity.magnitude * unitsToKmH;
        displayedSpeed = Mathf.Lerp(displayedSpeed, target, smoothSpeed * Time.deltaTime);
        speedText.text = $"{Mathf.RoundToInt(displayedSpeed)} km/h";
    }

    // ── Animación de viaje ──────────────────────────────────────
    private IEnumerator TravelDistanceText(bool isNewRecord, float previousRecord)
    {
        Vector2 fromPos = distanceText.rectTransform.anchoredPosition;
        Vector3 fromScale = distanceText.rectTransform.localScale;
        float fromFontSize = distanceText.fontSize;

        Vector2 toPos = finalDistanceText != null
                             ? finalDistanceText.rectTransform.anchoredPosition
                             : fromPos;
        float toFontSize = finalDistanceText != null
                             ? finalDistanceText.fontSize
                             : fromFontSize;

        float elapsed = 0f;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float p = travelCurve.Evaluate(Mathf.Clamp01(elapsed / travelDuration));

            distanceText.rectTransform.anchoredPosition = Vector2.Lerp(fromPos, toPos, p);
            distanceText.fontSize = Mathf.Lerp(fromFontSize, toFontSize, p);

            yield return null;
        }

        // Valores exactos al llegar
        distanceText.rectTransform.anchoredPosition = toPos;
        distanceText.fontSize = toFontSize;

        // Punch de escala
        yield return StartCoroutine(PunchScale(
            distanceText.rectTransform, fromScale, scaleOnArrival, scaleDuration));

        // Textos secundarios con fade
        yield return new WaitForSeconds(0.05f);
        ShowGameOverSecondary(isNewRecord, previousRecord);
    }

    private void ShowGameOverSecondary(bool isNewRecord, float previousRecord)
    {
        if (recordText != null)
        {
            recordText.gameObject.SetActive(true);
            recordText.text = isNewRecord
                ? "¡Nuevo récord!"
                : $"Récord: {FormatDistance(PlayerPrefs.GetFloat(RECORD_KEY, 0f))}";
            recordText.color = isNewRecord
                ? new Color(0.11f, 0.62f, 0.46f, 1f)
                : new Color(1f, 1f, 1f, 0f);
            StartCoroutine(FadeIn(recordText));
        }

        if (!isNewRecord && previousRecordText != null)
        {
            previousRecordText.gameObject.SetActive(true);
            previousRecordText.text = $"anterior: {FormatDistance(previousRecord)}";
            SetAlpha(previousRecordText, 0f);
            StartCoroutine(FadeIn(previousRecordText));
        }
    }

    // ── Punch de escala ─────────────────────────────────────────
    private IEnumerator PunchScale(RectTransform rt, Vector3 baseScale,
                                   float multiplier, float duration)
    {
        float half = duration * 0.5f;
        Vector3 peak = baseScale * multiplier;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            rt.localScale = Vector3.Lerp(baseScale, peak, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            rt.localScale = Vector3.Lerp(peak, baseScale, t / half);
            yield return null;
        }

        rt.localScale = baseScale;
    }

    // ── Fade ────────────────────────────────────────────────────
    private IEnumerator FadeIn(TextMeshProUGUI tmp)
    {
        if (tmp == null) yield break;
        tmp.gameObject.SetActive(true);

        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetAlpha(tmp, Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetAlpha(tmp, 1f);
    }

    private IEnumerator FadeOut(TextMeshProUGUI tmp)
    {
        if (tmp == null) yield break;

        for (float t = fadeInDuration; t > 0f; t -= Time.deltaTime)
        {
            SetAlpha(tmp, Mathf.Clamp01(t / fadeInDuration));
            yield return null;
        }
        SetAlpha(tmp, 0f);
        tmp.gameObject.SetActive(false);
    }

    // ── Helpers ─────────────────────────────────────────────────
    private void CacheDistanceDefaults()
    {
        if (distanceText == null) return;
        distanceOriginalPos = distanceText.rectTransform.anchoredPosition;
        distanceOriginalScale = distanceText.rectTransform.localScale;
        distanceOriginalFontSize = distanceText.fontSize;
    }

    private void ResetDistanceToOrigin()
    {
        if (distanceText == null) return;
        distanceText.rectTransform.anchoredPosition = distanceOriginalPos;
        distanceText.rectTransform.localScale = distanceOriginalScale;
        distanceText.fontSize = distanceOriginalFontSize;
    }

    private void HideGameOverUI()
    {
        SetActive(finalDistanceText, false);
        SetActive(recordText, false);
        SetActive(previousRecordText, false);
    }

    private static void SetActive(TextMeshProUGUI tmp, bool active)
    {
        if (tmp != null) tmp.gameObject.SetActive(active);
    }

    private static void SetAlpha(TextMeshProUGUI tmp, float alpha)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = alpha;
        tmp.color = c;
    }

    private static string FormatDistance(float meters)
    {
        return meters < 1000f
            ? $"{Mathf.FloorToInt(meters)} m"
            : $"{meters / 1000f:F2} km";
    }
}