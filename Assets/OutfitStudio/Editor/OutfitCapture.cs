using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Capture backend for the Outfit Studio window.
    ///
    /// Stills render the main camera into an arbitrary-resolution RenderTexture through the
    /// render pipeline (independent from the Game view size), optionally with a transparent
    /// background. Video records the Game view to MP4 via the Unity Recorder package.
    /// </summary>
    public static class OutfitCapture
    {
        private static RecorderController _recorderController;

        public static bool IsRecording => _recorderController != null && _recorderController.IsRecording();

        /// <summary>Where captures land by default, relative to the project root.</summary>
        public const string DEFAULT_OUTPUT_FOLDER = "Captures";

        public static string CaptureStill(int width, int height, bool transparentBackground, string outputFolder)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[OutfitStudio] No main camera found - are you in play mode?");
                return null;
            }

            var previousFlags = camera.clearFlags;
            var previousColor = camera.backgroundColor;

            if (transparentBackground)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            // The card frame (if active) sizes its quads from camera.aspect on a 0.5 s poll, which
            // tracks the Game view. Force the capture aspect and re-lay-out so a still at a different
            // resolution still frames the card correctly. Restored via ResetAspect() below.
            camera.aspect = width / (float)height;
            StudioCardFrame.RelayoutFor(camera);

            // The legacy RenderTexture(w,h,depth,format) constructor leaves sRGB read/write
            // ambiguous, which can gamma-encode the capture differently than the Game view's
            // backbuffer (showing up as slightly flattened/desaturated colors). Building it from a
            // descriptor with sRGB explicitly forced on keeps it display-referred, matching what the
            // Linear-color-space project's URP pipeline outputs for the live view.
            var rtDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 32)
            {
                sRGB = true,
                msaaSamples = 4
            };
            var rt = new RenderTexture(rtDesc);

            Texture2D texture = null;

            try
            {
                var request = new RenderPipeline.StandardRequest();

                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    request.destination = rt;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    // Fallback for pipelines without render request support
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.targetTexture = null;
                }

                var previousActive = RenderTexture.active;
                RenderTexture.active = rt;

                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                RenderTexture.active = previousActive;

                var path = GetOutputPath(outputFolder, "png");
                File.WriteAllBytes(path, texture.EncodeToPNG());

                Debug.Log($"[OutfitStudio] Screenshot saved: {path}");
                return path;
            }
            finally
            {
                camera.clearFlags = previousFlags;
                camera.backgroundColor = previousColor;
                camera.ResetAspect();
                StudioCardFrame.RelayoutFor(camera);

                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        public static string StartVideo(int width, int height, int frameRate, string outputFolder)
        {
            if (IsRecording)
            {
                Debug.LogWarning("[OutfitStudio] Already recording");
                return null;
            }

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "Outfit Studio Video";
            movieSettings.Enabled = true;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = width,
                OutputHeight = height
            };

            var path = GetOutputPath(outputFolder, null); // Recorder appends the extension
            movieSettings.OutputFile = path;

            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = frameRate;
            controllerSettings.CapFrameRate = true;

            _recorderController = new RecorderController(controllerSettings);
            _recorderController.PrepareRecording();
            _recorderController.StartRecording();

            return path + ".mp4";
        }

        public static void StopVideo()
        {
            if (_recorderController == null) return;

            if (_recorderController.IsRecording())
            {
                _recorderController.StopRecording();
                Debug.Log("[OutfitStudio] Video recording stopped");
            }

            _recorderController = null;
        }

        private static string GetOutputPath(string outputFolder, string extension)
        {
            if (string.IsNullOrWhiteSpace(outputFolder)) outputFolder = DEFAULT_OUTPUT_FOLDER;

            // Relative paths land next to the project (outside Assets so Unity doesn't import them)
            var folder = Path.IsPathRooted(outputFolder)
                ? outputFolder
                : Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, outputFolder);

            Directory.CreateDirectory(folder);

            var fileName = $"outfit_{DateTime.Now:yyyyMMdd_HHmmss}";
            var path = Path.Combine(folder, fileName);

            return extension != null ? $"{path}.{extension}" : path;
        }

        public static void RevealInFinder(string path)
        {
            if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
        }
    }
}
