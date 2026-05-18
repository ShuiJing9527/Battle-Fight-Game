using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player2PrototypeController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5f;
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("Q - 神临光剑")]
    public float qDelay = 0.35f;
    public float qSwordSpeed = 14f;

    [Header("W - 圣轮偏转")]
    public float wDuration = 1.5f;
    public float wDamageReduction = 0.4f;
    public int maxStandbySwords = 3;

    [Header("E - 天轨换位")]
    public float eRailDuration = 0.6f;

    [Header("R - 万剑神罚")]
    public int swordEnergy = 4;

    [Header("Refs")]
    public Rigidbody rb;

    private Vector3 lastMoveDir = Vector3.forward;
    private int standbySwords;
    private bool isDashing;
    private bool isShielding;

    private readonly List<GameObject> standbySwordVisuals = new List<GameObject>();

    private Material bodyMat;

    private Sprite qSprite;
    private Sprite wSprite;
    private Sprite eSprite;
    private Sprite rSprite;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        bodyMat = CreateLitMaterial(new Color(0.25f, 0.8f, 1f));
        ApplyBodyMaterial();
        BuildSkillSprites();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame) CastQ();
        if (Keyboard.current.wKey.wasPressedThisFrame) CastW();
        if (Keyboard.current.eKey.wasPressedThisFrame) CastE();
        if (Keyboard.current.rKey.wasPressedThisFrame) CastR();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        Vector2 input = ReadMoveInput();
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            lastMoveDir = moveDir.normalized;
            transform.forward = lastMoveDir;
        }

        // Use direct position move to avoid rigidbody sleep/constraint side effects after runtime toggling.
        Vector3 delta = new Vector3(moveDir.x, 0f, moveDir.z) * moveSpeed * Time.fixedDeltaTime;
        transform.position += delta;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        if (Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    private void CastQ()
    {
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.right * 0.8f;
        Vector3 qDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : lastMoveDir;
        GameObject sword = SpawnSkillSprite("Q_Sword", qSprite, spawnPos, 0.9f);
        StartCoroutine(FireAfterDelay(sword, qDirection, qDelay, qSwordSpeed));
        swordEnergy += 1;
    }

    private void CastW()
    {
        if (!isShielding) StartCoroutine(ShieldRoutine());
        AddStandbySword();
    }

    private void CastE()
    {
        if (!isDashing) StartCoroutine(DashRoutine());
        Vector3 eDirection = ResolveFacingDirection();
        LaunchStandbySwords(eDirection, 18f);
    }

    private void CastR()
    {
        if (swordEnergy <= 0) return;

        int count = swordEnergy;
        swordEnergy = 0;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
            GameObject sword = SpawnSkillSprite("R_Sword", rSprite, spawnPos, 0.85f);
            StartCoroutine(FireAfterDelay(sword, dir, 0.05f * i, 16f));
        }
    }

    private IEnumerator FireAfterDelay(GameObject sword, Vector3 dir, float delay, float speed)
    {
        float t = 0f;
        while (t < delay)
        {
            if (sword == null) yield break;
            t += Time.deltaTime;
            sword.transform.Rotate(Vector3.forward, 360f * Time.deltaTime, Space.Self);
            FaceCamera(sword);
            yield return null;
        }

        float life = 2.2f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            if (sword == null) yield break;
            sword.transform.position += dir.normalized * speed * Time.deltaTime;
            FaceCamera(sword);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sword != null) Destroy(sword);
    }

    private IEnumerator ShieldRoutine()
    {
        isShielding = true;
        GameObject shield = SpawnSkillSprite("W_Shield", wSprite, transform.position + Vector3.up * 1.1f, 2.1f);

        float t = 0f;
        while (t < wDuration)
        {
            if (shield != null)
            {
                shield.transform.position = transform.position + Vector3.up * 1.1f;
                shield.transform.Rotate(Vector3.forward, 220f * Time.deltaTime, Space.Self);
                float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.08f;
                shield.transform.localScale = Vector3.one * (2.1f * pulse);
                FaceCamera(shield);
            }
            t += Time.deltaTime;
            yield return null;
        }

        if (shield != null) Destroy(shield);
        isShielding = false;
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        Vector3 dir = ResolveFacingDirection();
        Vector3 start = transform.position;
        Vector3 end = start + dir * dashDistance;

        float t = 0f;
        while (t < dashDuration)
        {
            float p = Mathf.Clamp01(t / dashDuration);
            transform.position = Vector3.Lerp(start, end, p);

            if (Random.value < 0.45f)
            {
                Vector3 trailPos = transform.position + Vector3.up * 0.5f;
                GameObject trail = SpawnSkillSprite("E_Rail", eSprite, trailPos, 0.7f);
                StartCoroutine(FadeAndDestroy(trail, eRailDuration));
            }

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        isDashing = false;
    }

    private IEnumerator FadeAndDestroy(GameObject go, float duration)
    {
        if (go == null) yield break;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.white;

        float t = 0f;
        while (t < duration)
        {
            if (go == null) yield break;
            float a = 1f - (t / duration);
            if (sr != null)
            {
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            }
            FaceCamera(go);
            t += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private void AddStandbySword()
    {
        if (standbySwords >= maxStandbySwords) return;

        standbySwords += 1;
        Vector3 offset = Quaternion.Euler(0f, standbySwords * 360f / maxStandbySwords, 0f) * Vector3.forward * 1.1f;
        GameObject standby = SpawnSkillSprite("StandbySword", qSprite, transform.position + Vector3.up + offset, 0.7f);
        standbySwordVisuals.Add(standby);
        StartCoroutine(OrbitStandbySword(standby, standbySwords - 1));
    }

    private IEnumerator OrbitStandbySword(GameObject standby, int index)
    {
        while (standby != null && standbySwords > 0)
        {
            float angle = Time.time * 120f + index * 120f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.1f;
            standby.transform.position = transform.position + Vector3.up + offset;
            FaceCamera(standby);
            yield return null;
        }
    }

    private void LaunchStandbySwords(Vector3 dir, float speed)
    {
        foreach (GameObject standby in standbySwordVisuals)
        {
            if (standby == null) continue;
            StartCoroutine(FireAfterDelay(standby, dir, 0f, speed));
        }

        standbySwordVisuals.Clear();
        standbySwords = 0;
    }

    private Vector3 ResolveFacingDirection()
    {
        if (transform.forward.sqrMagnitude > 0.0001f) return transform.forward.normalized;
        if (lastMoveDir.sqrMagnitude > 0.0001f) return lastMoveDir.normalized;
        return Vector3.forward;
    }

    private GameObject SpawnSkillSprite(string name, Sprite sprite, Vector3 pos, float scale)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 100;
        sr.color = Color.white;
        FaceCamera(go);
        return go;
    }

    private void FaceCamera(GameObject go)
    {
        if (go == null || Camera.main == null) return;
        Vector3 lookDir = go.transform.position - Camera.main.transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            go.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }
    }

    private void ApplyBodyMaterial()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) r.material = bodyMat;
    }

    private Material CreateLitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    private void BuildSkillSprites()
    {
        qSprite = CreateSprite(128, (u, v) =>
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float blade = Mathf.SmoothStep(0.08f, 0f, Mathf.Abs(x)) * Mathf.SmoothStep(1f, 0.25f, Mathf.Abs(y));
            float tip = Mathf.SmoothStep(0.22f, 0f, Mathf.Abs(x)) * Mathf.SmoothStep(1f, 0.65f, y);
            float glow = Mathf.Exp(-(x * x + y * y) * 4f) * 0.25f;
            float a = Mathf.Clamp01(blade + tip + glow);
            return new Color(1f, 0.93f, 0.35f, a);
        });

        wSprite = CreateSprite(128, (u, v) =>
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float ring = Mathf.SmoothStep(0.95f, 0.75f, r) * (1f - Mathf.SmoothStep(0.65f, 0.5f, r));
            float core = Mathf.Exp(-r * r * 9f) * 0.4f;
            float a = Mathf.Clamp01(ring + core);
            return new Color(0.45f, 1f, 0.95f, a);
        });

        eSprite = CreateSprite(128, (u, v) =>
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float streak = Mathf.Exp(-x * x * 18f) * Mathf.SmoothStep(1f, 0.2f, Mathf.Abs(y));
            float tail = Mathf.Exp(-((x + 0.45f) * (x + 0.45f) * 8f + y * y * 18f)) * 0.8f;
            float a = Mathf.Clamp01(streak + tail);
            return new Color(1f, 0.65f, 0.15f, a);
        });

        rSprite = CreateSprite(128, (u, v) =>
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float burst = Mathf.Exp(-r * r * 6f);
            float rays = Mathf.Abs(Mathf.Sin(Mathf.Atan2(y, x) * 6f)) * Mathf.SmoothStep(1f, 0.2f, r);
            float a = Mathf.Clamp01(burst * 0.7f + rays * 0.55f);
            return new Color(1f, 0.35f, 0.22f, a);
        });
    }

    private Sprite CreateSprite(int size, System.Func<float, float, Color> painter)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                pixels[y * size + x] = painter(u, v);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
