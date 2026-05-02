using System.Numerics;
using Raylib_cs;

namespace AriaEngine.Rendering;

/// <summary>
/// Simple particle system for weather/ambient effects.
/// Pre-defined emitters for rain, snow, and sakura petals.
/// Managed per-scene — call Update and Draw each frame.
/// </summary>
public class ParticleSystem
{
    public enum ParticleType { None, Rain, Snow, Sakura }

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;       // remaining seconds
        public float MaxLife;    // total lifespan for alpha calc
        public float Size;
        public Color Color;

        public readonly bool IsAlive => Life > 0f;
    }

    private Particle[] _particles;
    private int _count;
    private int _maxParticles;
    private float _emitTimer;
    private ParticleType _type;
    private float _screenW = 1280f;
    private float _screenH = 720f;

    public ParticleType Type => _type;
    public bool Active => _type != ParticleType.None;

    public ParticleSystem(int maxParticles = 500)
    {
        _maxParticles = maxParticles;
        _particles = new Particle[maxParticles];
    }

    public void Start(ParticleType type, int screenW, int screenH)
    {
        _type = type;
        _screenW = screenW;
        _screenH = screenH;
        _count = 0;
        _emitTimer = 0f;
        Array.Clear(_particles);
    }

    public void Stop()
    {
        _type = ParticleType.None;
    }

    public void Update(float deltaTimeSeconds)
    {
        if (!Active) return;

        // Emit new particles
        _emitTimer += deltaTimeSeconds;
        float emitInterval = _type switch
        {
            ParticleType.Rain => 0.02f,
            ParticleType.Snow => 0.08f,
            ParticleType.Sakura => 0.12f,
            _ => 0.1f
        };
        int perEmit = _type switch
        {
            ParticleType.Rain => 3,
            ParticleType.Snow => 2,
            ParticleType.Sakura => 1,
            _ => 1
        };

        while (_emitTimer >= emitInterval && _count < _maxParticles)
        {
            _emitTimer -= emitInterval;
            for (int i = 0; i < perEmit && _count < _maxParticles; i++)
            {
                _particles[_count++] = CreateParticle(_type);
            }
        }

        // Update existing particles
        float dt = deltaTimeSeconds;
        float gravity = _type switch
        {
            ParticleType.Rain => 900f,
            ParticleType.Snow => 30f,
            ParticleType.Sakura => 40f,
            _ => 50f
        };

        for (int i = 0; i < _count; i++)
        {
            if (!_particles[i].IsAlive) continue;

            var p = _particles[i];

            // Apply velocity + gravity
            p.Velocity.Y += gravity * dt;
            p.Position += p.Velocity * dt;

            // Wind drift
            if (_type == ParticleType.Snow || _type == ParticleType.Sakura)
            {
                float time = p.MaxLife - p.Life;
                p.Position.X += MathF.Sin(time * 2f + p.Position.Y * 0.01f) * 30f * dt;
            }

            p.Life -= dt;

            // Wrap around if off-screen
            if (p.Position.Y > _screenH + 20f)
                p.Position.Y = -20f;
            if (p.Position.X < -20f)
                p.Position.X = _screenW + 20f;
            if (p.Position.X > _screenW + 20f)
                p.Position.X = -20f;

            _particles[i] = p;
        }

        // Compact dead particles
        int alive = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_particles[i].IsAlive)
            {
                if (i != alive)
                    _particles[alive] = _particles[i];
                alive++;
            }
        }
        _count = alive;
    }

    public void Draw()
    {
        if (!Active || _count == 0) return;

        for (int i = 0; i < _count; i++)
        {
            var p = _particles[i];
            if (!p.IsAlive) continue;

            float alpha = Math.Clamp(p.Life / Math.Max(p.MaxLife, 0.001f), 0f, 1f);
            Color c = p.Color;
            c.A = (byte)(c.A * alpha);

            int size = (int)p.Size;
            Raylib.DrawRectangle((int)p.Position.X, (int)p.Position.Y, size, size, c);
        }
    }

    private Particle CreateParticle(ParticleType type)
    {
        var rng = new Random();
        return type switch
        {
            ParticleType.Rain => new Particle
            {
                Position = new Vector2(rng.Next((int)_screenW), -rng.Next(40)),
                Velocity = new Vector2(0, 300f + rng.Next(200)),
                Life = 1.5f + (float)rng.NextDouble() * 1f,
                MaxLife = 2.5f,
                Size = 2,
                Color = new Color(180, 200, 255, 160)
            },
            ParticleType.Snow => new Particle
            {
                Position = new Vector2(rng.Next((int)_screenW), -rng.Next(20)),
                Velocity = new Vector2(0, 25f + rng.Next(35)),
                Life = 8f + (float)rng.NextDouble() * 6f,
                MaxLife = 14f,
                Size = rng.Next(3, 6),
                Color = new Color(240, 245, 255, 200)
            },
            ParticleType.Sakura => new Particle
            {
                Position = new Vector2(rng.Next((int)_screenW), -rng.Next(60)),
                Velocity = new Vector2(0, 15f + rng.Next(25)),
                Life = 10f + (float)rng.NextDouble() * 8f,
                MaxLife = 18f,
                Size = rng.Next(3, 6),
                Color = GetSakuraColor(rng)
            },
            _ => new Particle { Life = 0 }
        };
    }

    private static Color GetSakuraColor(Random rng)
    {
        return rng.Next(5) switch
        {
            0 => new Color(255, 200, 210, 200),
            1 => new Color(255, 180, 200, 180),
            2 => new Color(255, 220, 230, 220),
            3 => new Color(245, 190, 210, 190),
            _ => new Color(255, 210, 220, 210),
        };
    }
}
