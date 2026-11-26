using UnityEngine;

[RequireComponent(typeof(UnityEngine.Rendering.Universal.Light2D))]
public class LightIntensityController : MonoBehaviour
{
    [SerializeField] float minIntensity = 0.0f;
    [SerializeField] float maxIntensity = 0.0f;
    [SerializeField] float speed = 1.0f;

    private UnityEngine.Rendering.Universal.Light2D lightSource;
    private float initialIntensity;

    void Start()
    {
        lightSource = GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        initialIntensity = lightSource.intensity;
    }

    // ОПТИМИЗАЦИЯ: Убрали корутину с while(true) и Random.Range каждый кадр
    void Update()
    {
        // Используем Mathf.Sin для плавного мигания. Это быстрее, чем Random и корутины.
        float t = (Mathf.Sin(Time.time * speed) + 1.0f) / 2.0f;
        lightSource.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
    }
}