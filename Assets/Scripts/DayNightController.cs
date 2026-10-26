using System;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class DayNightController : MonoBehaviour
{
    public enum TimeMode
    {
        RealTimeClock,   // Mengikuti jam asli perangkat (Laptop/PC/HP)
        ManualSlider,    // Mengatur jam secara manual lewat slider 0 - 24
        SimulationLoop   // Jam berputar otomatis (misal 1 hari = 60 detik)
    }

    [Header("Mode Waktu")]
    [Tooltip("Pilih bagaimana jam diatur: Jam Asli (RealTime), Geser Manual (ManualSlider), atau Berputar Otomatis (SimulationLoop)")]
    public TimeMode mode = TimeMode.RealTimeClock;

    [Header("Pengaturan Jam")]
    [Range(0f, 24f)]
    [Tooltip("Waktu dalam format 24 jam (Contoh: 6.5 = jam 06:30, 10.0 = jam 10:00, 18.0 = jam 18:00)")]
    public float timeOfDay = 10f;

    [Tooltip("Kecepatan waktu berjalan saat mode SimulationLoop (contoh: 60 = 1 menit asli jadi 1 jam game)")]
    public float simulationSpeed = 60f;

    [Header("Informasi Jam Saat Ini")]
    [SerializeField]
    private string currentTimeDisplay = "10:00:00 (Pagi)";

    [Header("Arah Matahari (Azimuth)")]
    [Tooltip("Arah mata angin perputaran matahari (default -30 derajat sesuai orientasi scene Anda)")]
    public float sunAzimuth = -30f;

    [Header("Preset Warna Alami (Rekomendasi)")]
    [Tooltip("Jika dicentang, jam 07:30 s/d 16:30 otomatis menggunakan cahaya putih bersih alami (tidak oranye). Hapus centang jika ingin mengedit gradien manual.")]
    public bool useNaturalDaylightPreset = true;

    [Header("Warna Cahaya Kustom (Jika Preset tidak dicentang)")]
    public Gradient sunColorGradient;

    [Header("Intensitas Cahaya")]
    public AnimationCurve sunIntensityCurve;

    [Header("Pencahayaan Lingkungan (Ambient Light)")]
    [Tooltip("Apakah warna lingkungan di sekitar kontainer juga menyesuaikan waktu?")]
    public bool adjustAmbientLight = true;

    public Gradient ambientColorGradient;

    [Header("Pengatur Kecerahan Global (Anti-Silau / Anti-Glare)")]
    [Range(0.1f, 2.0f)]
    [Tooltip("Pengali intensitas matahari. Default 1.05f terang cerah natural.")]
    public float sunIntensityMultiplier = 1.05f;

    [Range(0.05f, 2.0f)]
    [Tooltip("Pengali ambient light (lingkungan). Default 0.85f terang merata dan membuat bayangan tidak gelap gulita.")]
    public float ambientIntensityMultiplier = 0.85f;

    [Range(20f, 85f)]
    [Tooltip("Sudut kemiringan matahari maksimal di siang hari (default 55 derajat). Mencegah cahaya tegak lurus 90 derajat yang menyilaukan lantai horizontal.")]
    public float maxSunElevationAngle = 55f;

    [Header("Deteksi Cahaya Ganda")]
    [Tooltip("Otomatis matikan Directional Light lain di scene jika tidak sengaja ada 2 matahari bertumpuk")]
    public bool autoDisableDuplicateLights = true;

    private Light directionalLight;

    void Awake()
    {
        directionalLight = GetComponent<Light>();
        InitializeDefaults();
    }

    void Reset()
    {
        directionalLight = GetComponent<Light>();
        ResetToNaturalDaylightPreset();
    }

    [ContextMenu("Reset to Natural Daylight Preset")]
    public void ResetToNaturalDaylightPreset()
    {
        useNaturalDaylightPreset = true;
        sunIntensityMultiplier = 1.05f;
        ambientIntensityMultiplier = 0.85f;
        maxSunElevationAngle = 55f;
        InitializeDefaults(true);
        ApplySunLighting();
    }

    /// <summary>
    /// Menyiapkan warna gradien dan kurva pencahayaan bawaan yang bersih dan realistis
    /// </summary>
    void InitializeDefaults(bool forceReset = false)
    {
        if (directionalLight == null) directionalLight = GetComponent<Light>();
        if (directionalLight != null)
        {
            directionalLight.type = LightType.Directional;
            directionalLight.shadows = LightShadows.Soft;
        }

        // 1. Setup Sun Color Gradient dengan transisi natural (orange hanya di sunrise 06:00 & sunset 18:00)
        // Jam 07:30 - 16:30 adalah putih bersih (natural daylight)
        if (forceReset || sunColorGradient == null || sunColorGradient.colorKeys.Length <= 1)
        {
            sunColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[8];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

            // 00:00 (0.0) -> Malam (Cahaya bulan kebiruan lembut)
            colorKeys[0] = new GradientColorKey(new Color(0.35f, 0.45f, 0.65f), 0.0f);
            // 05:30 (0.23) -> Subuh fajar (Biru keunguan tipis)
            colorKeys[1] = new GradientColorKey(new Color(0.60f, 0.65f, 0.75f), 0.23f);
            // 06:15 (0.26) -> Terbit matahari (Oranye keemasan lembut, bukan oranye neon)
            colorKeys[2] = new GradientColorKey(new Color(1.0f, 0.82f, 0.65f), 0.26f);
            // 07:30 (0.31) -> Pagi terang (Sudah putih bersih alami!)
            colorKeys[3] = new GradientColorKey(new Color(1.0f, 0.99f, 0.96f), 0.31f);
            // 12:00 (0.50) -> Tengah hari (Putih bersih murni)
            colorKeys[4] = new GradientColorKey(Color.white, 0.50f);
            // 16:30 (0.69) -> Sore (Masih putih bersih natural)
            colorKeys[5] = new GradientColorKey(new Color(1.0f, 0.98f, 0.93f), 0.69f);
            // 17:45 (0.74) -> Senja / Sunset (Jingga keemasan hangat)
            colorKeys[6] = new GradientColorKey(new Color(1.0f, 0.65f, 0.38f), 0.74f);
            // 20:00 (0.83) -> Malam (Biru bulan)
            colorKeys[7] = new GradientColorKey(new Color(0.35f, 0.45f, 0.65f), 0.83f);

            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            sunColorGradient.SetKeys(colorKeys, alphaKeys);
        }

        // 2. Setup Sun Intensity Curve (Terang alami siang hari)
        if (forceReset || sunIntensityCurve == null || sunIntensityCurve.length <= 1)
        {
            sunIntensityCurve = new AnimationCurve();
            sunIntensityCurve.AddKey(new Keyframe(0f, 0.10f));    // Jam 00:00 (Malam)
            sunIntensityCurve.AddKey(new Keyframe(5.5f, 0.15f));  // Jam 05:30 (Subuh)
            sunIntensityCurve.AddKey(new Keyframe(6.5f, 0.65f));  // Jam 06:30 (Terbit)
            sunIntensityCurve.AddKey(new Keyframe(8.0f, 0.95f));  // Jam 08:00 (Pagi)
            sunIntensityCurve.AddKey(new Keyframe(12.0f, 1.05f)); // Jam 12:00 (Siang terik cerah)
            sunIntensityCurve.AddKey(new Keyframe(16.5f, 0.95f)); // Jam 16:30 (Sore)
            sunIntensityCurve.AddKey(new Keyframe(18.0f, 0.50f)); // Jam 18:00 (Sunset)
            sunIntensityCurve.AddKey(new Keyframe(19.0f, 0.15f)); // Jam 19:00 (Malam)
            sunIntensityCurve.AddKey(new Keyframe(24.0f, 0.10f)); // Jam 24:00 (Malam)
        }

        // 3. Setup Ambient Light (Warna langit sekitar - terang, sejuk, tidak berkabut)
        if (forceReset || ambientColorGradient == null || ambientColorGradient.colorKeys.Length <= 1)
        {
            ambientColorGradient = new Gradient();
            GradientColorKey[] aKeys = new GradientColorKey[6];
            GradientAlphaKey[] aAlphas = new GradientAlphaKey[2];

            // Malam -> Biru redup netral
            aKeys[0] = new GradientColorKey(new Color(0.12f, 0.15f, 0.22f), 0.0f);
            // 06:00 -> Pagi fajar netral
            aKeys[1] = new GradientColorKey(new Color(0.25f, 0.28f, 0.32f), 0.25f);
            // 07:30 s/d 16:30 -> Langit siang cerah terang
            aKeys[2] = new GradientColorKey(new Color(0.42f, 0.44f, 0.48f), 0.31f);
            aKeys[3] = new GradientColorKey(new Color(0.42f, 0.44f, 0.48f), 0.69f);
            // 18:00 -> Senja lembut
            aKeys[4] = new GradientColorKey(new Color(0.28f, 0.26f, 0.30f), 0.75f);
            // 20:00 -> Malam
            aKeys[5] = new GradientColorKey(new Color(0.12f, 0.15f, 0.22f), 0.83f);

            aAlphas[0] = new GradientAlphaKey(1.0f, 0.0f);
            aAlphas[1] = new GradientAlphaKey(1.0f, 1.0f);

            ambientColorGradient.SetKeys(aKeys, aAlphas);
        }
    }

    void Update()
    {
        UpdateTime();
        ApplySunLighting();
    }

    void OnValidate()
    {
        if (directionalLight == null) directionalLight = GetComponent<Light>();
        ApplySunLighting();
    }

    void UpdateTime()
    {
        if (Application.isPlaying)
        {
            if (mode == TimeMode.RealTimeClock)
            {
                DateTime now = DateTime.Now;
                timeOfDay = now.Hour + (now.Minute / 60f) + (now.Second / 3600f);
            }
            else if (mode == TimeMode.SimulationLoop)
            {
                timeOfDay += (Time.deltaTime / 3600f) * simulationSpeed;
                if (timeOfDay >= 24f) timeOfDay -= 24f;
                if (timeOfDay < 0f) timeOfDay += 24f;
            }
        }
        else
        {
            if (mode == TimeMode.RealTimeClock)
            {
                DateTime now = DateTime.Now;
                timeOfDay = now.Hour + (now.Minute / 60f) + (now.Second / 3600f);
            }
        }

        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
        int seconds = Mathf.FloorToInt(((timeOfDay - hours) * 60f - minutes) * 60f);

        string waktuKeterangan = "Siang";
        if (hours >= 4 && hours < 6) waktuKeterangan = "Subuh";
        else if (hours >= 6 && hours < 11) waktuKeterangan = "Pagi";
        else if (hours >= 11 && hours < 15) waktuKeterangan = "Siang";
        else if (hours >= 15 && hours < 18) waktuKeterangan = "Sore";
        else if (hours >= 18 && hours < 19) waktuKeterangan = "Senja (Sunset)";
        else waktuKeterangan = "Malam";

        currentTimeDisplay = string.Format("{0:00}:{1:00}:{2:00} ({3})", hours, minutes, seconds, waktuKeterangan);
    }

    /// <summary>
    /// Menghitung warna matahari alami: bersih putih terang dari jam 07:30 s/d 16:30
    /// </summary>
    Color GetNaturalSunColor(float hour)
    {
        // 07:30 s/d 16:30 -> Putih Bersih Murni (Natural Crisp Sunlight)
        if (hour >= 7.5f && hour <= 16.5f)
        {
            return Color.white;
        }

        // 06:45 s/d 07:30 -> Transisi cepat dari pagi hangat ke putih bersih
        if (hour > 6.75f && hour < 7.5f)
        {
            float t = (hour - 6.75f) / 0.75f;
            return Color.Lerp(new Color(1.0f, 0.90f, 0.78f), Color.white, t);
        }

        // 05:45 s/d 06:45 -> Fajar / Terbit matahari (Oranye keemasan lembut)
        if (hour >= 5.75f && hour <= 6.75f)
        {
            float t = (hour - 5.75f) / 1.0f;
            return Color.Lerp(new Color(0.85f, 0.75f, 0.70f), new Color(1.0f, 0.90f, 0.78f), t);
        }

        // 16:30 s/d 17:30 -> Transisi sore ke senja keemasan
        if (hour > 16.5f && hour <= 17.5f)
        {
            float t = (hour - 16.5f) / 1.0f;
            return Color.Lerp(Color.white, new Color(1.0f, 0.82f, 0.62f), t);
        }

        // 17:30 s/d 18:30 -> Sunset senja jingga hangat
        if (hour > 17.5f && hour <= 18.5f)
        {
            float t = (hour - 17.5f) / 1.0f;
            return Color.Lerp(new Color(1.0f, 0.82f, 0.62f), new Color(1.0f, 0.55f, 0.32f), t);
        }

        // 18:30 s/d 19:30 -> Senja meredup menuju malam
        if (hour > 18.5f && hour <= 19.5f)
        {
            float t = (hour - 18.5f) / 1.0f;
            return Color.Lerp(new Color(1.0f, 0.55f, 0.32f), new Color(0.35f, 0.45f, 0.65f), t);
        }

        // Malam hari (19:30 s/d 05:45) -> Cahaya bulan biru redup
        return new Color(0.35f, 0.45f, 0.65f);
    }

    /// <summary>
    /// Menghitung warna ambient alami yang bersih & cerah di siang hari
    /// </summary>
    Color GetNaturalAmbientColor(float hour)
    {
        // 07:30 s/d 16:30 -> Ambience langit siang cerah terang alami
        if (hour >= 7.5f && hour <= 16.5f)
        {
            return new Color(0.42f, 0.44f, 0.48f);
        }

        // 06:00 s/d 07:30 -> Pagi
        if (hour >= 6.0f && hour < 7.5f)
        {
            float t = (hour - 6.0f) / 1.5f;
            return Color.Lerp(new Color(0.24f, 0.26f, 0.30f), new Color(0.42f, 0.44f, 0.48f), t);
        }

        // 16:30 s/d 18:30 -> Sore ke senja
        if (hour > 16.5f && hour <= 18.5f)
        {
            float t = (hour - 16.5f) / 2.0f;
            return Color.Lerp(new Color(0.42f, 0.44f, 0.48f), new Color(0.28f, 0.24f, 0.28f), t);
        }

        // Malam
        return new Color(0.12f, 0.15f, 0.22f);
    }

    void CheckDuplicateLights()
    {
        if (!autoDisableDuplicateLights) return;
        Light[] allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in allLights)
        {
            if (l != null && l != directionalLight && l.type == LightType.Directional && l.enabled)
            {
                Debug.LogWarning($"[DayNightController] Ditemukan Directional Light ganda '{l.name}'. Menonaktifkan agar tidak menumpuk cahaya dua kali lipat!");
                l.enabled = false;
            }
        }
    }

    void ApplySunLighting()
    {
        if (directionalLight == null) directionalLight = GetComponent<Light>();
        if (directionalLight == null) return;

        CheckDuplicateLights();

        float normalizedTime = Mathf.Clamp01(timeOfDay / 24f);

        // 1. Kalkulasi Rotasi Matahari
        // Sudut elevasi matahari yang natural (maksimal ~55 derajat di siang hari) agar cahaya tidak tegak lurus 90 derajat menyilaukan lantai
        float sunPitch;
        if (timeOfDay >= 5.5f && timeOfDay <= 18.5f)
        {
            float dayProgress = (timeOfDay - 5.5f) / 13f; // 0..1 selama siang
            sunPitch = Mathf.Sin(dayProgress * Mathf.PI) * maxSunElevationAngle;
        }
        else
        {
            sunPitch = ((timeOfDay - 6f) / 24f) * 360f;
        }

        transform.rotation = Quaternion.Euler(sunPitch, sunAzimuth, 0f);

        // 2. Terapkan Warna Cahaya Matahari
        if (useNaturalDaylightPreset)
        {
            directionalLight.color = GetNaturalSunColor(timeOfDay);
        }
        else if (sunColorGradient != null)
        {
            directionalLight.color = sunColorGradient.Evaluate(normalizedTime);
        }

        // 3. Terapkan Intensitas Cahaya Matahari dengan Pengali Anti-Silau
        float baseIntensity = (sunIntensityCurve != null) ? sunIntensityCurve.Evaluate(timeOfDay) : 0.8f;
        directionalLight.intensity = baseIntensity * sunIntensityMultiplier;

        // 4. Atur Bayangan
        bool isNight = (timeOfDay < 5.5f || timeOfDay > 18.5f);
        directionalLight.shadowStrength = isNight ? 0.3f : 0.85f;

        // 5. Terapkan Ambient Light ke Environment
        if (adjustAmbientLight)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Color baseAmbient = useNaturalDaylightPreset
                ? GetNaturalAmbientColor(timeOfDay)
                : (ambientColorGradient != null ? ambientColorGradient.Evaluate(normalizedTime) : new Color(0.28f, 0.30f, 0.35f));

            RenderSettings.ambientLight = baseAmbient * ambientIntensityMultiplier;
        }
    }
}
