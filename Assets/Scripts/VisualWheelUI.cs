using UnityEngine;

[RequireComponent(typeof(Car))]
public class VisualWheelUI : MonoBehaviour
{
    [Header("Layout Settings")]
    public Vector2 uiPosition = new Vector2(50f, 50f);
    public Vector2 wheelLayoutSize = new Vector2(200f, 150f);
    public float maxLineLength = 40f;
    public float dotSize = 12f;
    
    [Header("RPM Gauge Settings")]
    public Vector2 rpmGaugePosition = new Vector2(350f, 50f);
    public float rpmGaugeRadius = 60f;
    public float needleLength = 45f;
    public float needleWidth = 3f;
    
    [Header("Colors")]
    public Color normalColor = Color.green;
    public Color cautionColor = Color.yellow;
    public Color warningColor = Color.red;
    public Color criticalColor = Color.blue;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.3f);
    public Color gaugeBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color needleColor = Color.white;
    
    private Car car;
    private float[] wheelSlip;
    private Vector2[] wheelPositions;
    private Texture2D dotTexture;
    private GUIStyle backgroundStyle;
    private GUIStyle textStyle;
    
    void Start()
    {
        car = GetComponent<Car>();
        wheelSlip = new float[car.wheels.Length];
        
        // Initialize wheel slip array
        for (int i = 0; i < wheelSlip.Length; i++)
        {
            wheelSlip[i] = 0.0f;
        }
        
        // Calculate wheel positions
        CalculateWheelPositions();
        
        // Create dot texture
        CreateDotTexture();
        
        // Create GUI styles
        CreateGUIStyles();
    }
    
    void OnDestroy()
    {
        if (dotTexture != null) Destroy(dotTexture);
        if (backgroundStyle?.normal.background != null) Destroy(backgroundStyle.normal.background);
    }

    void CalculateWheelPositions()
    {
        wheelPositions = new Vector2[car.wheels.Length];
        
        if (car.wheels.Length == 4)
        {
            // Standard 4-wheel layout
            float halfWidth = wheelLayoutSize.x * 0.4f;
            float halfHeight = wheelLayoutSize.y * 0.4f;
            
            wheelPositions[0] = new Vector2(-halfWidth, -halfHeight);  // Front-left
            wheelPositions[1] = new Vector2(halfWidth, -halfHeight);   // Front-right
            wheelPositions[2] = new Vector2(-halfWidth, halfHeight);   // Rear-left
            wheelPositions[3] = new Vector2(halfWidth, halfHeight);    // Rear-right
        }
        else
        {
            // Generic circular layout
            float radius = Mathf.Min(wheelLayoutSize.x, wheelLayoutSize.y) * 0.3f;
            for (int i = 0; i < car.wheels.Length; i++)
            {
                float angle = (float)i / car.wheels.Length * 2f * Mathf.PI;
                wheelPositions[i] = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
            }
        }
    }
    
    void CreateDotTexture()
    {
        int size = 16;
        dotTexture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        dotTexture.SetPixels(pixels);
        dotTexture.Apply();
    }
    
    void CreateGUIStyles()
    {
        backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = CreateSolidTexture(backgroundColor);
        
        textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 16;
        textStyle.fontStyle = FontStyle.Bold;
    }
    
    Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
    
    void OnGUI()
    {
        if (car == null || car.rb == null || backgroundStyle == null || wheelSlip == null) return;

        // Update wheel slip data
        UpdateWheelData();
        
        // Draw background panel
        Vector2 panelSize = new Vector2(wheelLayoutSize.x + 60f, wheelLayoutSize.y + 120f);
        Rect backgroundRect = new Rect(uiPosition.x - 30f, uiPosition.y - 30f, panelSize.x, panelSize.y);
        GUI.Box(backgroundRect, "", backgroundStyle);
        
        // Draw speed and gear info
        DrawTextInfo();
        
        // Draw wheel visualization
        DrawWheelVisualization();
        
        // Draw RPM gauge
        DrawRPMGauge();
    }
    
    void UpdateWheelData()
    {
        for (int i = 0; i < car.wheels.Length; i++)
        {
            float slip = float.IsNaN(car.wheels[i].slip) ? 0f : car.wheels[i].slip;
            wheelSlip[i] = slip;
        }
    }
    
    void DrawTextInfo()
    {
        // Speed
        float speed = car.rb.velocity.magnitude * 3.6f;
        string speedText = $"{speed:F0} kph";
        GUI.Label(new Rect(uiPosition.x, uiPosition.y, 200f, 25f), speedText, textStyle);
        
        // Gear and RPM
        float currentRPM = car.e.getRPM();
        float maxRPM = car.e.maxRPM;
        string gearText = car.e.getCurrentGear().ToString();
        string rpmText = $"Gear {gearText} - {currentRPM:F0} RPM";
        
        GUIStyle rpmStyle = new GUIStyle(textStyle);
        if (car.e.isSwitchingGears())
            rpmStyle.normal.textColor = criticalColor;
        else if (currentRPM > 0.8f * maxRPM)
            rpmStyle.normal.textColor = warningColor;
        else if (currentRPM > 0.6f * maxRPM)
            rpmStyle.normal.textColor = cautionColor;
        else
            rpmStyle.normal.textColor = normalColor;
            
        GUI.Label(new Rect(uiPosition.x, uiPosition.y + 25f, 300f, 25f), rpmText, rpmStyle);
    }
    
    void DrawRPMGauge()
    {
        Vector2 gaugeCenter = rpmGaugePosition + new Vector2(rpmGaugeRadius, rpmGaugeRadius);
        
        // Draw gauge background circle
        Color originalColor = GUI.color;
        GUI.color = gaugeBackgroundColor;
        
        // Draw background circle using multiple small rectangles (circle approximation)
        int circleSegments = 32;
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * 2f * Mathf.PI;
            Vector2 pos = gaugeCenter + new Vector2(
                Mathf.Cos(angle) * rpmGaugeRadius,
                Mathf.Sin(angle) * rpmGaugeRadius
            );
            GUI.DrawTexture(new Rect(pos.x - 2f, pos.y - 2f, 4f, 4f), Texture2D.whiteTexture);
        }
        
        // Draw gauge tick marks
        GUI.color = Color.white;
        for (int i = 0; i <= 10; i++)
        {
            float tickAngle = -Mathf.PI + (float)i / 10f * Mathf.PI; // From -180 to 0 degrees
            Vector2 outerPos = gaugeCenter + new Vector2(
                Mathf.Cos(tickAngle) * (rpmGaugeRadius - 5f),
                Mathf.Sin(tickAngle) * (rpmGaugeRadius - 5f)
            );
            Vector2 innerPos = gaugeCenter + new Vector2(
                Mathf.Cos(tickAngle) * (rpmGaugeRadius - 15f),
                Mathf.Sin(tickAngle) * (rpmGaugeRadius - 15f)
            );
            
            DrawLine(innerPos, outerPos, Color.white, 2f);
        }
        
        // Get RPM data
        float currentRPM = car.e.getRPM();
        float maxRPM = car.e.maxRPM;
        float rpmRatio = Mathf.Clamp01(currentRPM / maxRPM);
        
        // Calculate needle angle (from -180 to 0 degrees based on RPM ratio)
        float needleAngle = -Mathf.PI + rpmRatio * Mathf.PI;
        
        // Determine needle color based on RPM and gear switching
        Color currentNeedleColor = needleColor;
        if (car.e.isSwitchingGears())
            currentNeedleColor = criticalColor;
        else if (rpmRatio > 0.8f)
            currentNeedleColor = warningColor;
        else if (rpmRatio > 0.6f)
            currentNeedleColor = cautionColor;
        else
            currentNeedleColor = normalColor;
        
        // Draw needle
        Vector2 needleEnd = gaugeCenter + new Vector2(
            Mathf.Cos(needleAngle) * needleLength,
            Mathf.Sin(needleAngle) * needleLength
        );
        
        DrawLine(gaugeCenter, needleEnd, currentNeedleColor, needleWidth);
        
        // Draw center dot
        GUI.color = currentNeedleColor;
        GUI.DrawTexture(new Rect(gaugeCenter.x - 4f, gaugeCenter.y - 4f, 8f, 8f), dotTexture);
        
        // Draw RPM text
        GUIStyle rpmGaugeStyle = new GUIStyle(textStyle);
        rpmGaugeStyle.fontSize = 12;
        rpmGaugeStyle.alignment = TextAnchor.MiddleCenter;
        rpmGaugeStyle.normal.textColor = currentNeedleColor;
        
        string rpmDisplayText = $"{currentRPM:F0}\nRPM";
        GUI.Label(new Rect(gaugeCenter.x - 30f, gaugeCenter.y + 20f, 60f, 30f), rpmDisplayText, rpmGaugeStyle);
        
        // Restore original color
        GUI.color = originalColor;
    }
    
    void DrawWheelVisualization()
    {
        Vector2 centerOffset = uiPosition + new Vector2(wheelLayoutSize.x / 2f, wheelLayoutSize.y / 2f + 60f);
        
        for (int i = 0; i < car.wheels.Length; i++)
        {
            Vector2 wheelCenter = centerOffset + wheelPositions[i];
            
            // Get wheel data
            float slip = wheelSlip[i];
            float throttleInput = Mathf.Abs(car.userInput.y);
            float steerAngle = car.wheels[i].input.x * car.wheels[i].turnAngle * Mathf.Deg2Rad;
            
            // Get color based on slip
            Color wheelColor = GetSlipColor(slip);
            
            // Draw wheel dot
            DrawWheelDot(wheelCenter, wheelColor);
            
            DrawWheelLine(wheelCenter, steerAngle, throttleInput, wheelColor);

            float slipAngle = car.wheels[i].xSlipAngle * Mathf.Deg2Rad;

                DrawWheelLine(wheelCenter, slipAngle, 0.6f, wheelColor);
            
            // Draw wheel index
            GUI.Label(new Rect(wheelCenter.x - 10f, wheelCenter.y + dotSize, 20f, 15f), i.ToString(), textStyle);
        }
    }
    
    void DrawWheelDot(Vector2 center, Color color)
    {
        // Store original GUI color
        Color originalColor = GUI.color;
        
        // Set color and draw dot
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - dotSize/2f, center.y - dotSize/2f, dotSize, dotSize), dotTexture);
        
        // Restore original color
        GUI.color = originalColor;
    }
    
    void DrawWheelLine(Vector2 center, float angle, float throttle, Color color)
    {
        float lineLength = Mathf.Max(throttle, 0.3f) * maxLineLength;
        
        // Calculate line end point
        Vector2 lineEnd = center + new Vector2(
            Mathf.Sin(angle) * lineLength,
            -Mathf.Cos(angle) * lineLength
        );
        
        // Draw line using multiple small rectangles (simple line approximation)
        DrawLine(center, lineEnd, color, 2f);
    }
    
    void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        
        // Store original GUI color
        Color originalColor = GUI.color;
        GUI.color = color;
        
        // Draw line as series of small rectangles
        int segments = Mathf.Max(1, (int)(distance / 2f));
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 pos = Vector2.Lerp(start, end, t);
            GUI.DrawTexture(new Rect(pos.x - thickness/2f, pos.y - thickness/2f, thickness, thickness), Texture2D.whiteTexture);
        }
        
        // Restore original color
        GUI.color = originalColor;
    }
    
    Color GetSlipColor(float slip)
    {
        if (slip > 1f)
            return criticalColor;
        else if (slip > 0.9f)
            return warningColor;
        else if (slip > 0.7f)
            return cautionColor;
        else
            return normalColor;
    }
}
