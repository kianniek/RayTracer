using Assimp;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace INFOGR_RAYTRACER
{
    public static class Util
    {
        // conversions from Assimp datatypes to corresponding OpenTK datatypes
        public static Matrix4 ToOpenTK(this Matrix4x4 m)
        {
            return new Matrix4(m.A1, m.A2, m.A3, m.A4,
                m.B1, m.B2, m.B3, m.B4,
                m.C1, m.C2, m.C3, m.C4,
                m.D1, m.D2, m.D3, m.D4);
        }

        public static Vector3 ToOpenTK(this Vector3D v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static TriangleMesh ImportMesh(string filename, Material? material = null)
        {
            Material defaultMeshMaterial = material ?? new Material
            {
                Diffuse = 1.0f,
                Specular = 0.1f,
                Shininess = 20.0f,
                Reflectivity = 0.0f,
                Type = Material.MaterialType.Solid
            };

            TriangleMesh result = new TriangleMesh(defaultMeshMaterial)
            {
                Filename = filename
            };

            AssimpContext importer = new();
            Assimp.Scene model = importer.ImportFile(filename, PostProcessSteps.Triangulate);
            Node node = model.RootNode;
            Matrix4 prev = Matrix4.Identity;
            ImportMesh(model, node, prev, result);

            result.BuildBVH();

            return result;
        }

        private static void ImportMesh(Assimp.Scene model, Node node, Matrix4 prev, TriangleMesh result)
        {
            Matrix4 transform = node.Transform.ToOpenTK() * prev;
            Matrix3 normalTransform = new Matrix3(transform).Inverted().Transposed();

            if (node.HasMeshes)
            {
                foreach (int index in node.MeshIndices)
                {
                    Assimp.Mesh mesh = model.Meshes[index];
                    bool hasTexCoords = mesh.HasTextureCoords(0);
                    foreach (Face face in mesh.Faces)
                    {
                        if (face.IndexCount != 3) continue;

                        Vector3 p1 = (new Vector4(mesh.Vertices[face.Indices[0]].ToOpenTK(), 1) * transform).Xyz;
                        Vector3 p2 = (new Vector4(mesh.Vertices[face.Indices[1]].ToOpenTK(), 1) * transform).Xyz;
                        Vector3 p3 = (new Vector4(mesh.Vertices[face.Indices[2]].ToOpenTK(), 1) * transform).Xyz;
                        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        Vector3 n1 = faceNormal;
                        Vector3 n2 = faceNormal;
                        Vector3 n3 = faceNormal;
                        if (mesh.HasNormals)
                        {
                            Vector3 normalVec1 = new Vector3(mesh.Normals[face.Indices[0]].ToOpenTK());
                            Vector3 normalVec2 = new Vector3(mesh.Normals[face.Indices[1]].ToOpenTK());
                            Vector3 normalVec3 = new Vector3(mesh.Normals[face.Indices[2]].ToOpenTK());
                            n1 = Vector3.Normalize(normalVec1 * normalTransform);
                            n2 = Vector3.Normalize(normalVec2 * normalTransform);
                            n3 = Vector3.Normalize(normalVec3 * normalTransform);
                        }
                        Vector2 uv1 = Vector2.Zero, uv2 = Vector2.Zero, uv3 = Vector2.Zero;
                        if (hasTexCoords)
                        {
                            Vector2 offset = new(0, 1);
                            Vector2 scale = new(1, -1);
                            uv1 = (mesh.TextureCoordinateChannels[0][face.Indices[0]].ToOpenTK().Xy * scale) + offset;
                            uv2 = (mesh.TextureCoordinateChannels[0][face.Indices[1]].ToOpenTK().Xy * scale) + offset;
                            uv3 = (mesh.TextureCoordinateChannels[0][face.Indices[2]].ToOpenTK().Xy * scale) + offset;
                        }
                        result.AddTriangle(new Triangle(p1, p2, p3, n1, n2, n3, uv1, uv2, uv3, result.Material));
                    }
                }
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                ImportMesh(model, node.Children[i], transform, result);
            }
        }

        public static void ScaleToScreen(Surface source, Surface destination)
        {
            for (int y = 0; y < destination.height; y++)
            {
                for (int x = 0; x < destination.width; x++)
                {
                    // Map large pixel (x,y) back to small buffer (srcX, srcY)
                    int srcX = (int)(x * (float)source.width / destination.width);
                    int srcY = (int)(y * (float)source.height / destination.height);

                    destination.Plot(x, y, source.GetPixel(srcX, srcY));
                }
            }
        }
    }

    public struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetPoint(float t) => Origin + (Direction * t);
    }

    public class SceneNode
    {
        public Matrix4 LocalTransform = Matrix4.Identity;
        public Matrix4 WorldTransform = Matrix4.Identity;
        public Matrix4 WorldInverse = Matrix4.Identity;

        public Primitive? Primitive { get; set; }

        public SceneNode? Parent { get; private set; }
        public List<SceneNode> Children { get; } = new List<SceneNode>();

        public BoundingBox WorldBounds { get; private set; }

        public void AddChild(SceneNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public void UpdateTransforms(Matrix4 parentTransform)
        {
            WorldTransform = LocalTransform * parentTransform;
            WorldInverse = WorldTransform.Inverted();

            if (Primitive != null)
            {
                // Transform the 8 corners of the AABB
                Vector3 min = Primitive.LocalBounds.Min;
                Vector3 max = Primitive.LocalBounds.Max;
                Vector3[] corners =
                [
                    new(min.X, min.Y, min.Z), new(min.X, min.Y, max.Z),
                    new(min.X, max.Y, min.Z), new(min.X, max.Y, max.Z),
                    new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z),
                    new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z)
                ];

                Vector3 newMin = new Vector3(float.MaxValue);
                Vector3 newMax = new Vector3(float.MinValue);

                foreach (var corner in corners)
                {
                    Vector3 worldCorner = Vector3.TransformPosition(corner, WorldTransform);
                    newMin = Vector3.ComponentMin(newMin, worldCorner);
                    newMax = Vector3.ComponentMax(newMax, worldCorner);
                }
                WorldBounds = new BoundingBox(newMin, newMax);
            }

            foreach (var child in Children)
                child.UpdateTransforms(WorldTransform);
        }

        public void Flatten(List<SceneNode> flatList)
        {
            if (Primitive != null)
            {
                flatList.Add(this);
            }

            foreach (var child in Children)
            {
                child.Flatten(flatList);
            }
        }
    }

    public struct Intersection
    {
        public float Distance;
        public Vector3 Normal;
        public Vector3 LocalNormal;
        public Vector3 Tangent;
        public Vector3 Bitangent;
        public object? HitObject;
        public SceneNode? HitNode;
        public Vector2? UV;
    }
}
