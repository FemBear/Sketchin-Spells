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
    Sword,
    Test,
    // To train the AI Model i swear
    Penis,
    Not_Penis
}

public class Sketcher : Singleton<Sketcher>
{
    [SerializeField]
    private Color color = Color.black;

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
        else if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverImage())
            {
                if (isErasing)
                {
                    EraseAtPosition(Input.mousePosition);
                }
                else
                {
                    StartNewLine();
                }
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (IsMouseOverImage())
            {
                if (isErasing)
                {
                    EraseAtPosition(Input.mousePosition);
                }
                else
                {
                    DrawLine();
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (!isErasing)
            {
                StopLineDraw();
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
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        numClicks = 0;
        currentLine.positionCount = 0;
    }

    private void StopLineDraw()
    {
        currentLine = null;
    }

    private void DrawLine()
    {
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
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(position);
        worldPos.z = 0;
        foreach (var line in lines)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                if (Vector3.Distance(line.GetPosition(i), worldPos) < width)
                {
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
    #endregion

    #region Symmetry
    private void ApplySymmetry()
    {
        if (currentLine == null) return;

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
        string folderPath = Path.Combine(Application.persistentDataPath, "sketches", sketchType.ToString());
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
    public void OpenSketcher(SketchType type, string name)
    {
        sketchType = type;
        sketchName = name;
        gameObject.SetActive(true);
    }
    #endregion
}
