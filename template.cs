using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

// The template provides you with a window which displays a 'linear frame buffer', i.e.
// a 1D array of pixels that represents the graphical contents of the window.

// Under the hood, this array is encapsulated in a 'Surface' object, and copied once per
// frame to an OpenGL texture, which is then used to texture 2 triangles that exactly
// cover the window. This is all handled automatically by the template code.

// Before drawing the two triangles, the template calls the Tick method in MyApplication,
// in which you are expected to modify the contents of the linear frame buffer.

// After (or instead of) rendering the triangles you can add your own OpenGL code.

// We will use both the pure pixel rendering as well as straight OpenGL code in the
// tutorial. After the tutorial you can throw away this template code, or modify it at
// will, or maybe it simply suits your needs.

namespace INFOGR_RAYTRACER
{
    public class OpenTKApp : GameWindow
    {
        /**
         * IMPORTANT:
         * 
         * Modern OpenGL (introduced in 2009) does NOT allow Immediate Mode or
         * Fixed-Function Pipeline commands, e.g., GL.MatrixMode, GL.Begin,
         * GL.End, GL.Vertex, GL.TexCoord, or GL.Enable certain capabilities
         * related to the Fixed-Function Pipeline. It also REQUIRES you to use
         * shaders.
         * 
         * If you want to try prehistoric OpenGL code, such as many code
         * samples still found online, enable it below.
         * 
         * MacOS doesn't support prehistoric OpenGL anymore since 2018.
         */
        public const bool allowPrehistoricOpenGL = false;

        private int screenID;            // unique integer identifier of the OpenGL texture
        private MyApplication? app;      // instance of the application
        private bool terminated = false; // application terminates gracefully when this is true

        // The following variables are only needed in Modern OpenGL
        public int vertexArrayObject;
        public int vertexBufferObject;
        public int programID;
        // All the data for the vertices interleaved in one array:
        // - XYZ in normalized device coordinates
        // - UV
        private readonly float[] vertices =
        { //  X      Y     Z     U     V
            -1.0f, -1.0f, 0.0f, 0.0f, 1.0f, // bottom-left  2-----3 triangles:
             1.0f, -1.0f, 0.0f, 1.0f, 1.0f, // bottom-right | \   |     012
            -1.0f,  1.0f, 0.0f, 0.0f, 0.0f, // top-left     |   \ |     123
             1.0f,  1.0f, 0.0f, 1.0f, 0.0f, // top-right    0-----1
        };

        public OpenTKApp()
            : base(GameWindowSettings.Default, new NativeWindowSettings()
            {
                ClientSize = new Vector2i(1024, 512),
                Profile = allowPrehistoricOpenGL ? ContextProfile.Compatability : ContextProfile.Core,
                Flags = allowPrehistoricOpenGL ? ContextFlags.Default : ContextFlags.ForwardCompatible,
            })
        {
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Keys.Escape) terminated = true;

            // Queue discrete keypresses to prevent skipped buttons
            app?.RegisterKeyPress(e.Key);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButton.Left && app != null)
            {
                var pos = MouseState.Position;
                int mappedX = (int)(pos.X * app.screen.width / ClientSize.X);
                int mappedY = (int)(pos.Y * app.screen.height / ClientSize.Y);
                mappedX = Math.Clamp(mappedX, 0, app.screen.width - 1);
                mappedY = Math.Clamp(mappedY, 0, app.screen.height - 1);
                app.OnMouseClick(mappedX, mappedY);
            }
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            // called during application initialization
            GL.ClearColor(0, 0, 0, 0);
            GL.Disable(EnableCap.DepthTest);
            Surface screen = new(ClientSize.X, ClientSize.Y);
            app = new MyApplication(screen, this);
            screenID = app.screen.GenTexture();
            if (allowPrehistoricOpenGL)
            {
#pragma warning disable CS0162 // Unreachable code detected
                GL.Enable(EnableCap.Texture2D);
                GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
#pragma warning restore CS0162 // Unreachable code detected
            }
            else
            {   // setting up a Modern OpenGL pipeline takes a lot of code
                // Vertex Array Object: will store the meaning of the data in the buffer
                vertexArrayObject = GL.GenVertexArray();
                GL.BindVertexArray(vertexArrayObject);
                // Vertex Buffer Object: a buffer of raw data
                vertexBufferObject = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
                // Vertex Shader
                string shaderSource = File.ReadAllText("../../../shaders/screen_vs.glsl");
                int vertexShader = GL.CreateShader(ShaderType.VertexShader);
                GL.ShaderSource(vertexShader, shaderSource);
                GL.CompileShader(vertexShader);
                GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int status);
                if (status != (int)All.True)
                {
                    string log = GL.GetShaderInfoLog(vertexShader);
                    throw new Exception($"Error occurred whilst compiling vertex shader ({vertexShader}):\n{log}");
                }
                // Fragment Shader
                shaderSource = File.ReadAllText("../../../shaders/screen_fs.glsl");
                int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(fragmentShader, shaderSource);
                GL.CompileShader(fragmentShader);
                GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out status);
                if (status != (int)All.True)
                {
                    string log = GL.GetShaderInfoLog(fragmentShader);
                    throw new Exception($"Error occurred whilst compiling fragment shader ({fragmentShader}):\n{log}");
                }
                // Program: a set of shaders to be used together in a pipeline
                programID = GL.CreateProgram();
                GL.AttachShader(programID, vertexShader);
                GL.AttachShader(programID, fragmentShader);
                GL.LinkProgram(programID);
                GL.GetProgram(programID, GetProgramParameterName.LinkStatus, out status);
                if (status != (int)All.True)
                {
                    string log = GL.GetProgramInfoLog(programID);
                    throw new Exception($"Error occurred whilst linking program ({programID}):\n{log}");
                }
                // the program contains the compiled shaders, we can delete the source
                GL.DetachShader(programID, vertexShader);
                GL.DetachShader(programID, fragmentShader);
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
                // send all the following draw calls through this pipeline
                GL.UseProgram(programID);
                // tell the VAO which part of the VBO data should go to each shader input
                int location = GL.GetAttribLocation(programID, "vPosition");
                GL.EnableVertexAttribArray(location);
                GL.VertexAttribPointer(location, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
                location = GL.GetAttribLocation(programID, "vUV");
                GL.EnableVertexAttribArray(location);
                GL.VertexAttribPointer(location, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
                // connect the texture to the shader uniform variable
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, screenID);
                GL.Uniform1(GL.GetUniformLocation(programID, "pixels"), 0);
            }
            app.Init();
        }
        protected override void OnUnload()
        {
            base.OnUnload();
            // called upon app close
            GL.DeleteTextures(1, ref screenID);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            app?.Resize(ClientSize.X, ClientSize.Y);

            GL.BindTexture(TextureTarget.Texture2D, screenID);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          ClientSize.X, ClientSize.Y, 0,
                          PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            if (app != null)
            {
                app.keyboard = KeyboardState;
                app.mouse = MouseState;
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            // called once per frame; render
            app?.Tick();
            if (terminated)
            {
                Close();
                return;
            }
            // convert MyApplication._screen to OpenGL texture
            if (app != null)
            {
                GL.BindTexture(TextureTarget.Texture2D, screenID);
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                                 app.screen.width, app.screen.height,
                                 PixelFormat.Bgra, PixelType.UnsignedByte, app.screen.pixels);
                // draw _screen filling quad
                if (allowPrehistoricOpenGL)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    GL.Begin(PrimitiveType.Quads);
                    GL.TexCoord2(0.0f, 1.0f); GL.Vertex2(-1.0f, -1.0f);
                    GL.TexCoord2(1.0f, 1.0f); GL.Vertex2(1.0f, -1.0f);
                    GL.TexCoord2(1.0f, 0.0f); GL.Vertex2(1.0f, 1.0f);
                    GL.TexCoord2(0.0f, 0.0f); GL.Vertex2(-1.0f, 1.0f);
                    GL.End();
#pragma warning restore CS0162 // Unreachable code detected
                }
                else
                {
                    GL.BindVertexArray(vertexArrayObject);
                    GL.UseProgram(programID);
                    GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
                }
            }
            // tell OpenTK we're done rendering
            SwapBuffers();
        }

        public static void Main()
        {
            // entry point
            using OpenTKApp app = new();
            app.UpdateFrequency = 30.0;
            app.Run();
        }
    }
}