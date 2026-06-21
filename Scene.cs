using OpenTK.Mathematics;

namespace INFOGR_RAYTRACER
{
    public class Cubemap
    {
        private readonly Surface posX;
        private readonly Surface negX;
        private readonly Surface posY;
        private readonly Surface negY;
        private readonly Surface posZ;
        private readonly Surface negZ;

        public Cubemap(Surface posX, Surface negX, Surface posY, Surface negY, Surface posZ, Surface negZ)
        {
            this.posX = posX;
            this.negX = negX;
            this.posY = posY;
            this.negY = negY;
            this.posZ = posZ;
            this.negZ = negZ;
        }

        public Vector3 Sample(Vector3 direction)
        {
            direction = Vector3.Normalize(direction);

            float absX = MathF.Abs(direction.X);
            float absY = MathF.Abs(direction.Y);
            float absZ = MathF.Abs(direction.Z);

            Surface face;
            float u;
            float v;
            bool sideFace;

            if (absX >= absY && absX >= absZ)
            {
                sideFace = true;
                if (direction.X > 0)
                {
                    face = posX;
                    u = -direction.Z / absX;
                    v = -direction.Y / absX;
                }
                else
                {
                    face = negX;
                    u = direction.Z / absX;
                    v = -direction.Y / absX;
                }
            }
            else if (absY >= absX && absY >= absZ)
            {
                sideFace = false;
                if (direction.Y > 0)
                {
                    face = posY;
                    u = direction.X / absY;
                    v = -direction.Z / absY;
                }
                else
                {
                    face = negY;
                    u = direction.X / absY;
                    v = direction.Z / absY;
                }
            }
            else
            {
                sideFace = true;
                if (direction.Z > 0)
                {
                    face = posZ;
                    u = direction.X / absZ;
                    v = -direction.Y / absZ;
                }
                else
                {
                    face = negZ;
                    u = -direction.X / absZ;
                    v = -direction.Y / absZ;
                }
            }

            float mappedU = (u + 1.0f) * 0.5f;
            float mappedV = (v + 1.0f) * 0.5f;

            int x = Math.Clamp((int)(mappedU * (face.width - 1)), 0, face.width - 1);
            int y = sideFace
                ? Math.Clamp((int)(mappedV * (face.height - 1)), 0, face.height - 1)
                : Math.Clamp((int)((1.0f - mappedV) * (face.height - 1)), 0, face.height - 1);

            int pixel = face.pixels[(y * face.width) + x];
            float r = ((pixel >> 16) & 0xFF) / 255.0f;
            float g = ((pixel >> 8) & 0xFF) / 255.0f;
            float b = (pixel & 0xFF) / 255.0f;
            return new Vector3(r, g, b);
        }
    }

    public class Scene
    {
        public SceneNode RootNode = new SceneNode();
        public List<SceneNode> FlatNodes = new List<SceneNode>();
        public List<Light> Lights = new List<Light>();

        public Vector3 AmbientLight { get; internal set; }
        public Cubemap? EnvironmentCubemap { get; private set; }

        private Vector3 _skyColor = Vector3.Zero;

        public void SetAmbientLight(Vector3 color)
        {
            AmbientLight = color;
        }

        public void SetSkyColor(Vector3 newSkyColor)
        {
            _skyColor = newSkyColor;
        }

        public void SetCubemap(Cubemap cubemap)
        {
            EnvironmentCubemap = cubemap;
        }

        public Vector3 SampleEnvironment(Vector3 direction)
        {
            if (EnvironmentCubemap == null)
            {
                return _skyColor;
            }

            return EnvironmentCubemap.Sample(direction);
        }


        public void AddLight(Light light)
        {
            Lights.Add(light);
        }


        public void UpdateSceneGraph()
        {
            RootNode.UpdateTransforms(Matrix4.Identity);
            FlatNodes.Clear();
            RootNode.Flatten(FlatNodes);
        }

        public Intersection? Intersect(Ray ray)
        {
            float nearestT = float.MaxValue;
            Intersection? nearestIntersection = null;

            foreach (SceneNode node in FlatNodes)
            {
                if (!node.Primitive!.IsInfinite)
                {
                    if (!node.WorldBounds.IntersectRay(ray, out _, out _))
                        continue;
                }

                Matrix4 inv = node.WorldInverse;
                Vector3 objOrigin = Vector3.TransformPosition(ray.Origin, inv);
                Vector3 objDir = Vector3.TransformVector(ray.Direction, inv);
                Ray objRay = new Ray(objOrigin, objDir);

                // Intersect in Object Space
                Intersection? hit = node.Primitive!.Intersect(objRay);
                if (hit.HasValue)
                {
                    float t = hit.Value.Distance;
                    if (t > 0.0f && t < nearestT)
                    {
                        nearestT = t;
                        Intersection worldHit = hit.Value;
                        worldHit.LocalNormal = hit.Value.Normal;

                        Matrix4 invTranspose = Matrix4.Transpose(node.WorldInverse);
                        Vector3 worldNormal = Vector3.Normalize((new Vector4(hit.Value.Normal, 0.0f) * invTranspose).Xyz);

                        Vector3 worldTangent = Vector3.Normalize((new Vector4(hit.Value.Tangent, 0.0f) * node.WorldTransform).Xyz);
                        Vector3 worldBitangent = Vector3.Normalize((new Vector4(hit.Value.Bitangent, 0.0f) * node.WorldTransform).Xyz);

                        worldTangent = Vector3.Normalize(worldTangent - (worldNormal * Vector3.Dot(worldTangent, worldNormal)));
                        worldBitangent = Vector3.Cross(worldNormal, worldTangent);

                        worldHit.Normal = worldNormal;
                        worldHit.Tangent = worldTangent;
                        worldHit.Bitangent = worldBitangent;
                        worldHit.HitNode = node;

                        nearestIntersection = worldHit;
                    }
                }
            }

            return nearestIntersection;
        }

        public bool IsOccluded(Ray ray, float maxDist)
        {
            foreach (SceneNode node in FlatNodes)
            {
                if (!node.Primitive!.IsInfinite)
                {
                    if (!node.WorldBounds.IntersectRay(ray, out _, out _))
                        continue;
                }

                Matrix4 inv = node.WorldInverse;
                Vector3 objOrigin = Vector3.TransformPosition(ray.Origin, inv);
                Vector3 objDir = Vector3.TransformVector(ray.Direction, inv);
                Ray objRay = new Ray(objOrigin, objDir);

                Intersection? hit = node.Primitive!.Intersect(objRay);
                if (hit.HasValue)
                {
                    Primitive primitive = (Primitive)hit.Value.HitObject;
                    if (primitive.Material.Emissive) continue;

                    float worldT = hit.Value.Distance;

                    if (worldT > 0.001f && worldT < maxDist)
                        return true;
                }
            }
            return false;
        }
    }
}
