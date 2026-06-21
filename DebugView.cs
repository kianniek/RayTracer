using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class DebugView
    {
        private readonly Surface _screen;
        private float _scale = 40f;
        private int _offsetX = 256;
        private int _offsetZ = 256;

        private readonly int _minX = 0;
        private readonly int _minY = 0;
        private readonly int _maxX = 511;
        private readonly int _maxY = 511;

        public DebugView(Surface screen)
        {
            this._screen = screen;
        }

        public int transformX(float x) => _offsetX + (int)(x * _scale);
        public int transformZ(float z) => _offsetZ + (int)(z * _scale);

        public void DrawBackground()
        {
            _screen.Bar(_minX, _minY, _maxX, _maxY, new Color3(0, 0, 0));
        }

        /// <summary>
        /// Automatically adjusts scale and offsets to tightly frame the camera and all objects.
        /// </summary>
        public void AutoFrame(Vector3 cameraPos, List<SceneNode> nodes)
        {
            float minX = cameraPos.X;
            float maxX = cameraPos.X;
            float minZ = cameraPos.Z;
            float maxZ = cameraPos.Z;

            foreach (var node in nodes)
            {
                // Skip infinite planes or null primitives
                if (node.Primitive == null || node.Primitive is Plane)
                    continue;

                Vector3 pos = (node.WorldTransform * new Vector4(Vector3.Zero, 1.0f)).Xyz;

                if (node.Primitive is Sphere s)
                {
                    minX = MathF.Min(minX, pos.X - s.Radius);
                    maxX = MathF.Max(maxX, pos.X + s.Radius);
                    minZ = MathF.Min(minZ, pos.Z - s.Radius);
                    maxZ = MathF.Max(maxZ, pos.Z + s.Radius);
                }
                // Extends dynamically if custom bounding dimensions or mesh points are checked
            }

            // Add a padding margin so geometry doesn't clip the edges
            float padding = 2.0f;
            minX -= padding; maxX += padding;
            minZ -= padding; maxZ += padding;

            float sceneWidth = maxX - minX;
            float sceneDepth = maxZ - minZ;

            if (sceneWidth < 0.1f) sceneWidth = 1.0f;
            if (sceneDepth < 0.1f) sceneDepth = 1.0f;

            // Compute ideal scale factor based on 512x512 canvas limits
            float viewWidth = _maxX - _minX;
            float viewHeight = _maxY - _minY;
            _scale = MathF.Min(viewWidth / sceneWidth, viewHeight / sceneDepth);

            // Calculate offsets to center the bounding grid
            _offsetX = (int)((viewWidth / 2f) - ((minX + maxX) / 2f * _scale));
            _offsetZ = (int)((viewHeight / 2f) - ((minZ + maxZ) / 2f * _scale));
        }

        public void Line(Vector3 p1, Vector3 p2, Color3 color)
        {
            // Convert world coordinates to _screen pixel coordinates
            int x1 = transformX(p1.X);
            int y1 = transformZ(p1.Z);
            int x2 = transformX(p2.X);
            int y2 = transformZ(p2.Z);

            // Simple Cohen-Sutherland or Min/Max bounding clip approach
            // If both points are completely outside the canvas area, skip it
            if ((x1 < _minX && x2 < _minX) || (x1 > _maxX && x2 > _maxX) ||
                (y1 < _minY && y2 < _minY) || (y1 > _maxY && y2 > _maxY))
            {
                return;
            }

            // Clamp coordinates safely to screen constraints before drawing
            x1 = Math.Clamp(x1, _minX, _maxX);
            x2 = Math.Clamp(x2, _minX, _maxX);
            y1 = Math.Clamp(y1, _minY, _maxY);
            y2 = Math.Clamp(y2, _minY, _maxY);

            _screen.Line(x1, y1, x2, y2, color);
        }

        public void DashedLine(Vector3 p1, Vector3 p2, Color3 color)
        {
            float dist = (p2 - p1).Length;
            int segments = (int)(dist * (_scale * 0.1f));
            if (segments < 1) segments = 1;

            for (int i = 0; i < segments; i += 2)
            {
                Vector3 start = Vector3.Lerp(p1, p2, (float)i / segments);
                Vector3 end = Vector3.Lerp(p1, p2, (float)(i + 1) / segments);
                Line(start, end, color);
            }
        }

        public void Circle(Vector3 center, float radius, Color3 color)
        {
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i * MathF.PI * 2.0f / segments;
                float a2 = (i + 1) * MathF.PI * 2.0f / segments;

                Vector3 p1 = new Vector3(center.X + (MathF.Cos(a1) * radius), 0, center.Z + (MathF.Sin(a1) * radius));
                Vector3 p2 = new Vector3(center.X + (MathF.Cos(a2) * radius), 0, center.Z + (MathF.Sin(a2) * radius));

                Line(p1, p2, color);
            }
        }

        public void AABB(Vector3 min, Vector3 max, Color3 color)
        {
            Vector3 p00 = new Vector3(min.X, 0, min.Z);
            Vector3 p10 = new Vector3(max.X, 0, min.Z);
            Vector3 p01 = new Vector3(min.X, 0, max.Z);
            Vector3 p11 = new Vector3(max.X, 0, max.Z);

            Line(p00, p10, color);
            Line(p10, p11, color);
            Line(p11, p01, color);
            Line(p01, p00, color);
        }

        private bool IsInside(int x, int y)
        {
            return x >= _minX && x <= _maxX && y >= _minY && y <= _maxY;
        }
    }
}