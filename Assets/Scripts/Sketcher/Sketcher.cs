using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public enum SketchType
{
    Enemy,
    Spell,
    Player,
    Test,
}

public class Sketcher : Singleton<Sketcher>
{
    [SerializeField]
    private Color color = Color.black;

    [SerializeField]
    private Color eraserColor = Color.red;

    [SerializeField]
    private float width = 1.0f;

    [SerializeField]
    private Image image;

    [SerializeField]
    private RenderTexture renderTexture;

    [SerializeField]
    private string sketchName = "DefaultSketch";

    [SerializeField]
    private SketchType sketchType = SketchType.Spell;

    private List<LineRenderer> lines = new List<LineRenderer>();
    private LineRenderer currentLine;
    private int numClicks = 0;
    private bool isErasing = false;
    private Stack<LineRenderer> undoStack = new Stack<LineRenderer>();
    private bool isSymmetryEnabled = false;
    private bool isFilling = false;

    // Event for when an image is saved
    public event Action<string> OnImageSaved;

    #region Unity
    
    void Update()
    {
        HandleInput();
    }
    #endregion

    #region Input Handling

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            SaveImage();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            ToggleSymmetry();
        }
        else if (Input.GetKeyDown(KeyCode.Z))
        {
            UndoLastLine();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            EraseAllLines();
        }
        else if (Input.GetKeyDown(KeyCode.X))
        {
            ToggleEraser();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleFill();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverImage())
            {
                HandleMouseDown();
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (IsMouseOverImage())
            {
                HandleMouseDrag();
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (!isErasing && !isFilling)
            {
                StopLineDraw();
            }
        }
    }

    private void HandleMouseDown()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // Ensure the eraser stays on the 2D plane

        if (isErasing)
        {
            EraseAtPosition(mousePos);
            DrawEraserFeedback(mousePos);
        }
        else if (isFilling)
        {
            Vector2 localMousePos = image.rectTransform.InverseTransformPoint(mousePos);
            FloodFill(localMousePos, color);
        }
        else
        {
            StartNewLine();
        }
    }

    private void HandleMouseDrag()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // Ensure the eraser stays on the 2D plane

        if (isErasing)
        {
            EraseAtPosition(mousePos);
            DrawEraserFeedback(mousePos);
        }
        else if (isFilling)
        {
            Vector2 localMousePos = image.rectTransform.InverseTransformPoint(mousePos);
            FloodFill(localMousePos, color);
        }
        else
        {
            if (currentLine != null)
            {
                DrawLine();
            }
        }
    }
    #endregion

    #region Drawing
    private void CreateNewLine()
    {
        GameObject lineObject = new GameObject("Line");
        lineObject.transform.parent = this.transform;
        currentLine = lineObject.AddComponent<LineRenderer>();
        currentLine.material = new Material(Shader.Find("Sprites/Default"));
        currentLine.startColor = color;
        currentLine.endColor = color;
        currentLine.startWidth = width;
        currentLine.endWidth = width;
        currentLine.positionCount = 0;
        currentLine.useWorldSpace = true;
        lines.Add(currentLine);
        undoStack.Push(currentLine);

        if (isSymmetryEnabled)
        {
            CreateMirroredLine();
        }
    }

    private void StartNewLine()
    {
        CreateNewLine();
        numClicks = 0;
        currentLine.positionCount = 0;
    }

    private void StopLineDraw()
    {
        currentLine = null;
    }

    private void DrawLine()
    {
        if (currentLine == null)
            return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        numClicks++;
        currentLine.positionCount = numClicks;
        mousePos.z = 0;
        currentLine.SetPosition(numClicks - 1, mousePos);

        if (isSymmetryEnabled)
        {
            Vector3 mirroredPos = new Vector3(-mousePos.x, mousePos.y, mousePos.z);
            LineRenderer mirroredLine = lines[lines.Count - 1];
            mirroredLine.positionCount = numClicks;
            mirroredLine.SetPosition(numClicks - 1, mirroredPos);
        }

        EraseOutsideCanvas();
    }
    #endregion

    #region Erasing
    public void ToggleEraser()
    {
        isErasing = !isErasing;
    }

    private void EraseAtPosition(Vector3 position)
    {
        foreach (var line in lines)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                // Check if the point is within the eraser's radius
                if (Vector3.Distance(line.GetPosition(i), position) < width)
                {
                    // Store the erased line for undo
                    LineRenderer erasedLine = line;
                    undoStack.Push(erasedLine);

                    // Remove the point by setting it to an invalid position (could also fade it, etc.)
                    line.SetPosition(
                        i,
                        new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)
                    );
                }
            }
        }
    }

    private void EraseOutsideCanvas()
    {
        RectTransform rectTransform = image.rectTransform;
        for (int i = 0; i < currentLine.positionCount; i++)
        {
            Vector3 linePos = currentLine.GetPosition(i);
            Vector2 localPos = rectTransform.InverseTransformPoint(
                Camera.main.WorldToScreenPoint(linePos)
            );
            if (!rectTransform.rect.Contains(localPos))
            {
                currentLine.SetPosition(
                    i,
                    new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)
                );
            }
        }
    }

    public void EraseAllLines()
    {
        foreach (var line in lines)
        {
            Destroy(line.gameObject);
        }
        lines.Clear();
        undoStack.Clear();
    }

    private void DrawEraserFeedback(Vector3 position)
    {
        Gizmos.color = eraserColor;
        Gizmos.DrawWireSphere(position, width); // Visual feedback for the eraser region
    }
    #endregion

    #region Symmetry
    private void ApplySymmetry()
    {
        if (currentLine == null)
            return;

        LineRenderer mirroredLine = lines[lines.Count - 1];
        for (int i = 0; i < currentLine.positionCount; i++)
        {
            Vector3 originalPos = currentLine.GetPosition(i);
            Vector3 mirroredPos = new Vector3(-originalPos.x, originalPos.y, originalPos.z);
            mirroredLine.SetPosition(i, mirroredPos);
        }
    }

    private LineRenderer CreateMirroredLine()
    {
        GameObject lineObject = new GameObject("MirroredLine");
        lineObject.transform.parent = this.transform;
        LineRenderer mirroredLine = lineObject.AddComponent<LineRenderer>();
        mirroredLine.material = new Material(Shader.Find("Sprites/Default"));
        mirroredLine.startColor = color;
        mirroredLine.endColor = color;
        mirroredLine.startWidth = width;
        mirroredLine.endWidth = width;
        mirroredLine.positionCount = currentLine.positionCount;
        mirroredLine.useWorldSpace = true;
        lines.Add(mirroredLine);
        undoStack.Push(mirroredLine);
        return mirroredLine;
    }

    private void ToggleSymmetry()
    {
        isSymmetryEnabled = !isSymmetryEnabled;
        if (isSymmetryEnabled)
        {
            ApplySymmetry();
        }
    }
    #endregion

    #region Shapes
    private void CreateNewLineBetweenPoints(Vector2 start, Vector2 end)
    {
        GameObject lineObject = new GameObject("Line");
        lineObject.transform.parent = this.transform;
        currentLine = lineObject.AddComponent<LineRenderer>();
        currentLine.material = new Material(Shader.Find("Sprites/Default"));
        currentLine.startColor = color;
        currentLine.endColor = color;
        currentLine.startWidth = width;
        currentLine.endWidth = width;
        currentLine.positionCount = 2;
        currentLine.useWorldSpace = true;
        lines.Add(currentLine);
        undoStack.Push(currentLine);

        currentLine.SetPosition(0, start);
        currentLine.SetPosition(1, end);
    }

    private void Circle(Vector2 center, float radius, int numPoints)
    {
        float angle = 0;
        float angleStep = 360f / numPoints;
        Vector2 lastPoint = Vector2.zero;
        for (int i = 0; i < numPoints; i++)
        {
            float x = center.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float y = center.y + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            Vector2 currentPoint = new Vector2(x, y);
            if (i > 0)
            {
                CreateNewLineBetweenPoints(lastPoint, currentPoint);
            }
            lastPoint = currentPoint;
            angle += angleStep;
        }
    }

    private void Rectangle(Vector2 center, float width, float height)
    {
        Vector2 topLeft = new Vector2(center.x - width / 2, center.y + height / 2);
        Vector2 topRight = new Vector2(center.x + width / 2, center.y + height / 2);
        Vector2 bottomRight = new Vector2(center.x + width / 2, center.y - height / 2);
        Vector2 bottomLeft = new Vector2(center.x - width / 2, center.y - height / 2);

        CreateNewLineBetweenPoints(topLeft, topRight);
        CreateNewLineBetweenPoints(topRight, bottomRight);
        CreateNewLineBetweenPoints(bottomRight, bottomLeft);
        CreateNewLineBetweenPoints(bottomLeft, topLeft);
    }

    private void Triangle(Vector2 center, float width, float height)
    {
        Vector2 top = new Vector2(center.x, center.y + height / 2);
        Vector2 bottomRight = new Vector2(center.x + width / 2, center.y - height / 2);
        Vector2 bottomLeft = new Vector2(center.x - width / 2, center.y - height / 2);

        CreateNewLineBetweenPoints(top, bottomRight);
        CreateNewLineBetweenPoints(bottomRight, bottomLeft);
        CreateNewLineBetweenPoints(bottomLeft, top);
    }

    private void Star(Vector2 center, float radius, int numPoints)
    {
        float angle = 0;
        float angleStep = 360f / numPoints;
        Vector2 lastPoint = Vector2.zero;
        for (int i = 0; i < numPoints; i++)
        {
            float x = center.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float y = center.y + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            Vector2 currentPoint = new Vector2(x, y);
            if (i > 0)
            {
                CreateNewLineBetweenPoints(lastPoint, currentPoint);
            }
            lastPoint = currentPoint;
            angle += angleStep;
        }
    }
    #endregion

    #region Filling
    private void FloodFill(Vector2 start, Color fillColor)
    {
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Vector2Int startPixel = new Vector2Int((int)start.x, (int)start.y);
        Color originalColor = tex.GetPixel(startPixel.x, startPixel.y);

        if (originalColor == fillColor)
            return;

        Queue<Vector2Int> pixelsToFill = new Queue<Vector2Int>();
        pixelsToFill.Enqueue(startPixel);

        Vector2Int[] directions =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        while (pixelsToFill.Count > 0)
        {
            Vector2Int current = pixelsToFill.Dequeue();

            if (current.x < 0 || current.x >= tex.width || current.y < 0 || current.y >= tex.height)
                continue;
            if (tex.GetPixel(current.x, current.y) != originalColor)
                continue;

            tex.SetPixel(current.x, current.y, fillColor);

            foreach (var direction in directions)
            {
                Vector2Int neighbor = current + direction;
                pixelsToFill.Enqueue(neighbor);
            }
        }

        tex.Apply();
        RenderTexture.active = renderTexture;
        Graphics.Blit(tex, renderTexture);
        RenderTexture.active = null;
    }

    private void ToggleFill()
    {
        isFilling = !isFilling;
    }
    #endregion

    #region Utility
    private bool IsMouseOverImage()
    {
        Vector2 localMousePos = image.rectTransform.InverseTransformPoint(Input.mousePosition);
        return image.rectTransform.rect.Contains(localMousePos);
    }

    public void SetColor(Color color)
    {
        this.color = color;
    }

    public void SetWidth(float newWidth)
    {
        width = newWidth;
        if (currentLine != null)
        {
            currentLine.startWidth = width;
            currentLine.endWidth = width;
        }
    }

    public void UndoLastLine()
    {
        if (undoStack.Count > 0)
        {
            LineRenderer lastLine = undoStack.Pop();
            Destroy(lastLine.gameObject);
            lines.Remove(lastLine);
        }
    }

    private void SaveImage()
    {
        RenderTexture tempRT = RenderTexture.GetTemporary(
            renderTexture.width,
            renderTexture.height,
            0,
            RenderTextureFormat.ARGB32
        );
        tempRT.antiAliasing = 8;

        Graphics.Blit(renderTexture, tempRT);

        Texture2D tex = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGBA32, false);
        RenderTexture.active = tempRT;
        tex.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] == Color.white)
            {
                pixels[i] = new Color(0, 0, 0, 0);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        string folderPath = Path.Combine(
            Application.persistentDataPath,
            "sketches",
            sketchType.ToString()
        );
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        string path = Path.Combine(folderPath, sketchName + ".png");

        int fileIndex = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(folderPath, sketchName + "_" + fileIndex + ".png");
            fileIndex++;
        }

        File.WriteAllBytes(path, bytes);
        Debug.Log("Saved image to: " + path);

        OnImageSaved?.Invoke(path);

        RenderTexture.ReleaseTemporary(tempRT);
    }
    #endregion

    #region UI
    public void SetSketcher(SketchType type, string name)
    {
        sketchType = type;
        sketchName = name;
    }
    #endregion
}
