using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class Camera
    {
        public enum ProjectionMode
        {
            Perspective,
            Orthographic
        }

        public Vector3 Position;
        public Vector3 Target;
        public Vector3 Up;

        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 UpCam { get; private set; }

        private Vector3 _p0; // Top-left corner
        private Vector3 _p1; // Top-right corner
        private Vector3 _p2; // Bottom-left corner

        private float _fov;
        public float FOV
        {
            get => _fov;
            set => _fov = Math.Clamp(value, 1f, 179f);
        } // Field of View angle in degrees (alpha)
        public float Distance;
        public float AspectRatio;
        public float FocusDistance;
        public float Aperture;
        public ProjectionMode Mode = ProjectionMode.Perspective;

        public Camera(Vector3 position, Vector3 target, Vector3 up, float fov, float distance, float aspectRatio, float focusDistance = 10f, float aperture = 0.0f)
        {
            Position = position;
            Target = target;
            Up = up;
            FOV = fov;
            Distance = distance;
            AspectRatio = aspectRatio;
            FocusDistance = focusDistance;
            Aperture = aperture;

            Update();
        }

        public void Update()
        {
            Forward = Vector3.Normalize(Target - Position);
            if (MathF.Abs(Vector3.Dot(Forward, Up)) > 0.99f)
            {
                Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitZ));
            }
            else
            {
                Right = Vector3.Normalize(Vector3.Cross(Forward, Up));
            }
            UpCam = Vector3.Cross(Right, Forward);

            float w, h;

            if (Mode == ProjectionMode.Perspective)
            {
                // Perspective: plane size depends on FOV and Distance
                float fovRadiants = FOV * (MathF.PI / 180);
                float tanHalfFOV = MathF.Tan(fovRadiants * 0.5f);
                h = 2.0f * Distance * tanHalfFOV;
                w = h * AspectRatio;
            }
            else
            {
                // Orthographic: plane size is fixed (e.g., 10 units wide) 
                // regardless of camera distance
                float orthoHeight = 10.0f;
                h = orthoHeight;
                w = orthoHeight * AspectRatio;
            }

            Vector3 center = Position + (Distance * Forward);

            _p0 = center - (Right * (w / 2.0f)) + (UpCam * (h / 2.0f));
            _p1 = center + (Right * (w / 2.0f)) + (UpCam * (h / 2.0f));
            _p2 = center - (Right * (w / 2.0f)) - (UpCam * (h / 2.0f));
        }

        private static Vector2 RandomInUnitDisk()
        {
            Vector2 p;
            do
            {
                p = new Vector2((Random.Shared.NextSingle() * 2.0f) - 1.0f, (Random.Shared.NextSingle() * 2.0f) - 1.0f);
            } while (p.LengthSquared >= 1.0f);

            return p;
        }

        public Ray GetRay(float u, float v)
        {
            // P_uv = P0 + u(P1 - P0) + v(P2 - P0)
            Vector3 puv = _p0 + (u * (_p1 - _p0)) + (v * (_p2 - _p0));

            if (Mode == ProjectionMode.Orthographic)
            {
                Vector3 orthoOrigin = puv - (Forward * Distance);
                return new Ray(orthoOrigin, Forward);
            }

            Vector3 pinholeDirection = Vector3.Normalize(puv - Position);

            if (Aperture <= 0.0f)
            {
                return new Ray(Position, pinholeDirection);
            }

            Vector3 focusPoint = Position + (pinholeDirection * MathF.Max(0.001f, FocusDistance));
            Vector2 lensSample = RandomInUnitDisk() * Aperture;
            Vector3 origin = Position + (Right * lensSample.X) + (UpCam * lensSample.Y);
            Vector3 direction = Vector3.Normalize(focusPoint - origin);

            return new Ray(origin, direction);
        }
    }
}
