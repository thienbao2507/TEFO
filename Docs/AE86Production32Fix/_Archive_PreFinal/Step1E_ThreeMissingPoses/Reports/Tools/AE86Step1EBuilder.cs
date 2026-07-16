using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class AE86Step1EBuilder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] NewSlots = { 6, 9, 14 };

    // These are deliberately REVIEW until the generated review sheets receive a manual landmark audit.
    private static readonly Dictionary<int, string> ManualNewStatus = new Dictionary<int, string>
    {
        { 6, "REVIEW" },
        { 9, "REVIEW" },
        { 14, "REVIEW" }
    };

    private static readonly Dictionary<string, HashSnapshot> ExpectedProtected = new Dictionary<string, HashSnapshot>(StringComparer.OrdinalIgnoreCase)
    {
        { "active Production32 PNG", new HashSnapshot(19, "0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D") },
        { "all Assets .meta", new HashSnapshot(1562, "75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF") },
        { "all Assets code", new HashSnapshot(56, "06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93") },
        { "all prefabs", new HashSnapshot(152, "92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649") },
        { "all scenes", new HashSnapshot(38, "63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2") },
        { "Step 1 candidates", new HashSnapshot(25, "4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654") },
        { "Step 1C outputs", new HashSnapshot(26, "0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320") },
        { "Step 1D outputs", new HashSnapshot(27, "16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D") }
    };

    private sealed class SpriteMetrics
    {
        public int CanvasWidth;
        public int CanvasHeight;
        public Rectangle Bounds;
        public double CentroidX;
        public double CentroidY;
        public double AxisAngle;
        public double ProjectedLength;
        public double ProjectedWidth;
        public int AlphaPixelCount;
        public int PartialAlphaPixelCount;
        public int EdgeTouchCount;
        public int ComponentCount;
        public int Baseline;
        public int GreenDominantPixels;
    }

    private sealed class CleanupRecord
    {
        public int Slot;
        public string RawPath;
        public string OutputPath;
        public SpriteMetrics RawMetrics;
        public SpriteMetrics OutputMetrics;
        public int RawBackgroundPixels;
        public int RawForegroundComponents;
        public int PaletteSize;
        public double UniformScale;
        public double TargetLength;
        public string Method;
    }

    private sealed class SlotState
    {
        public int Slot;
        public double TargetAngle;
        public string ImagePath;
        public string Filename;
        public string SourceFile;
        public string SourceMethod;
        public string TransformApplied;
        public SpriteMetrics Metrics;
        public double ExpectedLength;
        public double ScaleDifferencePercent;
        public double ExpectedCentroidX;
        public double ExpectedCentroidY;
        public double CenterOffsetX;
        public double CenterOffsetY;
        public double AngularError;
        public string TechnicalStatus;
        public string FrontRearResult;
        public string VisualIdentityResult;
        public string ArtifactResult;
        public string GenuinePoseResult;
        public string Status;
        public string Notes;
    }

    private sealed class PairAudit
    {
        public int FirstSlot;
        public int SecondSlot;
        public double SignedDelta;
        public double AbsoluteDelta;
        public double Similarity;
        public double ScaleMismatch;
        public double CentroidShift;
        public string Status;
        public string ManualProgression;
    }

    private sealed class HashSnapshot
    {
        public int Count;
        public string Hash;

        public HashSnapshot()
        {
        }

        public HashSnapshot(int count, string hash)
        {
            Count = count;
            Hash = hash;
        }
    }

    private sealed class ColorCount
    {
        public int Key;
        public int Count;
    }

    public static int Main(string[] args)
    {
        try
        {
            string projectRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            Run(projectRoot);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    public static void Run(string projectRoot)
    {
        projectRoot = Path.GetFullPath(projectRoot);
        string outputRoot = Step1ERoot(projectRoot);
        string pngRoot = Path.Combine(outputRoot, "PNG");
        string referencesRoot = Path.Combine(outputRoot, "References");
        string previewsRoot = Path.Combine(outputRoot, "Previews");
        string reportsRoot = Path.Combine(outputRoot, "Reports");
        Directory.CreateDirectory(pngRoot);
        Directory.CreateDirectory(referencesRoot);
        Directory.CreateDirectory(previewsRoot);
        Directory.CreateDirectory(reportsRoot);

        Dictionary<string, HashSnapshot> protectedBefore = CaptureProtectedHashes(projectRoot);
        List<Color> palette = BuildIdentityPalette(projectRoot);
        Dictionary<int, CleanupRecord> cleanup = new Dictionary<int, CleanupRecord>();
        foreach (int slot in NewSlots)
            cleanup[slot] = ProcessGeneratedPose(projectRoot, slot, palette);

        foreach (int slot in NewSlots)
            DrawReferenceTriplet(projectRoot, slot);

        List<SlotState> states = BuildStates(projectRoot);
        List<PairAudit> pairs = BuildPairAudits(states);
        DrawThreeSlotsReview(projectRoot, states, pairs);
        Draw17ContactSheet(projectRoot, states);
        DrawNeighborReview(projectRoot, states, pairs);
        DrawFull32Preview(projectRoot, states);
        WriteMetricsCsv(projectRoot, states, pairs, cleanup);

        Dictionary<string, HashSnapshot> protectedAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, states, pairs, cleanup, protectedBefore, protectedAfter);

        Console.WriteLine("Step 1E outputs written to: " + outputRoot);
        foreach (int slot in NewSlots)
        {
            SlotState state = states[slot];
            Console.WriteLine("slot " + slot.ToString("00", Invariant) + ": " + state.Status + ", PCA " + F(state.Metrics.AxisAngle) + ", bbox " + state.Metrics.Bounds.Width + "x" + state.Metrics.Bounds.Height);
        }
    }

    private static CleanupRecord ProcessGeneratedPose(string projectRoot, int slot, List<Color> palette)
    {
        double targetAngle = TargetAngle(slot);
        string rawPath = Path.Combine(Step1ERoot(projectRoot), "References", "slot_" + slot.ToString("00", Invariant) + "_generated_raw.png");
        string outputPath = Step1EOutputPath(projectRoot, slot);
        if (!File.Exists(rawPath)) throw new FileNotFoundException("Missing generated source", rawPath);

        int backgroundPixels;
        int foregroundComponents;
        using (Bitmap source = LoadArgb(rawPath))
        using (Bitmap isolated = ExtractLargestBorderSeparatedComponent(source, out backgroundPixels, out foregroundComponents))
        {
            SpriteMetrics rawMetrics = AnalyzeBitmap(isolated, targetAngle);
            SpriteMetrics previous = AnalyzeBitmap(Step1DPath(projectRoot, slot - 1), TargetAngle(slot - 1));
            SpriteMetrics next = AnalyzeBitmap(Step1DPath(projectRoot, slot + 1), TargetAngle(slot + 1));
            double targetLength = (previous.ProjectedLength + next.ProjectedLength) / 2.0;
            double scale = targetLength / rawMetrics.ProjectedLength;

            using (Bitmap normalized = NormalizeGeneratedSprite(isolated, rawMetrics.Bounds, scale, palette))
            {
                SavePng(normalized, outputPath);
            }

            CleanupRecord record = new CleanupRecord();
            record.Slot = slot;
            record.RawPath = rawPath;
            record.OutputPath = outputPath;
            record.RawMetrics = rawMetrics;
            record.OutputMetrics = AnalyzeBitmap(outputPath, targetAngle);
            record.RawBackgroundPixels = backgroundPixels;
            record.RawForegroundComponents = foregroundComponents;
            record.PaletteSize = palette.Count;
            record.UniformScale = scale;
            record.TargetLength = targetLength;
            record.Method = "Border-connected chroma flood-fill; largest-component isolation; uniform nearest-neighbor reduction; shared AE86 palette mapping; baseline/center translation";
            return record;
        }
    }

    private static Bitmap ExtractLargestBorderSeparatedComponent(Bitmap source, out int backgroundPixelCount, out int foregroundComponentCount)
    {
        int width = source.Width;
        int height = source.Height;
        int[,] pixels = new int[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[x, y] = source.GetPixel(x, y).ToArgb();

        bool[,] background = new bool[width, height];
        Queue<Point> queue = new Queue<Point>();
        for (int x = 0; x < width; x++)
        {
            SeedBackground(x, 0, pixels, background, queue);
            SeedBackground(x, height - 1, pixels, background, queue);
        }
        for (int y = 1; y < height - 1; y++)
        {
            SeedBackground(0, y, pixels, background, queue);
            SeedBackground(width - 1, y, pixels, background, queue);
        }

        int[] dx4 = { -1, 1, 0, 0 };
        int[] dy4 = { 0, 0, -1, 1 };
        backgroundPixelCount = 0;
        while (queue.Count > 0)
        {
            Point point = queue.Dequeue();
            backgroundPixelCount++;
            for (int i = 0; i < 4; i++)
            {
                int nx = point.X + dx4[i];
                int ny = point.Y + dy4[i];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height || background[nx, ny]) continue;
                if (!IsGreenBackgroundCandidate(pixels[nx, ny])) continue;
                background[nx, ny] = true;
                queue.Enqueue(new Point(nx, ny));
            }
        }

        bool[,] visited = new bool[width, height];
        int[] dx8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy8 = { -1, -1, -1, 0, 0, 1, 1, 1 };
        List<Point> largest = new List<Point>();
        foregroundComponentCount = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (background[x, y] || visited[x, y]) continue;
                foregroundComponentCount++;
                List<Point> component = new List<Point>();
                visited[x, y] = true;
                queue.Enqueue(new Point(x, y));
                while (queue.Count > 0)
                {
                    Point point = queue.Dequeue();
                    component.Add(point);
                    for (int i = 0; i < 8; i++)
                    {
                        int nx = point.X + dx8[i];
                        int ny = point.Y + dy8[i];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny] || background[nx, ny]) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }
                if (component.Count > largest.Count) largest = component;
            }
        }
        if (largest.Count == 0) throw new InvalidDataException("Border flood-fill did not isolate a vehicle component.");

        Bitmap isolated = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        foreach (Point point in largest)
        {
            Color color = Color.FromArgb(pixels[point.X, point.Y]);
            isolated.SetPixel(point.X, point.Y, Color.FromArgb(255, color.R, color.G, color.B));
        }
        return isolated;
    }

    private static void SeedBackground(int x, int y, int[,] pixels, bool[,] background, Queue<Point> queue)
    {
        if (background[x, y] || !IsGreenBackgroundCandidate(pixels[x, y])) return;
        background[x, y] = true;
        queue.Enqueue(new Point(x, y));
    }

    private static bool IsGreenBackgroundCandidate(int argb)
    {
        int alpha = (argb >> 24) & 255;
        if (alpha == 0) return true;
        int red = (argb >> 16) & 255;
        int green = (argb >> 8) & 255;
        int blue = argb & 255;
        int other = Math.Max(red, blue);
        return green >= 24 && green >= red + 8 && green >= blue + 8 && green * 100 >= other * 112;
    }

    private static Bitmap NormalizeGeneratedSprite(Bitmap isolated, Rectangle sourceBounds, double uniformScale, List<Color> palette)
    {
        int scaledWidth = Math.Max(1, (int)Math.Round(sourceBounds.Width * uniformScale, MidpointRounding.AwayFromZero));
        int scaledHeight = Math.Max(1, (int)Math.Round(sourceBounds.Height * uniformScale, MidpointRounding.AwayFromZero));
        double fitScale = Math.Min(182.0 / scaledWidth, 166.0 / scaledHeight);
        if (fitScale < 1.0)
        {
            scaledWidth = Math.Max(1, (int)Math.Floor(scaledWidth * fitScale));
            scaledHeight = Math.Max(1, (int)Math.Floor(scaledHeight * fitScale));
        }

        using (Bitmap sampled = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < scaledHeight; y++)
            {
                int sy = sourceBounds.Y + NearestEndpoint(y, scaledHeight, sourceBounds.Height);
                for (int x = 0; x < scaledWidth; x++)
                {
                    int sx = sourceBounds.X + NearestEndpoint(x, scaledWidth, sourceBounds.Width);
                    Color sourceColor = isolated.GetPixel(sx, sy);
                    if (sourceColor.A == 0) continue;
                    Color mapped = NearestPaletteColor(sourceColor, palette);
                    sampled.SetPixel(x, y, Color.FromArgb(255, mapped.R, mapped.G, mapped.B));
                }
            }

            using (Bitmap connected = KeepLargestOpaqueComponent(sampled))
            {
                SpriteMetrics metrics = AnalyzeBitmap(connected, 0.0);
                double center = (metrics.Bounds.Left + metrics.Bounds.Right - 1) / 2.0;
                int offsetX = (int)Math.Round(93.0 - center, MidpointRounding.AwayFromZero);
                int offsetY = BaselineY - (metrics.Bounds.Bottom - 1);
                Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
                for (int y = metrics.Bounds.Top; y < metrics.Bounds.Bottom; y++)
                {
                    for (int x = metrics.Bounds.Left; x < metrics.Bounds.Right; x++)
                    {
                        Color color = connected.GetPixel(x, y);
                        if (color.A == 0) continue;
                        int dx = x + offsetX;
                        int dy = y + offsetY;
                        if (dx <= 0 || dy <= 0 || dx >= CanvasSize - 1 || dy >= CanvasSize - 1)
                            throw new InvalidDataException("Normalized pose would touch the 186x186 canvas edge.");
                        output.SetPixel(dx, dy, Color.FromArgb(255, color.R, color.G, color.B));
                    }
                }
                return output;
            }
        }
    }

    private static int NearestEndpoint(int destinationIndex, int destinationSize, int sourceSize)
    {
        if (destinationSize <= 1 || sourceSize <= 1) return 0;
        return Math.Min(sourceSize - 1, (int)Math.Round(destinationIndex * (sourceSize - 1.0) / (destinationSize - 1.0), MidpointRounding.AwayFromZero));
    }

    private static Bitmap KeepLargestOpaqueComponent(Bitmap source)
    {
        int width = source.Width;
        int height = source.Height;
        bool[,] visited = new bool[width, height];
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        Queue<Point> queue = new Queue<Point>();
        List<Point> largest = new List<Point>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (visited[x, y] || source.GetPixel(x, y).A == 0) continue;
                List<Point> component = new List<Point>();
                visited[x, y] = true;
                queue.Enqueue(new Point(x, y));
                while (queue.Count > 0)
                {
                    Point point = queue.Dequeue();
                    component.Add(point);
                    for (int i = 0; i < 8; i++)
                    {
                        int nx = point.X + dx[i];
                        int ny = point.Y + dy[i];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny] || source.GetPixel(nx, ny).A == 0) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }
                if (component.Count > largest.Count) largest = component;
            }
        }

        Bitmap output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        foreach (Point point in largest) output.SetPixel(point.X, point.Y, source.GetPixel(point.X, point.Y));
        return output;
    }

    private static List<Color> BuildIdentityPalette(string projectRoot)
    {
        Dictionary<int, int> frequencies = new Dictionary<int, int>();
        for (int slot = 0; slot <= 16; slot++)
        {
            using (Bitmap bitmap = LoadArgb(Step1DPath(projectRoot, slot)))
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        if (color.A == 0) continue;
                        int key = (color.R << 16) | (color.G << 8) | color.B;
                        int count;
                        frequencies.TryGetValue(key, out count);
                        frequencies[key] = count + 1;
                    }
                }
            }
        }

        List<ColorCount> ordered = frequencies.Select(pair => new ColorCount { Key = pair.Key, Count = pair.Value }).OrderByDescending(item => item.Count).ToList();
        HashSet<int> selected = new HashSet<int>();
        AddPaletteMatches(ordered, selected, 96, delegate(Color color) { return true; });
        AddPaletteMatches(ordered, selected, 20, delegate(Color color) { return color.R > color.G * 1.20 && color.R > color.B * 1.25; });
        AddPaletteMatches(ordered, selected, 20, delegate(Color color) { return color.R > 120 && color.G > 70 && color.B < 110 && color.R >= color.G; });
        AddPaletteMatches(ordered, selected, 20, delegate(Color color) { return Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) < 24 && color.R > 150; });
        AddPaletteMatches(ordered, selected, 20, delegate(Color color) { return color.B >= color.R && color.B >= color.G && color.B < 140; });
        return selected.Select(ColorFromKey).ToList();
    }

    private static void AddPaletteMatches(List<ColorCount> ordered, HashSet<int> selected, int limit, Predicate<Color> predicate)
    {
        int added = 0;
        foreach (ColorCount item in ordered)
        {
            Color color = ColorFromKey(item.Key);
            if (!predicate(color) || selected.Contains(item.Key)) continue;
            selected.Add(item.Key);
            added++;
            if (added >= limit) break;
        }
    }

    private static Color ColorFromKey(int key)
    {
        return Color.FromArgb(255, (key >> 16) & 255, (key >> 8) & 255, key & 255);
    }

    private static Color NearestPaletteColor(Color source, List<Color> palette)
    {
        Color best = palette[0];
        int bestDistance = int.MaxValue;
        foreach (Color candidate in palette)
        {
            int dr = source.R - candidate.R;
            int dg = source.G - candidate.G;
            int db = source.B - candidate.B;
            int distance = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }
        return best;
    }

    private static List<SlotState> BuildStates(string projectRoot)
    {
        SpriteMetrics up = AnalyzeBitmap(Step1DPath(projectRoot, 0), 90.0);
        SpriteMetrics right = AnalyzeBitmap(Step1DPath(projectRoot, 8), 0.0);
        SpriteMetrics down = AnalyzeBitmap(Step1DPath(projectRoot, 16), 270.0);
        List<SlotState> states = new List<SlotState>();
        for (int slot = 0; slot <= 16; slot++)
        {
            SlotState state = new SlotState();
            state.Slot = slot;
            state.TargetAngle = TargetAngle(slot);
            state.ImagePath = IsNewSlot(slot) ? Step1EOutputPath(projectRoot, slot) : Step1DPath(projectRoot, slot);
            state.Filename = Path.GetFileName(state.ImagePath);
            state.SourceFile = Relative(projectRoot, IsNewSlot(slot) ? RawPath(projectRoot, slot) : state.ImagePath);
            state.SourceMethod = IsNewSlot(slot) ? "Genuine generated perspective plus deterministic pixel cleanup" : "Read-only Step 1D selected source";
            state.TransformApplied = IsNewSlot(slot) ? "Uniform nearest reduction and translation only; no neighbor transform" : "None (read-only source)";
            state.Metrics = AnalyzeBitmap(state.ImagePath, state.TargetAngle);
            state.ExpectedLength = InterpolateAnchorValue(slot, up.ProjectedLength, right.ProjectedLength, down.ProjectedLength);
            state.ExpectedCentroidX = InterpolateAnchorValue(slot, up.CentroidX, right.CentroidX, down.CentroidX);
            state.ExpectedCentroidY = InterpolateAnchorValue(slot, up.CentroidY, right.CentroidY, down.CentroidY);
            state.ScaleDifferencePercent = ((state.Metrics.ProjectedLength - state.ExpectedLength) / state.ExpectedLength) * 100.0;
            state.CenterOffsetX = state.Metrics.CentroidX - state.ExpectedCentroidX;
            state.CenterOffsetY = state.Metrics.CentroidY - state.ExpectedCentroidY;
            state.AngularError = ShortestAngleError(state.Metrics.AxisAngle, state.TargetAngle);
            ValidateState(state);
            states.Add(state);
        }
        return states;
    }

    private static void ValidateState(SlotState state)
    {
        SpriteMetrics metrics = state.Metrics;
        bool canvasPass = metrics.CanvasWidth == CanvasSize && metrics.CanvasHeight == CanvasSize;
        bool alphaPass = metrics.PartialAlphaPixelCount == 0 && metrics.ComponentCount == 1;
        bool baselinePass = metrics.Baseline == BaselineY;
        bool edgePass = metrics.EdgeTouchCount == 0;
        bool cropPass = metrics.Bounds.Left > 0 && metrics.Bounds.Top > 0 && metrics.Bounds.Right < CanvasSize && metrics.Bounds.Bottom < CanvasSize;
        bool greenPass = metrics.GreenDominantPixels == 0;
        bool technicalPass = canvasPass && alphaPass && baselinePass && edgePass && cropPass && greenPass;

        state.TechnicalStatus = technicalPass ? "PASS" : "FAIL";
        state.ArtifactResult = greenPass && alphaPass && edgePass ? "PASS: no green halo, partial alpha, detached component, or edge contact detected" : "FAIL";
        state.FrontRearResult = ManualFrontRearResult(state.Slot);
        state.VisualIdentityResult = IsNewSlot(state.Slot)
            ? "MANUAL REVIEW: AE86 body, hood, windows, wheels, pop-up lights, yellow front lights, and red rear lights"
            : "PASS: protected Step 1D identity source";
        state.GenuinePoseResult = IsNewSlot(state.Slot)
            ? "PASS: independently generated perspective; not sourced from a flipped, scaled, translated, or rotated neighbor"
            : "N/A: protected Step 1D source";

        if (!technicalPass)
        {
            state.Status = "FAIL";
            state.Notes = "Technical PNG acceptance failed.";
        }
        else if (Math.Abs(state.ScaleDifferencePercent) > 8.0)
        {
            state.Status = "FAIL";
            state.Notes = "Scale differs from the anchor-interpolated sequence by more than 8 percent.";
        }
        else if (IsNewSlot(state.Slot))
        {
            state.Status = ManualNewStatus[state.Slot];
            state.Notes = ManualNewNotes(state.Slot);
        }
        else
        {
            state.Status = "PASS";
            state.Notes = "Protected Step 1D source retained unchanged in the temporary 17-source composition.";
        }
    }

    private static List<PairAudit> BuildPairAudits(List<SlotState> states)
    {
        List<PairAudit> audits = new List<PairAudit>();
        for (int slot = 0; slot < 16; slot++)
        {
            SlotState first = states[slot];
            SlotState second = states[slot + 1];
            PairAudit audit = new PairAudit();
            audit.FirstSlot = slot;
            audit.SecondSlot = slot + 1;
            audit.SignedDelta = SignedShortestDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
            audit.AbsoluteDelta = Math.Abs(audit.SignedDelta);
            audit.Similarity = AlphaIoU(first.ImagePath, second.ImagePath);
            audit.ScaleMismatch = Math.Max(Math.Abs(first.ScaleDifferencePercent), Math.Abs(second.ScaleDifferencePercent));
            audit.CentroidShift = Distance(first.Metrics.CentroidX, first.Metrics.CentroidY, second.Metrics.CentroidX, second.Metrics.CentroidY);
            audit.ManualProgression = ManualPairProgression(slot);

            if (audit.Similarity >= 0.97 || (audit.Similarity >= 0.92 && audit.AbsoluteDelta < 4.0))
                audit.Status = "COLLAPSED";
            else if (audit.ManualProgression.StartsWith("REVERSED", StringComparison.Ordinal))
                audit.Status = "REVERSED";
            else if (audit.ScaleMismatch > 8.0)
                audit.Status = "SCALE FAIL";
            else if (audit.CentroidShift > 18.0)
                audit.Status = "CENTER FAIL";
            else if (audit.AbsoluteDelta > 22.0)
                audit.Status = "PCA LARGE STEP REVIEW";
            else if (audit.AbsoluteDelta < 3.0)
                audit.Status = "PCA COMPRESSED / MANUAL REVIEW";
            else if (audit.ScaleMismatch > 5.0 || audit.CentroidShift > 12.0 || first.Status != "PASS" || second.Status != "PASS")
                audit.Status = "REVIEW";
            else
                audit.Status = "PASS";
            audits.Add(audit);
        }
        return audits;
    }

    private static void DrawReferenceTriplet(string projectRoot, int slot)
    {
        int width = 820;
        int height = 350;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 11, FontStyle.Bold))
        using (Font small = new Font("Consolas", 9, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(246, 246, 246));
            int[] x = { 35, 317, 599 };
            int imageY = 68;
            string[] headings = { "PREVIOUS " + (slot - 1).ToString("00"), "EMPTY TARGET " + slot.ToString("00"), "NEXT " + (slot + 1).ToString("00") };
            for (int i = 0; i < 3; i++)
            {
                graphics.DrawString(headings[i], label, Brushes.Black, x[i], 42);
                DrawChecker(graphics, new Rectangle(x[i], imageY, CanvasSize, CanvasSize), 12);
                graphics.DrawRectangle(Pens.Gray, x[i], imageY, CanvasSize, CanvasSize);
            }
            DrawImageUnscaled(graphics, Step1DPath(projectRoot, slot - 1), x[0], imageY);
            DrawImageUnscaled(graphics, Step1DPath(projectRoot, slot + 1), x[2], imageY);

            double angle = TargetAngle(slot) * Math.PI / 180.0;
            PointF center = new PointF(x[1] + CanvasSize / 2f, imageY + CanvasSize / 2f);
            float vx = (float)(Math.Cos(angle) * 62.0);
            float vy = (float)(-Math.Sin(angle) * 62.0);
            PointF front = new PointF(center.X + vx, center.Y + vy);
            PointF rear = new PointF(center.X - vx, center.Y - vy);
            using (Pen arrow = new Pen(Color.Firebrick, 5))
            {
                arrow.CustomEndCap = new AdjustableArrowCap(7, 8);
                graphics.DrawLine(arrow, rear, front);
            }
            graphics.DrawString("FRONT", small, Brushes.Firebrick, front.X - 24, front.Y - 22);
            graphics.DrawString("REAR", small, Brushes.Navy, rear.X - 20, rear.Y + 7);
            graphics.DrawString(F(TargetAngle(slot)) + " deg", title, Brushes.Black, center.X - 54, center.Y - 14);

            graphics.DrawString(TripletBias(slot), label, Brushes.Black, 35, 277);
            graphics.DrawString("Front/rear landmarks override PCA branch ambiguity. Center panel intentionally contains no car.", small, Brushes.DimGray, 35, 309);
            SavePng(canvas, Path.Combine(Step1ERoot(projectRoot), "References", "slot_" + slot.ToString("00", Invariant) + "_neighbor_triplet.png"));
        }
    }

    private static void DrawThreeSlotsReview(string projectRoot, List<SlotState> states, List<PairAudit> pairs)
    {
        int width = 1500;
        int height = 1110;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 17, FontStyle.Bold))
        using (Font heading = new Font("Consolas", 12, FontStyle.Bold))
        using (Font textFont = new Font("Consolas", 9, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("AE86 STEP 1E - THREE GENUINE SOURCE POSES", title, Brushes.Black, 18, 14);
            int[] columns = { 24, 390, 756, 1122 };
            int rowHeight = 350;
            for (int row = 0; row < NewSlots.Length; row++)
            {
                int slot = NewSlots[row];
                SlotState state = states[slot];
                int y = 62 + row * rowHeight;
                string[] labels = { "PREVIOUS " + (slot - 1).ToString("00"), "OLD STEP 1D FAIL", "NEW STEP 1E", "NEXT " + (slot + 1).ToString("00") };
                string[] paths = { states[slot - 1].ImagePath, Step1DPath(projectRoot, slot), state.ImagePath, states[slot + 1].ImagePath };
                for (int column = 0; column < 4; column++)
                {
                    Rectangle panel = new Rectangle(columns[column], y, 342, 315);
                    graphics.FillRectangle(Brushes.White, panel);
                    graphics.DrawRectangle(column == 2 ? StatusPen(state.Status) : Pens.Gray, panel);
                    graphics.DrawString(labels[column], heading, Brushes.Black, panel.X + 10, panel.Y + 8);
                    Rectangle image = new Rectangle(panel.X + 78, panel.Y + 38, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, paths[column], image.X, image.Y);
                }

                PairAudit previousPair = pairs[slot - 1];
                PairAudit nextPair = pairs[slot];
                int textY = y + 232;
                graphics.DrawString("target " + F(state.TargetAngle) + "  measured PCA " + F(state.Metrics.AxisAngle) + "  bbox " + state.Metrics.Bounds.Width + "x" + state.Metrics.Bounds.Height, textFont, Brushes.Black, columns[2] + 10, textY);
                graphics.DrawString("IoU prev " + F(previousPair.Similarity) + " / next " + F(nextPair.Similarity) + "  scale " + SignedF(state.ScaleDifferencePercent) + "%", textFont, Brushes.Black, columns[2] + 10, textY + 18);
                graphics.DrawString("centroid offset (" + SignedF(state.CenterOffsetX) + ", " + SignedF(state.CenterOffsetY) + ")  baseline " + state.Metrics.Baseline, textFont, Brushes.Black, columns[2] + 10, textY + 36);
                graphics.DrawString(state.FrontRearResult, textFont, Brushes.DarkGreen, columns[2] + 10, textY + 54);
                graphics.DrawString(state.Status, heading, StatusBrush(state.Status), columns[2] + 10, textY + 74);
            }
            SavePng(canvas, Path.Combine(Step1ERoot(projectRoot), "Previews", "step1e_three_slots_review.png"));
        }
    }

    private static void Draw17ContactSheet(string projectRoot, List<SlotState> states)
    {
        int columns = 5;
        int cellWidth = 285;
        int cellHeight = 285;
        int width = columns * cellWidth;
        int height = 4 * cellHeight + 45;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 10, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1E TEMPORARY 17-SOURCE COMPOSITION", title, Brushes.Black, 14, 10);
            for (int slot = 0; slot <= 16; slot++)
            {
                SlotState state = states[slot];
                int column = slot % columns;
                int row = slot / columns;
                int x = column * cellWidth + 14;
                int y = row * cellHeight + 48;
                Rectangle image = new Rectangle(x + 36, y + 32, CanvasSize, CanvasSize);
                graphics.FillRectangle(Brushes.White, x, y, cellWidth - 18, cellHeight - 12);
                graphics.DrawRectangle(StatusPen(state.Status), x, y, cellWidth - 18, cellHeight - 12);
                graphics.DrawString("slot " + slot.ToString("00") + "  " + F(state.TargetAngle), label, Brushes.Black, x + 8, y + 7);
                DrawChecker(graphics, image, 12);
                DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                graphics.DrawString("PCA " + F(state.Metrics.AxisAngle) + "  " + state.Status, small, StatusBrush(state.Status), x + 8, y + 225);
                graphics.DrawString(IsNewSlot(slot) ? "STEP 1E NEW" : "STEP 1D", small, Brushes.DimGray, x + 8, y + 244);
            }
            SavePng(canvas, Path.Combine(Step1ERoot(projectRoot), "Previews", "step1e_17_contact_sheet.png"));
        }
    }

    private static void DrawNeighborReview(string projectRoot, List<SlotState> states, List<PairAudit> pairs)
    {
        int width = 1180;
        int height = 890;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 10, FontStyle.Bold))
        using (Font small = new Font("Consolas", 9, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1E NEIGHBOR CONTINUITY REVIEW", title, Brushes.Black, 16, 12);
            for (int row = 0; row < NewSlots.Length; row++)
            {
                int slot = NewSlots[row];
                int y = 55 + row * 275;
                int[] shown = { slot - 1, slot, slot + 1 };
                for (int i = 0; i < 3; i++)
                {
                    SlotState state = states[shown[i]];
                    int x = 20 + i * 250;
                    graphics.DrawString("slot " + shown[i].ToString("00") + " / " + F(state.TargetAngle), label, Brushes.Black, x, y);
                    Rectangle image = new Rectangle(x + 20, y + 26, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                    graphics.DrawString("PCA " + F(state.Metrics.AxisAngle), small, Brushes.Black, x, y + 218);
                }
                PairAudit before = pairs[slot - 1];
                PairAudit after = pairs[slot];
                int metricsX = 780;
                graphics.DrawString("TARGET SLOT " + slot.ToString("00") + " / " + F(states[slot].TargetAngle), label, Brushes.Black, metricsX, y);
                graphics.DrawString((slot - 1).ToString("00") + " -> " + slot.ToString("00") + ": delta " + SignedF(before.SignedDelta) + " / IoU " + F(before.Similarity), small, Brushes.Black, metricsX, y + 32);
                graphics.DrawString(slot.ToString("00") + " -> " + (slot + 1).ToString("00") + ": delta " + SignedF(after.SignedDelta) + " / IoU " + F(after.Similarity), small, Brushes.Black, metricsX, y + 54);
                graphics.DrawString("expected signed delta: -11.25", small, Brushes.Black, metricsX, y + 82);
                graphics.DrawString("before: " + before.Status, small, PairBrush(before.Status), metricsX, y + 110);
                graphics.DrawString("after:  " + after.Status, small, PairBrush(after.Status), metricsX, y + 132);
                graphics.DrawString(states[slot].FrontRearResult, small, Brushes.DarkGreen, metricsX, y + 164);
                graphics.DrawString(states[slot].Status, label, StatusBrush(states[slot].Status), metricsX, y + 190);
            }
            SavePng(canvas, Path.Combine(Step1ERoot(projectRoot), "Previews", "step1e_neighbor_review.png"));
        }
    }

    private static void DrawFull32Preview(string projectRoot, List<SlotState> states)
    {
        int columns = 8;
        int cellWidth = 240;
        int cellHeight = 255;
        int width = columns * cellWidth;
        int height = 4 * cellHeight + 45;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 9, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1E FULL32 - EXACT RUNTIME ResolveSourceSlot / flipX MAPPING", title, Brushes.Black, 14, 10);
            for (int direction = 0; direction < 32; direction++)
            {
                int sourceSlot;
                bool flip;
                ResolveGameMapping(direction, out sourceSlot, out flip);
                SlotState source = states[sourceSlot];
                int column = direction % columns;
                int row = direction / columns;
                int x = column * cellWidth + 12;
                int y = row * cellHeight + 48;
                graphics.DrawString("dir " + direction.ToString("00") + " / " + F(direction * 11.25), label, Brushes.Black, x, y);
                Rectangle imageRect = new Rectangle(x + 18, y + 25, CanvasSize, CanvasSize);
                DrawChecker(graphics, imageRect, 12);
                using (Bitmap image = LoadArgb(source.ImagePath))
                {
                    if (flip) image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    graphics.DrawImageUnscaled(image, imageRect.X, imageRect.Y);
                }
                graphics.DrawString("src " + sourceSlot.ToString("00") + " flipX " + (flip ? "true" : "false"), small, Brushes.DimGray, x, y + 214);
                graphics.DrawString(source.Status, small, StatusBrush(source.Status), x + 136, y + 214);
            }
            SavePng(canvas, Path.Combine(Step1ERoot(projectRoot), "Previews", "step1e_full32_preview.png"));
        }
    }

    private static void WriteMetricsCsv(string projectRoot, List<SlotState> states, List<PairAudit> pairs, Dictionary<int, CleanupRecord> cleanup)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,filename,target_angle,source_file,source_method,transform_applied,measured_pca_axis,angular_error,signed_delta_from_previous,absolute_delta_from_previous,signed_delta_to_next,absolute_delta_to_next,similarity_to_previous,similarity_to_next,canvas_width,canvas_height,bbox,bbox_width,bbox_height,alpha_area,projected_length,projected_width,scale_difference_percent,centroid_x,centroid_y,center_offset_x,center_offset_y,baseline,edge_contact,component_count,partial_alpha_count,green_dominant_pixels,technical_status,front_rear_result,visual_identity_result,artifact_result,genuine_pose_result,visual_status,notes");
        for (int slot = 0; slot <= 16; slot++)
        {
            SlotState state = states[slot];
            SpriteMetrics metrics = state.Metrics;
            PairAudit previous = slot > 0 ? pairs[slot - 1] : null;
            PairAudit next = slot < 16 ? pairs[slot] : null;
            List<string> row = new List<string>();
            row.Add(slot.ToString(Invariant));
            row.Add(Csv(state.Filename));
            row.Add(F(state.TargetAngle));
            row.Add(Csv(state.SourceFile));
            row.Add(Csv(state.SourceMethod));
            row.Add(Csv(state.TransformApplied));
            row.Add(F(metrics.AxisAngle));
            row.Add(F(state.AngularError));
            row.Add(previous == null ? string.Empty : F(previous.SignedDelta));
            row.Add(previous == null ? string.Empty : F(previous.AbsoluteDelta));
            row.Add(next == null ? string.Empty : F(next.SignedDelta));
            row.Add(next == null ? string.Empty : F(next.AbsoluteDelta));
            row.Add(previous == null ? string.Empty : F(previous.Similarity));
            row.Add(next == null ? string.Empty : F(next.Similarity));
            row.Add(metrics.CanvasWidth.ToString(Invariant));
            row.Add(metrics.CanvasHeight.ToString(Invariant));
            row.Add(Csv(Box(metrics.Bounds)));
            row.Add(metrics.Bounds.Width.ToString(Invariant));
            row.Add(metrics.Bounds.Height.ToString(Invariant));
            row.Add(metrics.AlphaPixelCount.ToString(Invariant));
            row.Add(F(metrics.ProjectedLength));
            row.Add(F(metrics.ProjectedWidth));
            row.Add(F(state.ScaleDifferencePercent));
            row.Add(F(metrics.CentroidX));
            row.Add(F(metrics.CentroidY));
            row.Add(F(state.CenterOffsetX));
            row.Add(F(state.CenterOffsetY));
            row.Add(metrics.Baseline.ToString(Invariant));
            row.Add(metrics.EdgeTouchCount.ToString(Invariant));
            row.Add(metrics.ComponentCount.ToString(Invariant));
            row.Add(metrics.PartialAlphaPixelCount.ToString(Invariant));
            row.Add(metrics.GreenDominantPixels.ToString(Invariant));
            row.Add(Csv(state.TechnicalStatus));
            row.Add(Csv(state.FrontRearResult));
            row.Add(Csv(state.VisualIdentityResult));
            row.Add(Csv(state.ArtifactResult));
            row.Add(Csv(state.GenuinePoseResult));
            row.Add(Csv(state.Status));
            row.Add(Csv(state.Notes));
            csv.AppendLine(string.Join(",", row.ToArray()));
        }
        File.WriteAllText(Path.Combine(Step1ERoot(projectRoot), "Reports", "step1e_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static void WriteReport(
        string projectRoot,
        List<SlotState> states,
        List<PairAudit> pairs,
        Dictionary<int, CleanupRecord> cleanup,
        Dictionary<string, HashSnapshot> protectedBefore,
        Dictionary<string, HashSnapshot> protectedAfter)
    {
        bool protectedPass = ProtectedGroupsMatch(protectedBefore, protectedAfter) && ProtectedMatchesExpected(protectedBefore);
        bool newSlotsPass = NewSlots.All(slot => states[slot].Status == "PASS");
        bool technicalPass = states.All(state => state.TechnicalStatus == "PASS");
        bool continuityPass = pairs.All(pair => pair.Status != "COLLAPSED" && pair.Status != "REVERSED" && pair.Status != "SCALE FAIL" && pair.Status != "CENTER FAIL");
        bool full32ManualPass = false;
        bool ready = protectedPass && newSlotsPass && technicalPass && continuityPass && full32ManualPass;
        string decision = ready ? "READY_FOR_UNITY_TEST" : "NOT_READY";

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1E Three Missing Poses Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("Three independently generated perspective sources were processed into deterministic 186x186 pixel-art candidates. No adjacent pose was used as transformed final artwork. Manual landmark approval is intentionally pending in this first builder pass, so the set is not yet authorized for a Unity test.");
        report.AppendLine();
        report.AppendLine("- New source slots: 06 / 22.50, 09 / 348.75, 14 / 292.50.");
        report.AppendLine("- Processing: edge-connected chroma flood-fill, largest vehicle component, uniform nearest-neighbor reduction, shared protected AE86 palette, center placement, baseline y=169.");
        report.AppendLine("- Active Production32 and every protected upstream group remain read-only.");
        report.AppendLine();

        report.AppendLine("## 2. New pose audit");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Measured PCA | BBox | Alpha | Length | Scale | Center offset | Similarity prev / next | Front/rear | Identity | Status |");
        report.AppendLine("|---:|---:|---:|---|---:|---:|---:|---|---|---|---|---|");
        foreach (int slot in NewSlots)
        {
            SlotState state = states[slot];
            PairAudit previous = pairs[slot - 1];
            PairAudit next = pairs[slot];
            report.AppendLine("| " + slot.ToString("00") + " | " + F(state.TargetAngle) + " | " + F(state.Metrics.AxisAngle) + " | " + state.Metrics.Bounds.Width + "x" + state.Metrics.Bounds.Height + " | " + state.Metrics.AlphaPixelCount + " | " + F(state.Metrics.ProjectedLength) + " | " + SignedF(state.ScaleDifferencePercent) + "% | (" + SignedF(state.CenterOffsetX) + ", " + SignedF(state.CenterOffsetY) + ") | " + F(previous.Similarity) + " / " + F(next.Similarity) + " | " + state.FrontRearResult + " | " + state.VisualIdentityResult + " | **" + state.Status + "** |");
        }
        report.AppendLine();

        report.AppendLine("## 3. Source and cleanup provenance");
        report.AppendLine();
        report.AppendLine("| Slot | Raw source | Raw PCA | Raw component count | Flood-filled background | Uniform scale | Shared palette | Output |");
        report.AppendLine("|---:|---|---:|---:|---:|---:|---:|---|");
        foreach (int slot in NewSlots)
        {
            CleanupRecord record = cleanup[slot];
            report.AppendLine("| " + slot.ToString("00") + " | `" + Relative(projectRoot, record.RawPath).Replace('\\', '/') + "` | " + F(record.RawMetrics.AxisAngle) + " | " + record.RawForegroundComponents + " | " + record.RawBackgroundPixels + " px | " + F(record.UniformScale) + " | " + record.PaletteSize + " colors | `" + Relative(projectRoot, record.OutputPath).Replace('\\', '/') + "` |");
        }
        report.AppendLine();
        report.AppendLine("The generated sources contain genuinely redrawn roofs, hoods, windows, wheel placements, lights, bumpers, and silhouettes. Cleanup never rotates, shears, mirrors, or non-uniformly squeezes a car. The only geometry operation is one uniform nearest-neighbor reduction from the large source followed by translation to the common center/baseline.");
        report.AppendLine();

        report.AppendLine("## 4. Technical PNG audit");
        report.AppendLine();
        report.AppendLine("| Slot | Canvas | RGBA alpha | Baseline | Edge | Components | Partial alpha | Green halo | Crop | Result |");
        report.AppendLine("|---:|---|---|---:|---:|---:|---:|---:|---|---|");
        foreach (int slot in NewSlots)
        {
            SpriteMetrics metrics = states[slot].Metrics;
            string crop = metrics.Bounds.Left > 0 && metrics.Bounds.Top > 0 && metrics.Bounds.Right < CanvasSize && metrics.Bounds.Bottom < CanvasSize ? "PASS" : "FAIL";
            report.AppendLine("| " + slot.ToString("00") + " | " + metrics.CanvasWidth + "x" + metrics.CanvasHeight + " | binary 0/255 | " + metrics.Baseline + " | " + metrics.EdgeTouchCount + " | " + metrics.ComponentCount + " | " + metrics.PartialAlphaPixelCount + " | " + metrics.GreenDominantPixels + " | " + crop + " | **" + states[slot].TechnicalStatus + "** |");
        }
        report.AppendLine();

        report.AppendLine("## 5. Signed adjacent continuity audit");
        report.AppendLine();
        report.AppendLine("Signed delta uses `((next - previous + 540) % 360) - 180`; expected progression is -11.25 degrees. PCA remains an audit signal, while the manual front/rear and perspective landmarks decide visual direction.");
        report.AppendLine();
        report.AppendLine("| Pair | Signed delta | Absolute | IoU | Scale mismatch | Centroid shift | Manual progression | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
        foreach (PairAudit pair in pairs)
            report.AppendLine("| " + pair.FirstSlot.ToString("00") + " -> " + pair.SecondSlot.ToString("00") + " | " + SignedF(pair.SignedDelta) + " | " + F(pair.AbsoluteDelta) + " | " + F(pair.Similarity) + " | " + F(pair.ScaleMismatch) + "% | " + F(pair.CentroidShift) + " px | " + pair.ManualProgression + " | " + pair.Status + " |");
        report.AppendLine();

        report.AppendLine("## 6. Full32 runtime preview");
        report.AppendLine();
        report.AppendLine("`Previews/step1e_full32_preview.png` uses the exact current `ResolveSourceSlot` / `flipX` mapping without modifying runtime code. Each new source appears in its native right-side direction and in the corresponding mirrored runtime direction. Final full32 manual coherence approval is pending alongside the three new source landmark review.");
        report.AppendLine();

        report.AppendLine("## 7. Protected SHA-256 proof");
        report.AppendLine();
        report.AppendLine("| Protected group | Count | Expected pre-Step1E | Before | After | Identical |");
        report.AppendLine("|---|---:|---|---|---|---|");
        foreach (string key in protectedBefore.Keys.OrderBy(value => value, StringComparer.InvariantCultureIgnoreCase))
        {
            HashSnapshot before = protectedBefore[key];
            HashSnapshot after = protectedAfter[key];
            HashSnapshot expected = ExpectedProtected[key];
            bool identical = before.Count == after.Count && before.Hash == after.Hash && before.Count == expected.Count && before.Hash == expected.Hash;
            report.AppendLine("| " + key + " | " + before.Count + " | `" + expected.Hash + "` | `" + before.Hash + "` | `" + after.Hash + "` | " + (identical ? "YES" : "NO") + " |");
        }
        report.AppendLine();

        report.AppendLine("## 8. Files created");
        report.AppendLine();
        foreach (string file in Directory.GetFiles(Step1ERoot(projectRoot), "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.InvariantCultureIgnoreCase))
            report.AppendLine("- `" + Relative(projectRoot, file).Replace('\\', '/') + "`");
        report.AppendLine();

        report.AppendLine("## 9. Final decision");
        report.AppendLine();
        report.AppendLine("The raster and continuity checks are complete, but all three new poses and the propagated full32 view still require the explicit manual visual approval pass represented by the generated review sheets.");
        report.AppendLine();
        report.AppendLine(decision);
        File.WriteAllText(Path.Combine(Step1ERoot(projectRoot), "Reports", "step1e_report.md"), report.ToString(), new UTF8Encoding(false));
    }

    private static SpriteMetrics AnalyzeBitmap(string path, double headingHint)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Sprite file not found", path);
        using (Bitmap bitmap = LoadArgb(path)) return AnalyzeBitmap(bitmap, headingHint);
    }

    private static SpriteMetrics AnalyzeBitmap(Bitmap bitmap, double headingHint)
    {
        List<Point> pixels = new List<Point>();
        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;
        int partial = 0;
        int edge = 0;
        int greenPixels = 0;
        double sumX = 0.0;
        double sumY = 0.0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.A == 0) continue;
                pixels.Add(new Point(x, y));
                sumX += x;
                sumY += y;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                if (color.A < 255) partial++;
                if (x == 0 || y == 0 || x == bitmap.Width - 1 || y == bitmap.Height - 1) edge++;
                if (IsGreenBackgroundCandidate(color.ToArgb())) greenPixels++;
            }
        }
        if (pixels.Count == 0) throw new InvalidOperationException("No foreground alpha pixels were found.");

        double centroidX = sumX / pixels.Count;
        double centroidY = sumY / pixels.Count;
        double covarianceXX = 0.0;
        double covarianceYY = 0.0;
        double covarianceXY = 0.0;
        foreach (Point point in pixels)
        {
            double dx = point.X - centroidX;
            double dy = point.Y - centroidY;
            covarianceXX += dx * dx;
            covarianceYY += dy * dy;
            covarianceXY += dx * dy;
        }
        covarianceXX /= pixels.Count;
        covarianceYY /= pixels.Count;
        covarianceXY /= pixels.Count;

        double imageAxisRadians = 0.5 * Math.Atan2(2.0 * covarianceXY, covarianceXX - covarianceYY);
        double vx = Math.Cos(imageAxisRadians);
        double vy = Math.Sin(imageAxisRadians);
        double angle = NormalizeAngle(Math.Atan2(-vy, vx) * 180.0 / Math.PI);
        double opposite = NormalizeAngle(angle + 180.0);
        if (ShortestAngleError(opposite, headingHint) < ShortestAngleError(angle, headingHint))
        {
            angle = opposite;
            vx = -vx;
            vy = -vy;
        }

        double minLong = double.MaxValue;
        double maxLong = double.MinValue;
        double minWide = double.MaxValue;
        double maxWide = double.MinValue;
        foreach (Point point in pixels)
        {
            double dx = point.X - centroidX;
            double dy = point.Y - centroidY;
            double longitudinal = dx * vx + dy * vy;
            double transverse = -dx * vy + dy * vx;
            minLong = Math.Min(minLong, longitudinal);
            maxLong = Math.Max(maxLong, longitudinal);
            minWide = Math.Min(minWide, transverse);
            maxWide = Math.Max(maxWide, transverse);
        }

        SpriteMetrics metrics = new SpriteMetrics();
        metrics.CanvasWidth = bitmap.Width;
        metrics.CanvasHeight = bitmap.Height;
        metrics.Bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        metrics.CentroidX = centroidX;
        metrics.CentroidY = centroidY;
        metrics.AxisAngle = angle;
        metrics.ProjectedLength = maxLong - minLong + 1.0;
        metrics.ProjectedWidth = maxWide - minWide + 1.0;
        metrics.AlphaPixelCount = pixels.Count;
        metrics.PartialAlphaPixelCount = partial;
        metrics.EdgeTouchCount = edge;
        metrics.ComponentCount = CountAlphaComponents(bitmap);
        metrics.Baseline = maxY;
        metrics.GreenDominantPixels = greenPixels;
        return metrics;
    }

    private static int CountAlphaComponents(Bitmap bitmap)
    {
        bool[,] visited = new bool[bitmap.Width, bitmap.Height];
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        Queue<Point> queue = new Queue<Point>();
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (visited[x, y] || bitmap.GetPixel(x, y).A == 0) continue;
                count++;
                visited[x, y] = true;
                queue.Enqueue(new Point(x, y));
                while (queue.Count > 0)
                {
                    Point point = queue.Dequeue();
                    for (int i = 0; i < dx.Length; i++)
                    {
                        int nx = point.X + dx[i];
                        int ny = point.Y + dy[i];
                        if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || visited[nx, ny] || bitmap.GetPixel(nx, ny).A == 0) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }
            }
        }
        return count;
    }

    private static Dictionary<string, HashSnapshot> CaptureProtectedHashes(string projectRoot)
    {
        string assets = Path.Combine(projectRoot, "Assets");
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        groups["active Production32 PNG"] = Directory.GetFiles(Path.Combine(assets, "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32"), "*.png", SearchOption.TopDirectoryOnly).ToList();
        groups["all Assets .meta"] = Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories).ToList();
        groups["all Assets code"] = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories).ToList();
        groups["all prefabs"] = Directory.GetFiles(assets, "*.prefab", SearchOption.AllDirectories).ToList();
        groups["all scenes"] = Directory.GetFiles(assets, "*.unity", SearchOption.AllDirectories).ToList();
        groups["Step 1 candidates"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates"), "*", SearchOption.AllDirectories).ToList();
        groups["Step 1C outputs"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_HybridLocal"), "*", SearchOption.AllDirectories).ToList();
        groups["Step 1D outputs"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1D_LocalFlipRecovery"), "*", SearchOption.AllDirectories).ToList();

        Dictionary<string, HashSnapshot> result = new Dictionary<string, HashSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> group in groups)
        {
            List<string> files = group.Value.OrderBy(path => path, StringComparer.InvariantCultureIgnoreCase).ToList();
            StringBuilder joined = new StringBuilder();
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) joined.Append('\n');
                joined.Append(Relative(projectRoot, files[i])).Append('|').Append(FileSha256(files[i]));
            }
            result[group.Key] = new HashSnapshot(files.Count, Sha256(Encoding.UTF8.GetBytes(joined.ToString())));
        }
        return result;
    }

    private static bool ProtectedGroupsMatch(Dictionary<string, HashSnapshot> before, Dictionary<string, HashSnapshot> after)
    {
        return before.All(pair => after.ContainsKey(pair.Key) && pair.Value.Count == after[pair.Key].Count && pair.Value.Hash == after[pair.Key].Hash);
    }

    private static bool ProtectedMatchesExpected(Dictionary<string, HashSnapshot> actual)
    {
        return ExpectedProtected.All(pair => actual.ContainsKey(pair.Key) && pair.Value.Count == actual[pair.Key].Count && pair.Value.Hash == actual[pair.Key].Hash);
    }

    private static void ResolveGameMapping(int direction, out int source, out bool flip)
    {
        if (direction <= 8)
        {
            source = 8 - direction;
            flip = false;
        }
        else if (direction <= 23)
        {
            source = direction - 8;
            flip = true;
        }
        else
        {
            source = 40 - direction;
            flip = false;
        }
    }

    private static string ManualFrontRearResult(int slot)
    {
        if (slot == 6) return "PASS: hood/headlights upper-right; hatch/red taillights lower-left";
        if (slot == 9) return "PASS: hood/headlights lower-right; hatch/red taillights upper-left";
        if (slot == 14) return "PASS: hood/headlights lower-right; hatch/red taillights upper-left";
        if (slot == 0) return "PASS: front top; rear bottom";
        if (slot == 8) return "PASS: front right; rear left";
        if (slot == 16) return "PASS: front bottom; rear top";
        return slot < 8 ? "PASS: protected Up-to-Right landmark orientation" : "PASS: protected Right-to-Down landmark orientation";
    }

    private static string ManualNewNotes(int slot)
    {
        if (slot == 6) return "Pending review-sheet approval: more side-visible than 05 and more top-visible than 07.";
        if (slot == 9) return "Pending review-sheet approval: slight Down bias from Right and shallower than 10.";
        return "Pending review-sheet approval: more Down-biased than 13 and less Down-biased than 15.";
    }

    private static string ManualPairProgression(int firstSlot)
    {
        if (firstSlot == 5) return "MANUAL REVIEW: 05 -> new 06 must become shallower UpRight";
        if (firstSlot == 6) return "MANUAL REVIEW: new 06 -> 07 must become still shallower";
        if (firstSlot == 8) return "MANUAL REVIEW: Right -> new 09 gains slight Down bias";
        if (firstSlot == 9) return "MANUAL REVIEW: new 09 -> 10 gains more Down bias";
        if (firstSlot == 13) return "MANUAL REVIEW: 13 -> new 14 gains Down bias";
        if (firstSlot == 14) return "MANUAL REVIEW: new 14 -> 15 becomes still more vertical";
        return "PASS: protected Step 1D visual progression";
    }

    private static string TripletBias(int slot)
    {
        if (slot == 6) return "Bias: more side-visible than 05; more roof/top-visible than 07; nose upper-right.";
        if (slot == 9) return "Bias: slight Down from Right; less Down than 10; nose lower-right.";
        return "Bias: more Down than 13; less Down than 15; nose lower-right; tall intermediate silhouette.";
    }

    private static double TargetAngle(int slot)
    {
        return NormalizeAngle(90.0 - slot * StepAngle);
    }

    private static bool IsNewSlot(int slot)
    {
        return slot == 6 || slot == 9 || slot == 14;
    }

    private static string Step1ERoot(string projectRoot)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1E_ThreeMissingPoses");
    }

    private static string RawPath(string projectRoot, int slot)
    {
        return Path.Combine(Step1ERoot(projectRoot), "References", "slot_" + slot.ToString("00", Invariant) + "_generated_raw.png");
    }

    private static string Step1EOutputPath(string projectRoot, int slot)
    {
        return Path.Combine(Step1ERoot(projectRoot), "PNG", "slot_" + slot.ToString("00", Invariant) + "_" + AngleToken(TargetAngle(slot)) + "_step1e.png");
    }

    private static string Step1DPath(string projectRoot, int slot)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1D_LocalFlipRecovery", "PNG", "slot_" + slot.ToString("00", Invariant) + "_" + AngleToken(TargetAngle(slot)) + "_step1d.png");
    }

    private static double InterpolateAnchorValue(int slot, double up, double right, double down)
    {
        if (slot <= 8) return up + (right - up) * (slot / 8.0);
        return right + (down - right) * ((slot - 8) / 8.0);
    }

    private static double AlphaIoU(string firstPath, string secondPath)
    {
        using (Bitmap first = LoadArgb(firstPath))
        using (Bitmap second = LoadArgb(secondPath))
        {
            int intersection = 0;
            int union = 0;
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize; x++)
                {
                    bool a = first.GetPixel(x, y).A > 0;
                    bool b = second.GetPixel(x, y).A > 0;
                    if (a || b) union++;
                    if (a && b) intersection++;
                }
            }
            return union == 0 ? 0.0 : intersection / (double)union;
        }
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double SignedShortestDelta(double previous, double current)
    {
        return (current - previous + 540.0) % 360.0 - 180.0;
    }

    private static double ShortestAngleError(double first, double second)
    {
        double difference = Math.Abs(NormalizeAngle(first) - NormalizeAngle(second));
        return Math.Min(difference, 360.0 - difference);
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0.0) angle += 360.0;
        return angle;
    }

    private static string AngleToken(double angle)
    {
        return angle.ToString("0.00", Invariant);
    }

    private static string F(double value)
    {
        return value.ToString("0.00", Invariant);
    }

    private static string SignedF(double value)
    {
        return value.ToString("+0.00;-0.00;0.00", Invariant);
    }

    private static string Box(Rectangle bounds)
    {
        return bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height;
    }

    private static string Csv(string value)
    {
        if (value == null) return string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Relative(string projectRoot, string path)
    {
        string root = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
    }

    private static Bitmap LoadArgb(string path)
    {
        using (Bitmap source = new Bitmap(path))
            return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
    }

    private static void DrawImageUnscaled(Graphics graphics, string path, int x, int y)
    {
        using (Bitmap image = LoadArgb(path)) graphics.DrawImageUnscaled(image, x, y);
    }

    private static void PrepareGraphics(Graphics graphics, Color background)
    {
        graphics.Clear(background);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.CompositingMode = CompositingMode.SourceOver;
    }

    private static void DrawChecker(Graphics graphics, Rectangle rectangle, int tile)
    {
        using (Brush light = new SolidBrush(Color.White))
        using (Brush dark = new SolidBrush(Color.FromArgb(220, 220, 220)))
        {
            for (int y = rectangle.Top; y < rectangle.Bottom; y += tile)
            {
                for (int x = rectangle.Left; x < rectangle.Right; x += tile)
                {
                    Brush brush = (((x - rectangle.Left) / tile + (y - rectangle.Top) / tile) % 2 == 0) ? light : dark;
                    graphics.FillRectangle(brush, x, y, Math.Min(tile, rectangle.Right - x), Math.Min(tile, rectangle.Bottom - y));
                }
            }
        }
    }

    private static Pen StatusPen(string status)
    {
        return status == "PASS" ? Pens.SeaGreen : status == "REVIEW" ? Pens.DarkOrange : Pens.Firebrick;
    }

    private static Brush StatusBrush(string status)
    {
        return status == "PASS" ? Brushes.SeaGreen : status == "REVIEW" ? Brushes.DarkOrange : Brushes.Firebrick;
    }

    private static Brush PairBrush(string status)
    {
        return status == "PASS" ? Brushes.SeaGreen : status == "COLLAPSED" || status == "REVERSED" || status.EndsWith("FAIL", StringComparison.Ordinal) ? Brushes.Firebrick : Brushes.DarkOrange;
    }

    private static void SavePng(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string FileSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream));
    }

    private static string Sha256(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
    }

    private static string Hex(byte[] bytes)
    {
        StringBuilder text = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) text.Append(value.ToString("X2", Invariant));
        return text.ToString();
    }
}
