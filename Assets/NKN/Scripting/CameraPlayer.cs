using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[DisallowMultipleComponent]
public class CameraPlayer : MonoBehaviour
{
    [Header("Jugadores")]
    [Tooltip("Puedes arrastrar aquí los Transforms de tus jugadores o dejar activado el autopopulate por tag.")]
    public List<Transform> players = new List<Transform>();

    [Tooltip("Rellenar la lista automáticamente con todos los objetos que tengan este tag.")]
    public bool autoPopulate = true;
    public string playerTag = "Player";

    [Header("Ajuste inicial de la cámara")]
    public Vector3 cameraOffset = new Vector3(0f, 5f, -10f);
    public float followSpeed = 5f;
    public float horizontalPadding = 2f;
    public float verticalPadding = 2f;
    public float minCameraHeight = 3f;
    public float maxCameraHeight = 15f;

    [Header("Límite de no retorno")]
    [Tooltip("El jugador no puede volver más atrás que este valor X relativo al borde izquierdo de la cámara.")]
    [Range(0f, 5f)]
    public float leftLockMargin = 2f;

    [Header("Desplazamiento hacia la derecha")]
    [Tooltip("Cuánto espacio delante del jugador debe haber para empujar la cámara.")]
    public float forwardOffset = 2f;

    private Camera cam;
    private Vector3 targetPosition;
    private float leftLockX;
    private bool initializedLeftLock;

    void Awake()
    {
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("CameraPlayer necesita una cámara principal con tag MainCamera.");
            enabled = false;
            return;
        }

        if (autoPopulate) AutoPopulatePlayers();
        ResetLeftLock();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (autoPopulate && Application.isPlaying == false)
            AutoPopulatePlayers();
    }
#endif

    void OnEnable()
    {
        ResetLeftLock();
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (autoPopulate && (players == null || players.Count == 0))
            AutoPopulatePlayers();

        if (players == null || players.Count == 0) return;

        // Calcular bounds de todos los jugadores
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = players.Count - 1; i >= 0; i--)
        {
            var t = players[i];
            if (t == null)
            {
                players.RemoveAt(i);
                continue;
            }

            Vector3 pos = t.position;
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }

        if (players.Count == 0) return;

        // Sólo soportamos cámara ortográfica para el zoom vertical dinámico
        if (!cam.orthographic)
        {
            Debug.LogWarning("CameraPlayer está pensado para cámara ortográfica. Se usará seguimiento sin zoom dinámico.");
        }

        // Ajuste vertical y tamaño ortográfico
        float midY = (minY + maxY) * 0.5f + cameraOffset.y;

        if (cam.orthographic)
        {
            float heightSpan = (maxY - minY) + verticalPadding * 2f;
            float cameraHalfHeight = Mathf.Clamp(heightSpan * 0.5f, minCameraHeight, maxCameraHeight);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, cameraHalfHeight, followSpeed * Time.deltaTime);
        }

        float halfHeight = cam.orthographic ? cam.orthographicSize : Mathf.Abs(cameraOffset.z);
        float halfWidth = halfHeight * cam.aspect;

        // Padding horizontal real para contener a todos los jugadores
        float desiredLeft = (minX - horizontalPadding);
        float desiredRight = (maxX + horizontalPadding + forwardOffset);
        float desiredCenterX = (desiredLeft + desiredRight) * 0.5f;

        // Empuje hacia delante: no permitimos que el centro vaya hacia atrás si ya avanzó
        float minCenterXByPush = desiredRight - halfWidth;
        float desiredCameraX = Mathf.Max(transform.position.x, minCenterXByPush, desiredCenterX);

        // Límite de no retorno
        float cameraLeftEdge = desiredCameraX - halfWidth;
        if (!initializedLeftLock)
        {
            leftLockX = cameraLeftEdge - leftLockMargin;
            initializedLeftLock = true;
        }
        else
        {
            leftLockX = Mathf.Max(leftLockX, cameraLeftEdge - leftLockMargin);
        }

        // Posición objetivo de la cámara
        targetPosition = new Vector3(desiredCameraX, midY, cameraOffset.z);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

        // Limitar jugadores a leftLockX
        ClampPlayersLeftLock();
    }

    private void AutoPopulatePlayers()
    {
        players ??= new List<Transform>();
        players.Clear();

        var found = GameObject.FindGameObjectsWithTag(playerTag);
        foreach (var go in found)
            players.Add(go.transform);
    }

    private void ResetLeftLock()
    {
        initializedLeftLock = false;
        leftLockX = float.NegativeInfinity;
    }

    private void ClampPlayersLeftLock()
    {
        if (float.IsNegativeInfinity(leftLockX)) return;

        foreach (var t in players)
        {
            if (t == null) continue;

            Vector3 pos = t.position;
            if (pos.x < leftLockX)
            {
                pos.x = leftLockX;

                // Si tiene Rigidbody, mejor mover por posición de rigidbody
                if (t.TryGetComponent<Rigidbody>(out var rb))
                {
#if UNITY_6000_0_OR_NEWER
                    rb.position = new Vector3(pos.x, rb.position.y, rb.position.z);
#else
                    // Compatibilidad: mover el transform si no hay API nueva
                    t.position = pos;
#endif
                }
                else
                {
                    t.position = pos;
                }
            }
        }
    }
}
