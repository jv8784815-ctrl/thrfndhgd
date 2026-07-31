using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace JarvisTeto.Controls
{
    public enum OrbMode
    {
        /// <summary>En reposo: una esfera de orbes flotando y rotando despacio.</summary>
        Idle,
        /// <summary>Pensando: los orbes giran en anillos concéntricos estables alrededor del núcleo, como el disco de un agujero negro o los planetas de un sistema solar. Sin caos.</summary>
        Thinking,
        /// <summary>Hablando: los orbes se agrupan en bandas radiales que suben y bajan como un ecualizador circular.</summary>
        Speaking,
        /// <summary>Escuchando (mic activo): pulso suave hacia afuera.</summary>
        Listening
    }

    /// <summary>
    /// La "cara" de Jarvis: una esfera hecha de orbes que cambia de comportamiento según el estado
    /// del asistente. No usa shaders ni GPU custom -- son Ellipses de WPF posicionadas a mano cada
    /// frame con CompositionTarget.Rendering, así que corre en cualquier PC sin dependencias extra.
    /// </summary>
    public partial class OrbVisualizer : System.Windows.Controls.UserControl
    {
        private class Orb
        {
            public Ellipse Visual = null!;
            public double AngleOffset;   // posición angular base (0..2π)
            public double Phase;         // desfasaje temporal propio, para que no todos se muevan igual
            public double RadiusVariance; // 0.8..1.2, variación individual de radio
            public double BaseSize;      // tamaño base del orbe en píxeles
            public int Band;             // banda angular (0..BandCount-1), usada en modo "Speaking"
            public int Ring;             // anillo orbital (0..ThinkingRingCount-1), usado en modo "Thinking"
            public double SpeedVariance; // ~0.9..1.1, variación sutil de velocidad angular al pensar (evita que se vea sincronizado/robótico sin llegar al caos)
        }

        private const int OrbCount = 130;
        private const int BandCount = 26;
        private const int ThinkingRingCount = 7;

        private readonly List<Orb> _orbs = new();
        private readonly double[] _bandAmplitude = new double[BandCount];
        private readonly double[] _thinkingRingRadius = new double[ThinkingRingCount];
        private readonly Random _rng = new();

        private OrbMode _mode = OrbMode.Idle;
        private double _modeBlend = 1.0; // 0..1, para transicionar suavemente entre modos
        private OrbMode _previousMode = OrbMode.Idle;

        private double _time;
        private DateTime _lastFrame = DateTime.Now;
        private bool _hooked;

        public OrbVisualizer()
        {
            InitializeComponent();

            // Anillos concéntricos para el modo "Thinking": del núcleo (0.16) al borde (0.90),
            // repartidos en partes iguales. Cada orbe vive en un anillo fijo -> disco estable,
            // no una caída individual descoordinada.
            for (int r = 0; r < ThinkingRingCount; r++)
            {
                double t = ThinkingRingCount > 1 ? (double)r / (ThinkingRingCount - 1) : 0.0;
                _thinkingRingRadius[r] = 0.16 + t * 0.74;
            }

            BuildOrbs();

            Loaded += (s, e) =>
            {
                if (_hooked) return;
                CompositionTarget.Rendering += OnRendering;
                _hooked = true;
            };
            Unloaded += (s, e) =>
            {
                if (!_hooked) return;
                CompositionTarget.Rendering -= OnRendering;
                _hooked = false;
            };
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double size = Math.Min(e.NewSize.Width, e.NewSize.Height);
            AmbientGlow.Width = size * 1.15;
            AmbientGlow.Height = size * 1.15;
            Canvas.SetLeft(AmbientGlow, (e.NewSize.Width - AmbientGlow.Width) / 2);
            Canvas.SetTop(AmbientGlow, (e.NewSize.Height - AmbientGlow.Height) / 2);
        }

        private void BuildOrbs()
        {
            // Paleta fija de "temperatura": del centro caliente (blanco/cian) al borde frío (azul profundo).
            var palette = new[]
            {
                MakeFrozenBrush("#FFE8FBFF"), // casi blanco (núcleo caliente)
                MakeFrozenBrush("#FF6FE6FF"), // cian brillante
                MakeFrozenBrush("#FF00D9FF"), // cian Jarvis
                MakeFrozenBrush("#FF2E86E0"), // azul medio
                MakeFrozenBrush("#FF1B4FA0"), // azul profundo (borde)
            };

            for (int i = 0; i < OrbCount; i++)
            {
                double angleOffset = (double)i / OrbCount * Math.PI * 2.0;
                int band = (int)(((double)i / OrbCount) * BandCount) % BandCount;

                var orb = new Orb
                {
                    AngleOffset = angleOffset,
                    Phase = _rng.NextDouble() * Math.PI * 2.0,
                    RadiusVariance = 0.8 + _rng.NextDouble() * 0.4,
                    BaseSize = 3.0 + _rng.NextDouble() * 5.0,
                    Band = band,
                    Ring = i % ThinkingRingCount,
                    SpeedVariance = 0.9 + _rng.NextDouble() * 0.2,
                };

                // Los orbes más grandes tienden a colores más cálidos/brillantes (leve sesgo hacia el centro).
                int paletteIndex = orb.BaseSize > 6.5
                    ? _rng.Next(0, 2)
                    : _rng.Next(1, palette.Length);

                orb.Visual = new Ellipse
                {
                    Width = orb.BaseSize,
                    Height = orb.BaseSize,
                    Fill = palette[Math.Min(paletteIndex, palette.Length - 1)],
                    Opacity = 0.6
                };

                OrbCanvas.Children.Add(orb.Visual);
                _orbs.Add(orb);
            }
        }

        private static SolidColorBrush MakeFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        /// <summary>Cambia de estado con una pequeña transición suave en vez de un salto brusco.</summary>
        public void SetMode(OrbMode mode)
        {
            if (_mode == mode) return;
            _previousMode = _mode;
            _mode = mode;
            _modeBlend = 0.0;
        }

        /// <summary>
        /// Llamar por cada palabra que Jarvis está pronunciando (evento WordSpoken de VoiceService).
        /// Hace "saltar" un puñado de bandas del ecualizador, simulando el golpe de energía de esa
        /// sílaba/palabra. El resto del tiempo las bandas decaen solas hacia el silencio.
        /// </summary>
        public void PulseSpeech()
        {
            int hits = _rng.Next(3, 7);
            for (int h = 0; h < hits; h++)
            {
                int band = _rng.Next(0, BandCount);
                double spike = 0.55 + _rng.NextDouble() * 0.45;
                if (spike > _bandAmplitude[band]) _bandAmplitude[band] = spike;

                // También contagia un poco a las bandas vecinas para que no se vea "punteado".
                int left = (band - 1 + BandCount) % BandCount;
                int right = (band + 1) % BandCount;
                _bandAmplitude[left] = Math.Max(_bandAmplitude[left], spike * 0.5);
                _bandAmplitude[right] = Math.Max(_bandAmplitude[right], spike * 0.5);
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            double dt = Math.Min((now - _lastFrame).TotalSeconds, 0.05);
            _lastFrame = now;
            _time += dt;

            if (_modeBlend < 1.0)
                _modeBlend = Math.Min(1.0, _modeBlend + dt / 0.5); // ~0.5s de transición entre modos

            double width = RootGrid.ActualWidth;
            double height = RootGrid.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double centerX = width / 2.0;
            double centerY = height / 2.0;
            double baseRadius = Math.Min(width, height) * 0.42;

            UpdateBandAmplitudes(dt);
            UpdateCore(baseRadius);

            foreach (var orb in _orbs)
            {
                var (radiusFrac, angle, sizeMul, opacity, squashY) = ComputeOrbState(orb, _mode);

                if (_modeBlend < 1.0)
                {
                    var prev = ComputeOrbState(orb, _previousMode);
                    radiusFrac = Lerp(prev.radiusFrac, radiusFrac, _modeBlend);
                    sizeMul = Lerp(prev.sizeMul, sizeMul, _modeBlend);
                    opacity = Lerp(prev.opacity, opacity, _modeBlend);
                    squashY = Lerp(prev.squashY, squashY, _modeBlend);
                    // El ángulo no se interpola (evita que gire "para el lado equivocado" en la transición).
                }

                double radius = baseRadius * radiusFrac * orb.RadiusVariance;
                double x = centerX + radius * Math.Cos(angle);
                double y = centerY + radius * Math.Sin(angle) * squashY;

                double size = Math.Max(1.5, orb.BaseSize * sizeMul);
                orb.Visual.Width = size;
                orb.Visual.Height = size;
                orb.Visual.Opacity = Math.Clamp(opacity, 0.0, 1.0);

                Canvas.SetLeft(orb.Visual, x - size / 2.0);
                Canvas.SetTop(orb.Visual, y - size / 2.0);
            }
        }

        private void UpdateBandAmplitudes(double dt)
        {
            // Zumbido ambiental de fondo aunque no haya "golpes" de PulseSpeech, para que nunca se vea congelado.
            double ambient = 0.10 + 0.05 * Math.Sin(_time * 2.3);

            for (int b = 0; b < BandCount; b++)
            {
                double target = _mode == OrbMode.Speaking ? ambient : 0.0;
                // Decaimiento exponencial hacia el objetivo: rápido para que se sienta "vivo".
                double decay = 1.0 - Math.Exp(-dt * 6.0);
                _bandAmplitude[b] += (target - _bandAmplitude[b]) * decay;
                if (_bandAmplitude[b] < 0.001) _bandAmplitude[b] = 0;
            }
        }

        private void UpdateCore(double baseRadius)
        {
            switch (_mode)
            {
                case OrbMode.Thinking:
                {
                    // La singularidad: un núcleo que se achica y se pone casi negro en el medio,
                    // con un anillo brillante (photon ring) apretado alrededor. Respira despacio,
                    // acorde al disco ya calmado (nada de parpadeos rápidos).
                    double pulse = 0.94 + 0.06 * Math.Sin(_time * 2.4);
                    double coreSize = baseRadius * 0.22 * pulse;
                    Core.Width = coreSize;
                    Core.Height = coreSize;
                    CoreCenterStop.Color = Color.FromArgb(255, 4, 6, 10);
                    CoreMidStop.Offset = 0.78;
                    CoreMidStop.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.BlurRadius = 55;
                    CoreGlow.Opacity = 1.0;
                    break;
                }
                case OrbMode.Speaking:
                {
                    double avg = 0;
                    for (int b = 0; b < BandCount; b++) avg += _bandAmplitude[b];
                    avg /= BandCount;
                    double coreSize = baseRadius * (0.30 + avg * 0.18);
                    Core.Width = coreSize;
                    Core.Height = coreSize;
                    CoreCenterStop.Color = Color.FromArgb(255, 239, 255, 255);
                    CoreMidStop.Offset = 0.45;
                    CoreMidStop.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.BlurRadius = 35 + avg * 40;
                    CoreGlow.Opacity = 0.75 + avg * 0.25;
                    break;
                }
                case OrbMode.Listening:
                {
                    double pulse = 0.5 + 0.5 * Math.Sin(_time * 4.0);
                    double coreSize = baseRadius * (0.30 + pulse * 0.05);
                    Core.Width = coreSize;
                    Core.Height = coreSize;
                    CoreCenterStop.Color = Color.FromArgb(255, 239, 255, 255);
                    CoreMidStop.Offset = 0.45;
                    CoreMidStop.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.BlurRadius = 40;
                    CoreGlow.Opacity = 0.8;
                    break;
                }
                default: // Idle
                {
                    double breathe = 0.94 + 0.06 * Math.Sin(_time * 0.8);
                    double coreSize = baseRadius * 0.30 * breathe;
                    Core.Width = coreSize;
                    Core.Height = coreSize;
                    CoreCenterStop.Color = Color.FromArgb(255, 239, 255, 255);
                    CoreMidStop.Offset = 0.45;
                    CoreMidStop.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.Color = Color.FromArgb(255, 0, 217, 255);
                    CoreGlow.BlurRadius = 30;
                    CoreGlow.Opacity = 0.55 + 0.15 * Math.Sin(_time * 0.8);
                    break;
                }
            }

            Canvas.SetLeft(Core, RootGrid.ActualWidth / 2.0 - Core.Width / 2.0);
            Canvas.SetTop(Core, RootGrid.ActualHeight / 2.0 - Core.Height / 2.0);
        }

        private (double radiusFrac, double angle, double sizeMul, double opacity, double squashY) ComputeOrbState(Orb orb, OrbMode mode)
        {
            switch (mode)
            {
                case OrbMode.Thinking:
                {
                    // Disco estable de anillos concéntricos: cada orbe vive en un anillo fijo y gira
                    // a velocidad tipo Kepler (más rápido cuanto más cerca del centro), igual que un
                    // disco de acreción alrededor de un agujero negro o los planetas de un sistema
                    // solar. Nada "cae" ni se sincroniza de golpe: es un giro parejo y continuo.
                    double ringFrac = _thinkingRingRadius[orb.Ring];
                    double angularSpeed = (0.50 / Math.Pow(ringFrac, 0.85)) * orb.SpeedVariance;
                    double angle = orb.AngleOffset + _time * angularSpeed;

                    // Respiración muy suave del anillo (da vida sin que se note como movimiento errático).
                    double breathe = 1.0 + 0.02 * Math.Sin(_time * 0.35 + orb.Phase);
                    double radiusFrac = ringFrac * breathe;

                    double twinkle = 0.5 + 0.5 * Math.Sin(_time * 1.1 + orb.Phase);
                    double sizeMul = 0.80 + 0.25 * twinkle;
                    double opacity = 0.50 + 0.30 * twinkle;

                    return (radiusFrac, angle, sizeMul, opacity, 0.55);
                }
                case OrbMode.Speaking:
                {
                    double amp = _bandAmplitude[orb.Band];
                    double radiusFrac = 0.30 + 0.75 * amp;
                    double angle = orb.AngleOffset + _time * 0.06;
                    double sizeMul = 0.75 + 1.1 * amp;
                    double opacity = 0.35 + 0.65 * amp;
                    return (radiusFrac, angle, sizeMul, opacity, 0.85);
                }
                case OrbMode.Listening:
                {
                    double wave = 0.5 + 0.5 * Math.Sin(_time * 3.0 + orb.Phase);
                    double radiusFrac = 0.55 + 0.18 * wave;
                    double angle = orb.AngleOffset + _time * 0.10;
                    double sizeMul = 0.85 + 0.3 * wave;
                    double opacity = 0.45 + 0.35 * wave;
                    return (radiusFrac, angle, sizeMul, opacity, 0.85);
                }
                default: // Idle
                {
                    double radiusFrac = 0.60 + 0.10 * Math.Sin(_time * 0.6 + orb.Phase);
                    double angle = orb.AngleOffset + _time * 0.14;
                    double sizeMul = 0.85 + 0.20 * Math.Sin(_time * 1.2 + orb.Phase);
                    double opacity = 0.45 + 0.25 * Math.Sin(_time * 0.9 + orb.Phase);
                    return (radiusFrac, angle, sizeMul, opacity, 0.85);
                }
            }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
