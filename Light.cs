using Vector3 = OpenTK.Mathematics.Vector3;

namespace INFOGR_RAYTRACER
{
    public abstract class Light
    {
        public Vector3 Color;

        public bool LightState { get; private set; }

        protected Light(Vector3 color)
        {
            Color = color;
        }

        public void ToggleLight(bool state)
        {
            LightState = state;
        }
    }

    public class PointLight : Light
    {
        public Vector3 Position;

        public PointLight(Vector3 position, Vector3 color) : base(color)
        {
            Position = position;
        }
    }

    public class DirectionalLight : Light
    {
        public Vector3 Direction;

        public DirectionalLight(Vector3 direction, Vector3 color) : base(color)
        {
            Direction = Vector3.Normalize(direction);
        }
    }
    public class SpotLight : Light
    {
        public Vector3 Position;
        private float angle;
        public float CutOffAngle
        {
            get { return angle * (MathF.PI / 180f); }
            private set { angle = value; }
        } // in degrees
        public Vector3 Direction { get; private set; }
        public SpotLight(Vector3 position, Vector3 color, float cutOffAngle, Vector3 direction) : base(color)
        {
            Position = position;
            CutOffAngle = cutOffAngle;
            Direction = direction;
        }
    }

    // area light is a sphere for now, ideally id also have one with a quad and sphere
    public class AreaLight : Light
    {
        public Primitive Primitive;
        public Vector3 Position;
        public readonly float Intensity;
        public AreaLight(Primitive primitive, Vector3 position, float intensity) : base(primitive.Material.Color)
        {
            Primitive = primitive;
            Position = position;
            Intensity = intensity;
        }
    }

    public class SphereLight : AreaLight
    {
        public Sphere shape;
        public SphereLight(Sphere sphere, Vector3 position, float intensity) : base(sphere, position, intensity)
        {
            shape = sphere;
        }

        // Area Sampling
        public Vector3 GetRandomPoint()
        {
            //random variables for stochastic sampling
            float epsilon1 = Random.Shared.NextSingle();
            float epsilon2 = Random.Shared.NextSingle();

            float z = 1.0f - (2.0f * epsilon1);
            float r = MathF.Sqrt(MathF.Max(0f, 1f - (z * z)));
            float phi = 2.0f * MathF.PI * epsilon2;

            Vector3 dir = new Vector3(r * MathF.Cos(phi), r * MathF.Sin(phi), z);
            return Position + (dir * shape.Radius);
        }

    }
}
