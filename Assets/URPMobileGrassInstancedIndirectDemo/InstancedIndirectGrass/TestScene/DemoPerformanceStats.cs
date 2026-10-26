using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Replacement for the unavailable Advanced FPS Counter; values describe this process.
public class DemoPerformanceStats : MonoBehaviour
{
    public Font labelsFont;
    public float updateInterval = .5f;
    public int averageSamples = 50;
    public int minMaxIntervalsToSkip = 3;
    public bool visible = true;
    public double FramesPerSecond { get; private set; }
    public double AverageFramesPerSecond { get; private set; }
    public double MinimumFramesPerSecond { get; private set; }
    public double MaximumFramesPerSecond { get; private set; }
    public int SampleCount { get; private set; }
    public long ReservedBytes { get; private set; }
    public long AllocatedBytes { get; private set; }
    public long GraphicsBytes { get; private set; }
    public bool HasExtrema => SampleCount > Mathf.Max(0, minMaxIntervalsToSkip);

    struct Sample { public int frames; public double seconds; }
    Sample[] samples;
    int nextSample, sampleLength, frames;
    double intervalStart;
    string performanceText = "Measuring FPS...", memoryText = "Measuring memory...", deviceText;
    GUIStyle style;

    void OnEnable()
    {
        samples = new Sample[Mathf.Clamp(averageSamples, 1, 1000)];
        nextSample = sampleLength = frames = SampleCount = 0;
        FramesPerSecond = AverageFramesPerSecond = MinimumFramesPerSecond = MaximumFramesPerSecond = 0;
        intervalStart = Time.realtimeSinceStartupAsDouble;
        performanceText = "Measuring FPS...";
        if (!labelsFont) labelsFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deviceText = $"{Application.platform} | Unity {Application.unityVersion}\n{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\n{SystemInfo.graphicsDeviceType} | shader {SystemInfo.graphicsShaderLevel}\nGPU capacity: {SystemInfo.graphicsMemorySize} MiB\nRAM capacity: {SystemInfo.systemMemorySize} MiB";
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame) visible = !visible;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.BackQuote)) visible = !visible;
#endif
        frames++;
        double now = Time.realtimeSinceStartupAsDouble;
        double seconds = now - intervalStart;
        if (seconds < Math.Max(.05, updateInterval)) return;
        FramesPerSecond = frames / seconds;
        samples[nextSample] = new Sample { frames = frames, seconds = seconds };
        nextSample = (nextSample + 1) % samples.Length;
        sampleLength = Math.Min(sampleLength + 1, samples.Length);
        int totalFrames = 0; double totalSeconds = 0;
        for (int i = 0; i < sampleLength; i++) { totalFrames += samples[i].frames; totalSeconds += samples[i].seconds; }
        AverageFramesPerSecond = totalFrames / totalSeconds;
        SampleCount++;
        if (HasExtrema) {
            if (SampleCount == Mathf.Max(0, minMaxIntervalsToSkip) + 1) MinimumFramesPerSecond = MaximumFramesPerSecond = FramesPerSecond;
            MinimumFramesPerSecond = Math.Min(MinimumFramesPerSecond, FramesPerSecond);
            MaximumFramesPerSecond = Math.Max(MaximumFramesPerSecond, FramesPerSecond);
        }
        ReservedBytes = Profiler.GetTotalReservedMemoryLong();
        AllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
        GraphicsBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
        var text = new StringBuilder();
        text.AppendLine($"FPS: {FramesPerSecond:F1} | {1000 / FramesPerSecond:F2} ms");
        text.AppendLine($"Avg: {AverageFramesPerSecond:F1} | {1000 / AverageFramesPerSecond:F2} ms");
        text.AppendLine(HasExtrema ? $"Min / Max: {MinimumFramesPerSecond:F1} / {MaximumFramesPerSecond:F1}" : "Min / Max: warming up");
        text.AppendLine($"Target: {Application.targetFrameRate} | VSync: {QualitySettings.vSyncCount}");
        text.Append("` toggles statistics");
        performanceText = text.ToString();
        memoryText = $"Unity reserved: {Memory(ReservedBytes)} | allocated: {Memory(AllocatedBytes)}\nGraphics estimate: {Memory(GraphicsBytes)} | {(Application.isEditor ? "Editor process" : "Player process")}";
        frames = 0; intervalStart = now;
    }

    static string Memory(long bytes) => bytes > 0 ? $"{bytes / (1024d * 1024):F1} MiB" : "unavailable";

    void OnGUI()
    {
        if (!visible) return;
        using var gui = DemoGUI.Begin();
        if (style == null) style = new GUIStyle(GUI.skin.label) { font = labelsFont, fontSize = 14, wordWrap = true, padding = new RectOffset(5, 5, 5, 5) };
        GUI.depth = -10000;
        Panel(new Rect(5, 5, 190, 125), performanceText, SampleCount == 0 ? Color.white : FramesPerSecond < 20 ? new Color(.98f, .36f, .36f) : FramesPerSecond < 50 ? new Color(.93f, .88f, .35f) : new Color(.33f, .85f, .4f));
        Panel(new Rect(610, 5, 345, 190), deviceText + $"\nScreen: {Screen.width} x {Screen.height}", new Color(.8f, .8f, .8f));
        Panel(new Rect(5, 555, 595, 40), memoryText, new Color(.92f, .93f, .4f));
    }

    void Panel(Rect rect, string text, Color foreground)
    {
        GUI.color = new Color(0, 0, 0, .61f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.contentColor = foreground;
        GUI.Label(rect, text, style);
    }
}

// Keep the original control positions while fitting the complete demo into smaller Game views.
public static class DemoGUI
{
    public static Scope Begin() => new Scope(true);
    public readonly struct Scope : IDisposable
    {
        readonly Matrix4x4 matrix;
        readonly Color color, contentColor;
        readonly int depth;
        public Scope(bool fit)
        {
            matrix = GUI.matrix; color = GUI.color; contentColor = GUI.contentColor; depth = GUI.depth;
            float scale = Mathf.Min(1, Mathf.Min(Screen.width / 960f, Screen.height / 600f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)) * matrix;
            GUI.contentColor = Color.black;
        }
        public void Dispose() { GUI.matrix = matrix; GUI.color = color; GUI.contentColor = contentColor; GUI.depth = depth; }
    }
}
