using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class TriangleMesh : Primitive
    {
        public List<Triangle> Triangles { get; } = [];
        public string? Filename { get; set; }

        private BVHNode? _bvhRoot;
        private bool _bvhDirty = true;
        private readonly Lock _bvhLock = new();

        private const int BvhTriangleThreshold = 4;

        public TriangleMesh(Material material) : base(material) { }

        public override bool IsInfinite => false;

        public override BoundingBox LocalBounds
        {
            get
            {
                if (_bvhDirty)
                {
                    BuildBVH();
                }

                return _bvhRoot?.Bounds ?? new BoundingBox(Vector3.Zero);
            }
        }

        public void AddTriangle(Triangle triangle)
        {
            Triangles.Add(triangle);
            InvalidateBVH();
        }

        private void InvalidateBVH()
        {
            lock (_bvhLock)
            {
                _bvhDirty = true;
                _bvhRoot = null;
            }
        }

        public void BuildBVH()
        {
            if (!_bvhDirty || Triangles.Count == 0)
            {
                return;
            }

            lock (_bvhLock)
            {
                if (!_bvhDirty || Triangles.Count == 0)
                {
                    return;
                }

                // Only build BVH for meshes with more than a few triangles
                if (Triangles.Count <= BvhTriangleThreshold)
                {
                    Console.WriteLine($"Skipping BVH for {Filename ?? "unknown mesh"} ({Triangles.Count} triangles below threshold of {BvhTriangleThreshold})");

                    _bvhDirty = false;
                    _bvhRoot = null;
                    return;
                }

                Console.WriteLine($"Building BVH for {Filename ?? "unknown mesh"} (mesh contains {Triangles.Count} triangles)");

                _bvhRoot = BuildBVHNode(Enumerable.Range(0, Triangles.Count).ToArray(), 0);
                _bvhDirty = false;
            }
        }

        private BVHNode BuildBVHNode(int[] triangleIndices, int depth)
        {
            BoundingBox nodeBounds = GetBoundsForTriangles(triangleIndices);
            BVHNode node = new BVHNode(nodeBounds);

            // Too few triangles or depth limit
            if (triangleIndices.Length <= BvhTriangleThreshold || depth > 32)
            {
                node.TriangleIndices = triangleIndices;
                return node;
            }

            // Precalculate centroids for binning
            Vector3[] centroids = new Vector3[triangleIndices.Length];
            for (int i = 0; i < triangleIndices.Length; i++)
            {
                Triangle tri = Triangles[triangleIndices[i]];
                centroids[i] = (tri.A + tri.B + tri.C) / 3.0f;
            }

            const int Bins = 8;
            float bestCost = float.MaxValue;
            int bestAxis = -1;
            int bestSplitIndex = -1;

            float nodeArea = nodeBounds.SurfaceArea();

            // Evaluate splits across all 3 axes
            for (int axis = 0; axis < 3; axis++)
            {
                float boundsMin = nodeBounds.Min[axis];
                float boundsMax = nodeBounds.Max[axis];

                // If bounds are completely flat on this axis, we can't split it
                if (MathF.Abs(boundsMax - boundsMin) < 1e-5f) continue;

                int[] binCounts = new int[Bins];
                BoundingBox[] binBounds = new BoundingBox[Bins];
                bool[] binHasBounds = new bool[Bins];

                // Populate bins
                for (int i = 0; i < triangleIndices.Length; i++)
                {
                    float centroidPos = centroids[i][axis];
                    float normalizedPos = (centroidPos - boundsMin) / (boundsMax - boundsMin);

                    int binIdx = (int)(normalizedPos * Bins);
                    binIdx = Math.Clamp(binIdx, 0, Bins - 1);

                    binCounts[binIdx]++;
                    BoundingBox triBounds = BoundingBox.FromTriangle(Triangles[triangleIndices[i]]);

                    if (!binHasBounds[binIdx])
                    {
                        binBounds[binIdx] = triBounds;
                        binHasBounds[binIdx] = true;
                    }
                    else
                    {
                        binBounds[binIdx] = BoundingBox.Combine(binBounds[binIdx], triBounds);
                    }
                }

                // Arrays to hold cumulative data from Left-to-Right and Right-to-Left sweeps
                int[] leftCounts = new int[Bins - 1];
                BoundingBox[] leftBounds = new BoundingBox[Bins - 1];
                bool[] leftHasBounds = new bool[Bins - 1];

                int[] rightCounts = new int[Bins - 1];
                BoundingBox[] rightBounds = new BoundingBox[Bins - 1];
                bool[] rightHasBounds = new bool[Bins - 1];

                // Sweep Left-to-Right
                int currentLeftCount = 0;
                BoundingBox currentLeftBounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
                bool hasLeftBounds = false;

                for (int i = 0; i < Bins - 1; i++)
                {
                    currentLeftCount += binCounts[i];
                    if (binHasBounds[i])
                    {
                        currentLeftBounds = !hasLeftBounds ? binBounds[i] : BoundingBox.Combine(currentLeftBounds, binBounds[i]);
                        hasLeftBounds = true;
                    }
                    leftCounts[i] = currentLeftCount;
                    leftBounds[i] = currentLeftBounds;
                    leftHasBounds[i] = hasLeftBounds;
                }

                // Sweep Right-to-Left
                int currentRightCount = 0;
                BoundingBox currentRightBounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
                bool hasRightBounds = false;

                for (int i = Bins - 1; i > 0; i--)
                {
                    currentRightCount += binCounts[i];
                    if (binHasBounds[i])
                    {
                        currentRightBounds = !hasRightBounds ? binBounds[i] : BoundingBox.Combine(currentRightBounds, binBounds[i]);
                        hasRightBounds = true;
                    }
                    rightCounts[i - 1] = currentRightCount;
                    rightBounds[i - 1] = currentRightBounds;
                    rightHasBounds[i - 1] = hasRightBounds;
                }

                // Evaluate SAH Cost for the (Bins - 1) possible splits on this axis
                for (int i = 0; i < Bins - 1; i++)
                {
                    if (leftCounts[i] == 0 || rightCounts[i] == 0) continue;

                    float leftArea = leftHasBounds[i] ? leftBounds[i].SurfaceArea() : 0f;
                    float rightArea = rightHasBounds[i] ? rightBounds[i].SurfaceArea() : 0f;

                    float cost = 1.0f + (((leftArea * leftCounts[i]) + (rightArea * rightCounts[i])) / nodeArea);

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestAxis = axis;
                        bestSplitIndex = i;
                    }
                }
            }

            // Cost of making this node a leaf (intersecting all triangles directly)
            int leafCost = triangleIndices.Length;

            // Apply the split if it's beneficial
            if (bestCost >= leafCost || bestAxis == -1)
            {
                node.TriangleIndices = triangleIndices;
                return node;
            }

            List<int> leftIndices = new List<int>();
            List<int> rightIndices = new List<int>();

            float splitBoundsMin = nodeBounds.Min[bestAxis];
            float splitBoundsMax = nodeBounds.Max[bestAxis];

            for (int i = 0; i < triangleIndices.Length; i++)
            {
                float centroidPos = centroids[i][bestAxis];
                float normalizedPos = (centroidPos - splitBoundsMin) / (splitBoundsMax - splitBoundsMin);

                int binIdx = (int)(normalizedPos * Bins);
                binIdx = Math.Clamp(binIdx, 0, Bins - 1);

                if (binIdx <= bestSplitIndex)
                {
                    leftIndices.Add(triangleIndices[i]);
                }
                else
                {
                    rightIndices.Add(triangleIndices[i]);
                }
            }

            // Fallback in case floating point inaccuracies cause an empty split
            if (leftIndices.Count == 0 || rightIndices.Count == 0)
            {
                node.TriangleIndices = triangleIndices;
                return node;
            }

            node.Left = BuildBVHNode(leftIndices.ToArray(), depth + 1);
            node.Right = BuildBVHNode(rightIndices.ToArray(), depth + 1);

            return node;
        }

        private BoundingBox GetBoundsForTriangles(int[] indices)
        {
            if (indices.Length == 0)
            {
                return new BoundingBox(Vector3.Zero, Vector3.Zero);
            }

            BoundingBox bounds = BoundingBox.FromTriangle(Triangles[indices[0]]);
            for (int i = 1; i < indices.Length; i++)
            {
                BoundingBox triBounds = BoundingBox.FromTriangle(Triangles[indices[i]]);
                bounds = BoundingBox.Combine(bounds, triBounds);
            }

            return bounds;
        }

        public override Intersection? Intersect(Ray ray)
        {
            // For small meshes, skip BVH overhead
            if (Triangles.Count <= BvhTriangleThreshold)
            {
                return IntersectDirect(ray);
            }

            // Build BVH on first intersection or after invalidation
            if (_bvhDirty)
            {
                BuildBVH();
            }

            // If BVH is null (very small mesh), do direct intersection
            if (_bvhRoot == null)
            {
                return IntersectDirect(ray);
            }

            return IntersectBVH(ray, _bvhRoot);
        }

        private Intersection? IntersectDirect(Ray ray)
        {
            float nearestDistance = float.MaxValue;
            Intersection? nearestIntersection = null;

            foreach (Triangle triangle in Triangles)
            {
                Intersection? hit = triangle.Intersect(ray);
                if (hit.HasValue && hit.Value.Distance < nearestDistance)
                {
                    nearestDistance = hit.Value.Distance;
                    nearestIntersection = hit;
                }
            }

            if (!nearestIntersection.HasValue)
            {
                return null;
            }

            Intersection intersection = nearestIntersection.Value;
            intersection.HitObject = this;
            return intersection;
        }

        private Intersection? IntersectBVH(Ray ray, BVHNode node)
        {
            if (!node.Bounds.IntersectRay(ray, out float _, out float _))
            {
                return null;
            }

            if (node.IsLeaf)
            {
                float nearestDistance = float.MaxValue;
                Intersection? nearestIntersection = null;

                if (node.TriangleIndices.Length >= 8 && TriangleIntersectionMath.IsSimdSupported)
                {
                    for (int i = 0; i + 7 < node.TriangleIndices.Length; i += 8)
                    {
                        Triangle t0 = Triangles[node.TriangleIndices[i]];
                        Triangle t1 = Triangles[node.TriangleIndices[i + 1]];
                        Triangle t2 = Triangles[node.TriangleIndices[i + 2]];
                        Triangle t3 = Triangles[node.TriangleIndices[i + 3]];
                        Triangle t4 = Triangles[node.TriangleIndices[i + 4]];
                        Triangle t5 = Triangles[node.TriangleIndices[i + 5]];
                        Triangle t6 = Triangles[node.TriangleIndices[i + 6]];
                        Triangle t7 = Triangles[node.TriangleIndices[i + 7]];

                        if (TriangleIntersectionMath.TryIntersectPacket(ray, t0, t1, t2, t3, out Intersection firstPacketHit) && firstPacketHit.Distance < nearestDistance)
                        {
                            nearestDistance = firstPacketHit.Distance;
                            nearestIntersection = firstPacketHit;
                        }

                        if (TriangleIntersectionMath.TryIntersectPacket(ray, t4, t5, t6, t7, out Intersection secondPacketHit) && secondPacketHit.Distance < nearestDistance)
                        {
                            nearestDistance = secondPacketHit.Distance;
                            nearestIntersection = secondPacketHit;
                        }
                    }

                    int remainderStart = node.TriangleIndices.Length - (node.TriangleIndices.Length % 8);
                    for (int i = remainderStart; i < node.TriangleIndices.Length; i++)
                    {
                        Intersection? hit = Triangles[node.TriangleIndices[i]].Intersect(ray);
                        if (hit.HasValue && hit.Value.Distance < nearestDistance)
                        {
                            nearestDistance = hit.Value.Distance;
                            nearestIntersection = hit;
                        }
                    }
                }
                else
                {
                    foreach (int idx in node.TriangleIndices)
                    {
                        Intersection? hit = Triangles[idx].Intersect(ray);
                        if (hit.HasValue && hit.Value.Distance < nearestDistance)
                        {
                            nearestDistance = hit.Value.Distance;
                            nearestIntersection = hit;
                        }
                    }
                }

                if (!nearestIntersection.HasValue)
                {
                    return null;
                }

                Intersection intersection = nearestIntersection.Value;
                intersection.HitObject = this;
                return intersection;
            }

            float nearestDist = float.MaxValue;
            Intersection? nearest = null;

            if (node.Left != null)
            {
                Intersection? leftHit = IntersectBVH(ray, node.Left);
                if (leftHit.HasValue && leftHit.Value.Distance < nearestDist)
                {
                    nearestDist = leftHit.Value.Distance;
                    nearest = leftHit;
                }
            }

            if (node.Right != null)
            {
                Intersection? rightHit = IntersectBVH(ray, node.Right);
                if (rightHit.HasValue && rightHit.Value.Distance < nearestDist)
                {
                    nearestDist = rightHit.Value.Distance;
                    nearest = rightHit;
                }
            }

            return nearest;
        }
    }
}
