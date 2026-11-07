using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class DrawingBoard : NetworkBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Line Settings")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private RectTransform drawingRect;
    [SerializeField] private float minDistanceBetweenPoints = 0.1f;

    [Header("Current Brush Settings")]
    [SerializeField] private Color currentColor = Color.black;
    [SerializeField] private float currentWidth = 0.15f;

    [Header("State")]
    public bool isDrawer = true;

    private LineRenderer currentLine;
    private Vector3 lastPoint;
    private List<GameObject> spawnedLines = new List<GameObject>();
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isDrawer) return;

        if (RectTransformUtility.RectangleContainsScreenPoint(drawingRect, eventData.position, mainCamera))
        {
            Vector3 worldPos = GetWorldPosition(eventData.position);
            StartNewLine(worldPos, currentColor, currentWidth);
            StartNewLineRpc(worldPos, currentColor, currentWidth);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDrawer || currentLine == null) return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(drawingRect, eventData.position, mainCamera))
        {
            currentLine = null;
            return;
        }

        Vector3 worldPos = GetWorldPosition(eventData.position);

        if (Vector3.Distance(worldPos, lastPoint) >= minDistanceBetweenPoints)
        {
            AddPointToLine(worldPos);
            lastPoint = worldPos;
            AddPointRpc(worldPos);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDrawer) return;
        currentLine = null;
    }

    private void StartNewLine(Vector3 startPoint, Color color, float width)
    {
        GameObject lineObj = Instantiate(linePrefab, transform);
        spawnedLines.Add(lineObj);

        currentLine = lineObj.GetComponent<LineRenderer>();
        currentLine.startWidth = width;
        currentLine.endWidth = width;
        currentLine.startColor = color;
        currentLine.endColor = color;

        currentLine.positionCount = 1;
        currentLine.SetPosition(0, startPoint);
        lastPoint = startPoint;
    }

    private void AddPointToLine(Vector3 newPoint)
    {
        if (currentLine == null) return;

        currentLine.positionCount++;
        currentLine.SetPosition(currentLine.positionCount - 1, newPoint);
    }

    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));
        worldPos.z = 0f;
        return worldPos;
    }

    #region Public Tools Methods

    // تغيير اللون عبر كود HEX
    public void SetColorHex(string hexColor)
    {
        if (ColorUtility.TryParseHtmlString(hexColor, out Color newColor))
        {
            currentColor = newColor;
        }
    }

    // دوال الألوان المباشرة للأزرار
    public void SetColorBlack() => currentColor = Color.black;
    public void SetColorRed()   => currentColor = Color.red;
    public void SetColorBlue()  => currentColor = Color.blue;
    public void SetColorGreen() => currentColor = Color.green;
    public void SetColorYellow() => currentColor = Color.yellow;

    public void SetEraser()
    {
        currentColor = Color.white; // لون اللوحة البيضاء
    }

    public void SetLineWidth(float width)
    {
        currentWidth = width;
    }

    // مسح اللوحة
    public void ClearBoard()
    {
        ClearBoardLocal();
        if (IsSpawned)
        {
            ClearBoardRpc();
        }
    }

    public void ClearBoardLocal()
    {
        foreach (var line in spawnedLines)
        {
            if (line != null) Destroy(line);
        }
        spawnedLines.Clear();
        currentLine = null;
    }

    #endregion

    #region Remote Procedure Calls (RPCs)

    [Rpc(SendTo.NotMe)]
    private void StartNewLineRpc(Vector3 startPoint, Color color, float width)
    {
        StartNewLine(startPoint, color, width);
    }

    [Rpc(SendTo.NotMe)]
    private void AddPointRpc(Vector3 newPoint)
    {
        AddPointToLine(newPoint);
    }

    [Rpc(SendTo.NotMe)]
    private void ClearBoardRpc()
    {
        ClearBoardLocal();
    }

    #endregion
}