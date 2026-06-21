using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace INFOGR_RAYTRACER
{
    internal static class SimdConfig
    {
        public const bool EnableSimd = true;
    }

    internal static class TriangleIntersectionMath
    {
        private const float Epsilon = 1e-6f;

        public static bool IsSimdSupported => SimdConfig.EnableSimd && (Avx512F.IsSupported || Avx2.IsSupported || Sse.IsSupported);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectScalar(Triangle triangle, Ray ray, out Intersection intersection)
        {
            Vector3 pVector = Vector3.Cross(ray.Direction, triangle.Edge2);
            float determinant = Vector3.Dot(triangle.Edge1, pVector);

            if (MathF.Abs(determinant) < Epsilon)
            {
                intersection = default;
                return false;
            }

            float inverseDeterminant = 1.0f / determinant;
            Vector3 tVector = ray.Origin - triangle.A;
            float barycentricU = Vector3.Dot(tVector, pVector) * inverseDeterminant;

            if (barycentricU < 0.0f || barycentricU > 1.0f)
            {
                intersection = default;
                return false;
            }

            Vector3 qVector = Vector3.Cross(tVector, triangle.Edge1);
            float barycentricV = Vector3.Dot(ray.Direction, qVector) * inverseDeterminant;

            if (barycentricV < 0.0f || (barycentricU + barycentricV) > 1.0f)
            {
                intersection = default;
                return false;
            }

            float hitDistance = Vector3.Dot(triangle.Edge2, qVector) * inverseDeterminant;
            if (hitDistance <= 0.0f)
            {
                intersection = default;
                return false;
            }

            intersection = BuildIntersection(triangle, ray, hitDistance, barycentricU, barycentricV);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectSingleSimd(Triangle triangle, Ray ray, out Intersection intersection)
        {
            if (!SimdConfig.EnableSimd || (!Avx512F.IsSupported && !Avx2.IsSupported && !Sse.IsSupported))
            {
                return TryIntersectScalar(triangle, ray, out intersection);
            }

            return TryIntersectPacket(ray, triangle, triangle, triangle, triangle, 1, out intersection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectPacket(Ray ray, Triangle t0, Triangle t1, Triangle t2, Triangle t3, out Intersection intersection)
        {
            return TryIntersectPacket(ray, t0, t1, t2, t3, 4, out intersection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectPacket(Ray ray, Triangle t0, Triangle t1, Triangle t2, Triangle t3, int triangleCount, out Intersection intersection)
        {
            if (!SimdConfig.EnableSimd || (!Avx512F.IsSupported && !Avx2.IsSupported && !Sse.IsSupported))
            {
                return TryIntersectPacketScalar(ray, t0, t1, t2, t3, triangleCount, out intersection);
            }

            Vector128<float> dirX = Vector128.Create(ray.Direction.X);
            Vector128<float> dirY = Vector128.Create(ray.Direction.Y);
            Vector128<float> dirZ = Vector128.Create(ray.Direction.Z);
            Vector128<float> originX = Vector128.Create(ray.Origin.X);
            Vector128<float> originY = Vector128.Create(ray.Origin.Y);
            Vector128<float> originZ = Vector128.Create(ray.Origin.Z);

            Vector128<float> edge1X = Vector128.Create(t0.Edge1.X, t1.Edge1.X, t2.Edge1.X, t3.Edge1.X);
            Vector128<float> edge1Y = Vector128.Create(t0.Edge1.Y, t1.Edge1.Y, t2.Edge1.Y, t3.Edge1.Y);
            Vector128<float> edge1Z = Vector128.Create(t0.Edge1.Z, t1.Edge1.Z, t2.Edge1.Z, t3.Edge1.Z);
            Vector128<float> edge2X = Vector128.Create(t0.Edge2.X, t1.Edge2.X, t2.Edge2.X, t3.Edge2.X);
            Vector128<float> edge2Y = Vector128.Create(t0.Edge2.Y, t1.Edge2.Y, t2.Edge2.Y, t3.Edge2.Y);
            Vector128<float> edge2Z = Vector128.Create(t0.Edge2.Z, t1.Edge2.Z, t2.Edge2.Z, t3.Edge2.Z);
            Vector128<float> ax = Vector128.Create(t0.A.X, t1.A.X, t2.A.X, t3.A.X);
            Vector128<float> ay = Vector128.Create(t0.A.Y, t1.A.Y, t2.A.Y, t3.A.Y);
            Vector128<float> az = Vector128.Create(t0.A.Z, t1.A.Z, t2.A.Z, t3.A.Z);

            Vector128<float> pvecX = Sse.Subtract(Sse.Multiply(dirY, edge2Z), Sse.Multiply(dirZ, edge2Y));
            Vector128<float> pvecY = Sse.Subtract(Sse.Multiply(dirZ, edge2X), Sse.Multiply(dirX, edge2Z));
            Vector128<float> pvecZ = Sse.Subtract(Sse.Multiply(dirX, edge2Y), Sse.Multiply(dirY, edge2X));
            Vector128<float> determinant = Sse.Add(Sse.Add(Sse.Multiply(edge1X, pvecX), Sse.Multiply(edge1Y, pvecY)), Sse.Multiply(edge1Z, pvecZ));
            Vector128<float> inverseDeterminant = Sse.Divide(Vector128.Create(1.0f), determinant);

            Vector128<float> tvecX = Sse.Subtract(originX, ax);
            Vector128<float> tvecY = Sse.Subtract(originY, ay);
            Vector128<float> tvecZ = Sse.Subtract(originZ, az);
            Vector128<float> barycentricU = Sse.Multiply(Sse.Add(Sse.Add(Sse.Multiply(tvecX, pvecX), Sse.Multiply(tvecY, pvecY)), Sse.Multiply(tvecZ, pvecZ)), inverseDeterminant);

            Vector128<float> qvecX = Sse.Subtract(Sse.Multiply(tvecY, edge1Z), Sse.Multiply(tvecZ, edge1Y));
            Vector128<float> qvecY = Sse.Subtract(Sse.Multiply(tvecZ, edge1X), Sse.Multiply(tvecX, edge1Z));
            Vector128<float> qvecZ = Sse.Subtract(Sse.Multiply(tvecX, edge1Y), Sse.Multiply(tvecY, edge1X));
            Vector128<float> barycentricV = Sse.Multiply(Sse.Add(Sse.Add(Sse.Multiply(dirX, qvecX), Sse.Multiply(dirY, qvecY)), Sse.Multiply(dirZ, qvecZ)), inverseDeterminant);
            Vector128<float> hitDistance = Sse.Multiply(Sse.Add(Sse.Add(Sse.Multiply(edge2X, qvecX), Sse.Multiply(edge2Y, qvecY)), Sse.Multiply(edge2Z, qvecZ)), inverseDeterminant);

            float bestDistance = float.MaxValue;
            Intersection bestIntersection = default;
            bool hitFound = false;

            for (int i = 0; i < triangleCount; i++)
            {
                float determinantValue = determinant.GetElement(i);
                if (MathF.Abs(determinantValue) < Epsilon)
                {
                    continue;
                }

                float u = barycentricU.GetElement(i);
                if (u < 0.0f || u > 1.0f)
                {
                    continue;
                }

                float v = barycentricV.GetElement(i);
                if (v < 0.0f || (u + v) > 1.0f)
                {
                    continue;
                }

                float distance = hitDistance.GetElement(i);
                if (distance <= 0.0f || distance >= bestDistance)
                {
                    continue;
                }

                Triangle triangle = i switch
                {
                    0 => t0,
                    1 => t1,
                    2 => t2,
                    _ => t3
                };

                bestDistance = distance;
                bestIntersection = BuildIntersection(triangle, ray, distance, u, v);
                hitFound = true;
            }

            intersection = bestIntersection;
            return hitFound;
        }

        private static bool TryIntersectPacketScalar(Ray ray, Triangle t0, Triangle t1, Triangle t2, Triangle t3, int triangleCount, out Intersection intersection)
        {
            float bestDistance = float.MaxValue;
            Intersection bestIntersection = default;
            bool hitFound = false;

            for (int i = 0; i < triangleCount; i++)
            {
                Triangle triangle = i switch
                {
                    0 => t0,
                    1 => t1,
                    2 => t2,
                    _ => t3
                };

                if (TryIntersectScalar(triangle, ray, out Intersection candidate) && candidate.Distance < bestDistance)
                {
                    bestDistance = candidate.Distance;
                    bestIntersection = candidate;
                    hitFound = true;
                }
            }

            intersection = bestIntersection;
            return hitFound;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Intersection BuildIntersection(Triangle triangle, Ray ray, float hitDistance, float barycentricU, float barycentricV)
        {
            float barycentricW = 1.0f - barycentricU - barycentricV;
            Vector3 interpolatedNormal = (triangle.nA * barycentricW) + (triangle.nB * barycentricU) + (triangle.nC * barycentricV);
            if (interpolatedNormal.LengthSquared < Epsilon)
            {
                interpolatedNormal = Vector3.Cross(triangle.Edge1, triangle.Edge2);
            }

            interpolatedNormal = Vector3.Normalize(interpolatedNormal);
            if (Vector3.Dot(interpolatedNormal, ray.Direction) > 0.0f)
            {
                interpolatedNormal = -interpolatedNormal;
            }

            Vector2 interpolatedUv = (triangle.uvA * barycentricW) + (triangle.uvB * barycentricU) + (triangle.uvC * barycentricV);

            return new Intersection
            {
                Distance = hitDistance,
                Normal = interpolatedNormal,
                HitObject = triangle,
                UV = interpolatedUv
            };
        }
    }
}
