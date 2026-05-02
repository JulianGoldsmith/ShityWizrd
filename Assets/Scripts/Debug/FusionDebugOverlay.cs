using Fusion;
using UnityEngine;
using System.Text;
using UnityEngine.InputSystem;

public class FusionDebugOverlay : MonoBehaviour
{
    [SerializeField] private NetworkRunner runner;
    [Range(0.01f, 0.5f)]
    [SerializeField] private float smoothing = 0.1f;

    private GUIStyle _style;
    private readonly StringBuilder _sb = new StringBuilder(1024);
    private bool _visible = true;

    // Averages for jittery data
    private MovingAverage _fpsAvg = new MovingAverage();
    private MovingAverage _rttAvg = new MovingAverage();
    private MovingAverage _inBwAvg = new MovingAverage();
    private MovingAverage _outBwAvg = new MovingAverage();
    private MovingAverage _resimAvg = new MovingAverage();

    private void Awake()
    {
        if (runner == null) runner = FindFirstObjectByType<NetworkRunner>();

        _style = new GUIStyle
        {
            fontSize = 11,
            richText = true,
            padding = new RectOffset(10, 10, 10, 10)
        };

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
        tex.Apply();
        _style.normal.background = tex;
    }

    private void Update()
    {
        if(runner == null)
            runner = FindFirstObjectByType<NetworkRunner>();

        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            _visible = !_visible;

        if (!_visible || runner == null || !runner.IsRunning) return;

        // Update Averages
        _fpsAvg.Update(1f / Time.unscaledDeltaTime, smoothing);

        // Get RTT from the Runner (usually more accurate for clients)
        if (runner.LocalPlayer != PlayerRef.None)
        {
            // GetPlayerRtt returns seconds, so * 1000 for ms
            float currentRtt = (float)runner.GetPlayerRtt(runner.LocalPlayer) * 1000f;
            _rttAvg.Update(currentRtt, smoothing);
        }

        if (runner.TryGetFusionStatistics(out var stats))
        {
            _inBwAvg.Update(stats.CompleteSnapshot.InBandwidth, smoothing);
            _outBwAvg.Update(stats.CompleteSnapshot.OutBandwidth, smoothing);
            _resimAvg.Update(stats.CompleteSnapshot.Resimulations, smoothing);
        }
    }

    private void OnGUI()
    {
        if (!_visible || runner == null) return;

        _sb.Clear();
        AppendHeader("=== Fusion 2 Debug ===");

        // Performance
        AppendStat("FPS", $"{_fpsAvg.Value:0}", _fpsAvg.Value > 55 ? "lime" : "yellow");
        AppendStat("Tick", runner.Tick.ToString(), "white");
        AppendStat("Simulating", runner.IsRunning.ToString(), "lime");

        if (runner.TryGetFusionStatistics(out var stats))
        {
            _sb.AppendLine();
            AppendHeader("=== Network ===");

            // If RTT is still 0, we fallback to stats snapshot
            float displayRtt = _rttAvg.Value > 0 ? _rttAvg.Value : stats.CompleteSnapshot.RoundTripTime;
            AppendStat("RTT (Avg)", $"{displayRtt:0.0} ms", displayRtt < 100 ? "lime" : "red");

            AppendStat("In", $"{_inBwAvg.Value / 1024f:0.0} KB/s", "white");
            AppendStat("Out", $"{_outBwAvg.Value / 1024f:0.0} KB/s", "white");
            AppendStat("In Packets", stats.CompleteSnapshot.InPackets.ToString(), "cyan");
            AppendStat("Out Packets", stats.CompleteSnapshot.OutPackets.ToString(), "cyan");

            _sb.AppendLine();
            AppendHeader("=== Prediction ===");
            AppendStat("Forward Ticks", stats.CompleteSnapshot.ForwardTicks.ToString(), "white");
            AppendStat("Resims (Avg)", $"{_resimAvg.Value:0.0}", _resimAvg.Value < 1.0f ? "lime" : "yellow");
        }

        _sb.AppendLine();
        long gc = System.GC.GetTotalMemory(false) / 1024 / 1024;
        AppendStat("GC Memory", $"{gc} MB", gc < 500 ? "lime" : "red");

        string text = _sb.ToString();
        Vector2 size = _style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(Screen.width - size.x - 20, 20, size.x, size.y), text, _style);
    }

    private void AppendHeader(string text) => _sb.AppendLine($"<color=#00FFFF><b>{text}</b></color>");
    private void AppendStat(string name, string value, string color) =>
        _sb.AppendLine($"<color=white>{name}:</color> <color={color}>{value}</color>");

    private class MovingAverage
    {
        public float Value { get; private set; }
        private bool _init;
        public void Update(float n, float a)
        {
            Value = !_init ? n : (n * a) + (Value * (1f - a));
            _init = true;
        }
    }
}