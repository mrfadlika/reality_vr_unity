using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class DashedLineGenerator : MonoBehaviour
{
    public enum DirectionAxis { Z_Axis, X_Axis }

    [Header("Pengaturan Mode Line")]
    [Tooltip("Arah pembuatan garis putus-putus (Mengikuti Sumbu Z atau Sumbu X objek ini)")]
    public DirectionAxis directionAxis = DirectionAxis.Z_Axis;

    [Tooltip("Target titik akhir (opsional). Jika diisi, garis akan dibuat dari posisi objek ini menuju targetPoint.")]
    public Transform targetEndPoint;

    [Header("Pengaturan Garis Putus-Putus")]
    [Tooltip("Panjang tiap potongan garis (meter)")]
    public float dashLength = 2.5f;

    [Tooltip("Jarak spasi / jeda antar garis (meter)")]
    public float gapLength = 2.5f;

    [Tooltip("Lebar / ketebalan garis (meter)")]
    public float dashWidth = 0.25f;

    [Tooltip("Warna garis putus-putus")]
    public Color lineColor = new Color(0.95f, 0.95f, 0.95f, 1.0f); // Bright Industrial White

    [Tooltip("Material kustom (opsional)")]
    public Material customMaterial;

    [Header("Pengaturan Ketinggian Lantai")]
    [Tooltip("Centang untuk mengunci ketinggian garis di permukaan lantai vr-env")]
    public bool autoSnapToFloor = true;

    [Tooltip("Offset ketinggian di atas lantai (meter)")]
    public float heightOffset = 0.02f;

    private Vector3 lastPos;
    private Vector3 lastScale;
    private Quaternion lastRot;
    private Material instanceMaterial;

    void Start()
    {
        GenerateDashedLine();
    }

    void OnDestroy()
    {
        if (instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
            if (transform.position != lastPos || transform.lossyScale != lastScale || transform.rotation != lastRot)
            {
                lastPos = transform.position;
                lastScale = transform.lossyScale;
                lastRot = transform.rotation;
                GenerateDashedLine();
            }
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            UnityEditor.EditorApplication.delayCall -= DelayGenerate;
            UnityEditor.EditorApplication.delayCall += DelayGenerate;
#endif
        }
    }

#if UNITY_EDITOR
    void DelayGenerate()
    {
        if (this != null && gameObject != null)
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
            GenerateDashedLine();
        }
    }
#endif

    [ContextMenu("Generate Dashed Line")]
    public void GenerateDashedLine()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
        // 1. Hapus garis lama
        Transform oldParent = transform.Find("DashedLineContainer");
        if (oldParent != null)
        {
            oldParent.SetParent(null);
            if (Application.isPlaying) Destroy(oldParent.gameObject);
            else DestroyImmediate(oldParent.gameObject);
        }

        // 2. Tentukan titik awal & titik akhir
        Vector3 startPos = transform.position;
        Vector3 endPos;

        if (targetEndPoint != null)
        {
            endPos = targetEndPoint.position;
        }
        else
        {
            // Hitung panjang berdasarkan lossyScale objek ini
            float totalLength = (directionAxis == DirectionAxis.Z_Axis) ? transform.lossyScale.z : transform.lossyScale.x;
            Vector3 dir = (directionAxis == DirectionAxis.Z_Axis) ? transform.forward : transform.right;
            startPos = transform.position - dir * (totalLength / 2f);
            endPos = transform.position + dir * (totalLength / 2f);
        }

        // 3. Tentukan ketinggian Y
        float targetY = startPos.y;
        if (autoSnapToFloor)
        {
            float envTopY = 19.0f;
            GameObject vrEnv = GameObject.Find("vr-env");
            if (vrEnv != null)
            {
                Renderer rend = vrEnv.GetComponentInChildren<Renderer>();
                envTopY = (rend != null) ? rend.bounds.max.y : vrEnv.transform.position.y;
            }
            targetY = envTopY;
        }
        targetY += heightOffset;

        startPos.y = targetY;
        endPos.y = targetY;

        // 4. Hitung jarak total & arah
        Vector3 directionVector = endPos - startPos;
        float totalDistance = directionVector.magnitude;
        if (totalDistance < 0.1f) return;

        Vector3 normDir = directionVector.normalized;
        Quaternion dashRotation = Quaternion.LookRotation(normDir, Vector3.up);

        // 5. Buat objek induk pembungkus
        GameObject lineContainer = new GameObject("DashedLineContainer");
        lineContainer.transform.SetParent(transform);
        lineContainer.transform.localPosition = Vector3.zero;

        Material lineMat = GetMaterial();

        // 6. Buat potongan-potongan garis putus-putus
        float step = Mathf.Max(dashLength + gapLength, 0.2f);
        float currentDashLength = Mathf.Max(dashLength, 0.1f);

        for (float dist = 0; dist < totalDistance; dist += step)
        {
            float actualLength = Mathf.Min(currentDashLength, totalDistance - dist);
            if (actualLength <= 0.05f) break;

            Vector3 dashCenter = startPos + normDir * (dist + (actualLength / 2f));

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dash";
            dash.transform.SetParent(lineContainer.transform);
            dash.transform.position = dashCenter;
            dash.transform.rotation = dashRotation;

            // Kompensasi skala parent agar tidak terdistorsi jika parent Cube di-scale
            Vector3 pScale = lineContainer.transform.lossyScale;
            Vector3 targetSize = new Vector3(dashWidth, 0.02f, actualLength);
            
            dash.transform.localScale = new Vector3(
                pScale.x != 0 ? targetSize.x / pScale.x : targetSize.x,
                pScale.y != 0 ? targetSize.y / pScale.y : targetSize.y,
                pScale.z != 0 ? targetSize.z / pScale.z : targetSize.z
            );

            Collider col = dash.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Renderer rend = dash.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = lineMat;
            }
        }
    }

    private Material GetMaterial()
    {
        if (customMaterial != null) return customMaterial;

        if (instanceMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");

            instanceMaterial = new Material(s);
            instanceMaterial.name = "DashedLine_Mat_" + gameObject.name;
        }

        instanceMaterial.color = lineColor;
        if (instanceMaterial.HasProperty("_BaseColor")) instanceMaterial.SetColor("_BaseColor", lineColor);
        if (instanceMaterial.HasProperty("_Color")) instanceMaterial.SetColor("_Color", lineColor);

        return instanceMaterial;
    }
}
