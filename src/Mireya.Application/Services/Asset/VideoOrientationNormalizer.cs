using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xabe.FFmpeg;

namespace Mireya.Application.Services.Asset;

internal static class VideoOrientationNormalizer
{
    private const int ProcessErrorTailLength = 2_000;

    public static async Task<int> ProbeRotationDegreesAsync(string filePath)
    {
        var output = await RunProcessAsync(
            GetExecutablePath("ffprobe"),
            [
                "-v",
                "error",
                "-select_streams",
                "v:0",
                "-show_entries",
                "stream_tags=rotate:stream_side_data",
                "-of",
                "json",
                filePath,
            ]
        );

        return ParseRotationDegrees(output);
    }

    public static async Task NormalizeAsync(string inputPath, string outputPath)
    {
        try
        {
            await RunProcessAsync(
                GetExecutablePath("ffmpeg"),
                [
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-y",
                    "-i",
                    inputPath,
                    "-map",
                    "0:v:0",
                    "-map",
                    "0:a?",
                    "-c:v",
                    "libx264",
                    "-preset",
                    "medium",
                    "-crf",
                    "18",
                    "-pix_fmt",
                    "yuv420p",
                    "-vf",
                    "scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1",
                    "-c:a",
                    "aac",
                    "-b:a",
                    "192k",
                    "-movflags",
                    "+faststart",
                    "-map_metadata",
                    "0",
                    "-metadata:s:v:0",
                    "rotate=0",
                    outputPath,
                ]
            );
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }
    }

    internal static int ParseRotationDegrees(string ffprobeJson)
    {
        using var document = JsonDocument.Parse(ffprobeJson);
        if (
            !document.RootElement.TryGetProperty("streams", out var streams)
            || streams.ValueKind != JsonValueKind.Array
            || streams.GetArrayLength() == 0
        )
        {
            throw new InvalidOperationException("ffprobe did not return a video stream.");
        }

        var stream = streams[0];

        // A display matrix is the authoritative representation used by modern MP4/MOV files.
        // Prefer it over the legacy rotate tag when both are present.
        if (
            stream.TryGetProperty("side_data_list", out var sideDataList)
            && sideDataList.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var sideData in sideDataList.EnumerateArray())
            {
                if (
                    sideData.TryGetProperty("rotation", out var rotation)
                    && TryReadRotation(rotation, out var degrees)
                )
                {
                    return NormalizeDegrees(degrees);
                }
            }
        }

        if (
            stream.TryGetProperty("tags", out var tags)
            && tags.ValueKind == JsonValueKind.Object
            && tags.TryGetProperty("rotate", out var rotateTag)
            && TryReadRotation(rotateTag, out var taggedDegrees)
        )
        {
            return NormalizeDegrees(taggedDegrees);
        }

        return 0;
    }

    private static bool TryReadRotation(JsonElement value, out double degrees)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDouble(out degrees);

        if (value.ValueKind == JsonValueKind.String)
            return double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out degrees
            );

        degrees = 0;
        return false;
    }

    private static int NormalizeDegrees(double degrees)
    {
        var rounded = (int)Math.Round(degrees, MidpointRounding.AwayFromZero);
        return ((rounded % 360) + 360) % 360;
    }

    private static string GetExecutablePath(string executableName)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{executableName}.exe" : executableName;
        return string.IsNullOrWhiteSpace(FFmpeg.ExecutablesPath)
            ? fileName
            : Path.Combine(FFmpeg.ExecutablesPath, fileName);
    }

    private static async Task<string> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Could not start {Path.GetFileName(executablePath)}."
            );

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            var errorTail =
                error.Length <= ProcessErrorTailLength ? error : error[^ProcessErrorTailLength..];
            throw new InvalidOperationException(
                $"{Path.GetFileName(executablePath)} exited with code {process.ExitCode}: {errorTail.Trim()}"
            );
        }

        return output;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Preserve the original conversion exception. A later upload cleanup can retry.
        }
    }
}
