using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class Material
    {
        public enum MaterialType { Solid, Checkerboard, Image }
        public enum TextureWrapMode { Clamp, Repeat, MirrorRepeat }

        public Vector3 Color; // Kept as Vector3 to match OpenTK for colors per typical logic, but can be customized later if an external Color3 struct is added
        public float Diffuse;
        public float Specular;
        public float Shininess;
        public float Reflectivity;
        public float RefractionIndex;
        public Surface? Texture;
        public MaterialType materialType;
        public Surface? Albedo;
        public Surface? NormalMap;
        public Surface? RoughnessMap;
        public Surface? MetallicMap;
        public Surface? AmbientOcclusionMap;
        public MaterialType Type;
        public Vector2 TextureOffset;
        public Vector2 TextureScale;
        public TextureWrapMode WrapMode;
        public bool Emissive;
        public Material()
        {
            Color = Vector3.One;
            Diffuse = 1.0f;
            Specular = 0.0f;
            Shininess = 0.0f;
            Reflectivity = 0.0f;
            RefractionIndex = 0.0f;
            Texture = null;
            materialType = MaterialType.Solid;
            Albedo = null;
            NormalMap = null;
            RoughnessMap = null;
            MetallicMap = null;
            AmbientOcclusionMap = null;
            Type = MaterialType.Solid;
            TextureOffset = Vector2.Zero;
            TextureScale = Vector2.One;
            WrapMode = TextureWrapMode.Clamp;
            Emissive = false;
        }

        // Centralized sampling helpers to remove repetition
        private Vector2 NormalizeAndTileUV(Vector2 UV)
        {
            return (UV * TextureScale) + TextureOffset;
        }

        private int GetTexel(Surface tex, Vector2 uv)
        {
            int x = ApplyWrapMode(uv.X, tex.width);

            int y = ApplyWrapMode(uv.Y, tex.height);
            return tex.pixels[(y * tex.width) + x];
        }

        private Vector3 DecodeRGBFromPixel(int pixel)
        {
            float r = ((pixel >> 16) & 0xFF) / 255.0f;
            float g = ((pixel >> 8) & 0xFF) / 255.0f;
            float b = (pixel & 0xFF) / 255.0f;
            return new Vector3(r, g, b);
        }

        private float DecodeGrayscaleFromPixel(int pixel)
        {
            // Use red channel for grayscale (common convention)
            return ((pixel >> 16) & 0xFF) / 255.0f;
        }

        public Vector3 GetColor(Vector2 UV)
        {
            switch (Type)
            {
                case MaterialType.Solid:
                    return Color;
                case MaterialType.Checkerboard:
                    bool isWhite = (MathF.Floor(UV.X) + MathF.Floor(UV.Y)) % 2 == 0;
                    return isWhite ? Color : Vector3.Zero;
                case MaterialType.Image:
                    if (Albedo == null) return Color;
                    Vector2 uv = NormalizeAndTileUV(UV);
                    int pixel = GetTexel(Albedo, uv);
                    return DecodeRGBFromPixel(pixel);
            }

            return Color;
        }

        private int ApplyWrapMode(float coordinate, int textureSize)
        {
            int pixelCoord = (int)(coordinate * textureSize);

            switch (WrapMode)
            {
                case TextureWrapMode.Clamp:
                    return Math.Clamp(pixelCoord, 0, textureSize - 1);

                case TextureWrapMode.Repeat:
                    return ((pixelCoord % textureSize) + textureSize) % textureSize; // safe modulo

                case TextureWrapMode.MirrorRepeat:
                    int repeats = Math.Abs(pixelCoord / textureSize);
                    int offset = Math.Abs(pixelCoord % textureSize);
                    if (repeats % 2 == 1)
                    {
                        offset = textureSize - 1 - offset;
                    }
                    return Math.Clamp(offset, 0, textureSize - 1);

                default:
                    return Math.Clamp(pixelCoord, 0, textureSize - 1);
            }
        }

        public void SetTextureTransform(Vector2 offset, Vector2 scale)
        {
            TextureOffset = offset;
            TextureScale = scale;
        }

        public void SetWrapMode(TextureWrapMode mode)
        {
            WrapMode = mode;
        }

        public Vector3 GetNormal(Vector2 UV)
        {
            if (NormalMap == null) return Vector3.UnitZ;
            Vector2 uv = NormalizeAndTileUV(UV);
            int pixel = GetTexel(NormalMap, uv);
            return DecodeNormal(DecodeRGBFromPixel(pixel));
        }

        private Vector3 DecodeNormal(Vector3 encodedNormal)
        {
            // Standard normal map decoding: [0,1] -> [-1,1]
            float x = (encodedNormal.X * 2.0f) - 1.0f;
            float y = (encodedNormal.Y * 2.0f) - 1.0f;
            float zSquared = 1.0f - (x * x) - (y * y);
            float z = zSquared > 0.0f ? MathF.Sqrt(zSquared) : 0.0f;
            return Vector3.Normalize(new Vector3(x, y, z));
        }

        public float GetRoughness(Vector2 UV)
        {
            if (RoughnessMap == null) return 0.0f;
            Vector2 uv = NormalizeAndTileUV(UV);
            int pixel = GetTexel(RoughnessMap, uv);
            return DecodeGrayscaleFromPixel(pixel);
        }

        public float GetMetallic(Vector2 UV)
        {
            if (MetallicMap == null) return 0.0f;
            Vector2 uv = NormalizeAndTileUV(UV);
            int pixel = GetTexel(MetallicMap, uv);
            return DecodeGrayscaleFromPixel(pixel);
        }

        public float GetAmbientOcclusion(Vector2 UV)
        {
            if (AmbientOcclusionMap == null) return 1.0f;
            Vector2 uv = NormalizeAndTileUV(UV);
            int pixel = GetTexel(AmbientOcclusionMap, uv);
            return DecodeGrayscaleFromPixel(pixel);
        }
    }

    public abstract class Primitive
    {
        public Material Material;

        public Primitive(Material material)
        {
            Material = material;
        }

        public abstract bool IsInfinite { get; }
        public abstract BoundingBox LocalBounds { get; }
        public abstract Intersection? Intersect(Ray ray);
    }

    public class Sphere : Primitive
    {
        public Vector3 Center;
        public float Radius;
        private readonly BoundingBox _localBounds;

        public Vector3 InitialCenter { get; private set; }

        public Sphere(Vector3 center, float radius, Material material) : base(material)
        {
            Center = center;
            Radius = radius;
            InitialCenter = center;

            _localBounds = new BoundingBox(new Vector3(-Radius), new Vector3(Radius));
        }

        public override bool IsInfinite => false;
        public override BoundingBox LocalBounds => _localBounds;

        public override Intersection? Intersect(Ray ray)
        {
            Vector3 rayToCenter = ray.Origin - Center;

            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float halfB = Vector3.Dot(rayToCenter, ray.Direction);
            float c = Vector3.Dot(rayToCenter, rayToCenter) - (Radius * Radius);

            float discriminant = (halfB * halfB) - (a * c);
            if (discriminant < 0.0f) return null;
            discriminant = MathF.Sqrt(discriminant);

            float hitDistance = (-halfB - discriminant) / a;
            if (hitDistance <= 0.0f) { hitDistance = (-halfB + discriminant) / a; }
            if (hitDistance <= 0.0f) { return null; }

            Vector3 hitPoint = ray.GetPoint(hitDistance);
            Vector3 normal = (hitPoint - Center) / Radius;
            float u = 0.5f + (MathF.Atan2(normal.Z, normal.X) / (2 * MathF.PI));
            float v = 0.5f - (MathF.Asin(normal.Y) / MathF.PI);
            Vector3 tangent = ComputeTangent(normal);
            Vector3 bitangent = ComputeBitangent(normal, tangent);

            return new Intersection
            {
                Distance = hitDistance,
                Normal = normal,
                Tangent = tangent,
                Bitangent = bitangent,
                HitObject = this,
                UV = new Vector2(u, v)
            };
        }

        public void resetPosition() => Center = InitialCenter;
        public Vector3 ComputeTangent(Vector3 normal)
        {
            // For a sphere, tangent is along the longitude (phi) direction
            // perpendicular to the normal
            Vector3 tangent = new Vector3(-normal.Z, 0, normal.X);
            if (tangent.LengthSquared < 0.0001f) tangent = new Vector3(1, 0, 0);
            return Vector3.Normalize(tangent);
        }

        public Vector3 ComputeBitangent(Vector3 normal, Vector3 tangent) => Vector3.Normalize(Vector3.Cross(normal, tangent));
    }

    public class Plane : Primitive
    {
        public Vector3 Normal;
        public Vector3 Point;
        private Vector3 UAxis;
        private Vector3 VAxis;

        public Plane(Vector3 normal, Vector3 point, Material material) : base(material)
        {
            Normal = Vector3.Normalize(normal);
            Point = point;
            Vector3 arbitrary = MathF.Abs(Normal.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
            UAxis = Vector3.Normalize(Vector3.Cross(arbitrary, Normal));
            VAxis = Vector3.Cross(Normal, UAxis);
        }

        public override bool IsInfinite => true;
        public override BoundingBox LocalBounds => default;

        public override Intersection? Intersect(Ray ray)
        {
            float denominator = Vector3.Dot(ray.Direction, Normal);
            if (MathF.Abs(denominator) < 1e-6f) return null;
            float t = Vector3.Dot(Point - ray.Origin, Normal) / denominator;
            if (t > 0.0f)
            {
                Vector3 hitPoint = ray.GetPoint(t);
                Vector3 localHit = hitPoint - Point;
                float u = Vector3.Dot(localHit, UAxis);
                float v = Vector3.Dot(localHit, VAxis);

                return new Intersection { Distance = t, Normal = Normal, Tangent = UAxis, Bitangent = VAxis, HitObject = this, UV = new Vector2(u, v) };
            }
            return null;
        }

        public Vector3 GetTangent() => UAxis;
        public Vector3 GetBitangent() => VAxis;
    }
}
