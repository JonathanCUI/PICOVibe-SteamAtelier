using System;

namespace SwanAtelier
{
    // Uses elapsed wall time, not timeScale or the headset's compositor refresh rate.
    public sealed class SwanFrameRateCounter
    {
        const double WindowSeconds = .5;
        double previous = double.NaN, elapsed;
        int frames;
        public double Fps { get; private set; }
        public double Milliseconds => Fps > 0 ? 1000.0 / Fps : 0;
        public bool HasSample { get; private set; }
        public bool Sample(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (double.IsNaN(previous)) { previous = now; return false; }
            double delta = now - previous;
            if (delta <= 0) return false;
            previous = now; elapsed += delta; frames++;
            if (elapsed + 1e-9 < WindowSeconds) return false;
            Fps = frames / elapsed; HasSample = true; elapsed = 0; frames = 0; return true;
        }
        public void Reset() { previous = double.NaN; elapsed = 0; frames = 0; Fps = 0; HasSample = false; }
    }
}
