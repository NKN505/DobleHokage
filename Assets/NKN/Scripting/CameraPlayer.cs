using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CameraPlayer : MonoBehaviour
{
    [Header("Jugadores")]
    public List<PlayerPlay> players = new List<PlayerPlay>();

    [Header("Ajuste inicial de la cámara")]
    public Vector3 cameraOffset = new Vector3(0f, 5f, -10f);
    public float followSpeed = 5f;
    public float horizontalPadding = 2f;
    public float verticalPadding = 2f;
    public float minCameraHeight = 3f;
    public float maxCameraHeight = 15f;

    [Header("Límite de no retorno")]
    [Tooltip("El jugador no puede volver más atrás que este valor X.")]
    [Range(0f, 5f)] // <-- rango ajustable en el inspector
    public float leftLockMargin = 2f; // ahora puedes mover el slider en tiempo real

    [Header("Desplazamiento hacia la derecha")]
    [Tooltip("Cuánto espacio delante del jugador debe haber para empujar la cámara.")]
    public float forwardOffset = 2f;

    private Camera cam;
    private Vector3 targetPosition;
    private float leftLockX;

    void Awake()
    {
        cam = Camera.main;
        if (cam == null)
            Debug.LogError("CameraPlayer necesita una cámara principal con tag MainCamera.");
    }

    void LateUpdate()
    {
        if (players.Count == 0 || cam == null) return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var player in players)
        {
            if (player == null) continue;
            Vector3 pos = player.transform.position;
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }

        // Ajuste vertical
        float midY = (minY + maxY) / 2f + cameraOffset.y;
        float heightSpan = maxY - minY + verticalPadding * 2f;
        float cameraHalfHeight = Mathf.Clamp(heightSpan / 2f, minCameraHeight, maxCameraHeight);
        cam.orthographicSize = cameraHalfHeight;

        // Ajuste horizontal tipo "push forward"
        float halfWidth = cameraHalfHeight * cam.aspect;
        float playerFrontX = maxX + forwardOffset;
        float desiredCameraX = Mathf.Max(transform.position.x, playerFrontX - halfWidth);

        // Limite de no retorno (ajustable en tiempo real)
        float cameraLeftEdge = desiredCameraX - halfWidth;
        leftLockX = Mathf.Max(leftLockX, cameraLeftEdge - leftLockMargin);

        // Posición objetivo de la cámara
        targetPosition = new Vector3(desiredCameraX, midY, cameraOffset.z);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

        // Limitar jugadores a leftLockX
        ClampPlayersLeftLock();
    }

    private void ClampPlayersLeftLock()
    {
        foreach (var player in players)
        {
            if (player == null) continue;
            Vector3 pos = player.transform.position;

            if (pos.x < leftLockX)
                pos.x = leftLockX;

            player.transform.position = pos;
        }
    }
}
