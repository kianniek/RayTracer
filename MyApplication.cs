using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace INFOGR_RAYTRACER
{
    //temporarily setting it to public
    internal enum SceneMode
    {
        MeshHeavy,
        SphereGrid,
        MixedPrimitives,
        AreaLight
    }

    internal class MyApplication
    {
        private readonly Lock _lock = new();

        private readonly GameWindow window;
        private float renderScale = 1.0f;
        private Surface renderBuffer;

        private SceneMode currentSceneMode = SceneMode.MixedPrimitives;

        private int frameCount = 0;
        private int renderdAnimationFrameCount;
        private bool animate = false;
        private const int SamplesPerAnimationFrame = 10; // how many samples to render before advancing the animation frame
        private int currentAnimationFrameSamples;
        private bool showDebugView = false;
        private bool enableAntiAliasing = true;

        // member variables
        public Surface screen;
        public KeyboardState? keyboard;
        public MouseState? mouse;
        private readonly Stopwatch timer = new();

        private Scene scene;
        private Camera camera;
        private Raytracer raytracer;
        private DebugView debugView;
        private int hoveredColumn = -1;

        // Performance tracking
        private TimeSpan frameAccumulator = new();
        private uint frames = 0;
        private string timeString = "---- ms/frame";

        // Progressive accumulation
        private Vector3[] accumulationBuffer;
        private int accumulatedFrames = 0;
        private bool resetAccumulation = true;

        // For fun and ease of testing: dynamic resolution
        private bool autoScaleRenderScale = false;
        private const float TargetFrameTimeMs = 1000.0f / 60.0f;
        private const float ThresholdMarginMs = 1.5f;
        private const float MinRenderScale = 0.1f;
        private const float MaxRenderScale = 2.0f;
        private float initialRenderScale = 1.0f;


        private SceneNode _hoverBikeNode;

        // Controls
        private static readonly string[] ControlsInfo =
        {
            "WASD: Move",
            "Q/E: Yaw",
            "F/R: Pitch",
            "Space: Move Camera Up",
            "Left Shift: Move Camera Down",
            "T/G: Change FOV",
            "Right Shift: Toggle Animation",
            "O/I: Focus Distance",
            "K/L: Aperture",
            "Tab: Toggle Debug View",
            "Z: Toggle Anti-Aliasing",
            "P: Toggle Projection Mode",
            "1, 2, 3, 4: Switch Scenes",
            "[backtick]: Toggle Controls"
        };
        private bool hideControls = false;

        private Cubemap yokohama3Cubemap = new Cubemap(
            new Surface("../../../assets/cubemaps/yokohama3/posx.jpg"),
            new Surface("../../../assets/cubemaps/yokohama3/negx.jpg"),
            new Surface("../../../assets/cubemaps/yokohama3/posy.jpg"),
            new Surface("../../../assets/cubemaps/yokohama3/negy.jpg"),
            new Surface("../../../assets/cubemaps/yokohama3/posz.jpg"),
            new Surface("../../../assets/cubemaps/yokohama3/negz.jpg"));

        private ConcurrentQueue<Keys> keyBuffer = new ConcurrentQueue<Keys>();

        public MyApplication(Surface screen, GameWindow window)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            this.screen = screen;
            this.window = window;

            debugView = new DebugView(screen);

            scene = new Scene();
            camera = new Camera(
                new Vector3(0, 1, -5),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                90,
                1.0f,
                (float)screen.width / screen.height,
                focusDistance: 5.0f,
                aperture: 0.00f
                );

            raytracer = new Raytracer(scene, camera, debugView);
            renderBuffer = new Surface(screen.width, screen.height);
            accumulationBuffer = new Vector3[screen.width * screen.height];
        }

        public void RegisterKeyPress(Keys key)
        {
            keyBuffer.Enqueue(key);
        }

        public void Init()
        {
            scene.SetAmbientLight(new Vector3(0.1f, 0.1f, 0.1f));
            scene.SetCubemap(yokohama3Cubemap);
            scene.AddLight(new DirectionalLight(new Vector3(-1, -1, -0.5f), new Vector3(0.8f, 0.8f, 0.8f)));
            scene.AddLight(new PointLight(new Vector3(0, 1, 0), new Vector3(1f, 1f, 1f)));
            scene.AddLight(new PointLight(new Vector3(0, 1, 0), new Vector3(1f, 1f, 1f)));
            scene.AddLight(new SpotLight(new Vector3(1, 3, 1), new Vector3(1, 1, 0), 35, new Vector3(-.4f, -1, 0)));
            scene.AddLight(new SpotLight(new Vector3(0, 5, 0), new Vector3(1, 0, 0), 15, new Vector3(0, -1, 0)));
            scene.AddLight(new SpotLight(new Vector3(0, 5, 0), new Vector3(0, 1, 1), 20, new Vector3(-1, -1, 0.2f)));
            CreateAreaLights(true);


            LoadScene(currentSceneMode);

            Console.WriteLine(TriangleIntersectionMath.IsSimdSupported
                ? $"SIMD enabled: {GetSimdModeName()}"
                : "SIMD disabled");

            timer.Start();
        }

        private void LoadScene(SceneMode mode)
        {
            lock (_lock)
            {
                scene.RootNode = new SceneNode();
                currentSceneMode = mode;


                switch (mode)
                {
                    case SceneMode.MeshHeavy:
                        CreateMeshScene();
                        break;
                    case SceneMode.SphereGrid:
                        CreateSphereGridScene();
                        break;
                    case SceneMode.MixedPrimitives:
                        CreatePrimitivesScene();
                        break;
                    case SceneMode.AreaLight:
                        CreateAreaLightScene();
                        break;
                }

                scene.UpdateSceneGraph();
                resetAccumulation = true;
            }
        }

        private void CreateMeshScene()
        {
            ToggleAreaLights(false);
            Material rockMat = new Material
            {
                Type = Material.MaterialType.Image,
                TextureScale = new Vector2(0.5f),
                WrapMode = Material.TextureWrapMode.MirrorRepeat,
                Albedo = new Surface("../../../assets/textures/rock/Rock064_2K-PNG_Color.png"),
                NormalMap = new Surface("../../../assets/textures/rock/Rock064_2K-PNG_NormalGL.png"),
                RoughnessMap = new Surface("../../../assets/textures/rock/Rock064_2K-PNG_Roughness.png"),
                AmbientOcclusionMap = new Surface("../../../assets/textures/rock/Rock064_2K-PNG_AmbientOcclusion.png")
            };

            Material hoverBikeMat = new Material
            {
                Type = Material.MaterialType.Image,
                Albedo = new Surface("../../../assets/models/hover-bike/Color.png"),
                NormalMap = new Surface("../../../assets/models/hover-bike/Normal.png"),
                RoughnessMap = new Surface("../../../assets/models/hover-bike/Roughness.png"),
                AmbientOcclusionMap = new Surface("../../../assets/models/hover-bike/AO.png"),
                MetallicMap = new Surface("../../../assets/models/hover-bike/Metallic.png")
            };

            Plane planePrim = new Plane(Vector3.UnitY, Vector3.Zero, rockMat);
            SceneNode planeNode = new SceneNode { Primitive = planePrim };
            scene.RootNode.AddChild(planeNode);

            TriangleMesh hoverBikeMesh = Util.ImportMesh("../../../assets/models/hover-bike/hover-bike.fbx", hoverBikeMat);
            for (int z = 0; z < 10; z++)
            {
                for (int x = 0; x < 10; x++)
                {
                    var bikeNode = new SceneNode
                    {
                        Primitive = hoverBikeMesh,
                        LocalTransform = Matrix4.CreateScale(0.01f) *
                                         Matrix4.CreateRotationX(MathF.PI) *
                                         Matrix4.CreateRotationY(Random.Shared.NextSingle() * MathF.PI) *
                                         Matrix4.CreateTranslation(new Vector3(x * 7, 3, z * 7))
                    };
                    scene.RootNode.AddChild(bikeNode);

                    _hoverBikeNode = bikeNode;
                }
            }
        }

        private void CreateSphereGridScene()
        {
            ToggleAreaLights(false);
            Material floorMat = new Material { Type = Material.MaterialType.Checkerboard, Color = new Vector3(0.6f) };
            scene.RootNode.AddChild(new SceneNode { Primitive = new Plane(Vector3.UnitY, Vector3.Zero, floorMat) });

            int rows = 5;
            int cols = 5;
            float spacing = 2.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float roughness = (float)r / (rows - 1);
                    float metallic = (float)c / (cols - 1);

                    Material sphereMat = new Material
                    {
                        Type = Material.MaterialType.Solid,
                        Color = new Vector3(0.9f, 0.2f, 0.2f),
                        Specular = 0.5f,
                        Shininess = (1.0f - roughness) * 100.0f,
                        Reflectivity = metallic
                    };

                    Sphere spherePrim = new Sphere(Vector3.Zero, 1.0f, sphereMat);
                    SceneNode sphereNode = new SceneNode
                    {
                        Primitive = spherePrim,
                        LocalTransform = Matrix4.CreateTranslation(new Vector3((c - (cols / 2f)) * spacing, 1.0f, ((r - (rows / 2f)) * spacing) + 5.0f))
                    };
                    scene.RootNode.AddChild(sphereNode);
                }
            }
        }

        private void CreatePrimitivesScene()
        {
            ToggleAreaLights(false);
            Material floorMat = new Material { Type = Material.MaterialType.Checkerboard, Color = new Vector3(0.3f, 0.5f, 0.3f) };
            scene.RootNode.AddChild(new SceneNode { Primitive = new Plane(Vector3.UnitY, Vector3.Zero, floorMat) });

            Material wallMat = new Material { Type = Material.MaterialType.Solid, Color = new Vector3(0.5f, 0.5f, 0.7f), Reflectivity = 0.2f };
            Plane backWall = new Plane(new Vector3(0, 0, -1), new Vector3(0, 0, 10), wallMat);
            scene.RootNode.AddChild(new SceneNode { Primitive = backWall });

            Material goldMat = new Material { Type = Material.MaterialType.Solid, Color = new Vector3(1.0f, 0.85f, 0.57f), Reflectivity = 0.8f, Specular = 0.9f };
            Material glassMat = new Material { Type = Material.MaterialType.Solid, Color = new Vector3(0.1f, 0.1f, 0.1f), Reflectivity = 0.9f }; // Mirror-like setup
            Material windowMat = new Material
            {
                Type = Material.MaterialType.Solid,
                Color = new Vector3(0.1f, 0.1f, 0.1f),
                Reflectivity = 0.5f,
                RefractionIndex = 1.52f
            };
            Material refractiveMat = new Material
            { Type = Material.MaterialType.Solid, Color = new Vector3(0.1f, 0.1f, 0.1f), RefractionIndex = 1.33f };

            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 1.5f, goldMat),
                LocalTransform = Matrix4.CreateTranslation(-3, 1.5f, 5)
            });

            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 1.0f, glassMat),
                LocalTransform = Matrix4.CreateTranslation(0, 1.0f, 4)
            });

            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 0.5f, new Material { Color = new Vector3(0.1f, 0.8f, 0.8f) }),
                LocalTransform = Matrix4.CreateTranslation(2.5f, 0.5f, 3.5f)
            });
            scene.RootNode.AddChild(new SceneNode()
            {
                Primitive = new Sphere(Vector3.Zero, 0.8f, windowMat),
                LocalTransform = Matrix4.CreateTranslation(1, 1, 1)
            });
            scene.RootNode.AddChild(new SceneNode()
            {
                Primitive = new Sphere(Vector3.Zero, 0.5f, refractiveMat),
                LocalTransform = Matrix4.CreateTranslation(1, 0.5f, -1)
            });
        }

        private void CreateAreaLightScene()
        {
            ToggleAreaLights(true);
            Material floorMat = new Material { Type = Material.MaterialType.Checkerboard, Color = new Vector3(0.3f, 0.5f, 0.3f) };
            scene.RootNode.AddChild(new SceneNode { Primitive = new Plane(Vector3.UnitY, Vector3.Zero, floorMat) });

            Material wallMat = new Material { Type = Material.MaterialType.Solid, Color = new Vector3(0.5f, 0.5f, 0.7f), Reflectivity = 0.2f };
            Plane backWall = new Plane(new Vector3(0, 0, -1), new Vector3(0, 0, 10), wallMat);
            scene.RootNode.AddChild(new SceneNode { Primitive = backWall });

            Material sphereMat = new Material { Type = Material.MaterialType.Solid, Color = new Vector3(0.9f, 0.2f, 0.2f), Specular = 0.5f, };

            CreateAreaLights(false);


            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 0.5f, sphereMat),
                LocalTransform = Matrix4.CreateTranslation(1, 0.5f, -2)
            });
            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 1.2f, sphereMat),
                LocalTransform = Matrix4.CreateTranslation(0.5f, 1f, -1)
            });
            scene.RootNode.AddChild(new SceneNode
            {
                Primitive = new Sphere(Vector3.Zero, 0.5f, sphereMat),
                LocalTransform = Matrix4.CreateTranslation(-2, 1f, -3)
            });

        }

        public void CreateAreaLights(bool lights)
        {

            Material emissiveWhite = new Material()
            {
                Color = new Vector3(1, 1, 1),
                Emissive = true
            };
            Material emissiveRed = new Material()
            {
                Color = new Vector3(1, 1, 0),
                Emissive = true
            };

            Sphere whiteSphereLight = new Sphere(Vector3.Zero, 0.7f, emissiveWhite);
            Vector3 whiteSpherePos = new Vector3(-0.5f, 2f, -2f);
            Sphere redSphereLight = new Sphere(Vector3.Zero, 0.3f, emissiveRed);
            Vector3 redSpherePos = new Vector3(1, 1f, -3);

            if (lights)
            {
                scene.AddLight(new SphereLight(whiteSphereLight, whiteSpherePos, 1f));
                scene.AddLight(new SphereLight(redSphereLight, redSpherePos, 1f));
            }
            else
            {
                scene.RootNode.AddChild(new SceneNode
                {
                    Primitive = whiteSphereLight,
                    LocalTransform = Matrix4.CreateTranslation(whiteSpherePos)
                });
                scene.RootNode.AddChild(new SceneNode
                {
                    Primitive = redSphereLight,
                    LocalTransform = Matrix4.CreateTranslation(redSpherePos)
                });
            }
        }

        private void ToggleAreaLights(bool state)
        {
            foreach (Light light in scene.Lights)
            {
                if (light is SphereLight sphereLight)
                {
                    sphereLight.ToggleLight(state);
                }
                else
                {
                    light.ToggleLight(!state);
                }
            }
        }

        private static string GetSimdModeName()
        {
            if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported)
            {
                return "AVX-512";
            }

            if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            {
                return "AVX2";
            }

            if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
            {
                return "SSE";
            }

            return "Scalar";
        }

        public void OnMouseClick(int x, int y)
        {
            AutoFocus(x, y);
        }

        private void AutoFocus(int x, int y)
        {
            // get screen coords
            float u = (x + 0.5f) / screen.width;
            float v = (y + 0.5f) / screen.height;

            // use a pinhole ray (aperture = 0) for precise picking
            float oldAperture = camera.Aperture;
            camera.Aperture = 0.0f;
            Ray pickRay = camera.GetRay(u, v);
            camera.Aperture = oldAperture;

            Intersection? hit = scene.Intersect(pickRay);

            if (hit.HasValue)
            {
                camera.FocusDistance = hit.Value.Distance;
            }
            else
            {
                // if nothing was hit, set a large focus distance (infinite-ish)
                camera.FocusDistance = 1000.0f;
            }

            camera.Update();
            resetAccumulation = true;
        }

        private void ResetAccumulation()
        {
            Array.Clear(accumulationBuffer, 0, accumulationBuffer.Length);
            accumulatedFrames = 0;
            resetAccumulation = false;
        }

        public void Resize(int width, int height)
        {
            lock (_lock)
            {
                screen.Resize(width, height);

                // Calculate internal size based on scale
                int internalWidth = (int)(width * renderScale);
                int internalHeight = (int)(height * renderScale);

                // Ensure at least 1 pixel
                internalWidth = Math.Max(1, internalWidth);
                internalHeight = Math.Max(1, internalHeight);

                renderBuffer = new Surface(internalWidth, internalHeight);
                accumulationBuffer = new Vector3[internalWidth * internalHeight];

                camera.AspectRatio = width / (float)height;
                camera.Update();
                resetAccumulation = true;
            }
        }

        public void Resize()
        {
            Resize(screen.width, screen.height);
        }

        private void AutoBalanceRenderScale(float lastFrameTimeMs)
        {
            if (!autoScaleRenderScale) return;

            // Define thresholds based on our target performance
            float upperLimit = TargetFrameTimeMs + ThresholdMarginMs;
            float lowerLimit = TargetFrameTimeMs - ThresholdMarginMs;

            float oldScale = renderScale;

            if (lastFrameTimeMs > upperLimit)
            {
                // Frame took too long; drop resolution quickly to catch up.
                // The size of the performance gap dictates how drastically we drop it.
                float severity = (lastFrameTimeMs - TargetFrameTimeMs) / TargetFrameTimeMs;
                renderScale -= MathF.Max(0.02f, 0.1f * severity);
            }
            else if (lastFrameTimeMs < lowerLimit)
            {
                // Headroom available; incrementally scale resolution upward safely.
                renderScale += 0.005f;
            }

            // Constrain limits
            renderScale = Math.Clamp(renderScale, MinRenderScale, MaxRenderScale);

            // If changes are meaningful, trigger an actual texture resize
            if (MathF.Abs(renderScale - oldScale) > 0.001f)
            {
                Resize();
            }
        }

        private bool HandleInput(float dt)
        {
            bool cameraChanged = false;

            // Process Discrete Key Press Buffers (Ensures zero dropped single toggles)
            while (keyBuffer.TryDequeue(out Keys key))
            {
                if (key == Keys.Tab)
                {
                    showDebugView = !showDebugView;
                }

                if (key == Keys.Z)
                {
                    enableAntiAliasing = !enableAntiAliasing;
                    resetAccumulation = true;
                }

                if (key == Keys.P)
                {
                    camera.Mode = camera.Mode == Camera.ProjectionMode.Perspective
                        ? Camera.ProjectionMode.Orthographic
                        : Camera.ProjectionMode.Perspective;
                    resetAccumulation = true;
                }

                if (key == Keys.F11)
                {
                    ToggleFullscreen();
                }

                if (key == Keys.F12)
                {
                    SaveScreenshot();
                }

                if (key == Keys.D1 || key == Keys.KeyPad1)
                {
                    LoadScene(SceneMode.MeshHeavy);
                }
                if (key == Keys.D2 || key == Keys.KeyPad2)
                {
                    LoadScene(SceneMode.SphereGrid);
                }
                if (key == Keys.D3 || key == Keys.KeyPad3)
                {
                    LoadScene(SceneMode.MixedPrimitives);
                }

                if (key == Keys.D4 || key == Keys.KeyPad4)
                {
                    LoadScene(SceneMode.AreaLight);
                }
            }

            // Continuous Keyboard Input Handling
            if (keyboard != null)
            {
                float moveSpeed = 5.0f * dt;
                float rotSpeed = 2.0f * dt;

                Vector3 moveDir = Vector3.Zero;

                Vector3 HorizontalForward = (camera.Forward * new Vector3(1, 0, 1)).Normalized();
                Vector3 HorizontalRight = (camera.Right * new Vector3(1, 0, 1)).Normalized();

                // Accumulate movement
                if (keyboard.IsKeyDown(Keys.W)) { moveDir += HorizontalForward; }
                if (keyboard.IsKeyDown(Keys.S)) { moveDir -= HorizontalForward; }
                if (keyboard.IsKeyDown(Keys.A)) { moveDir -= HorizontalRight; }
                if (keyboard.IsKeyDown(Keys.D)) { moveDir += HorizontalRight; }
                if (keyboard.IsKeyDown(Keys.Space)) { moveDir += Vector3.UnitY; }
                if (keyboard.IsKeyDown(Keys.LeftShift)) { moveDir -= Vector3.UnitY; }
                if (keyboard.IsKeyDown(Keys.RightShift)) { animate = !animate; }
                if (keyboard.IsKeyDown(Keys.GraveAccent)) { hideControls = !hideControls; }


                if (moveDir != Vector3.Zero)
                {
                    // Normalize to prevent faster diagonal movement
                    Vector3 translation = Vector3.Normalize(moveDir) * moveSpeed;
                    camera.Position += translation;
                    camera.Target += translation;
                    cameraChanged = true;
                }

                // Handle Rotation
                float yaw = 0f;
                float pitch = 0f;

                if (keyboard.IsKeyDown(Keys.Q)) { yaw += rotSpeed; }
                if (keyboard.IsKeyDown(Keys.E)) { yaw -= rotSpeed; }
                if (keyboard.IsKeyDown(Keys.R)) { pitch += rotSpeed; }
                if (keyboard.IsKeyDown(Keys.F)) { pitch -= rotSpeed; }
                if (keyboard.IsKeyDown(Keys.T)) { camera.FOV += 1; cameraChanged = true; }
                if (keyboard.IsKeyDown(Keys.G)) { camera.FOV -= 1; cameraChanged = true; }

                if (yaw != 0 || pitch != 0)
                {
                    Vector3 direction = camera.Target - camera.Position;

                    // Rotate around World Up (Y) and Local Right
                    Quaternion rot = Quaternion.FromAxisAngle(Vector3.UnitY, yaw) *
                                     Quaternion.FromAxisAngle(camera.Right, pitch);

                    camera.Target = camera.Position + Vector3.Transform(direction, rot);
                    cameraChanged = true;
                }


                // Handle Focus Distance
                if (keyboard.IsKeyDown(Keys.O))
                {
                    camera.FocusDistance += 0.1f * dt * 10.0f;
                    cameraChanged = true;
                    resetAccumulation = true;
                }
                if (keyboard.IsKeyDown(Keys.I))
                {
                    camera.FocusDistance = Math.Max(0.1f, camera.FocusDistance - (0.1f * dt * 10.0f));
                    cameraChanged = true;
                    resetAccumulation = true;
                }
                if (keyboard.IsKeyDown(Keys.K))
                {
                    camera.Aperture += 0.01f * dt * 10.0f;
                    cameraChanged = true;
                    resetAccumulation = true;
                }
                if (keyboard.IsKeyDown(Keys.L))
                {
                    camera.Aperture = Math.Max(0.0f, camera.Aperture - (0.01f * dt * 10.0f));
                    cameraChanged = true;
                    resetAccumulation = true;
                }

                //Handle Render Scale Adjustments
                if (keyboard.IsKeyDown(Keys.N))
                {
                    renderScale = Math.Max(0.01f, renderScale - 0.01f);
                    Resize();
                }
                if (keyboard.IsKeyDown(Keys.M))
                {
                    renderScale += 0.01f;
                    Resize();
                }
                if (keyboard.IsKeyDown(Keys.Comma)) // reset render scale to 1.0
                {
                    renderScale = 1.0f;
                    Resize();
                }
                if (keyboard.IsKeyDown(Keys.B))
                {
                    if (!autoScaleRenderScale)
                    {
                        initialRenderScale = renderScale;
                    }
                    else
                    {
                        renderScale = initialRenderScale;
                    }
                    autoScaleRenderScale = !autoScaleRenderScale;
                }
            }

            // Continuous Mouse Input Handling
            if (mouse != null)
            {
                if (mouse.IsButtonPressed(MouseButton.Left))
                {
                    var pos = mouse.Position;
                    int mappedX = (int)(pos.X * screen.width / screen.width);
                    int mappedY = (int)(pos.Y * screen.height / screen.height);
                    mappedX = Math.Clamp(mappedX, 0, screen.width - 1);
                    mappedY = Math.Clamp(mappedY, 0, screen.height - 1);
                    OnMouseClick(mappedX, mappedY);
                }
            }

            if (cameraChanged)
            {
                camera.Update();
            }

            return cameraChanged;
        }

        public void Tick()
        {
            lock (_lock)
            {
                float dt = (float)timer.Elapsed.TotalSeconds;
                timer.Restart();

                AutoBalanceRenderScale(dt * 1000.0f);

                // Call the new input handling function
                bool cameraChanged = HandleInput(dt);

                // Render Loop Execution
                if (cameraChanged || resetAccumulation)
                {
                    ResetAccumulation();
                }

                screen.Clear(0);
                int sampleFrame = accumulatedFrames + 1;

                Parallel.For(0, renderBuffer.height, row =>
                {
                    for (int column = 0; column < renderBuffer.width; column++)
                    {
                        // Calculate U and V based on RENDER BUFFER dimensions
                        float jitterX = enableAntiAliasing ? Random.Shared.NextSingle() : 0.5f;
                        float jitterY = enableAntiAliasing ? Random.Shared.NextSingle() : 0.5f;
                        float u = (column + jitterX) / renderBuffer.width;
                        float v = (row + jitterY) / renderBuffer.height;

                        // Use renderBuffer dimensions to index the accumulation buffer
                        int pixelIndex = column + (row * renderBuffer.width);

                        Ray ray = camera.GetRay(u, v);
                        Vector3 sampleColor = raytracer.Trace(ray, 0, false);

                        Vector3 accumulatedColor = accumulationBuffer[pixelIndex] + sampleColor;
                        accumulationBuffer[pixelIndex] = accumulatedColor;

                        Vector3 traceColor = accumulatedColor / sampleFrame;
                        renderBuffer.Plot(column, row, new Color3(traceColor.X, traceColor.Y, traceColor.Z));
                    }
                });

                accumulatedFrames = sampleFrame;
                Util.ScaleToScreen(renderBuffer, screen);

                if (showDebugView)
                {
                    // Recalculate view matrix scales and bounding targets on-the-fly
                    raytracer.debug.AutoFrame(camera.Position, scene.FlatNodes);

                    // Clear background
                    raytracer.debug.DrawBackground();

                    if (mouse != null && mouse.Position.X >= 0 && mouse.Position.X < screen.width)
                    {
                        hoveredColumn = (int)mouse.Position.X;
                    }
                    else
                    {
                        hoveredColumn = -1;
                    }

                    // Bypass camera lens jittering on debug lines
                    float originalAperture = camera.Aperture;
                    camera.Aperture = 0.0f;
                    camera.Update();

                    // Draw localized field limits (Frustum edge borders)
                    Ray leftFrustumRay = camera.GetRay(0.0f, 0.5f);
                    Ray rightFrustumRay = camera.GetRay(1.0f, 0.5f);
                    raytracer.debug.Line(camera.Position, camera.Position + (leftFrustumRay.Direction * 8f), new Color3(0.5f, 0.5f, 0.5f));
                    raytracer.debug.Line(camera.Position, camera.Position + (rightFrustumRay.Direction * 8f), new Color3(0.5f, 0.5f, 0.5f));

                    // Trace sparse debug paths across the horizontal viewport array
                    for (int column = 0; column < screen.width; column += 64)
                    {
                        // Don't duplicate if it matches the exact hovered index
                        if (Math.Abs(column - hoveredColumn) < 32) continue;

                        float u = (float)column / screen.width;
                        Ray debugRay = camera.GetRay(u, 0.5f);
                        raytracer.Trace(debugRay, 0, true);
                    }

                    // Dynamic primitive drawing (Handles bounded primitives, skips infinite planes)
                    foreach (var node in scene.FlatNodes)
                    {
                        if (node.Primitive == null || node.Primitive is Plane)
                            continue; // Skip the infinite plane as requested

                        // Transform local coordinate centers via complete World Matrices
                        Vector3 worldCenter = (new Vector4(Vector3.Zero, 1.0f) * node.WorldTransform).Xyz;

                        if (node.Primitive is Sphere s)
                        {
                            raytracer.debug.Circle(worldCenter, s.Radius, new Color3(1.0f, 1.0f, 1.0f));
                        }
                        else
                        {
                            // Catch-all fallthrough for structural geometry (Meshes, Cubes, Triangles) via AABB
                            // Adjust bounds properties depending on how your primitive classes store their min/max values
                            Vector3 worldMin = worldCenter - new Vector3(1.0f);
                            Vector3 worldMax = worldCenter + new Vector3(1.0f);
                            raytracer.debug.AABB(worldMin, worldMax, new Color3(0.0f, 1.0f, 0.4f));
                        }
                    }

                    if (hoveredColumn >= 0 && hoveredColumn < screen.width)
                    {
                        float u = (float)hoveredColumn / screen.width;
                        Ray interactiveRay = camera.GetRay(u, 0.5f);

                        // Traced with full recursion, calls reflection/shadow ray tracing with the color codes we set up in Raytracer.cs
                        raytracer.Trace(interactiveRay, 0, true);
                    }

                    // Restore primary optical properties
                    camera.Aperture = originalAperture;
                    camera.Update();

                    // Visualize main camera direction vector
                    raytracer.debug.Line(camera.Position, camera.Position + ((camera.Target - camera.Position).Normalized() * 1.5f), new Color3(1.0f, 0.0f, 0.0f));
                }

                // Interface Overlays & Animations
                frameAccumulator += TimeSpan.FromSeconds(dt);
                frames++;
                if (frameAccumulator.TotalSeconds > 1)
                {
                    timeString = (frameAccumulator.TotalMilliseconds / frames).ToString("F1") + " ms/frame";
                    frames = 0;
                    frameAccumulator = TimeSpan.Zero;
                }

                if (!hideControls)
                {
                    screen.PrintOutlined(timeString, 2, 2, Color4.White);
                    screen.PrintOutlined($"Samples: {accumulatedFrames}", 2, 20, Color4.White);
                    screen.PrintOutlined($"AA: {(enableAntiAliasing ? "On" : "Off")}", 2, 40, Color4.White);
                    screen.PrintOutlined($"Projection: {camera.Mode}", 2, 60, Color4.White);
                    screen.PrintOutlined($"Focus: {camera.FocusDistance:F2}", 2, 80, Color4.White);
                    screen.PrintOutlined($"Aperture: {camera.Aperture:F3}", 2, 100, Color4.White);
                    screen.PrintOutlined($"Render Scale: {renderScale * 100:F0}% {(autoScaleRenderScale ? "(Auto)" : "")}", 2, 120, Color4.White);

                    for (int i = 0; i < ControlsInfo.Length; i++)
                    {
                        string fovLine = "FOV";
                        if (ControlsInfo[i].Contains(fovLine))
                        {
                            screen.PrintOutlined($"{ControlsInfo[i]}: {camera.FOV}deg", 2, 140 + (i * 20), Color4.White);
                            continue;
                        }
                        screen.PrintOutlined(ControlsInfo[i], 2, 140 + (i * 20), Color4.White);
                    }
                }

                // Flatten the matrix hierarchy for the current frame BEFORE tracing 
                if (cameraChanged)
                {
                    currentAnimationFrameSamples = 0;
                    scene.UpdateSceneGraph();
                }

                frameCount++;

                // animation logic
                currentAnimationFrameSamples++;

                if (animate && currentAnimationFrameSamples >= SamplesPerAnimationFrame)
                {
                    currentAnimationFrameSamples = 0;

                    Animate();

                    renderdAnimationFrameCount++;
                    scene.UpdateSceneGraph();
                    ResetAccumulation();
                }
            }
        }

        private void Animate()
        {
            float time = renderdAnimationFrameCount * 0.1f;

            if (currentSceneMode == SceneMode.MeshHeavy && _hoverBikeNode != null)
            {
                _hoverBikeNode.LocalTransform = Matrix4.CreateScale(0.01f) *
                                                Matrix4.CreateRotationX(MathF.PI) *
                                                Matrix4.CreateTranslation(new Vector3(0, 3, 0)) *
                                                Matrix4.CreateRotationY(time) *
                                                Matrix4.CreateTranslation(0, MathF.Sin(time), 0);
            }
        }

        private void ToggleFullscreen()
        {
            window.WindowState = window.WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen;
        }

        private void SaveScreenshot()
        {
            string filename = $"render_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            using Image<Rgba32> image = new(screen.width, screen.height);

            for (int y = 0; y < screen.height; y++)
            {
                for (int x = 0; x < screen.width; x++)
                {
                    int p = screen.pixels[x + (y * screen.width)];

                    const byte a = 255;
                    byte r = (byte)((p >> 16) & 0xFF);
                    byte g = (byte)((p >> 8) & 0xFF);
                    byte b = (byte)(p & 0xFF);

                    image[x, y] = new Rgba32(r, g, b, a);
                }
            }

            image.SaveAsPng(filename);

            Console.WriteLine($"Saved {filename}, {screen.width}x{screen.height} pixels");
        }
    }
}