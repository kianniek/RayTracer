using OpenTK.Mathematics;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace INFOGR_RAYTRACER
{
    public class Raytracer
    {
        private Scene scene;
        private Camera camera;
        public DebugView debug;

        private float epsilon = 0.001f;
        private const int SAMPLE_SIZE = 16;
        private const float MIN_ATTENUATION = 0.1f;

        public Raytracer(Scene scene, Camera camera, DebugView debug)
        {
            this.scene = scene;
            this.camera = camera;
            this.debug = debug;
        }

        private Vector3 GetPerturbedNormal(Primitive primitive, SceneNode node, Vector3 localNormal, Vector2 uv)
        {
            // Sample normal map
            Vector3 tangentSpaceNormal = primitive.Material.GetNormal(uv);

            // Get tangent and bitangent in Object Space
            Vector3 tangent, bitangent;

            if (primitive is Triangle tri)
            {
                tangent = tri.ComputeTangent(localNormal);
                bitangent = tri.ComputeBitangent(localNormal, tangent);
            }
            else if (primitive is Sphere sphere)
            {
                tangent = sphere.ComputeTangent(localNormal);
                bitangent = sphere.ComputeBitangent(localNormal, tangent);
            }
            else if (primitive is Plane plane)
            {
                tangent = plane.GetTangent();
                bitangent = plane.GetBitangent();
            }
            else
            {
                tangent = new Vector3(1, 0, 0);
                bitangent = new Vector3(0, 1, 0);
            }

            // Transform normal from tangent space to object space
            Vector3 objNormal = (tangent * tangentSpaceNormal.X) +
                                (bitangent * tangentSpaceNormal.Y) +
                                (localNormal * tangentSpaceNormal.Z);

            // Transform object space normal to world space using inverse-transpose 
            Matrix4 invTranspose = Matrix4.Transpose(node.WorldInverse);
            Vector3 worldNormal = (new Vector4(objNormal, 0.0f) * invTranspose).Xyz;

            return Vector3.Normalize(worldNormal);
        }

        public Vector3 Trace(Ray ray, int depth = 0, bool isDebugRay = false)
        {
            if (depth > 5) return Vector3.Zero;

            Intersection? hit = scene.Intersect(ray);

            if (!hit.HasValue)
            {
                if (isDebugRay)
                {
                    // Primary ray misses scene geometry: White/Light Gray
                    Color3 missColor = (depth == 0) ? new Color3(0.8f, 0.8f, 0.8f) : new Color3(0.2f, 0.6f, 0.6f);
                    debug.Line(ray.Origin, ray.Origin + (ray.Direction * 10f), missColor);
                }

                return scene.SampleEnvironment(ray.Direction);
            }

            Intersection intersection = hit.Value;
            Primitive hitPrimitive = (Primitive)intersection.HitObject!;
            SceneNode hitNode = intersection.HitNode!; // Unpack Node
            Vector2 uv = intersection.UV ?? Vector2.Zero;
            Material mat = hitPrimitive.Material;
            Vector3 hitPoint = ray.GetPoint(intersection.Distance);

            if (mat.Emissive)
            {
                return mat.Color;
            }

            // Apply normal mapping if available
            Vector3 normal = GetPerturbedNormal(hitPrimitive, intersection.HitNode!, intersection.LocalNormal, uv);

            float roughness = mat.GetRoughness(uv);
            float ambientOcclusion = mat.GetAmbientOcclusion(uv);
            Vector3 baseColor = mat.GetColor(uv);
            float metallic = mat.GetMetallic(uv);

            if (isDebugRay)
            {
                // Primary vs Secondary color sorting
                Color3 rayColor = (depth == 0) ? new Color3(1, 1, 1) : new Color3(0, 0.8f, 1);
                debug.Line(ray.Origin, hitPoint, rayColor);
            }

            // Calculate face normal
            Vector3 faceNormal = Vector3.Dot(ray.Direction, normal) < 0 ? normal : -normal;

            // Calculate lights impact
            Vector3 diffuseColor = Vector3.Zero;
            Vector3 specularColor = Vector3.Zero;

            float lightIntensity = 1.0f;
            foreach (Light light in scene.Lights)
            {

                if (!light.LightState) continue;
                Vector3 L;
                float lightDistance;
                Vector3 debugEnd;

                Ray shadowRay;
                bool blocked;
                float dotNL;

                lightIntensity = 1.0f;


                if (light is SphereLight sphereLight)
                {
                    Vector3 areaLight = Vector3.Zero;
                    Vector3 areaSpec = Vector3.Zero;

                    for (int i = 0; i < SAMPLE_SIZE; i++)
                    {
                        Vector3 randomPoint = sphereLight.GetRandomPoint();
                        Vector3 rayToLight = randomPoint - hitPoint;
                        lightDistance = rayToLight.Length;
                        float maxAttenuation = sphereLight.shape.Radius;
                        float lightAttenuation = Math.Clamp(maxAttenuation / lightDistance, 0.0f, 1.0f);

                        // if light attenuation is minimal, we can ignore lighting this pixel
                        if (lightAttenuation < MIN_ATTENUATION) continue;

                        L = Vector3.Normalize(rayToLight);

                        dotNL = Vector3.Dot(faceNormal, L);
                        if (dotNL <= 0.0f) continue; // light either faces perpendicular or away from surface

                        shadowRay = new Ray(hitPoint + (faceNormal * epsilon), L);
                        blocked = scene.IsOccluded(shadowRay, lightDistance);
                        debugEnd = randomPoint;

                        if (isDebugRay)
                        {
                            if (blocked) debug.DashedLine(hitPoint, debugEnd, new Color3(1.0f, 0.2f, 0.2f)); // Red: Blocked
                            else debug.Line(hitPoint, debugEnd, new Color3(1.0f, 0.9f, 0.0f)); // Yellow: Clear
                        }

                        if (blocked) continue;

                        areaLight += sphereLight.Color * baseColor * dotNL * (mat.Diffuse * (1.0f - metallic)) * lightAttenuation;

                        // yes i had to take over your specular code for this i know its redundant </3
                        if (mat.Specular > 0.0f)
                        {
                            Vector3 V = camera.Mode == Camera.ProjectionMode.Orthographic
                                ? -ray.Direction
                                : Vector3.Normalize(ray.Origin - hitPoint);

                            Vector3 incomingLight = -L;
                            Vector3 R = incomingLight -
                                        (2.0f * Vector3.Dot(incomingLight, faceNormal) * faceNormal);
                            R = Vector3.Normalize(R);

                            float dotVR = MathF.Max(0.0f, Vector3.Dot(V, R));
                            float effectiveShininess = MathF.Max(0.001f, mat.Shininess * (1.0f - roughness));
                            float specFactor = MathF.Pow(dotVR, effectiveShininess);

                            Vector3 specularTint = Vector3.Lerp(Vector3.One, baseColor, metallic);
                            areaSpec += sphereLight.Color * specFactor * mat.Specular * specularTint * lightAttenuation;
                        }
                    }
                    diffuseColor += areaLight / SAMPLE_SIZE;
                    specularColor += areaSpec / SAMPLE_SIZE;
                }

                if (light is SpotLight spotLight)
                {
                    lightIntensity = 1.0f;
                    L = spotLight.Position - hitPoint;
                    lightDistance = L.Length;
                    Vector3 spotDir = Vector3.Normalize(spotLight.Direction);
                    float dotSL = MathF.Max(0.0f, Vector3.Dot(spotDir, Vector3.Normalize(-L)));
                    float angle = MathF.Acos(Math.Clamp(dotSL, -1f, 1f));

                    if (angle >= spotLight.CutOffAngle)
                    {
                        continue;
                    }

                    float innerAngle = spotLight.CutOffAngle * 0.80f;
                    if (angle > innerAngle)
                    {
                        float i = (angle - innerAngle) / (spotLight.CutOffAngle - innerAngle);
                        lightIntensity = 1.0f - (i * i * (3f - (2f * i)));
                    }

                    debugEnd = spotLight.Position;
                }
                else if (light is DirectionalLight directionalLight)
                {
                    L = -directionalLight.Direction;
                    lightDistance = float.PositiveInfinity;
                    debugEnd = hitPoint + (L * 10f);
                }
                else if (light is PointLight pointLight)
                {
                    L = pointLight.Position - hitPoint;
                    lightDistance = L.Length;
                    L = Vector3.Normalize(L);
                    debugEnd = pointLight.Position;
                }
                else
                {
                    continue;
                }

                // Shadow ray logic
                // Only applies to other lights that is not AreaLighting
                // Small offset on hitPoint to prevent self-intersection (shadow acne)
                shadowRay = new Ray(hitPoint + (faceNormal * epsilon), L);
                blocked = scene.IsOccluded(shadowRay, lightDistance);

                if (isDebugRay)
                {
                    if (blocked) debug.DashedLine(hitPoint, debugEnd, new Color3(1.0f, 0.2f, 0.2f)); // Red: Blocked
                    else debug.Line(hitPoint, debugEnd, new Color3(1.0f, 0.9f, 0.0f)); // Yellow: Clear
                }

                if (blocked)
                {
                    continue;
                }

                // Diffuse (Using faceNormal)
                dotNL = MathF.Max(0.0f, Vector3.Dot(faceNormal, L));
                diffuseColor += light.Color * baseColor * dotNL * (mat.Diffuse * (1.0f - metallic)) *
                                lightIntensity;

                // Specular (Phong)
                if (mat.Specular > 0.0f && dotNL > 0.0f)
                {
                    Vector3 V = camera.Mode == Camera.ProjectionMode.Orthographic
                        ? -ray.Direction
                        : Vector3.Normalize(ray.Origin - hitPoint);

                    // R is reflected light vector 
                    // Phong reflection (Using faceNormal)
                    Vector3 incomingLight = -L;
                    Vector3 R = incomingLight - (2.0f * Vector3.Dot(incomingLight, faceNormal) * faceNormal);
                    R = Vector3.Normalize(R);

                    float dotVR = MathF.Max(0.0f, Vector3.Dot(V, R));

                    // Apply roughness to shininess & clamp to avoid MathF.Pow(x, 0)
                    float effectiveShininess = MathF.Max(0.001f, mat.Shininess * (1.0f - roughness));
                    float specFactor = MathF.Pow(dotVR, effectiveShininess);

                    // Simple metallic workflow: lerp between dielectric white specular and metal tinted specular
                    Vector3 specularTint = Vector3.Lerp(Vector3.One, baseColor, metallic);
                    specularColor += light.Color * specFactor * mat.Specular * specularTint;
                }
            }



            // Combine lighting (using cached AO and baseColor)
            Vector3 color = (baseColor * scene.AmbientLight * ambientOcclusion * (1.0f - metallic)) + diffuseColor +
                            specularColor;

            float outerIndex = 1.0f; // air, for now
            float innerIndex = mat.RefractionIndex;
            Vector3 viewDirNorm = Vector3.Normalize(ray.Direction);

            Vector3 surfaceNorm = Vector3.Normalize(normal);
            if (Vector3.Dot(surfaceNorm, viewDirNorm) > 0) //flips the normal if ray comes from within the shape
            {
                surfaceNorm = -surfaceNorm;
                (outerIndex, innerIndex) = (innerIndex, outerIndex);
            }

            float refraction = outerIndex / innerIndex;
            float dotVS = -Vector3.Dot(viewDirNorm, surfaceNorm);

            // fresnel refraction
            float ro = MathF.Pow((innerIndex - outerIndex) / (innerIndex + outerIndex), 2);
            float reflectionCoeff = ro + ((1 - ro) * MathF.Pow(1 - dotVS, 5));

            float transmittance = 1 - reflectionCoeff;

            // V(1 - (Nout^2/Nin^2) * (1-(D . N)^2)
            float discriminant = 1 - (refraction * refraction * (1 - (dotVS * dotVS)));

            //Refractive Ray Logic
            if (discriminant >= 0)
            {
                float refractAngle = MathF.Sqrt(discriminant);
                // t = refraction * (view - (DotVS)surfNorm) - refractedAngle * surfNorm
                Vector3 refractDir = (refraction * (viewDirNorm + (dotVS * surfaceNorm))) - (refractAngle * surfaceNorm);
                Ray refractedRay = new Ray(hitPoint - (surfaceNorm * epsilon), refractDir);
                Vector3 refractedColor = Trace(refractedRay, depth + 1, isDebugRay);
                color += refractedColor * transmittance;
            }
            // Reflective ray logic
            if (mat.Reflectivity > 0.0f)
            {
                // Reflective direction calculation
                Vector3 reflectDir = ray.Direction - (2.0f * Vector3.Dot(ray.Direction, normal) * normal);
                reflectDir = Vector3.Normalize(reflectDir);

                Ray reflectiveRay = new Ray(hitPoint + (normal * epsilon), reflectDir);
                Vector3 reflectedColor = Trace(reflectiveRay, depth + 1, isDebugRay);

                color = (color * (1 - mat.Reflectivity)) + (reflectedColor * mat.Reflectivity);
            }


            // Clamp on components
            color.X = MathF.Min(1.0f, color.X);
            color.Y = MathF.Min(1.0f, color.Y);
            color.Z = MathF.Min(1.0f, color.Z);
            return color;
        }
    }
}