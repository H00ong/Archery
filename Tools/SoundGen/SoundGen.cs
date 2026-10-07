// Procedural acoustic/synth BGM and layered combat SFX for Archery.
// Usage: cd Tools\SoundGen && dotnet run SoundGen.cs -- ..\..\Assets\Resources\Sounds
using System;
using System.Collections.Generic;
using System.IO;

if (args.Length == 2 && args[0] == "--verify")
{
    Wav.VerifyAll(args[1]);
    return;
}

string root = args.Length > 0 ? args[0] : Path.Combine("Assets", "Resources", "Sounds");
string sfxDir = Path.Combine(root, "SFX");
string bgmDir = Path.Combine(root, "BGM");
Directory.CreateDirectory(sfxDir);
Directory.CreateDirectory(bgmDir);

Sfx.GenerateAll(sfxDir);
Music.GenerateAll(bgmDir);
Wav.VerifyAll(root);
Console.WriteLine("Done.");

// ─────────────────────────────────────────────────────────────
// I/O
// ─────────────────────────────────────────────────────────────
static class Wav
{
    public static void Write(string path, float[] data, int sr, double peakTarget = 0.89)
    {
        double peak = 0, sumSq = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (!float.IsFinite(data[i])) throw new InvalidDataException($"Non-finite sample in {path}");
            peak = Math.Max(peak, Math.Abs(data[i]));
            sumSq += data[i] * (double)data[i];
        }
        double g = peak > 0 ? peakTarget / peak : 1;
        double rmsDb = 20 * Math.Log10(Math.Sqrt(sumSq / Math.Max(1, data.Length)) * g + 1e-9);

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        int n = data.Length;
        bw.Write("RIFF"u8);
        bw.Write(36 + n * 2);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sr);
        bw.Write(sr * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data"u8);
        bw.Write(n * 2);
        foreach (var s in data)
            bw.Write((short)Math.Clamp((int)Math.Round(s * g * 32767), -32768, 32767));

        Console.WriteLine($"  {Path.GetFileName(path)}  {n / (double)sr:F2}s  rms {rmsDb:F1} dBFS");
    }

    public static void VerifyAll(string root)
    {
        string[] files = Directory.GetFiles(root, "*.wav", SearchOption.AllDirectories);
        if (files.Length != 22) throw new InvalidDataException($"Expected 22 WAVs, found {files.Length}");
        foreach (string path in files)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            string ReadTag() => new string(reader.ReadChars(4));
            if (ReadTag() != "RIFF" || reader.ReadInt32() != reader.BaseStream.Length - 8 || ReadTag() != "WAVE" ||
                ReadTag() != "fmt " || reader.ReadInt32() != 16 || reader.ReadInt16() != 1 || reader.ReadInt16() != 1)
                throw new InvalidDataException($"Invalid PCM WAV header: {path}");
            int sampleRate = reader.ReadInt32();
            if (sampleRate is not (32000 or 44100) || reader.ReadInt32() != sampleRate * 2 ||
                reader.ReadInt16() != 2 || reader.ReadInt16() != 16 || ReadTag() != "data")
                throw new InvalidDataException($"Invalid PCM WAV format: {path}");
            int bytes = reader.ReadInt32();
            if (bytes != reader.BaseStream.Length - 44 || bytes < sampleRate / 5 || bytes % 2 != 0)
                throw new InvalidDataException($"Invalid WAV duration or data size: {path}");
            int count = bytes / 2;
            double energy = 0, mean = 0, peak = 0;
            short first = 0, last = 0;
            for (int sample = 0; sample < count; sample++)
            {
                short value = reader.ReadInt16();
                if (sample == 0) first = value;
                last = value;
                peak = Math.Max(peak, Math.Abs((int)value));
                energy += value * (double)value;
                mean += value;
            }
            double rms = Math.Sqrt(energy / count) / 32768;
            if (peak >= 32760 || rms < 0.0001 || Math.Abs(mean / count / 32768) > 0.01)
                throw new InvalidDataException($"Clipping, silence or DC offset: {path}");
            bool isMusic = Path.GetFileName(Path.GetDirectoryName(path)) == "BGM";
            if (isMusic ? Math.Abs(first - last) > 2 : first != 0 || last != 0)
                throw new InvalidDataException($"Discontinuous WAV boundary: {path}");
            Console.WriteLine($"  PASS {Path.GetFileName(path)}: {count / (double)sampleRate:F2}s, peak {peak / 32768:F3}, rms {20 * Math.Log10(rms):F1} dBFS");
        }
        Console.WriteLine("Verified 22 PCM WAVs: valid headers, audible signal, no clipping/DC, clean boundaries.");
    }
}

// ─────────────────────────────────────────────────────────────
// DSP primitives
// ─────────────────────────────────────────────────────────────
enum Wave { Sine, Triangle, Square, Saw }

sealed class Osc
{
    double _phase;

    public Osc(double phase = 0) => _phase = phase;

    static double Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1; }
        if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
        return 0;
    }

    public double Next(double freq, int sr, Wave w, double pw = 0.5)
    {
        double dt = Math.Clamp(freq / sr, 1e-6, 0.49);
        double p = _phase;
        double v = w switch
        {
            Wave.Sine => Math.Sin(2 * Math.PI * p),
            Wave.Triangle => 1 - 4 * Math.Abs(p - 0.5),
            Wave.Saw => 2 * p - 1 - Blep(p, dt),
            Wave.Square => (p < pw ? 1 : -1) + Blep(p, dt) - Blep((p + 1 - pw) % 1, dt),
            _ => 0,
        };
        _phase += dt;
        if (_phase >= 1) _phase -= 1;
        return v;
    }
}

sealed class Svf
{
    double _low, _band;
    public double Lp, Bp, Hp;

    public void Process(double x, double cutoff, double q, int sr)
    {
        // 2x oversampling
        double f = 2 * Math.Sin(Math.PI * Math.Clamp(cutoff, 20, sr * 0.22) / (2.0 * sr));
        double damp = 1.0 / Math.Max(0.5, q);
        for (int k = 0; k < 2; k++)
        {
            _low += f * _band;
            double high = x - _low - damp * _band;
            _band += f * high;
            Hp = high;
        }
        Lp = _low;
        Bp = _band;
    }
}

sealed class OnePole    
{
    double _z;
    public double Lp(double x, double cutoff, int sr)
    {
        double a = 1 - Math.Exp(-2 * Math.PI * cutoff / sr);
        _z += a * (x - _z);
        return _z;
    }
    public double Hp(double x, double cutoff, int sr) => x - Lp(x, cutoff, sr);
}

sealed class Reverb
{
    sealed class Comb
    {
        readonly double[] _buf; int _i; double _store;
        public Comb(int n) => _buf = new double[n];
        public double Process(double x, double fb, double damp)
        {
            double o = _buf[_i];
            _store = o * (1 - damp) + _store * damp;
            _buf[_i] = x + _store * fb;
            if (++_i >= _buf.Length) _i = 0;
            return o;
        }
    }

    sealed class AllPass
    {
        readonly double[] _buf; int _i;
        public AllPass(int n) => _buf = new double[n];
        public double Process(double x)
        {
            double b = _buf[_i];
            _buf[_i] = x + b * 0.5;
            if (++_i >= _buf.Length) _i = 0;
            return b - x;
        }
    }

    readonly Comb[] _combs;
    readonly AllPass[] _aps;
    readonly double _fb, _damp;

    public Reverb(int sr, double room = 0.84, double damp = 0.25)
    {
        double s = sr / 44100.0;
        int[] ct = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        int[] at = { 556, 441, 341, 225 };
        _combs = Array.ConvertAll(ct, c => new Comb(Math.Max(1, (int)(c * s))));
        _aps = Array.ConvertAll(at, a => new AllPass(Math.Max(1, (int)(a * s))));
        _fb = room;
        _damp = damp;
    }

    public double Process(double x)
    {
        double input = x * 0.015;
        double o = 0;
        foreach (var c in _combs) o += c.Process(input, _fb, _damp);
        foreach (var a in _aps) o = a.Process(o);
        return o;
    }

    /// <summary> loop=true면 버퍼를 한 번 미리 돌려 꼬리가 루프 시작 부분에 자연스럽게 이어지게 한다. </summary>
    public static void Apply(float[] buf, int sr, double wet, double room = 0.84, double damp = 0.25, bool loop = false)
    {
        var rv = new Reverb(sr, room, damp);
        if (loop)
            foreach (var s in buf) rv.Process(s);
        for (int i = 0; i < buf.Length; i++)
            buf[i] = (float)(buf[i] + rv.Process(buf[i]) * wet * 3.0);
    }
}

static class Dsp
{
    public static double Adsr(double t, double dur, double a, double d, double s, double r)
    {
        if (t < 0) return 0;
        double env;
        if (t < a) env = t / Math.Max(a, 1e-5);
        else if (t < a + d) env = 1 - (1 - s) * ((t - a) / Math.Max(d, 1e-5));
        else env = s;

        if (t > dur)
        {
            double rt = t - dur;
            if (rt >= r) return 0;
            double atRelease = dur < a ? dur / Math.Max(a, 1e-5) : (dur < a + d ? 1 - (1 - s) * ((dur - a) / Math.Max(d, 1e-5)) : s);
            env = atRelease * (1 - rt / Math.Max(r, 1e-5));
        }
        return env;
    }

    public static double MidiToFreq(double midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    public static double ExpSweep(double from, double to, double t, double dur)
        => from * Math.Pow(to / from, Math.Clamp(t / dur, 0, 1));

    public static void Fades(float[] buf, int sr, double fadeIn = 0.002, double fadeOut = 0.02)
    {
        int fi = (int)(fadeIn * sr), fo = (int)(fadeOut * sr);
        for (int i = 0; i < fi && i < buf.Length; i++) buf[i] *= (float)(i / (double)fi);
        for (int i = 0; i < fo && i < buf.Length; i++) buf[buf.Length - 1 - i] *= (float)(i / (double)fo);
    }

    public static void SoftClip(float[] buf, double drive = 1.5)
    {
        double peak = 0;
        foreach (var s in buf) peak = Math.Max(peak, Math.Abs(s));
        if (peak <= 0) return;
        double norm = Math.Tanh(drive);
        for (int i = 0; i < buf.Length; i++)
            buf[i] = (float)(Math.Tanh(drive * buf[i] / peak) / norm);
    }
}

// ─────────────────────────────────────────────────────────────
// Instruments
// ─────────────────────────────────────────────────────────────
enum Timbre { Guitar, Harp, PizzBass, Marimba, Celesta, Glock, Kalimba, Flute, Ocarina, Strings, Choir, SynthPluck }

// Bright: 현 튕김/현악 배음 밝기(Hz), Decay: 잔향 길이 배율
record Inst(Timbre T, double A = 0.01, double D = 0.1, double S = 1, double R = 0.1,
    double Bright = 4000, double Decay = 1, double Vibrato = 0, double Gain = 1);

record Partial(double Ratio, double Amp, double Decay);

static class Modes
{
    public static readonly Partial[] Marimba = { new(1, 1, 0.5), new(3.93, 0.28, 0.1), new(9.15, 0.08, 0.035) };
    public static readonly Partial[] Celesta = { new(1, 1, 1.2), new(2.0, 0.2, 0.45), new(3.0, 0.06, 0.25), new(4.16, 0.04, 0.12) };
    public static readonly Partial[] Glock = { new(1, 1, 1.5), new(2.76, 0.3, 0.5), new(5.4, 0.14, 0.2), new(8.93, 0.06, 0.08) };
    public static readonly Partial[] Kalimba = { new(1, 1, 0.85), new(5.95, 0.2, 0.05), new(12.5, 0.06, 0.02) };
    public static readonly Partial[] Coin = { new(1, 0.8, 0.32), new(2.41, 0.6, 0.26), new(3.89, 0.42, 0.18), new(5.63, 0.28, 0.12), new(7.4, 0.12, 0.08) };
    public static readonly Partial[] Wood = { new(1, 1, 0.045), new(2.57, 0.3, 0.02), new(4.9, 0.1, 0.01) };
    public static readonly Partial[] Membrane = { new(1, 1, 1), new(1.59, 0.45, 0.55), new(2.14, 0.3, 0.4), new(2.65, 0.18, 0.28), new(3.16, 0.1, 0.18) };
}

static class Synth
{
    public static void Note(float[] buf, int sr, double start, double dur, double freq, double vel, Inst inst, Random rng)
    {
        double v = vel * inst.Gain;
        double keyScale = Math.Clamp(Math.Pow(440 / freq, 0.4), 0.4, 2.5);
        switch (inst.T)
        {
            case Timbre.SynthPluck:
                BrightPluck(buf, sr, start, dur, freq, v, inst);
                return;
            case Timbre.Guitar:
                Pluck(buf, sr, start, dur, freq, v, inst.Bright, 1.4 * inst.Decay * keyScale, inst.R, 0.18, rng);
                return;
            case Timbre.Harp:
                Pluck(buf, sr, start, dur, freq, v, inst.Bright, 2.2 * inst.Decay * keyScale, inst.R, 0.35, rng);
                return;
            case Timbre.PizzBass:
                Pluck(buf, sr, start, dur, freq, v, inst.Bright, 0.7 * inst.Decay * keyScale, inst.R, 0.25, rng);
                return;
            case Timbre.Marimba:
                Modal(buf, sr, start, dur, freq, v, Modes.Marimba, inst.Decay * keyScale, inst.R, 0.12, 2500, rng: rng);
                return;
            case Timbre.Celesta:
                Modal(buf, sr, start, dur, freq, v, Modes.Celesta, inst.Decay * keyScale, inst.R, 0.05, 4000, rng: rng);
                return;
            case Timbre.Glock:
                Modal(buf, sr, start, dur, freq, v, Modes.Glock, inst.Decay * keyScale, inst.R, 0.06, 6000, rng: rng);
                return;
            case Timbre.Kalimba:
                Modal(buf, sr, start, dur, freq, v, Modes.Kalimba, inst.Decay * keyScale, inst.R, 0.1, 3000, rng: rng);
                return;
        }
        Sustained(buf, sr, start, dur, freq, v, inst, rng);
    }

    static void BrightPluck(float[] buffer, int sampleRate, double start, double duration, double frequency, double gain, Inst instrument)
    {
        int offset = (int)(start * sampleRate);
        int length = (int)((duration + instrument.R) * sampleRate);
        var oscillator = new Osc();
        var body = new Osc();
        var filter = new Svf();
        for (int sample = 0; sample < length && offset + sample < buffer.Length; sample++)
        {
            double time = sample / (double)sampleRate;
            double envelope = Math.Min(1, time / 0.003) * Math.Exp(-time / (0.12 * instrument.Decay));
            double release = time > duration ? Math.Max(0, 1 - (time - duration) / Math.Max(0.001, instrument.R)) : 1;
            double cutoff = 650 + instrument.Bright * Math.Exp(-time / 0.045);
            filter.Process(oscillator.Next(frequency, sampleRate, Wave.Saw), cutoff, 0.65, sampleRate);
            double tone = filter.Lp * 0.35 + body.Next(frequency, sampleRate, Wave.Sine) * 0.65;
            buffer[offset + sample] += (float)(tone * envelope * release * gain);
        }
    }

    static void Sustained(float[] buf, int sr, double start, double dur, double freq, double vel, Inst inst, Random rng)
    {
        int s0 = (int)(start * sr);
        int total = (int)((dur + inst.R) * sr);
        const int voices = 3;
        double[] detune = { 0.9965, 1.0, 1.0035 };
        var phase = new double[voices];
        for (int k = 0; k < voices; k++) phase[k] = rng.NextDouble();
        var saws = new[] { new Osc(rng.NextDouble()), new Osc(rng.NextDouble()), new Osc(rng.NextDouble()) };
        var breath = new Svf();
        var f1 = new Svf();
        var f2 = new Svf();
        var f3 = new Svf();
        double vibRate = 4.8 + rng.NextDouble() * 0.8;
        int maxH = Math.Clamp((int)(sr * 0.4 / freq), 1, 14);

        for (int i = 0; i < total; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;

            double t = i / (double)sr;
            double env = Dsp.Adsr(t, dur, inst.A, inst.D, inst.S, inst.R);
            if (env <= 0 && t > dur) break;

            double f = freq * (1 + inst.Vibrato * Math.Sin(2 * Math.PI * vibRate * t) * Math.Min(1, t / 0.3));
            double s;

            switch (inst.T)
            {
                case Timbre.Flute:
                case Timbre.Ocarina:
                {
                    bool flute = inst.T == Timbre.Flute;
                    phase[0] = (phase[0] + f / sr) % 1;
                    double x = 2 * Math.PI * phase[0];
                    s = Math.Sin(x) + (flute ? 0.16 : 0.05) * Math.Sin(2 * x) + (flute ? 0.05 : 0.015) * Math.Sin(3 * x);
                    breath.Process(rng.NextDouble() * 2 - 1, Math.Min(f * 2, 5000), 1.2, sr);
                    s += breath.Bp * ((flute ? 0.08 : 0.04) + 0.5 * Math.Exp(-t / 0.04));
                    break;
                }
                case Timbre.Strings:
                {
                    s = 0;
                    for (int k = 0; k < voices; k++)
                    {
                        phase[k] = (phase[k] + f * detune[k] / sr) % 1;
                        double x = 2 * Math.PI * phase[k];
                        // sin(hx) 점화식으로 배음 합산 (1/h 기울기 + 밝기 롤오프)
                        double c2 = 2 * Math.Cos(x), sPrev = 0, sCur = Math.Sin(x);
                        for (int h = 1; h <= maxH; h++)
                        {
                            double hf = h * f / inst.Bright;
                            s += sCur / h / (1 + hf * hf);
                            double sNext = c2 * sCur - sPrev;
                            sPrev = sCur;
                            sCur = sNext;
                        }
                    }
                    s *= 0.45;
                    breath.Process(rng.NextDouble() * 2 - 1, Math.Min(f * 3, 6000), 0.8, sr);
                    s += breath.Bp * 0.04;
                    break;
                }
                case Timbre.Choir:
                {
                    double src = 0;
                    for (int k = 0; k < voices; k++) src += saws[k].Next(f * detune[k], sr, Wave.Saw);
                    src /= voices;
                    // "아-" 모음 포먼트
                    f1.Process(src, 750, 6, sr);
                    f2.Process(src, 1150, 7, sr);
                    f3.Process(src, 2800, 8, sr);
                    s = (f1.Bp / 6 + 0.5 * f2.Bp / 7 + 0.18 * f3.Bp / 8) * 2.5;
                    break;
                }
                default:
                    s = 0;
                    break;
            }

            buf[idx] += (float)(s * env * vel);
        }
    }

    /// <summary> Karplus-Strong 현 튕김 (올패스로 미세 튜닝). </summary>
    public static void Pluck(float[] buf, int sr, double start, double dur, double freq, double vel,
        double bright, double t60, double release, double pickPos, Random rng)
    {
        int s0 = (int)(start * sr);
        double delay = sr / freq - 0.5;
        int n = Math.Max(2, (int)Math.Floor(delay - 0.1));
        double frac = delay - n;
        double c = (1 - frac) / (1 + frac);

        var raw = new double[n];
        var lp = new OnePole();
        for (int i = 0; i < n; i++) raw[i] = lp.Lp(rng.NextDouble() * 2 - 1, bright, sr);
        var line = new double[n];
        int pick = Math.Max(1, (int)(n * pickPos));
        double mean = 0, peak = 1e-9;
        for (int i = 0; i < n; i++) { line[i] = raw[i] - raw[(i + pick) % n]; mean += line[i]; }
        mean /= n;
        for (int i = 0; i < n; i++) { line[i] -= mean; peak = Math.Max(peak, Math.Abs(line[i])); }
        for (int i = 0; i < n; i++) line[i] /= peak;

        double loss = Math.Pow(0.001, 1.0 / Math.Max(0.05, t60 * freq));
        int total = (int)(Math.Min(dur + release, t60 * 1.2) * sr);
        double prev = 0, apX = 0, apY = 0;
        int p = 0;
        for (int i = 0; i < total; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;
            double y = line[p];
            double avg = 0.5 * (y + prev) * loss;
            prev = y;
            double ap = c * avg + apX - c * apY;
            apX = avg;
            apY = ap;
            line[p] = ap;
            if (++p >= n) p = 0;

            double t = i / (double)sr;
            double rel = t > dur ? Math.Max(0, 1 - (t - dur) / Math.Max(release, 1e-3)) : 1;
            double tail = i > total - 200 ? (total - i) / 200.0 : 1;
            buf[idx] += (float)(y * vel * rel * tail * Math.Min(1, t / 0.0015));
        }
    }

    /// <summary> 모달 합성 (마림바·셀레스타·글로켄·칼림바·북·나무·동전). </summary>
    public static void Modal(float[] buf, int sr, double start, double dur, double freq, double vel, Partial[] parts,
        double decayScale = 1, double release = 0.1, double click = 0.1, double clickCutoff = 3000,
        double pitchDrop = 0, double pitchDropTime = 0.03, Random? rng = null)
    {
        int s0 = (int)(start * sr);
        double longest = 0;
        foreach (var pt in parts) longest = Math.Max(longest, pt.Decay * decayScale);
        int total = (int)(Math.Min(dur + release, longest * 7) * sr);
        var phase = new double[parts.Length];
        var lp = new OnePole();
        double nyq = sr * 0.45;

        for (int i = 0; i < total; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;
            double t = i / (double)sr;
            double bend = 1 + pitchDrop * Math.Exp(-t / pitchDropTime);
            double v = 0;
            for (int k = 0; k < parts.Length; k++)
            {
                double f = freq * parts[k].Ratio * bend;
                if (f > nyq) continue;
                phase[k] += f / sr;
                if (phase[k] >= 1) phase[k] -= 1;
                v += Math.Sin(2 * Math.PI * phase[k]) * parts[k].Amp * Math.Exp(-t / (parts[k].Decay * decayScale));
            }
            if (click > 0 && rng != null && t < 0.03)
                v += lp.Lp(rng.NextDouble() * 2 - 1, clickCutoff, sr) * click * Math.Exp(-t / 0.005);

            double atk = Math.Min(1, t / 0.0015);
            double rel = t > dur ? Math.Max(0, 1 - (t - dur) / Math.Max(release, 1e-3)) : 1;
            buf[idx] += (float)(v * vel * atk * rel);
        }
    }

    // ── Acoustic percussion ──
    public static void SoftKick(float[] buf, int sr, double start, double vel, Random rng)
    {
        int s0 = (int)(start * sr);
        int len = (int)(0.45 * sr);
        double phase = 0;
        var lp = new OnePole();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)sr;
            phase += (50 + 50 * Math.Exp(-t / 0.025)) / sr;
            double v = Math.Sin(2 * Math.PI * phase) * Math.Exp(-t / 0.2);
            v += lp.Lp(rng.NextDouble() * 2 - 1, 400, sr) * Math.Exp(-t / 0.015) * 0.6;
            buf[s0 + i] += (float)(v * vel);
        }
    }

    public static void FrameDrum(float[] buf, int sr, double start, double vel, Random rng, double freq = 150, double decay = 0.22, double slap = 0.5)
    {
        Modal(buf, sr, start, 2, freq, vel, Modes.Membrane, decay, 0.1, 0, pitchDrop: 0.18, pitchDropTime: 0.02);
        int s0 = (int)(start * sr);
        int len = (int)(0.12 * sr);
        var svf = new Svf();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)sr;
            svf.Process(rng.NextDouble() * 2 - 1, 1600, 0.9, sr);
            buf[s0 + i] += (float)(svf.Bp * Math.Exp(-t / 0.025) * slap * vel);
        }
    }

    public static void Taiko(float[] buf, int sr, double start, double vel, Random rng, double freq = 70)
        => FrameDrum(buf, sr, start, vel, rng, freq, 0.55, 0.35);

    public static void Tambourine(float[] buf, int sr, double start, double vel, Random rng)
    {
        int s0 = (int)(start * sr);
        int len = (int)(0.18 * sr);
        var svf = new Svf();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)sr;
            double env = Math.Exp(-t / 0.05) + (t > 0.012 ? 0.6 * Math.Exp(-(t - 0.012) / 0.06) : 0);
            svf.Process(rng.NextDouble() * 2 - 1, 8200, 2.5, sr);
            buf[s0 + i] += (float)(svf.Bp / 2.5 * env * vel);
        }
    }

    public static void WoodBlock(float[] buf, int sr, double start, double vel, Random rng, double freq = 950)
        => Modal(buf, sr, start, 0.2, freq, vel, Modes.Wood, 1, 0.05, 0.3, 4000, rng: rng);

    public static void Shaker(float[] buf, int sr, double start, double vel, Random rng)
    {
        int s0 = (int)(start * sr);
        int len = (int)(0.09 * sr);
        var svf = new Svf();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)sr;
            double env = Math.Min(1, t / 0.012) * Math.Exp(-t / 0.035);
            svf.Process(rng.NextDouble() * 2 - 1, 5500, 1.5, sr);
            buf[s0 + i] += (float)(svf.Bp * env * vel);
        }
    }
}

// ─────────────────────────────────────────────────────────────
// Music
// ─────────────────────────────────────────────────────────────
enum BassStyle { None, Whole, Quarter, RootFifth, Eighths, Syncopated }
enum DrumStyle { None, Soft, Folk, Tribal, Taiko, HalfTime, Sparse }
enum ArpStyle { None, Up8, UpDown16, Broken8 }

record Song(
    string Name, int Bpm, int Root, int[] Scale, int[] Chords, int Bars,
    Inst Lead, Inst Bass, Inst Pad, Inst Arp,
    BassStyle BassStyle, DrumStyle Drums, ArpStyle ArpStyle,
    double LeadDensity, double ReverbWet, int Seed,
    double LeadVol = 0.24, double BassVol = 0.32, double PadVol = 0.12, double ArpVol = 0.1, double DrumVol = 1.0,
    bool LeadInIntro = false);

static class Music
{
    const int Sr = 32000;

    static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
    static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };
    static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
    static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
    static readonly int[] PhrygianDominant = { 0, 1, 4, 5, 7, 8, 10 };

    public static void GenerateAll(string dir)
    {
        var songs = new[]
        {
            new Song("Loading", 108, 60, Lydian, new[] { 0, 3, 5, 4 }, 8,
                Lead: new Inst(Timbre.Celesta, R: 0.25, Decay: 0.55),
                Bass: new Inst(Timbre.PizzBass, R: 0.12, Bright: 1100),
                Pad: new Inst(Timbre.Strings, 0.3, 0.4, 0.5, 0.4, Bright: 900),
                Arp: new Inst(Timbre.SynthPluck, R: 0.12, Bright: 2700),
                BassStyle.RootFifth, DrumStyle.Soft, ArpStyle.Broken8,
                LeadDensity: 0.5, ReverbWet: 0.18, Seed: 111,
                LeadVol: 0.09, BassVol: 0.23, PadVol: 0.025, ArpVol: 0.11, DrumVol: 0.45, LeadInIntro: true),

            new Song("Lobby", 118, 60, Major, new[] { 0, 4, 5, 3 }, 16,
                Lead: new Inst(Timbre.Marimba, R: 0.16, Decay: 0.6),
                Bass: new Inst(Timbre.PizzBass, R: 0.12, Bright: 1400, Gain: 1.5),
                Pad: new Inst(Timbre.Strings, 0.2, 0.3, 0.55, 0.3, Bright: 1300),
                Arp: new Inst(Timbre.SynthPluck, R: 0.12, Bright: 3700),
                BassStyle.Syncopated, DrumStyle.Folk, ArpStyle.Broken8,
                LeadDensity: 0.65, ReverbWet: 0.16, Seed: 121,
                LeadVol: 0.11, BassVol: 0.26, PadVol: 0.03, ArpVol: 0.15, DrumVol: 0.7, LeadInIntro: true),

            new Song("Map_Dungeon", 126, 62, Dorian, new[] { 0, 3, 6, 4 }, 16,
                Lead: new Inst(Timbre.Kalimba, R: 0.15, Decay: 0.6),
                Bass: new Inst(Timbre.PizzBass, R: 0.1, Bright: 1300, Gain: 1.6),
                Pad: new Inst(Timbre.Strings, 0.2, 0.4, 0.6, 0.4, Bright: 1100),
                Arp: new Inst(Timbre.SynthPluck, R: 0.1, Bright: 3200),
                BassStyle.Syncopated, DrumStyle.Folk, ArpStyle.Broken8,
                LeadDensity: 0.6, ReverbWet: 0.2, Seed: 131,
                LeadVol: 0.1, BassVol: 0.29, PadVol: 0.035, ArpVol: 0.15, DrumVol: 0.8, LeadInIntro: true),

            new Song("Map_Desert", 122, 64, PhrygianDominant, new[] { 0, 1, 0, 6 }, 16,
                Lead: new Inst(Timbre.Guitar, R: 0.18, Bright: 4200, Gain: 1.3),
                Bass: new Inst(Timbre.PizzBass, R: 0.1, Bright: 1500, Gain: 1.5),
                Pad: new Inst(Timbre.Strings, 0.25, 0.4, 0.5, 0.3, Bright: 1000),
                Arp: new Inst(Timbre.SynthPluck, R: 0.12, Bright: 3500),
                BassStyle.Syncopated, DrumStyle.Tribal, ArpStyle.Broken8,
                LeadDensity: 0.65, ReverbWet: 0.16, Seed: 141,
                LeadVol: 0.13, BassVol: 0.26, PadVol: 0.025, ArpVol: 0.13, DrumVol: 0.65, LeadInIntro: true),

            new Song("Map_Lava", 134, 57, Minor, new[] { 0, 5, 2, 6 }, 16,
                Lead: new Inst(Timbre.Marimba, R: 0.1, Decay: 0.6),
                Bass: new Inst(Timbre.PizzBass, R: 0.08, Bright: 1600, Gain: 1.7),
                Pad: new Inst(Timbre.Strings, 0.1, 0.2, 0.5, 0.25, Bright: 1500),
                Arp: new Inst(Timbre.SynthPluck, R: 0.09, Bright: 4300, Decay: 0.8),
                BassStyle.Eighths, DrumStyle.Taiko, ArpStyle.UpDown16,
                LeadDensity: 0.65, ReverbWet: 0.14, Seed: 151,
                LeadVol: 0.1, BassVol: 0.28, PadVol: 0.025, ArpVol: 0.12, DrumVol: 0.6, LeadInIntro: true),

            new Song("Map_Winter", 116, 64, Major, new[] { 0, 5, 3, 4 }, 16,
                Lead: new Inst(Timbre.Celesta, R: 0.25, Decay: 0.5),
                Bass: new Inst(Timbre.PizzBass, R: 0.12, Bright: 1200, Gain: 1.4),
                Pad: new Inst(Timbre.Strings, 0.3, 0.4, 0.5, 0.4, Bright: 1000),
                Arp: new Inst(Timbre.SynthPluck, R: 0.14, Bright: 2600),
                BassStyle.RootFifth, DrumStyle.Folk, ArpStyle.Broken8,
                LeadDensity: 0.55, ReverbWet: 0.22, Seed: 161,
                LeadVol: 0.09, BassVol: 0.25, PadVol: 0.03, ArpVol: 0.13, DrumVol: 0.55, LeadInIntro: true),

            new Song("Map_Forest", 120, 62, Dorian, new[] { 0, 3, 0, 6 }, 16,
                Lead: new Inst(Timbre.Flute, 0.025, 0.1, 0.6, 0.09, Vibrato: 0.004),
                Bass: new Inst(Timbre.PizzBass, R: 0.12, Bright: 1300, Gain: 1.5),
                Pad: new Inst(Timbre.Strings, 0.2, 0.4, 0.5, 0.3, Bright: 1200),
                Arp: new Inst(Timbre.SynthPluck, R: 0.12, Bright: 3200),
                BassStyle.Syncopated, DrumStyle.Folk, ArpStyle.Broken8,
                LeadDensity: 0.55, ReverbWet: 0.18, Seed: 171,
                LeadVol: 0.08, BassVol: 0.27, PadVol: 0.025, ArpVol: 0.14, DrumVol: 0.65, LeadInIntro: true),

            new Song("Map_Space", 124, 61, Lydian, new[] { 0, 4, 3, 4 }, 16,
                Lead: new Inst(Timbre.Celesta, R: 0.2, Decay: 0.5),
                Bass: new Inst(Timbre.PizzBass, R: 0.12, Bright: 1000, Gain: 1.5),
                Pad: new Inst(Timbre.Strings, 0.3, 0.4, 0.5, 0.4, Bright: 900),
                Arp: new Inst(Timbre.SynthPluck, R: 0.15, Bright: 3800),
                BassStyle.Syncopated, DrumStyle.Folk, ArpStyle.Up8,
                LeadDensity: 0.5, ReverbWet: 0.24, Seed: 181,
                LeadVol: 0.09, BassVol: 0.27, PadVol: 0.03, ArpVol: 0.16, DrumVol: 0.7, LeadInIntro: true),
        };

        foreach (var song in songs)
            Wav.Write(Path.Combine(dir, song.Name + ".wav"), Render(song), Sr, 0.7);
    }

    static int DegreeToMidi(int root, int[] scale, int degree)
    {
        int oct = (int)Math.Floor(degree / 7.0);
        int idx = ((degree % 7) + 7) % 7;
        return root + scale[idx] + 12 * oct;
    }

    static float[] Render(Song s)
    {
        var rng = new Random(s.Seed);
        double beat = 60.0 / s.Bpm;
        double bar = beat * 4;
        int loopLen = (int)Math.Round(s.Bars * bar * Sr);
        var buf = new float[loopLen + Sr * 4];

        int ChordAt(int b) => s.Chords[b % s.Chords.Length];

        for (int b = 0; b < s.Bars; b++)
        {
            double t0 = b * bar;
            int chord = ChordAt(b);
            int[] tones = { chord, chord + 2, chord + 4 };

            var guitar = new Inst(Timbre.Guitar, R: 0.13, Bright: 4200, Decay: 0.7);
            double[] strums = { 0, 0.75, 1.5, 2, 2.75, 3.5 };
            for (int stroke = 0; stroke < strums.Length; stroke++)
            {
                bool downstroke = stroke % 2 == 0;
                int[] voicing = { chord - 7, chord, chord + 2, chord + 4 };
                for (int course = 0; course < voicing.Length; course++)
                {
                    int degree = voicing[downstroke ? course : voicing.Length - 1 - course];
                    double start = t0 + strums[stroke] * beat + course * 0.007;
                    double velocity = (downstroke ? 0.2 : 0.13) * (0.9 + rng.NextDouble() * 0.1);
                    Synth.Note(buf, Sr, start, beat * (downstroke ? 0.42 : 0.24),
                        Dsp.MidiToFreq(DegreeToMidi(s.Root, s.Scale, degree)), velocity, guitar, rng);
                }
            }

            // Pad
            foreach (var d in tones)
                Synth.Note(buf, Sr, t0, bar * 0.98, Dsp.MidiToFreq(DegreeToMidi(s.Root, s.Scale, d)), s.PadVol, s.Pad, rng);

            // Bass
            double bassF(int deg) => Dsp.MidiToFreq(DegreeToMidi(s.Root - 24, s.Scale, deg));
            switch (s.BassStyle)
            {
                case BassStyle.Whole:
                    Synth.Note(buf, Sr, t0, bar * 0.95, bassF(chord), s.BassVol, s.Bass, rng);
                    break;
                case BassStyle.Quarter:
                    for (int q = 0; q < 4; q++) Synth.Note(buf, Sr, t0 + q * beat, beat * 0.8, bassF(chord), s.BassVol, s.Bass, rng);
                    break;
                case BassStyle.RootFifth:
                    for (int q = 0; q < 4; q++)
                        Synth.Note(buf, Sr, t0 + q * beat, beat * 0.8, bassF(q % 2 == 0 ? chord : chord + 4), s.BassVol, s.Bass, rng);
                    break;
                case BassStyle.Eighths:
                    for (int e = 0; e < 8; e++)
                        Synth.Note(buf, Sr, t0 + e * beat / 2, beat * 0.4, bassF(e == 6 ? chord + 7 : chord), s.BassVol * (e % 2 == 0 ? 1 : 0.8), s.Bass, rng);
                    break;
                case BassStyle.Syncopated:
                    foreach (var (pos, len, deg) in new[] { (0.0, 1.2, 0), (1.5, 0.4, 0), (2.0, 0.8, 4), (3.0, 0.4, 7), (3.5, 0.4, 4) })
                        Synth.Note(buf, Sr, t0 + pos * beat, len * beat, bassF(chord + deg), s.BassVol, s.Bass, rng);
                    break;
            }

            // Arp
            if (s.ArpStyle != ArpStyle.None)
            {
                int[] arpDegs = { chord + 7, chord + 9, chord + 11, chord + 14 };
                int steps = s.ArpStyle == ArpStyle.UpDown16 ? 16 : 8;
                double stepLen = bar / steps;
                int[] order = s.ArpStyle switch
                {
                    ArpStyle.UpDown16 => new[] { 0, 1, 2, 3, 2, 1 },
                    ArpStyle.Broken8 => new[] { 0, 2, 1, 3 },
                    _ => new[] { 0, 1, 2, 3 },
                };
                for (int k = 0; k < steps; k++)
                {
                    int deg = arpDegs[order[k % order.Length]];
                    Synth.Note(buf, Sr, t0 + k * stepLen, stepLen * 0.7, Dsp.MidiToFreq(DegreeToMidi(s.Root - 12, s.Scale, deg)), s.ArpVol * (k % 4 == 0 ? 1 : 0.75), s.Arp, rng);
                }
            }

            RenderDrums(buf, s, t0, beat, b, rng);
        }

        // Lead: intro(4) - A(4) - B(4) - A'(4), 8마디 곡은 A - B
        int phraseBars = 4;
        var chordsA = new int[phraseBars];
        for (int i = 0; i < phraseBars; i++) chordsA[i] = ChordAt(i);
        var phraseA = MakePhrase(rng, chordsA, s.LeadDensity);
        var phraseB = MakePhrase(rng, chordsA, s.LeadDensity);
        var phraseA2 = MakePhrase(rng, chordsA, s.LeadDensity);
        phraseA2 = phraseA.FindAll(n => n.beat < 16).Concat2(phraseA2.FindAll(n => n.beat >= 16));

        var sections = s.Bars >= 16
            ? new List<(int startBar, List<(double beat, double len, int deg)> phrase)> { (4, phraseA), (8, phraseB), (12, phraseA2) }
            : new List<(int startBar, List<(double beat, double len, int deg)> phrase)> { (0, phraseA), (4, phraseB) };
        if (s.Bars >= 16 && s.LeadInIntro) sections.Insert(0, (0, phraseB));

        foreach (var (startBar, phrase) in sections)
        {
            foreach (var (nb, len, deg) in phrase)
            {
                double start = startBar * bar + nb * beat / 2;
                double vel = s.LeadVol * (nb % 2 == 0 ? 1 : 0.85);
                Synth.Note(buf, Sr, start, len * beat / 2 * 0.92, Dsp.MidiToFreq(DegreeToMidi(s.Root, s.Scale, deg)), vel, s.Lead, rng);
            }
        }

        // 루프 길이를 넘어간 꼬리를 앞쪽으로 접어 넣어 끊김 없이 반복되게 한다.
        var loop = new float[loopLen];
        for (int i = 0; i < buf.Length; i++) loop[i % loopLen] += buf[i];

        Reverb.Apply(loop, Sr, s.ReverbWet, 0.86, 0.3, loop: true);
        int joinSamples = (int)(Sr * 0.003);
        double joinValue = (loop[0] + loop[^1]) * 0.5;
        double headOffset = joinValue - loop[0];
        double tailOffset = joinValue - loop[^1];
        for (int sample = 0; sample < joinSamples; sample++)
        {
            double weight = 0.5 + 0.5 * Math.Cos(Math.PI * sample / (joinSamples - 1));
            loop[sample] += (float)(headOffset * weight);
            loop[loop.Length - 1 - sample] += (float)(tailOffset * weight);
        }
        return loop;
    }

    static List<(double beat, double len, int deg)> Concat2(this List<(double beat, double len, int deg)> a, List<(double beat, double len, int deg)> b)
    {
        var r = new List<(double beat, double len, int deg)>(a);
        r.AddRange(b);
        return r;
    }

    // beat 단위는 8분음표(한 마디 = 8)
    static List<(double beat, double len, int deg)> MakePhrase(Random rng, int[] chords, double density)
    {
        int[][] rhythms =
        {
            new[] { 2, 2, 2, 2 }, new[] { 1, 1, 2, 2, 2 }, new[] { 3, 1, 2, 2 }, new[] { 2, 1, 1, 4 },
            new[] { 4, 2, 2 }, new[] { 2, 2, 4 }, new[] { 1, 1, 1, 1, 2, 2 }, new[] { 3, 3, 2 }, new[] { 6, 2 },
        };

        var notes = new List<(double beat, double len, int deg)>();
        int cur = chords[0] + 7;

        for (int b = 0; b < chords.Length; b++)
        {
            bool last = b == chords.Length - 1;
            int[] rhythm = last ? new[] { 2, 6 } : rhythms[rng.Next(rhythms.Length)];
            int pos = 0;
            for (int k = 0; k < rhythm.Length; k++)
            {
                int len = rhythm[k];
                double nb = b * 8 + pos;
                pos += len;

                if (k > 0 && rng.NextDouble() > density) continue;

                int chord = chords[b];
                if (last && k == rhythm.Length - 1)
                {
                    cur = NearestChordTone(cur, chord, rng, rootOnly: true);
                }
                else if (nb % 2 == 0)
                {
                    cur = NearestChordTone(cur, chord, rng, rootOnly: false);
                }
                else
                {
                    cur += rng.Next(3) - 1;
                }

                cur = Math.Clamp(cur, 4, 13);
                notes.Add((nb, len, cur));
            }
        }
        return notes;
    }

    static int NearestChordTone(int cur, int chord, Random rng, bool rootOnly)
    {
        var candidates = new List<int>();
        for (int oct = 0; oct <= 2; oct++)
        {
            candidates.Add(chord + oct * 7);
            if (!rootOnly)
            {
                candidates.Add(chord + 2 + oct * 7);
                candidates.Add(chord + 4 + oct * 7);
            }
        }
        candidates.RemoveAll(c => c < 4 || c > 13);
        if (candidates.Count == 0) return cur;
        candidates.Sort((a, b) => Math.Abs(a - cur).CompareTo(Math.Abs(b - cur)));
        int pick = candidates.Count > 1 && rng.NextDouble() < 0.35 ? 1 : 0;
        return candidates[pick];
    }

    static void RenderDrums(float[] buf, Song s, double t0, double beat, int barIndex, Random rng)
    {
        double v = s.DrumVol;
        bool fillBar = barIndex == s.Bars - 1;
        switch (s.Drums)
        {
            case DrumStyle.None:
                return;
            case DrumStyle.Soft:
                Synth.SoftKick(buf, Sr, t0, 0.35 * v, rng);
                Synth.SoftKick(buf, Sr, t0 + 2.5 * beat, 0.25 * v, rng);
                for (int e = 0; e < 8; e++) Synth.Shaker(buf, Sr, t0 + e * beat / 2, (e % 2 == 0 ? 0.12 : 0.08) * v, rng);
                break;
            case DrumStyle.Folk:
                Synth.SoftKick(buf, Sr, t0, 0.5 * v, rng);
                Synth.SoftKick(buf, Sr, t0 + 2 * beat, 0.45 * v, rng);
                Synth.FrameDrum(buf, Sr, t0 + beat, 0.3 * v, rng, 210, 0.12, 0.9);
                Synth.FrameDrum(buf, Sr, t0 + 3 * beat, 0.3 * v, rng, 210, 0.12, 0.9);
                for (int e = 0; e < 8; e++) Synth.Tambourine(buf, Sr, t0 + e * beat / 2, (e % 2 == 1 ? 0.1 : 0.05) * v, rng);
                break;
            case DrumStyle.HalfTime:
                Synth.SoftKick(buf, Sr, t0, 0.55 * v, rng);
                Synth.SoftKick(buf, Sr, t0 + 1.5 * beat, 0.35 * v, rng);
                Synth.FrameDrum(buf, Sr, t0 + 2 * beat, 0.35 * v, rng, 180, 0.18, 0.8);
                for (int e = 0; e < 8; e++) Synth.Shaker(buf, Sr, t0 + e * beat / 2, 0.08 * v, rng);
                break;
            case DrumStyle.Tribal:
                Synth.FrameDrum(buf, Sr, t0, 0.45 * v, rng, 110);
                Synth.FrameDrum(buf, Sr, t0 + 1.5 * beat, 0.3 * v, rng, 160, 0.15, 0.8);
                Synth.FrameDrum(buf, Sr, t0 + 2 * beat, 0.4 * v, rng, 110);
                Synth.FrameDrum(buf, Sr, t0 + 3 * beat, 0.3 * v, rng, 85);
                Synth.SoftKick(buf, Sr, t0, 0.35 * v, rng);
                for (int x = 0; x < 16; x++) Synth.Shaker(buf, Sr, t0 + x * beat / 4, (x % 4 == 0 ? 0.14 : 0.07) * v, rng);
                break;
            case DrumStyle.Taiko:
                Synth.Taiko(buf, Sr, t0, 0.6 * v, rng);
                Synth.Taiko(buf, Sr, t0 + 1.5 * beat, 0.4 * v, rng, 80);
                Synth.Taiko(buf, Sr, t0 + 2 * beat, 0.55 * v, rng);
                Synth.FrameDrum(buf, Sr, t0 + beat, 0.3 * v, rng, 200, 0.12, 0.9);
                Synth.FrameDrum(buf, Sr, t0 + 3 * beat, 0.3 * v, rng, 200, 0.12, 0.9);
                for (int x = 0; x < 16; x++) Synth.Shaker(buf, Sr, t0 + x * beat / 4, (x % 4 == 2 ? 0.12 : 0.06) * v, rng);
                break;
            case DrumStyle.Sparse:
                Synth.SoftKick(buf, Sr, t0, 0.3 * v, rng);
                Synth.Tambourine(buf, Sr, t0 + 2 * beat, 0.06 * v, rng);
                break;
        }

        if (fillBar && s.Drums is not (DrumStyle.Soft or DrumStyle.Sparse))
            for (int x = 0; x < 4; x++)
                Synth.FrameDrum(buf, Sr, t0 + 3 * beat + x * beat / 4, (0.15 + 0.05 * x) * v, rng, 190 - 15 * x, 0.12, 0.8);
    }
}

// ─────────────────────────────────────────────────────────────
// SFX
// ─────────────────────────────────────────────────────────────
static class Sfx
{
    const int Sr = 44100;

    public static void GenerateAll(string dir)
    {
        var rng = new Random(207);
        var map = new (string name, Func<Random, float[]> gen)[]
        {
            ("PlayerShoot", PlayerShoot),
            ("LevelUp", LevelUp),
            ("BarrelPickup", BarrelPickup),
            ("MeteorImpact", MeteorImpact),
            ("PlayerHit", PlayerHit),
            ("PlayerDie", PlayerDie),
            ("EnemyMelee", EnemyMelee),
            ("EnemyShoot", EnemyShoot),
            ("EnemyFlyingShoot", EnemyFlyingShoot),
            ("EnemyHit", EnemyHit),
            ("EnemyDie", EnemyDie),
            ("BossDie", BossDie),
            ("GoldPickup", GoldPickup),
            ("ExpPickup", ExpPickup),
        };

        foreach (var (name, gen) in map)
        {
            var data = gen(rng);
            Dsp.Fades(data, Sr, 0.0008, 0.018);
            double peak = name switch
            {
                "ExpPickup" => 0.38,
                "GoldPickup" => 0.5,
                "EnemyShoot" or "EnemyMelee" or "EnemyFlyingShoot" => 0.7,
                "LevelUp" or "BarrelPickup" or "BossDie" or "PlayerDie" => 0.75,
                _ => 0.85,
            };
            Wav.Write(Path.Combine(dir, name + ".wav"), data, Sr, peak);
        }
    }

    static float[] Buf(double sec) => new float[(int)(sec * Sr)];
    static double Noise(Random r) => r.NextDouble() * 2 - 1;

    static readonly Inst Harp = new(Timbre.Harp, R: 0.4, Bright: 3500);
    static readonly Inst Celesta = new(Timbre.Celesta, R: 0.4);
    static readonly Inst Glock = new(Timbre.Glock, R: 0.4, Decay: 0.6);
    static readonly Inst Kalimba = new(Timbre.Kalimba, R: 0.3);

    /// <summary> 대역 필터 노이즈 스윗 (바람 가르는 소리). </summary>
    static void Whoosh(float[] buf, Random rng, double start, double dur, double fFrom, double fTo, double gain, double q = 1.2)
    {
        int s0 = (int)(start * Sr);
        int len = (int)(dur * Sr);
        var svf = new Svf();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)Sr;
            double env = Math.Sin(Math.PI * t / dur);
            svf.Process(Noise(rng), Dsp.ExpSweep(fFrom, fTo, t, dur), q, Sr);
            buf[s0 + i] += (float)(svf.Bp * env * env * gain);
        }
    }

    /// <summary> 펜타토닉 셀레스타 반짝임. lowMidi는 C 음이어야 한다. </summary>
    static void Sparkle(float[] buf, Random rng, double start, double spread, int count, int lowMidi, int octaves, double vel)
    {
        int[] penta = { 0, 2, 4, 7, 9 };
        for (int k = 0; k < count; k++)
        {
            int m = lowMidi + 12 * rng.Next(octaves) + penta[rng.Next(penta.Length)];
            Synth.Modal(buf, Sr, start + rng.NextDouble() * spread, 0.6, Dsp.MidiToFreq(m), vel * (0.6 + 0.4 * rng.NextDouble()),
                Modes.Celesta, 0.35, 0.2, 0.02, 6000, rng: rng);
        }
    }

    /// <summary> 사인 + 숨소리로 만든 슬라이드 휘슬. </summary>
    static void SlideWhistle(float[] buf, Random rng, double start, double dur, Func<double, double> freqAt, double gain)
    {
        int s0 = (int)(start * Sr);
        int len = (int)(dur * Sr);
        double phase = 0;
        var breath = new Svf();
        for (int i = 0; i < len && s0 + i < buf.Length; i++)
        {
            double t = i / (double)Sr;
            double f = freqAt(t);
            phase += f / Sr;
            breath.Process(Noise(rng), f * 2, 1.5, Sr);
            double env = Math.Min(1, t / 0.015) * Math.Min(1, (dur - t) / 0.05);
            buf[s0 + i] += (float)((Math.Sin(2 * Math.PI * phase) + breath.Bp * 0.12) * env * gain);
        }
    }

    static float[] PlayerShoot(Random rng)
    {
        var buf = Buf(0.23);
        Synth.Pluck(buf, Sr, 0, 0.12, 185, 0.65, 5200, 0.19, 0.04, 0.16, rng);
        Transient(buf, rng, 0, 0.38, 4200, 0.009);
        Thump(buf, 0.003, 100, 0.55, 0.033);
        Whoosh(buf, rng, 0.006, 0.105, 4800, 1200, 0.6, 0.85);
        MagicSweep(buf, rng, 0.012, 0.12, 440, 0.1);
        return buf;
    }

    static void MagicSweep(float[] buffer, Random random, double start, double duration, double frequency, double gain)
    {
        int offset = (int)(start * Sr);
        var fundamental = new Osc();
        var fifth = new Osc();
        var filter = new Svf();
        for (int sample = 0; sample < duration * Sr && offset + sample < buffer.Length; sample++)
        {
            double time = sample / (double)Sr;
            double progress = time / duration;
            double pitch = frequency * (1 + 0.6 * progress);
            double envelope = Math.Min(1, time / 0.004) * Math.Exp(-time / (duration * 0.22)) * (1 - progress);
            double tone = fundamental.Next(pitch, Sr, Wave.Saw) * 0.65 + fifth.Next(pitch * 1.5, Sr, Wave.Saw) * 0.35;
            filter.Process(tone, 1200 + 3000 * progress, 0.7, Sr);
            buffer[offset + sample] += (float)(filter.Lp * envelope * gain);
        }
        Whoosh(buffer, random, start, duration, 1100, 4800, gain * 0.8, 0.8);
    }

    static void Transient(float[] buffer, Random random, double start, double gain, double cutoff, double decay)
    {
        int offset = (int)(start * Sr);
        var lowPass = new OnePole();
        var highPass = new OnePole();
        for (int sample = 0; sample < decay * 7 * Sr && offset + sample < buffer.Length; sample++)
        {
            double time = sample / (double)Sr;
            double noise = highPass.Hp(lowPass.Lp(Noise(random), cutoff, Sr), 450, Sr);
            double envelope = Math.Min(1, time / 0.001) * Math.Exp(-time / decay);
            buffer[offset + sample] += (float)(noise * envelope * gain);
        }
    }

    static void Thump(float[] buffer, double start, double frequency, double gain, double decay)
    {
        int offset = (int)(start * Sr);
        double phase = 0;
        for (int sample = 0; sample < decay * 7 * Sr && offset + sample < buffer.Length; sample++)
        {
            double time = sample / (double)Sr;
            phase += frequency * (1 + 0.65 * Math.Exp(-time / 0.012)) / Sr;
            double envelope = Math.Min(1, time / 0.0015) * Math.Exp(-time / decay);
            buffer[offset + sample] += (float)(Math.Sin(2 * Math.PI * phase) * envelope * gain);
        }
    }

    static float[] LevelUp(Random rng)
    {
        var buf = Buf(1.15);
        int[] gliss = { 60, 64, 67, 72, 76, 79, 84 };
        for (int i = 0; i < gliss.Length; i++)
            Synth.Note(buf, Sr, i * 0.028, 0.35, Dsp.MidiToFreq(gliss[i]), 0.3, Harp, rng);

        double hit = gliss.Length * 0.028;
        foreach (var n in new[] { 72, 76, 79, 84 })
            Synth.Note(buf, Sr, hit, 0.5, Dsp.MidiToFreq(n), 0.24, Celesta, rng);
        Synth.Note(buf, Sr, hit, 0.5, Dsp.MidiToFreq(84), 0.2, new Inst(Timbre.SynthPluck, R: 0.2), rng);
        foreach (var n in new[] { 60, 64, 67, 72 })
            Synth.Note(buf, Sr, hit - 0.05, 0.3, Dsp.MidiToFreq(n), 0.08, new Inst(Timbre.SynthPluck, R: 0.2), rng);
        Sparkle(buf, rng, hit + 0.05, 0.35, 5, 84, 1, 0.07);
        Reverb.Apply(buf, Sr, 0.16);
        return buf;
    }

    static float[] BarrelPickup(Random rng)
    {
        var buf = Buf(0.48);
        Thump(buf, 0, 130, 0.55, 0.03);
        MagicSweep(buf, rng, 0, 0.18, 520, 0.45);
        Synth.Note(buf, Sr, 0.035, 0.14, Dsp.MidiToFreq(79), 0.23, new Inst(Timbre.SynthPluck), rng);
        Synth.Note(buf, Sr, 0.085, 0.15, Dsp.MidiToFreq(86), 0.22, Celesta, rng);
        Sparkle(buf, rng, 0.07, 0.12, 3, 84, 1, 0.06);
        Reverb.Apply(buf, Sr, 0.08);
        return buf;
    }

    static float[] MeteorImpact(Random rng)
    {
        var buf = Buf(0.85);
        double phase = 0;
        var lp = new Svf();
        for (int i = 0; i < buf.Length; i++)
        {
            double t = i / (double)Sr;
            phase += (28 + 80 * Math.Exp(-t / 0.12)) / Sr;
            double boom = Math.Sin(2 * Math.PI * phase) * Math.Exp(-t / 0.17);
            lp.Process(Noise(rng), 150 + 1500 * Math.Exp(-t / 0.08), 0.7, Sr);
            double rumble = lp.Lp * Math.Exp(-t / 0.2) * 1.2;
            buf[i] += (float)(boom * 0.8 + rumble);
        }
        Thump(buf, 0, 65, 0.65, 0.065);
        Transient(buf, rng, 0, 1.3, 3800, 0.02);
        // 흩·돌 파편이 튜는 소리
        for (int k = 0; k < 5; k++)
            Synth.WoodBlock(buf, Sr, 0.025 + rng.NextDouble() * 0.2, 0.07 + rng.NextDouble() * 0.08, rng, 500 + rng.NextDouble() * 900);
        Reverb.Apply(buf, Sr, 0.1, 0.7, 0.5);
        return buf;
    }

    static float[] PlayerHit(Random rng)
    {
        var buf = Buf(0.28);
        Thump(buf, 0, 72, 0.95, 0.062);
        Transient(buf, rng, 0, 1.2, 2200, 0.024);
        Transient(buf, rng, 0.017, 0.55, 1400, 0.012);
        Synth.Modal(buf, Sr, 0.004, 0.12, 190, 0.18, Modes.Wood, 1.4, 0.04, 0, rng: rng);
        return buf;
    }

    static float[] PlayerDie(Random rng)
    {
        var buf = Buf(1.35);
        Thump(buf, 0, 65, 0.45, 0.075);
        int[] notes = { 72, 67, 64, 60 };
        for (int i = 0; i < notes.Length; i++)
        {
            double st = i * 0.14;
            double dur = i == notes.Length - 1 ? 0.45 : 0.18;
            Synth.Note(buf, Sr, st, dur, Dsp.MidiToFreq(notes[i]), 0.4, Harp, rng);
            Synth.Note(buf, Sr, st, dur, Dsp.MidiToFreq(notes[i] + 12), 0.15, Celesta, rng);
        }
        var strings = new Inst(Timbre.Strings, 0.1, 0.2, 0.5, 0.3, Bright: 1000, Vibrato: 0.003);
        foreach (var n in new[] { 45, 57, 60, 64 })
            Synth.Note(buf, Sr, 0.4, 0.4, Dsp.MidiToFreq(n), 0.07, strings, rng);
        Reverb.Apply(buf, Sr, 0.15);
        return buf;
    }

    static float[] EnemyMelee(Random rng)
    {
        var buf = Buf(0.2);
        var svf = new Svf();
        for (int i = 0; i < buf.Length; i++)
        {
            double t = i / (double)Sr;
            double x = t / 0.18;
            double env = Math.Sin(Math.PI * Math.Clamp(x, 0, 1));
            env *= env;
            double cutoff = 500 + 2300 * Math.Sin(Math.PI * Math.Clamp(x * 0.9, 0, 1));
            svf.Process(Noise(rng), cutoff, 1.3, Sr);
            buf[i] += (float)(svf.Bp * env);
        }
        Transient(buf, rng, 0, 0.35, 1800, 0.012);
        return buf;
    }

    static float[] EnemyShoot(Random rng)
    {
        var buf = Buf(0.22);
        Thump(buf, 0, 185, 0.5, 0.022);
        Transient(buf, rng, 0, 0.4, 2000, 0.008);
        MagicSweep(buf, rng, 0.005, 0.14, 260, 0.24);
        Whoosh(buf, rng, 0, 0.12, 1800, 600, 0.6, 0.8);
        return buf;
    }

    static float[] EnemyFlyingShoot(Random rng)
    {
        var buf = Buf(0.34);
        Thump(buf, 0, 115, 0.75, 0.035);
        Transient(buf, rng, 0, 0.5, 2600, 0.013);
        MagicSweep(buf, rng, 0.01, 0.22, 330, 0.3);
        Whoosh(buf, rng, 0.02, 0.22, 650, 2200, 0.5, 0.8);
        return buf;
    }

    static float[] EnemyHit(Random rng)
    {
        var buf = Buf(0.19);
        Thump(buf, 0, 155, 0.8, 0.028);
        Transient(buf, rng, 0, 1.1, 4200, 0.014);
        Transient(buf, rng, 0.012, 0.5, 3000, 0.008);
        Synth.Modal(buf, Sr, 0.015, 0.09, 1320, 0.085, Modes.Celesta, 0.045, 0.05, 0, rng: rng);
        return buf;
    }

    static float[] EnemyDie(Random rng)
    {
        var buf = Buf(0.38);
        var nlp = new OnePole();
        for (int i = 0; i < buf.Length; i++)
        {
            double t = i / (double)Sr;
            double poof = nlp.Lp(Noise(rng), 1700 * Math.Exp(-t / 0.1) + 200, Sr) * Math.Min(1, t / 0.002) * Math.Exp(-t / 0.055) * 1.5;
            buf[i] += (float)poof;
        }
        Thump(buf, 0, 120, 0.6, 0.035);
        Synth.Note(buf, Sr, 0.02, 0.1, Dsp.MidiToFreq(79), 0.12, Kalimba, rng);
        Synth.Note(buf, Sr, 0.065, 0.12, Dsp.MidiToFreq(72), 0.09, Kalimba, rng);
        Reverb.Apply(buf, Sr, 0.07);
        return buf;
    }

    static float[] BossDie(Random rng)
    {
        var buf = Buf(1.9);
        void Boom(double start, double gain)
        {
            int s0 = (int)(start * Sr);
            double phase = 0;
            var lp = new Svf();
            for (int i = 0; s0 + i < buf.Length; i++)
            {
                double t = i / (double)Sr;
                if (t > 1.6) break;
                phase += (26 + 90 * Math.Exp(-t / 0.15)) / Sr;
                double boom = Math.Sin(2 * Math.PI * phase) * Math.Exp(-t / 0.22);
                lp.Process(Noise(rng), 120 + 2200 * Math.Exp(-t / 0.1), 0.7, Sr);
                buf[s0 + i] += (float)((boom + lp.Lp * Math.Exp(-t / 0.25) * 1.3) * gain);
            }
        }
        Boom(0, 1.0);
        Boom(0.18, 0.65);
        Boom(0.4, 0.7);
        Transient(buf, rng, 0, 1.1, 3200, 0.018);
        Synth.Taiko(buf, Sr, 0, 0.4, rng, 65);

        var choir = new Inst(Timbre.SynthPluck, R: 0.4, Bright: 2200, Decay: 2);
        foreach (var n in new[] { 57, 60, 64, 69 })
            Synth.Note(buf, Sr, 0.25, 0.6, Dsp.MidiToFreq(n), 0.12, choir, rng);
        var strings = new Inst(Timbre.Strings, 0.1, 0.3, 0.5, 0.4, Bright: 800);
        foreach (var n in new[] { 33, 45 })
            Synth.Note(buf, Sr, 0.25, 0.7, Dsp.MidiToFreq(n), 0.08, strings, rng);

        // 마력이 흩어지는 하행 반짝임
        int[] fall = { 84, 81, 79, 76, 74, 72 };
        for (int i = 0; i < fall.Length; i++)
            Synth.Note(buf, Sr, 0.6 + i * 0.07, 0.3, Dsp.MidiToFreq(fall[i]), 0.09, Celesta, rng);
        Reverb.Apply(buf, Sr, 0.2, 0.8, 0.45);
        return buf;
    }

    static float[] GoldPickup(Random rng)
    {
        var buf = Buf(0.24);
        Synth.Modal(buf, Sr, 0, 0.1, 1200, 0.45, Modes.Coin, 0.35, 0.06, 0.08, 4000, rng: rng);
        Synth.Modal(buf, Sr, 0.04, 0.12, 1600, 0.5, Modes.Coin, 0.3, 0.06, 0.08, 4000, rng: rng);
        return buf;
    }

    static float[] ExpPickup(Random rng)
    {
        var buf = Buf(0.18);
        Synth.Modal(buf, Sr, 0, 0.1, Dsp.MidiToFreq(79), 0.5, Modes.Celesta, 0.08, 0.06, 0.015, 3000, rng: rng);
        Synth.Modal(buf, Sr, 0.02, 0.09, Dsp.MidiToFreq(86), 0.13, Modes.Celesta, 0.05, 0.06, 0, rng: rng);
        return buf;
    }
}
