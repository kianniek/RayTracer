using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class Triangle : Primitive
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

        // Precalculated edge vectors (B - A, C - A) — never change for static geometry.
        // Updated by TriangleMesh.Scale(Vector3) and TriangleMesh.Rotate() when vertices move.
        public Vector3 Edge1;  // B - A
        public Vector3 Edge2;  // C - A

        public Vector3 nA;
        public Vector3 nB;
        public Vector3 nC;

        public Vector2 uvA;
        public Vector2 uvB;
        public Vector2 uvC;

        private readonly BoundingBox _cachedBounds;
        public override BoundingBox LocalBounds => _cachedBounds;
        public override bool IsInfinite => false;

        public Triangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 normalA,
            Vector3 normalB,
            Vector3 normalC,
            Vector2 textureCoordinateA,
            Vector2 textureCoordinateB,
            Vector2 textureCoordinateC,
            Material material) : base(material)
        {
            A = a;
            B = b;
            C = c;
            Edge1 = B - A;
            Edge2 = C - A;
            nA = normalA;
            nB = normalB;
            nC = normalC;
            uvA = textureCoordinateA;
            uvB = textureCoordinateB;
            uvC = textureCoordinateC;

            Vector3 min = Vector3.ComponentMin(Vector3.ComponentMin(A, B), C);
            Vector3 max = Vector3.ComponentMax(Vector3.ComponentMax(A, B), C);
            _cachedBounds = new BoundingBox(min, max);
        }

        public override Intersection? Intersect(Ray ray)
        {
            if (TriangleIntersectionMath.TryIntersectSingleSimd(this, ray, out Intersection intersection))
            {
                return intersection;
            }

            return null;
        }

        public Vector3 ComputeTangent(Vector3 normal)
        {
            // Compute tangent using edge vectors and UV derivatives
            Vector2 uvDelta1 = uvB - uvA;
            Vector2 uvDelta2 = uvC - uvA;

            float r = 1.0f / ((uvDelta1.X * uvDelta2.Y) - (uvDelta1.Y * uvDelta2.X) + 0.0001f);

            Vector3 tangent = ((Edge1 * uvDelta2.Y) - (Edge2 * uvDelta1.Y)) * r;
            tangent = Vector3.Normalize(tangent - (normal * Vector3.Dot(normal, tangent)));

            return tangent;
        }

        public Vector3 ComputeBitangent(Vector3 normal, Vector3 tangent)
        {
            return Vector3.Normalize(Vector3.Cross(normal, tangent));
        }
    }
}
