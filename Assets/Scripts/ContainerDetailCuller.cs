using System.Collections.Generic;
using UnityEngine;

public class ContainerDetailCuller : MonoBehaviour
{
    public GameObject targetDetails;
    public float cullDistance = 45f;

    private static readonly List<ContainerDetailCuller> allCullers = new List<ContainerDetailCuller>();
    private static Camera cachedCam;
    private static float nextCamCheckTime;
    private static int currentIndex = 0;
    private const int BATCH_SIZE = 75; // Cek 75 kontainer per frame -> 1142 kontainer selesai dicek dalam ~15 frame (sangat ringan)

    private void Start()
    {
        // Pastikan detail teks pintu kontainer (TextMeshPro) non-aktif di awal
        // agar tidak membebani ribuan draw call saat kamera berada di atas/jauh
        if (targetDetails != null)
        {
            targetDetails.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (!allCullers.Contains(this))
        {
            allCullers.Add(this);
        }
    }

    private void OnDisable()
    {
        allCullers.Remove(this);
    }

    public static void GlobalUpdate()
    {
        int total = allCullers.Count;
        if (total == 0) return;

        // Cache referensi Camera.main hanya sekali tiap 1.5 detik (menghindari FindGameObjectWithTag tiap frame)
        if (cachedCam == null || Time.time > nextCamCheckTime)
        {
            cachedCam = Camera.main;
            nextCamCheckTime = Time.time + 1.5f;
        }

        if (cachedCam == null) return;

        Vector3 camPos = cachedCam.transform.position;
        float maxDistSqr = 45f * 45f;

        int checks = Mathf.Min(BATCH_SIZE, total);
        for (int i = 0; i < checks; i++)
        {
            if (currentIndex >= allCullers.Count) currentIndex = 0;
            var culler = allCullers[currentIndex];
            currentIndex++;

            if (culler != null && culler.targetDetails != null)
            {
                float distSqr = (culler.transform.position - camPos).sqrMagnitude;
                bool shouldShow = distSqr <= maxDistSqr;
                if (culler.targetDetails.activeSelf != shouldShow)
                {
                    culler.targetDetails.SetActive(shouldShow);
                }
            }
        }
    }
}
