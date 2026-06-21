using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = new Vector3(
                MathF.Min(min.X, max.X),
                MathF.Min(min.Y, max.Y),
                MathF.Min(min.Z, max.Z)
            );
            Max = new Vector3(
                MathF.Max(min.X, max.X),
                MathF.Max(min.Y, max.Y),
                MathF.Max(min.Z, max.Z)
            );
        }

        public BoundingBox(Vector3 point) : this(point, point) { }

        public static BoundingBox FromTriangle(Triangle triangle)
        {
            Vector3 min = Vector3.ComponentMin(Vector3.ComponentMin(triangle.A, triangle.B), triangle.C);
            Vector3 max = Vector3.ComponentMax(Vector3.ComponentMax(triangle.A, triangle.B), triangle.C);
            return new BoundingBox(min, max);
        }

        public static BoundingBox Combine(BoundingBox a, BoundingBox b)
        {
            Vector3 min = Vector3.ComponentMin(a.Min, b.Min);
            Vector3 max = Vector3.ComponentMax(a.Max, b.Max);
            return new BoundingBox(min, max);
        }

        public bool IntersectRay(Ray ray, out float tMin, out float tMax)
        {
            tMin = 0.0f;
            tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float rayOrigin = ray.Origin[i];
                float rayDir = ray.Direction[i];
                float boxMin = Min[i];
                float boxMax = Max[i];

                if (MathF.Abs(rayDir) < 1e-8f)
                {
                    if (rayOrigin < boxMin || rayOrigin > boxMax)
                    {
                        return false;
                    }
                }
                else
                {
                    float t1 = (boxMin - rayOrigin) / rayDir;
                    float t2 = (boxMax - rayOrigin) / rayDir;

                    if (t1 > t2)
                    {
                        (t1, t2) = (t2, t1);
                    }

                    tMin = MathF.Max(tMin, t1);
                    tMax = MathF.Min(tMax, t2);

                    if (tMin > tMax)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public float SurfaceArea()
        {
            Vector3 extent = Max - Min;
            return 2.0f * ((extent.X * extent.Y) + (extent.Y * extent.Z) + (extent.Z * extent.X));
        }
    }

    public class BVHNode
    {
        public BoundingBox Bounds;
        public BVHNode? Left;
        public BVHNode? Right;
        public int[] TriangleIndices = [];

        public bool IsLeaf => Left == null && Right == null;

        public BVHNode(BoundingBox bounds)
        {
            Bounds = bounds;
        }
    }
}
