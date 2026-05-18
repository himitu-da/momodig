using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Momodig.EditorTools
{
    public static class ProfilerCaptureSummaryExporter
    {
        private const float DefaultFrameBudgetMs = 16f;
        private const int DefaultWorstFramesToAnalyze = 30;
        private const int DefaultTopMarkerCount = 40;
        private const int MaxThreadSearchCount = 256;
        private const int MissingThreadStopCount = 8;

        private static readonly string CaptureDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ProfilerCaptures");
        private static readonly string ExportDirectory = Path.Combine(CaptureDirectory, "Exports");

        [MenuItem("Tools/Momodig/Profiler/Export Latest Capture Summary")]
        public static void ExportLatestCaptureSummaryMenu()
        {
            string capturePath = GetLatestCapturePath();
            ExportCaptureSummary(capturePath);
        }

        [MenuItem("Tools/Momodig/Profiler/Export Capture Summary...")]
        public static void ExportCaptureSummaryMenu()
        {
            string capturePath = EditorUtility.OpenFilePanel("Select Unity Profiler capture", CaptureDirectory, "data");
            if (string.IsNullOrEmpty(capturePath))
            {
                return;
            }

            ExportCaptureSummary(capturePath);
        }

        // Batchmode entry point:
        // Unity.exe -batchmode -projectPath <project> -executeMethod Momodig.EditorTools.ProfilerCaptureSummaryExporter.ExportLatestCaptureSummaryBatch -quit
        public static void ExportLatestCaptureSummaryBatch()
        {
            string capturePath = GetLatestCapturePath();
            ExportCaptureSummary(capturePath);
        }

        public static void ExportCaptureSummary(string capturePath)
        {
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                throw new ArgumentException("Profiler capture path is empty.", nameof(capturePath));
            }

            string absoluteCapturePath = Path.GetFullPath(capturePath);
            if (!File.Exists(absoluteCapturePath))
            {
                throw new FileNotFoundException("Profiler capture file was not found.", absoluteCapturePath);
            }

            Directory.CreateDirectory(ExportDirectory);

            string captureName = Path.GetFileNameWithoutExtension(absoluteCapturePath);
            string jsonPath = Path.Combine(ExportDirectory, captureName + ".profile-summary.json");
            string markdownPath = Path.Combine(ExportDirectory, captureName + ".profile-summary.md");

            ExportReport report = BuildReport(
                absoluteCapturePath,
                DefaultFrameBudgetMs,
                DefaultWorstFramesToAnalyze,
                DefaultTopMarkerCount);

            WriteUtf8NoBom(jsonPath, JsonUtility.ToJson(report, true));
            WriteUtf8NoBom(markdownPath, BuildMarkdown(report));

            Debug.Log($"Profiler summary exported.\nJSON: {jsonPath}\nMarkdown: {markdownPath}");
        }

        private static ExportReport BuildReport(
            string capturePath,
            float frameBudgetMs,
            int worstFramesToAnalyze,
            int topMarkerCount)
        {
            if (!ProfilerDriver.LoadProfile(capturePath, false))
            {
                throw new InvalidOperationException($"Failed to load profiler capture: {capturePath}");
            }

            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            if (firstFrame < 0 || lastFrame < firstFrame)
            {
                throw new InvalidOperationException($"Profiler capture has no readable frames: {capturePath}");
            }

            var warnings = new List<string>();
            var frameSummaries = new List<FrameSummary>();
            var cpuTimes = new List<float>();
            var gpuTimes = new List<float>();

            foreach (int frameIndex in EnumerateFrameIndices(firstFrame, lastFrame))
            {
                FrameSummary summary = ReadFrameSummary(frameIndex);
                if (summary == null)
                {
                    warnings.Add($"Frame {frameIndex} had no valid raw frame data.");
                    continue;
                }

                summary.overCpuBudget = summary.cpuFrameTimeMs > frameBudgetMs;
                summary.overGpuBudget = summary.gpuFrameTimeMs > frameBudgetMs;
                frameSummaries.Add(summary);
                cpuTimes.Add(summary.cpuFrameTimeMs);

                if (summary.gpuFrameTimeMs > 0f)
                {
                    gpuTimes.Add(summary.gpuFrameTimeMs);
                }
            }

            if (frameSummaries.Count == 0)
            {
                throw new InvalidOperationException($"Profiler capture produced no valid frame summaries: {capturePath}");
            }

            if (gpuTimes.Count == 0)
            {
                warnings.Add("GPU frame time was not available in this capture, or all GPU timings were reported as 0 ms.");
            }

            List<FrameSummary> worstFrames = frameSummaries
                .OrderByDescending(frame => frame.cpuFrameTimeMs)
                .Take(Math.Max(1, worstFramesToAnalyze))
                .ToList();

            var markerAccumulator = new Dictionary<string, MarkerAccumulator>(StringComparer.Ordinal);
            var threadAccumulator = new Dictionary<string, ThreadAccumulator>(StringComparer.Ordinal);

            foreach (FrameSummary frame in worstFrames)
            {
                AnalyzeFrameSamples(frame.frameIndex, markerAccumulator, threadAccumulator, warnings);
            }

            List<MarkerSummary> topMarkers = markerAccumulator.Values
                .OrderByDescending(marker => marker.totalInclusiveMs)
                .ThenByDescending(marker => marker.totalSelfMs)
                .Take(Math.Max(1, topMarkerCount))
                .Select(marker => marker.ToSummary())
                .ToList();

            List<ThreadSummary> topThreads = threadAccumulator.Values
                .OrderByDescending(thread => thread.totalRootMs)
                .Select(thread => thread.ToSummary())
                .ToList();

            var report = new ExportReport
            {
                schemaVersion = "1.0",
                generatedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                capturePath = capturePath,
                projectName = Application.productName,
                unityVersion = Application.unityVersion,
                frameBudgetMs = frameBudgetMs,
                firstFrameIndex = firstFrame,
                lastFrameIndex = lastFrame,
                analyzedFrameCount = frameSummaries.Count,
                gpuTimingAvailable = gpuTimes.Count > 0,
                cpuFrameTimeMs = TimingStatistics.From(cpuTimes),
                gpuFrameTimeMs = gpuTimes.Count > 0 ? TimingStatistics.From(gpuTimes) : TimingStatistics.Unavailable(),
                overCpuBudgetFrameCount = frameSummaries.Count(frame => frame.overCpuBudget),
                overGpuBudgetFrameCount = frameSummaries.Count(frame => frame.overGpuBudget),
                worstCpuFrames = worstFrames,
                topMarkersInWorstCpuFrames = topMarkers,
                threadTotalsInWorstCpuFrames = topThreads,
                warnings = warnings
            };

            return report;
        }

        private static FrameSummary ReadFrameSummary(int frameIndex)
        {
            for (int threadIndex = 0; threadIndex < MaxThreadSearchCount; threadIndex++)
            {
                using RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frameIndex, threadIndex);
                if (!view.valid)
                {
                    continue;
                }

                return new FrameSummary
                {
                    frameIndex = frameIndex,
                    cpuFrameTimeMs = SanitizeMs(view.frameTimeMs),
                    gpuFrameTimeMs = SanitizeMs(view.frameGpuTimeMs),
                    fps = SanitizeMs(view.frameFps),
                    mainThreadName = string.IsNullOrWhiteSpace(view.threadName) ? "Thread 0" : view.threadName,
                    sampleCountOnFirstThread = view.sampleCount
                };
            }

            return null;
        }

        private static void AnalyzeFrameSamples(
            int frameIndex,
            Dictionary<string, MarkerAccumulator> markerAccumulator,
            Dictionary<string, ThreadAccumulator> threadAccumulator,
            List<string> warnings)
        {
            int missingThreadStreak = 0;
            bool foundThread = false;

            for (int threadIndex = 0; threadIndex < MaxThreadSearchCount; threadIndex++)
            {
                using RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frameIndex, threadIndex);
                if (!view.valid)
                {
                    if (foundThread)
                    {
                        missingThreadStreak++;
                        if (missingThreadStreak >= MissingThreadStopCount)
                        {
                            break;
                        }
                    }

                    continue;
                }

                foundThread = true;
                missingThreadStreak = 0;

                string threadName = string.IsNullOrWhiteSpace(view.threadName) ? $"Thread {threadIndex}" : view.threadName;
                string threadKey = $"{threadIndex}:{threadName}";

                if (!threadAccumulator.TryGetValue(threadKey, out ThreadAccumulator thread))
                {
                    thread = new ThreadAccumulator(threadIndex, threadName, view.threadGroupName);
                    threadAccumulator.Add(threadKey, thread);
                }

                thread.frameHits++;
                thread.sampleCount += view.sampleCount;

                int sampleIndex = 0;
                while (sampleIndex < view.sampleCount)
                {
                    int nextIndex = AccumulateSampleTree(view, sampleIndex, true, thread, markerAccumulator);
                    if (nextIndex <= sampleIndex)
                    {
                        warnings.Add($"Stopped sample traversal at frame {frameIndex}, thread {threadIndex}, sample {sampleIndex} because the profiler sample tree did not advance.");
                        break;
                    }

                    sampleIndex = nextIndex;
                }
            }
        }

        private static int AccumulateSampleTree(
            RawFrameDataView view,
            int sampleIndex,
            bool isRootSample,
            ThreadAccumulator thread,
            Dictionary<string, MarkerAccumulator> markerAccumulator)
        {
            float inclusiveMs = SanitizeMs(view.GetSampleTimeMs(sampleIndex));
            int childCount = Math.Max(0, view.GetSampleChildrenCount(sampleIndex));
            int nextIndex = sampleIndex + 1;
            float childInclusiveMs = 0f;

            for (int childOffset = 0; childOffset < childCount && nextIndex < view.sampleCount; childOffset++)
            {
                int childIndex = nextIndex;
                float childMs = SanitizeMs(view.GetSampleTimeMs(childIndex));
                nextIndex = AccumulateSampleTree(view, childIndex, false, thread, markerAccumulator);
                childInclusiveMs += childMs;
            }

            float selfMs = Mathf.Max(0f, inclusiveMs - childInclusiveMs);
            string markerName = view.GetSampleName(sampleIndex);
            if (string.IsNullOrWhiteSpace(markerName))
            {
                markerName = "<unnamed sample>";
            }

            ushort categoryIndex = view.GetSampleCategoryIndex(sampleIndex);
            string categoryName = GetCategoryName(view, categoryIndex);
            string markerKey = $"{thread.threadIndex}:{thread.threadName}:{categoryName}:{markerName}";

            if (!markerAccumulator.TryGetValue(markerKey, out MarkerAccumulator marker))
            {
                marker = new MarkerAccumulator(markerName, categoryName, thread.threadIndex, thread.threadName);
                markerAccumulator.Add(markerKey, marker);
            }

            marker.sampleCount++;
            marker.totalInclusiveMs += inclusiveMs;
            marker.totalSelfMs += selfMs;
            marker.maxInclusiveMs = Mathf.Max(marker.maxInclusiveMs, inclusiveMs);
            marker.maxSelfMs = Mathf.Max(marker.maxSelfMs, selfMs);

            if (isRootSample)
            {
                thread.totalRootMs += inclusiveMs;
            }

            thread.totalInclusiveMs += inclusiveMs;
            thread.totalSelfMs += selfMs;

            return nextIndex;
        }

        private static string GetCategoryName(RawFrameDataView view, ushort categoryIndex)
        {
            try
            {
                ProfilerCategoryInfo category = view.GetCategoryInfo(categoryIndex);
                return string.IsNullOrWhiteSpace(category.name) ? $"Category {categoryIndex}" : category.name;
            }
            catch (Exception)
            {
                return $"Category {categoryIndex}";
            }
        }

        private static IEnumerable<int> EnumerateFrameIndices(int firstFrame, int lastFrame)
        {
            int frameIndex = firstFrame;
            while (frameIndex >= 0 && frameIndex <= lastFrame)
            {
                yield return frameIndex;

                if (frameIndex == lastFrame)
                {
                    yield break;
                }

                int nextFrame = ProfilerDriver.GetNextFrameIndex(frameIndex);
                if (nextFrame <= frameIndex)
                {
                    yield break;
                }

                frameIndex = nextFrame;
            }
        }

        private static string GetLatestCapturePath()
        {
            if (!Directory.Exists(CaptureDirectory))
            {
                throw new DirectoryNotFoundException($"Profiler capture directory was not found: {CaptureDirectory}");
            }

            FileInfo latestCapture = new DirectoryInfo(CaptureDirectory)
                .GetFiles("*.data", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestCapture == null)
            {
                throw new FileNotFoundException($"No .data profiler capture was found in {CaptureDirectory}");
            }

            return latestCapture.FullName;
        }

        private static float SanitizeMs(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        private static void WriteUtf8NoBom(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string BuildMarkdown(ExportReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Profiler Capture Summary");
            builder.AppendLine();
            builder.AppendLine($"- Capture: `{report.capturePath}`");
            builder.AppendLine($"- Unity: `{report.unityVersion}`");
            builder.AppendLine($"- Generated: `{report.generatedAtLocal}`");
            builder.AppendLine($"- Frame budget: `{Format(report.frameBudgetMs)} ms`");
            builder.AppendLine($"- Frames: `{report.analyzedFrameCount}` (`{report.firstFrameIndex}` to `{report.lastFrameIndex}`)");
            builder.AppendLine();
            builder.AppendLine("## Frame Times");
            builder.AppendLine();
            builder.AppendLine("| Metric | CPU ms | GPU ms |");
            builder.AppendLine("| --- | ---: | ---: |");
            builder.AppendLine($"| Average | {Format(report.cpuFrameTimeMs.average)} | {Format(report.gpuFrameTimeMs.average)} |");
            builder.AppendLine($"| P95 | {Format(report.cpuFrameTimeMs.p95)} | {Format(report.gpuFrameTimeMs.p95)} |");
            builder.AppendLine($"| P99 | {Format(report.cpuFrameTimeMs.p99)} | {Format(report.gpuFrameTimeMs.p99)} |");
            builder.AppendLine($"| Max | {Format(report.cpuFrameTimeMs.max)} | {Format(report.gpuFrameTimeMs.max)} |");
            builder.AppendLine($"| Over 16ms frames | {report.overCpuBudgetFrameCount} | {report.overGpuBudgetFrameCount} |");
            builder.AppendLine();
            builder.AppendLine("## Worst CPU Frames");
            builder.AppendLine();
            builder.AppendLine("| Frame | CPU ms | GPU ms | FPS | First thread samples |");
            builder.AppendLine("| ---: | ---: | ---: | ---: | ---: |");
            foreach (FrameSummary frame in report.worstCpuFrames.Take(20))
            {
                builder.AppendLine($"| {frame.frameIndex} | {Format(frame.cpuFrameTimeMs)} | {Format(frame.gpuFrameTimeMs)} | {Format(frame.fps)} | {frame.sampleCountOnFirstThread} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Top Markers In Worst CPU Frames");
            builder.AppendLine();
            builder.AppendLine("| Marker | Thread | Category | Total ms | Self ms | Max ms | Samples |");
            builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");
            foreach (MarkerSummary marker in report.topMarkersInWorstCpuFrames.Take(30))
            {
                builder.AppendLine($"| {EscapeMarkdown(marker.name)} | {EscapeMarkdown(marker.threadName)} | {EscapeMarkdown(marker.categoryName)} | {Format(marker.totalInclusiveMs)} | {Format(marker.totalSelfMs)} | {Format(marker.maxInclusiveMs)} | {marker.sampleCount} |");
            }

            if (report.warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Warnings");
                builder.AppendLine();
                foreach (string warning in report.warnings)
                {
                    builder.AppendLine($"- {warning}");
                }
            }

            return builder.ToString();
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeMarkdown(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|");
        }

        [Serializable]
        private sealed class ExportReport
        {
            public string schemaVersion;
            public string generatedAtLocal;
            public string capturePath;
            public string projectName;
            public string unityVersion;
            public float frameBudgetMs;
            public int firstFrameIndex;
            public int lastFrameIndex;
            public int analyzedFrameCount;
            public bool gpuTimingAvailable;
            public TimingStatistics cpuFrameTimeMs;
            public TimingStatistics gpuFrameTimeMs;
            public int overCpuBudgetFrameCount;
            public int overGpuBudgetFrameCount;
            public List<FrameSummary> worstCpuFrames = new();
            public List<MarkerSummary> topMarkersInWorstCpuFrames = new();
            public List<ThreadSummary> threadTotalsInWorstCpuFrames = new();
            public List<string> warnings = new();
        }

        [Serializable]
        private sealed class TimingStatistics
        {
            public float min;
            public float average;
            public float median;
            public float p95;
            public float p99;
            public float max;

            public static TimingStatistics From(List<float> values)
            {
                if (values == null || values.Count == 0)
                {
                    return Unavailable();
                }

                List<float> sorted = values.OrderBy(value => value).ToList();
                float sum = 0f;
                foreach (float value in sorted)
                {
                    sum += value;
                }

                return new TimingStatistics
                {
                    min = sorted[0],
                    average = sum / sorted.Count,
                    median = Percentile(sorted, 0.5f),
                    p95 = Percentile(sorted, 0.95f),
                    p99 = Percentile(sorted, 0.99f),
                    max = sorted[sorted.Count - 1]
                };
            }

            public static TimingStatistics Unavailable()
            {
                return new TimingStatistics();
            }

            private static float Percentile(List<float> sortedValues, float percentile)
            {
                if (sortedValues.Count == 1)
                {
                    return sortedValues[0];
                }

                float rawIndex = (sortedValues.Count - 1) * Mathf.Clamp01(percentile);
                int lowerIndex = Mathf.FloorToInt(rawIndex);
                int upperIndex = Mathf.CeilToInt(rawIndex);
                if (lowerIndex == upperIndex)
                {
                    return sortedValues[lowerIndex];
                }

                float amount = rawIndex - lowerIndex;
                return Mathf.Lerp(sortedValues[lowerIndex], sortedValues[upperIndex], amount);
            }
        }

        [Serializable]
        private sealed class FrameSummary
        {
            public int frameIndex;
            public float cpuFrameTimeMs;
            public float gpuFrameTimeMs;
            public float fps;
            public bool overCpuBudget;
            public bool overGpuBudget;
            public string mainThreadName;
            public int sampleCountOnFirstThread;
        }

        [Serializable]
        private sealed class MarkerSummary
        {
            public string name;
            public string categoryName;
            public int threadIndex;
            public string threadName;
            public int sampleCount;
            public float totalInclusiveMs;
            public float totalSelfMs;
            public float averageInclusiveMs;
            public float averageSelfMs;
            public float maxInclusiveMs;
            public float maxSelfMs;
        }

        [Serializable]
        private sealed class ThreadSummary
        {
            public int threadIndex;
            public string threadName;
            public string threadGroupName;
            public int frameHits;
            public int sampleCount;
            public float totalRootMs;
            public float totalInclusiveMs;
            public float totalSelfMs;
        }

        private sealed class MarkerAccumulator
        {
            public readonly string name;
            public readonly string categoryName;
            public readonly int threadIndex;
            public readonly string threadName;
            public int sampleCount;
            public float totalInclusiveMs;
            public float totalSelfMs;
            public float maxInclusiveMs;
            public float maxSelfMs;

            public MarkerAccumulator(string name, string categoryName, int threadIndex, string threadName)
            {
                this.name = name;
                this.categoryName = categoryName;
                this.threadIndex = threadIndex;
                this.threadName = threadName;
            }

            public MarkerSummary ToSummary()
            {
                return new MarkerSummary
                {
                    name = name,
                    categoryName = categoryName,
                    threadIndex = threadIndex,
                    threadName = threadName,
                    sampleCount = sampleCount,
                    totalInclusiveMs = totalInclusiveMs,
                    totalSelfMs = totalSelfMs,
                    averageInclusiveMs = sampleCount > 0 ? totalInclusiveMs / sampleCount : 0f,
                    averageSelfMs = sampleCount > 0 ? totalSelfMs / sampleCount : 0f,
                    maxInclusiveMs = maxInclusiveMs,
                    maxSelfMs = maxSelfMs
                };
            }
        }

        private sealed class ThreadAccumulator
        {
            public readonly int threadIndex;
            public readonly string threadName;
            public readonly string threadGroupName;
            public int frameHits;
            public int sampleCount;
            public float totalRootMs;
            public float totalInclusiveMs;
            public float totalSelfMs;

            public ThreadAccumulator(int threadIndex, string threadName, string threadGroupName)
            {
                this.threadIndex = threadIndex;
                this.threadName = threadName;
                this.threadGroupName = threadGroupName;
            }

            public ThreadSummary ToSummary()
            {
                return new ThreadSummary
                {
                    threadIndex = threadIndex,
                    threadName = threadName,
                    threadGroupName = threadGroupName,
                    frameHits = frameHits,
                    sampleCount = sampleCount,
                    totalRootMs = totalRootMs,
                    totalInclusiveMs = totalInclusiveMs,
                    totalSelfMs = totalSelfMs
                };
            }
        }
    }
}
