#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

internal static class ProjectAbyssVFXReferencePreviewRenderer
{
    private const int PreviewWidth = 640;
    private const int PreviewHeight = 640;
    private const int Seed = 1337;
    private const int PreviewLayer = 31;
    private const float VisibleRatioThreshold = 0.0005f;
    private const float TemporalDifferenceThreshold = 0.00025f;

    private static readonly Color DarkBackground = new(0.025f, 0.03f, 0.045f, 1f);
    private static readonly Color LightBackground = new(0.72f, 0.74f, 0.78f, 1f);

    internal static void Render(
        VisualEffectAsset asset,
        VfxAiReferenceReport report,
        string outputFolder)
    {
        string previewFolder = Path.Combine(outputFolder, "PREVIEW");
        Directory.CreateDirectory(previewFolder);

        VfxPreviewRecord preview = report.preview;
        preview.attempted = true;
        preview.width = PreviewWidth;
        preview.height = PreviewHeight;
        preview.deterministicSeed = Seed;
        preview.isolatedLayer = PreviewLayer;
        report.capture.renderedPreviewAttempted = true;

        Scene previewScene = default;
        GameObject effectObject = null;
        GameObject cameraObject = null;
        GameObject keyLightObject = null;
        GameObject fillLightObject = null;
        RenderTexture renderTexture = null;
        List<Texture2D> contactTextures = new();
        Texture2D previousFrame = null;

        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();

            effectObject = new GameObject("VFX_AI_Preview_Effect")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            SceneManager.MoveGameObjectToScene(effectObject, previewScene);
            VisualEffect effect = effectObject.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.resetSeedOnPlay = false;
            effect.startSeed = Seed;
            effect.pause = false;
            effect.playRate = 1f;

            cameraObject = new GameObject("VFX_AI_Preview_Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DarkBackground;
            camera.fieldOfView = 35f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.01f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.useOcclusionCulling = false;

            Bounds bounds = ToBounds(report.graph.suggestedBounds);
            ConfigureCamera(camera, bounds);
            preview.cameraDescription =
                $"Isolated PreviewScene, Layer={PreviewLayer}, FOV={camera.fieldOfView}, " +
                $"Position={camera.transform.position}, Target={bounds.center}, Bounds={bounds}";

            keyLightObject = CreateLight(
                previewScene,
                "VFX_AI_KeyLight",
                LightType.Directional,
                new Color(1f, 0.94f, 0.86f),
                1.4f,
                Quaternion.Euler(35f, -35f, 0f));
            fillLightObject = CreateLight(
                previewScene,
                "VFX_AI_FillLight",
                LightType.Point,
                new Color(0.45f, 0.58f, 1f),
                4f,
                Quaternion.identity);
            fillLightObject.transform.position =
                bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y, -bounds.extents.z);
            Light fill = fillLightObject.GetComponent<Light>();
            fill.range = Mathf.Max(5f, bounds.extents.magnitude * 5f);

            RenderTextureDescriptor descriptor = new(
                PreviewWidth,
                PreviewHeight,
                RenderTextureFormat.ARGB32,
                24)
            {
                msaaSamples = Mathf.Min(4, QualitySettings.antiAliasing <= 0
                    ? 1
                    : QualitySettings.antiAliasing),
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture = new RenderTexture(descriptor)
            {
                name = "VFX_AI_Preview_RT",
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();
            camera.targetTexture = renderTexture;

            float[] times = BuildPreviewTimes(report);
            HashSet<string> distinctHashes = new(StringComparer.Ordinal);
            float maxDifference = 0f;

            foreach (float time in times)
            {
                PrepareAndSimulate(effect, report.graph.initialEventName, time);
                string fileName = $"FRAME_{Mathf.RoundToInt(time * 1000f):D5}ms_DARK.png";
                string absolutePath = Path.Combine(previewFolder, fileName);
                Texture2D frameTexture = RenderToPng(
                    camera,
                    renderTexture,
                    absolutePath,
                    DarkBackground,
                    out float visibleRatio,
                    out string pixelSha256);

                bool aliveAvailable = TryGetAliveParticleCount(effect, out long alive);
                float difference = previousFrame == null
                    ? 0f
                    : CalculateFrameDifference(previousFrame, frameTexture);
                maxDifference = Mathf.Max(maxDifference, difference);
                distinctHashes.Add(pixelSha256);

                preview.frames.Add(new VfxPreviewFrameRecord
                {
                    simulationTime = time,
                    file = ProjectAbyssVFXReferenceAnalyzer.NormalizePath(
                        Path.Combine("PREVIEW", fileName)),
                    aliveParticleCount = alive,
                    aliveParticleCountAvailable = aliveAvailable,
                    nonBackgroundPixelRatio = visibleRatio,
                    containsVisibleEffect = visibleRatio >= VisibleRatioThreshold,
                    pixelSha256 = pixelSha256,
                    differenceFromPrevious = difference
                });

                if (frameTexture != null)
                {
                    contactTextures.Add(frameTexture);
                    previousFrame = frameTexture;
                }
            }

            preview.distinctFrameCount = distinctHashes.Count;
            preview.maxInterFrameDifference = maxDifference;
            preview.allFramesIdentical = preview.frames.Count > 1 && distinctHashes.Count <= 1;

            float peakTime = SelectPeakTime(preview.frames);
            PrepareAndSimulate(effect, report.graph.initialEventName, peakTime);
            camera.backgroundColor = LightBackground;
            string lightFile = $"FRAME_PEAK_{Mathf.RoundToInt(peakTime * 1000f):D5}ms_LIGHT.png";
            Texture2D lightTexture = RenderToPng(
                camera,
                renderTexture,
                Path.Combine(previewFolder, lightFile),
                LightBackground,
                out _,
                out _);
            preview.lightBackgroundFrameFile =
                ProjectAbyssVFXReferenceAnalyzer.NormalizePath(
                    Path.Combine("PREVIEW", lightFile));
            if (lightTexture != null)
                UnityEngine.Object.DestroyImmediate(lightTexture);

            if (contactTextures.Count > 0)
            {
                string contactFile = "CONTACT_SHEET.png";
                WriteContactSheet(
                    contactTextures,
                    Path.Combine(previewFolder, contactFile));
                preview.contactSheetFile =
                    ProjectAbyssVFXReferenceAnalyzer.NormalizePath(
                        Path.Combine("PREVIEW", contactFile));
            }

            bool hasVisibleFrame = preview.frames.Any(frame => frame.containsVisibleEffect);
            bool hasAliveParticles = preview.frames.Any(frame =>
                frame.aliveParticleCountAvailable && frame.aliveParticleCount > 0);
            bool hasTemporalChange = preview.distinctFrameCount > 1 &&
                                     preview.maxInterFrameDifference >= TemporalDifferenceThreshold;

            preview.rendered = hasVisibleFrame && (hasAliveParticles || hasTemporalChange);
            report.capture.renderedPreviewSucceeded = preview.rendered;

            if (preview.rendered)
            {
                preview.validationReason = hasTemporalChange
                    ? "PASS: isolated-layer frames contain visible VFX and meaningful temporal change."
                    : "PASS: isolated-layer frames contain visible VFX with a positive alive-particle count.";
            }
            else
            {
                preview.validationReason = BuildValidationFailureReason(
                    hasVisibleFrame,
                    hasAliveParticles,
                    hasTemporalChange,
                    preview);
                preview.failureReason = preview.validationReason;
                WriteFallbackAssetIcon(asset, previewFolder, preview);
            }
        }
        catch (Exception exception)
        {
            preview.rendered = false;
            preview.failureReason = exception.ToString();
            preview.validationReason = "FAIL: preview renderer threw an exception.";
            report.warnings.Add("Rendered VFX preview failed: " + exception.Message);
            WriteFallbackAssetIcon(asset, previewFolder, preview);
        }
        finally
        {
            foreach (Texture2D texture in contactTextures)
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            DestroyImmediateSafe(fillLightObject);
            DestroyImmediateSafe(keyLightObject);
            DestroyImmediateSafe(cameraObject);
            DestroyImmediateSafe(effectObject);

            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static string BuildValidationFailureReason(
        bool hasVisibleFrame,
        bool hasAliveParticles,
        bool hasTemporalChange,
        VfxPreviewRecord preview)
    {
        if (!hasVisibleFrame)
        {
            return "FAIL: isolated preview frames did not differ enough from the clear color. " +
                   "The graph may require scene bindings, custom events, exposed object overrides, or different bounds.";
        }

        if (preview.allFramesIdentical && !hasAliveParticles)
        {
            return "FAIL: every dark-background frame was pixel-identical and no positive alive-particle count was observed. " +
                   "The image is not accepted as a real VFX preview.";
        }

        if (!hasTemporalChange && !hasAliveParticles)
        {
            return "FAIL: visible pixels were captured, but neither temporal VFX change nor a positive alive-particle count was verified.";
        }

        return "FAIL: preview validation did not establish that the isolated VisualEffect produced the captured pixels.";
    }

    private static float[] BuildPreviewTimes(VfxAiReferenceReport report)
    {
        SortedSet<float> values = new()
        {
            0.10f,
            0.25f,
            0.50f,
            1.00f,
            2.00f,
            4.00f
        };

        float prewarm = report.graph.prewarmDeltaTime * report.graph.prewarmStepCount;
        if (prewarm > 0.01f)
            values.Add(Mathf.Clamp(prewarm, 0.05f, 8f));

        float largestLifetime = FindLargestNumericSetting(report, "lifetime");
        if (largestLifetime > 4f)
            values.Add(Mathf.Clamp(largestLifetime * 0.75f, 4.5f, 8f));

        return values.Take(8).ToArray();
    }

    private static float FindLargestNumericSetting(
        VfxAiReferenceReport report,
        string token)
    {
        float maximum = 0f;
        foreach (VfxGraphNodeRecord node in report.graph.nodes)
        {
            foreach (VfxSerializedPropertyRecord property in node.properties)
            {
                if (!property.path.Contains(token, StringComparison.OrdinalIgnoreCase))
                    continue;

                MatchNumbers(property.value, ref maximum);
            }
        }
        return maximum;
    }

    private static void MatchNumbers(string value, ref float maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(
                     value,
                     @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?"))
        {
            if (float.TryParse(
                    match.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float parsed))
            {
                maximum = Mathf.Max(maximum, parsed);
            }
        }
    }

    private static void PrepareAndSimulate(
        VisualEffect effect,
        string initialEventName,
        float targetTime)
    {
        effect.Reinit();
        effect.resetSeedOnPlay = false;
        effect.startSeed = Seed;
        effect.pause = false;

        if (!string.IsNullOrWhiteSpace(initialEventName) &&
            !string.Equals(initialEventName, "OnPlay", StringComparison.Ordinal))
        {
            try
            {
                effect.SendEvent(initialEventName);
            }
            catch
            {
                // Reinit already dispatches the graph's initial event in supported versions.
            }
        }

        const float delta = 1f / 60f;
        uint steps = (uint)Mathf.Max(1, Mathf.CeilToInt(targetTime / delta));
        effect.Simulate(delta, steps);
    }

    private static bool TryGetAliveParticleCount(
        VisualEffect effect,
        out long alive)
    {
        alive = 0;
        try
        {
            alive = effect.aliveParticleCount;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Texture2D RenderToPng(
        Camera camera,
        RenderTexture renderTexture,
        string outputPath,
        Color background,
        out float visibleRatio,
        out string pixelSha256)
    {
        RenderTexture previous = RenderTexture.active;
        Texture2D texture = null;
        visibleRatio = 0f;
        pixelSha256 = string.Empty;

        try
        {
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, background);
            camera.backgroundColor = background;
            camera.Render();

            RenderTexture.active = renderTexture;
            texture = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false,
                false);
            texture.ReadPixels(
                new Rect(0, 0, renderTexture.width, renderTexture.height),
                0,
                0,
                false);
            texture.Apply(false, false);
            visibleRatio = CalculateNonBackgroundRatio(texture, background);
            pixelSha256 = ComputePixelSha256(texture);
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            return texture;
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    private static string ComputePixelSha256(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        byte[] bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            int offset = i * 4;
            bytes[offset] = pixels[i].r;
            bytes[offset + 1] = pixels[i].g;
            bytes[offset + 2] = pixels[i].b;
            bytes[offset + 3] = pixels[i].a;
        }

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(bytes);
        return string.Concat(hash.Select(value => value.ToString("x2")));
    }

    private static float CalculateFrameDifference(
        Texture2D previous,
        Texture2D current)
    {
        if (previous == null || current == null ||
            previous.width != current.width || previous.height != current.height)
        {
            return 1f;
        }

        Color32[] a = previous.GetPixels32();
        Color32[] b = current.GetPixels32();
        if (a.Length == 0 || a.Length != b.Length)
            return 1f;

        double total = 0d;
        int sampled = 0;
        for (int i = 0; i < a.Length; i += 4)
        {
            total += Math.Abs(a[i].r - b[i].r);
            total += Math.Abs(a[i].g - b[i].g);
            total += Math.Abs(a[i].b - b[i].b);
            total += Math.Abs(a[i].a - b[i].a);
            sampled += 4;
        }

        return sampled <= 0 ? 0f : (float)(total / (sampled * 255d));
    }

    private static float CalculateNonBackgroundRatio(Texture2D texture, Color background)
    {
        Color32 target = background;
        Color32[] pixels = texture.GetPixels32();
        if (pixels == null || pixels.Length == 0)
            return 0f;

        int changed = 0;
        const int thresholdSquared = 18 * 18;
        for (int i = 0; i < pixels.Length; i += 2)
        {
            Color32 pixel = pixels[i];
            int dr = pixel.r - target.r;
            int dg = pixel.g - target.g;
            int db = pixel.b - target.b;
            int da = pixel.a - target.a;
            if (dr * dr + dg * dg + db * db + da * da > thresholdSquared)
                changed++;
        }

        int sampled = (pixels.Length + 1) / 2;
        return sampled <= 0 ? 0f : (float)changed / sampled;
    }

    private static void WriteContactSheet(
        List<Texture2D> textures,
        string outputPath)
    {
        const int columns = 3;
        int rows = Mathf.CeilToInt((float)textures.Count / columns);
        int cellWidth = textures[0].width;
        int cellHeight = textures[0].height;
        Texture2D sheet = new(
            cellWidth * columns,
            cellHeight * rows,
            TextureFormat.RGBA32,
            false);

        Color32[] background = Enumerable.Repeat(
            (Color32)new Color(0.01f, 0.01f, 0.015f, 1f),
            sheet.width * sheet.height).ToArray();
        sheet.SetPixels32(background);

        for (int i = 0; i < textures.Count; i++)
        {
            int column = i % columns;
            int rowFromTop = i / columns;
            int row = rows - rowFromTop - 1;
            sheet.SetPixels32(
                column * cellWidth,
                row * cellHeight,
                cellWidth,
                cellHeight,
                textures[i].GetPixels32());
        }

        sheet.Apply(false, false);
        File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
    }

    private static void ConfigureCamera(Camera camera, Bounds bounds)
    {
        Vector3 extents = bounds.extents;
        float radius = Mathf.Max(0.5f, extents.magnitude);
        float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float distance = radius / Mathf.Max(0.15f, Mathf.Sin(halfFov)) * 1.35f;
        Vector3 direction = new Vector3(1f, 0.35f, -1f).normalized;
        camera.transform.position = bounds.center - direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(
            bounds.center - camera.transform.position,
            Vector3.up);
        camera.farClipPlane = Mathf.Max(100f, distance + radius * 10f);
    }

    private static GameObject CreateLight(
        Scene scene,
        string name,
        LightType type,
        Color color,
        float intensity,
        Quaternion rotation)
    {
        GameObject value = new(name)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = PreviewLayer
        };
        SceneManager.MoveGameObjectToScene(value, scene);
        value.transform.rotation = rotation;
        Light light = value.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << PreviewLayer;
        return value;
    }

    private static Bounds ToBounds(VfxBoundsRecord record)
    {
        if (record != null && record.valid)
        {
            Vector3 size = new(
                Mathf.Max(0.1f, Mathf.Abs(record.sizeX)),
                Mathf.Max(0.1f, Mathf.Abs(record.sizeY)),
                Mathf.Max(0.1f, Mathf.Abs(record.sizeZ)));
            return new Bounds(
                new Vector3(record.centerX, record.centerY, record.centerZ),
                size);
        }

        return new Bounds(new Vector3(0f, 1f, 0f), new Vector3(4f, 4f, 4f));
    }

    private static float SelectPeakTime(List<VfxPreviewFrameRecord> frames)
    {
        VfxPreviewFrameRecord peak = frames
            .OrderByDescending(frame => frame.nonBackgroundPixelRatio)
            .ThenByDescending(frame => frame.aliveParticleCount)
            .FirstOrDefault();
        return peak?.simulationTime ?? 1f;
    }

    private static void WriteFallbackAssetIcon(
        VisualEffectAsset asset,
        string previewFolder,
        VfxPreviewRecord preview)
    {
        Texture2D source = AssetPreview.GetAssetPreview(asset) ??
                           AssetPreview.GetMiniThumbnail(asset);
        if (source == null)
            return;

        string fileName = "FALLBACK_ASSET_ICON.png";
        string outputPath = Path.Combine(previewFolder, fileName);
        Texture2D readable = null;
        RenderTexture temporary = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            temporary = RenderTexture.GetTemporary(
                Mathf.Max(64, source.width),
                Mathf.Max(64, source.height),
                0,
                RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(
                temporary.width,
                temporary.height,
                TextureFormat.RGBA32,
                false);
            readable.ReadPixels(
                new Rect(0, 0, temporary.width, temporary.height),
                0,
                0);
            readable.Apply();
            File.WriteAllBytes(outputPath, readable.EncodeToPNG());
            preview.usedFallbackAssetIcon = true;
            preview.fallbackAssetIconFile =
                ProjectAbyssVFXReferenceAnalyzer.NormalizePath(
                    Path.Combine("PREVIEW", fileName));
        }
        catch
        {
            // Preview failure is already represented in the report.
        }
        finally
        {
            RenderTexture.active = previous;
            if (temporary != null)
                RenderTexture.ReleaseTemporary(temporary);
            if (readable != null)
                UnityEngine.Object.DestroyImmediate(readable);
        }
    }

    private static void DestroyImmediateSafe(GameObject value)
    {
        if (value != null)
            UnityEngine.Object.DestroyImmediate(value);
    }
}
#endif
