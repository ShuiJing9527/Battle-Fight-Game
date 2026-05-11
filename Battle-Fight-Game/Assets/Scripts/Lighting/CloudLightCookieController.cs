using UnityEngine;

namespace UnderTheStars.Lighting
{
    [DisallowMultipleComponent]
    public class CloudLightCookieController : MonoBehaviour
    {
        [Header("Target Light")]
        [SerializeField] private Light targetDirectionalLight;

        [Header("Cookie")]
        [SerializeField] private int cookieResolution = 512;
        [SerializeField] private float cookieSize = 30f;
        [SerializeField] private int blurRadius = 10;
        [SerializeField] private float penumbraWidth = 0.35f;
        [SerializeField, Range(0f, 1f)] private float minLight = 0.75f;
        [SerializeField] private float shadowStrength = 0.35f;
        [SerializeField] private float brightness = 0.95f;
        [SerializeField] private float contrast = 0.18f;
        [SerializeField] private float edgeSoftness = 0.55f;

        [Header("Noise")]
        [SerializeField] private float bigNoiseScale = 6f;
        [SerializeField] private float detailNoiseScale = 28f;
        [SerializeField] private float detailStrength = 0.25f;

        [Header("Motion")]
        [SerializeField] private Vector2 cloudSpeed = new Vector2(0.005f, 0.012f);
        [SerializeField] private Vector2 detailSpeed = new Vector2(0.002f, 0.004f);

        [Header("Performance")]
        [SerializeField] private float updateInterval = 0.08f;

        private Texture2D cookieTexture;
        private Color[] cookiePixels;
        private float[] rawMask;
        private float[] blurTemp;
        private float[] blurMask;
        private float nextUpdateTime;

        private void Awake()
        {
            EnsureTargetLight();
            EnsureCookieTexture();
            UpdateCookie(force: true);
        }

        private void OnEnable()
        {
            EnsureTargetLight();
            EnsureCookieTexture();
            UpdateCookie(force: true);
        }

        private void OnValidate()
        {
            EnsureTargetLight();
            if (targetDirectionalLight != null)
            {
                targetDirectionalLight.cookieSize = Mathf.Max(0.01f, cookieSize);
            }
        }

        private void Update()
        {
            if (targetDirectionalLight == null)
            {
                EnsureTargetLight();
                if (targetDirectionalLight == null)
                {
                    return;
                }
            }

            if (Time.time >= nextUpdateTime)
            {
                UpdateCookie(force: false);
                nextUpdateTime = Time.time + Mathf.Max(0.01f, updateInterval);
            }
        }

        private void OnDisable()
        {
            ReleaseCookieTexture();
        }

        private void EnsureTargetLight()
        {
            if (targetDirectionalLight != null)
            {
                return;
            }

            Light selfLight = GetComponent<Light>();
            if (selfLight != null && selfLight.type == LightType.Directional)
            {
                targetDirectionalLight = selfLight;
                return;
            }

            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            {
                targetDirectionalLight = RenderSettings.sun;
            }
        }

        private void EnsureCookieTexture()
        {
            int res = Mathf.Clamp(cookieResolution, 64, 2048);
            if (cookieTexture != null && cookieTexture.width == res && cookieTexture.height == res)
            {
                return;
            }

            ReleaseCookieTexture();

            cookieTexture = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
                name = "CloudLightCookieRuntime"
            };
            cookiePixels = new Color[res * res];
            rawMask = new float[res * res];
            blurTemp = new float[res * res];
            blurMask = new float[res * res];
        }

        private void UpdateCookie(bool force)
        {
            EnsureCookieTexture();
            if (cookieTexture == null)
            {
                return;
            }

            int res = cookieTexture.width;
            float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            Vector2 bigOffset = cloudSpeed * t;
            Vector2 detailOffset = detailSpeed * t;

            float safeBigScale = Mathf.Max(0.001f, bigNoiseScale);
            float safeDetailScale = Mathf.Max(0.001f, detailNoiseScale);
            float safeShadow = Mathf.Clamp01(shadowStrength);
            float safeBrightness = Mathf.Clamp01(brightness);
            float safeContrast = Mathf.Clamp01(contrast);
            float safeDetailStrength = Mathf.Clamp01(detailStrength);
            float safeSoftness = Mathf.Clamp01(edgeSoftness);
            float safeMinLight = Mathf.Clamp01(minLight);
            int safeBlurRadius = Mathf.Clamp(blurRadius, 0, 64);
            float safePenumbra = Mathf.Clamp01(penumbraWidth);
            float edgeCenter = Mathf.Lerp(0.45f, 0.7f, safeSoftness);
            float edgeHalfWidth = Mathf.Lerp(0.01f, 0.45f, safePenumbra);
            float edge1 = Mathf.Clamp01(edgeCenter - edgeHalfWidth);
            float edge2 = Mathf.Clamp01(edgeCenter + edgeHalfWidth);
            if (edge2 <= edge1 + 1e-4f)
            {
                edge2 = Mathf.Min(1f, edge1 + 1e-4f);
            }

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1);
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    Vector2 uv = new Vector2(u, v);

                    float big = Mathf.PerlinNoise(uv.x * safeBigScale + bigOffset.x, uv.y * safeBigScale + bigOffset.y);
                    float detail = Mathf.PerlinNoise(uv.x * safeDetailScale + detailOffset.x, uv.y * safeDetailScale + detailOffset.y) * 2f - 1f;

                    float cloud = Mathf.Clamp01(big + detail * safeDetailStrength);
                    cloud = Mathf.Clamp01((cloud - 0.5f) * (1f + safeContrast * 2f) + 0.5f);
                    rawMask[y * res + x] = Mathf.SmoothStep(edge1, edge2, cloud);
                }
            }

            BlurMaskHorizontal(rawMask, blurTemp, res, safeBlurRadius);
            BlurMaskVertical(blurTemp, blurMask, res, safeBlurRadius);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = y * res + x;
                    float shadowMask = Mathf.Clamp01(blurMask[idx]);
                    float lightValue = Mathf.Lerp(1f, safeMinLight, shadowMask * safeShadow);
                    lightValue = Mathf.Lerp(lightValue, 1f, safeBrightness);
                    lightValue = Mathf.Clamp(lightValue, safeMinLight, 1f);
                    cookiePixels[idx] = new Color(lightValue, lightValue, lightValue, 1f);
                }
            }

            cookieTexture.SetPixels(cookiePixels);
            cookieTexture.Apply(false, false);

            if (targetDirectionalLight != null)
            {
                targetDirectionalLight.cookieSize = Mathf.Max(0.01f, cookieSize);
                targetDirectionalLight.cookie = cookieTexture;
            }
        }

        private void ReleaseCookieTexture()
        {
            if (cookieTexture == null)
            {
                return;
            }

            if (targetDirectionalLight != null && targetDirectionalLight.cookie == cookieTexture)
            {
                targetDirectionalLight.cookie = null;
            }

            if (Application.isPlaying)
            {
                Destroy(cookieTexture);
            }
            else
            {
                DestroyImmediate(cookieTexture);
            }

            cookieTexture = null;
            cookiePixels = null;
            rawMask = null;
            blurTemp = null;
            blurMask = null;
        }

        private static void BlurMaskHorizontal(float[] src, float[] dst, int resolution, int radius)
        {
            if (radius <= 0)
            {
                System.Array.Copy(src, dst, src.Length);
                return;
            }

            for (int y = 0; y < resolution; y++)
            {
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = (x + k + resolution) % resolution;
                        sum += src[row + sx];
                        count++;
                    }
                    dst[row + x] = sum / count;
                }
            }
        }

        private static void BlurMaskVertical(float[] src, float[] dst, int resolution, int radius)
        {
            if (radius <= 0)
            {
                System.Array.Copy(src, dst, src.Length);
                return;
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = (y + k + resolution) % resolution;
                        sum += src[sy * resolution + x];
                        count++;
                    }
                    dst[y * resolution + x] = sum / count;
                }
            }
        }
    }
}
