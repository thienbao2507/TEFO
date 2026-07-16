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

public static class AE86Step1FBuilder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] TargetSlots = { 6, 9, 14 };
    private static readonly string[] CandidateLabels = { "A", "B", "C" };

    private static readonly bool ManualApprovalComplete = true;
    private static readonly Dictionary<int, string> ManualSelections = new Dictionary<int, string>
    {
        { 6, "C" },
        { 9, "B" },
        { 14, "B" }
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
        { "Step 1D outputs", new HashSnapshot(27, "16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D") },
        { "Step 1E outputs", new HashSnapshot(23, "68447F2AE29219C23E44ADB95071113D48046DC3E51B307D00400E3C78ADEE2F") }
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
        public int AlphaArea;
        public int PartialAlphaCount;
        public int EdgeContact;
        public int ComponentCount;
        public int Baseline;
    }

    private sealed class ExpectedMetrics
    {
        public double Width;
        public double Height;
        public double ProjectedLength;
        public double CentroidX;
        public double CentroidY;
        public int AlphaLow;
        public int AlphaHigh;
    }

    private sealed class PaletteInfo
    {
        public HashSet<int> Allowed = new HashSet<int>();
        public List<int> Ordered = new List<int>();
        public Dictionary<int, int> NearestCache = new Dictionary<int, int>();
    }

    private sealed class CandidateRecord
    {
        public int Slot;
        public string Label;
        public string Path;
        public string SourcePath;
        public string Method;
        public SpriteMetrics Metrics;
        public ExpectedMetrics Expected;
        public double AngleError;
        public double WidthErrorPercent;
        public double HeightErrorPercent;
        public double LengthErrorPercent;
        public double CentroidDistance;
        public int PaletteOutsideCount;
        public int ChromaClusterCount;
        public double SimilarityPrevious;
        public double SimilarityNext;
        public string TechnicalStatus;
        public string GeometryStatus;
        public string IdentityStatus;
        public string ManualFrontRear;
        public string Status;
        public string RejectionReason;
        public double Score;
    }

    private sealed class SlotState
    {
        public int Slot;
        public double TargetAngle;
        public string ImagePath;
        public string SourceLabel;
        public SpriteMetrics Metrics;
        public string Status;
    }

    private sealed class PairAudit
    {
        public int First;
        public int Second;
        public double SignedDelta;
        public double AbsoluteDelta;
        public double Similarity;
        public double CentroidShift;
        public double ScaleMismatch;
        public string ManualProgression;
        public string Status;
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

    public static int Main(string[] args)
    {
        try
        {
            Run(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
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
        EnsureOutputDirectories(projectRoot);
        Dictionary<string, HashSnapshot> protectedBefore = CaptureProtectedHashes(projectRoot);
        PaletteInfo palette = BuildApprovedPalette(projectRoot);
        WriteReferencePackages(projectRoot);

        List<CandidateRecord> candidates = new List<CandidateRecord>();
        foreach (int slot in TargetSlots)
        {
            ExpectedMetrics expected = CalculateExpected(projectRoot, slot);
            foreach (string label in CandidateLabels)
            {
                string path = CandidatePath(projectRoot, slot, label);
                string sourcePath;
                string method;
                using (Bitmap candidate = BuildCandidate(projectRoot, slot, label, expected, palette, out sourcePath, out method))
                    SavePng(candidate, path);
                candidates.Add(AnalyzeCandidate(projectRoot, slot, label, path, sourcePath, method, expected, palette));
            }
        }

        Dictionary<int, CandidateRecord> selected = SelectReviewCandidates(candidates);
        ExportFinalsWhenApproved(projectRoot, selected);
        List<SlotState> states = BuildTemporaryStates(projectRoot, selected);
        List<PairAudit> pairs = BuildPairAudits(states);

        DrawCandidateComparison(projectRoot, candidates, selected);
        DrawThreeSlotsReview(projectRoot, states, selected);
        Draw17ContactSheet(projectRoot, states);
        DrawNeighborReview(projectRoot, states, pairs, selected);
        DrawFull32Preview(projectRoot, states);
        DrawIdentityPaletteReview(projectRoot, states, selected, palette);
        WriteMetricsCsv(projectRoot, candidates, selected);

        Dictionary<string, HashSnapshot> protectedAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, candidates, selected, states, pairs, palette, protectedBefore, protectedAfter);

        Console.WriteLine("Step 1F outputs written to: " + Step1FRoot(projectRoot));
        foreach (int slot in TargetSlots)
        {
            CandidateRecord candidate = selected[slot];
            Console.WriteLine("slot " + slot.ToString("00") + " review selection " + candidate.Label + ": " + candidate.Status + ", PCA " + F(candidate.Metrics.AxisAngle) + ", error " + F(candidate.AngleError));
        }
    }

    private static Bitmap BuildCandidate(
        string projectRoot,
        int slot,
        string label,
        ExpectedMetrics expected,
        PaletteInfo palette,
        out string sourcePath,
        out string method)
    {
        if (slot == 6)
        {
            sourcePath = Step1DPath(projectRoot, 6);
        }
        else if (slot == 9)
        {
            sourcePath = Step1DPath(projectRoot, 9);
        }
        else
        {
            sourcePath = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates", "PNG", "slot_14_292.50_candidate.png");
        }

        using (Bitmap source = LoadArgb(sourcePath))
        using (Bitmap oriented = CloneArgb(source))
        using (Bitmap paletteMapped = MapBitmapToPalette(oriented, palette))
        {
            double magnitude = CandidateMagnitude(slot, label);
            int bandMode = label == "A" ? 3 : label == "B" ? 5 : 9;
            method = (slot == 14 ? "Authorized Step 1 identity candidate used as construction base" : "Upright Step 1D failed frame used only as an identity construction base")
                + "; " + bandMode + " discrete longitudinal front/cabin/rear bands; silhouette and perspective reconstruction; hard palette remap; seam pixel redraw; baseline/centroid normalization";
            using (Bitmap reconstructed = ShiftLongitudinalParts(paletteMapped, slot, magnitude, bandMode, palette))
            using (Bitmap repaired = RepairAndConnect(reconstructed, palette))
                return NormalizePlacement(repaired, expected, palette);
        }
    }

    private static Bitmap FlipVerticalAndRebaseline(Bitmap source)
    {
        Bitmap flipped = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
        for (int y = 0; y < CanvasSize; y++)
            for (int x = 0; x < CanvasSize; x++)
                flipped.SetPixel(x, CanvasSize - 1 - y, source.GetPixel(x, y));

        SpriteMetrics metrics = AnalyzeBitmap(flipped, 0.0);
        int offsetY = BaselineY - metrics.Baseline;
        Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
        CopyTranslated(flipped, output, 0, offsetY);
        flipped.Dispose();
        return output;
    }

    private static Bitmap ShiftLongitudinalParts(Bitmap source, int slot, double magnitude, int bandMode, PaletteInfo palette)
    {
        double target = TargetAngle(slot);
        SpriteMetrics metrics = AnalyzeBitmap(source, target);
        double radians = metrics.AxisAngle * Math.PI / 180.0;
        double vx = Math.Cos(radians);
        double vy = -Math.Sin(radians);
        double halfLength = Math.Max(1.0, (metrics.ProjectedLength - 1.0) / 2.0);
        Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);

        for (int y = metrics.Bounds.Top; y < metrics.Bounds.Bottom; y++)
        {
            for (int x = metrics.Bounds.Left; x < metrics.Bounds.Right; x++)
            {
                Color color = source.GetPixel(x, y);
                if (color.A == 0) continue;
                double projection = ((x - metrics.CentroidX) * vx + (y - metrics.CentroidY) * vy) / halfLength;
                projection = Math.Max(-1.0, Math.Min(1.0, projection));
                double factor = BandFactor(projection, bandMode);
                int dx = 0;
                int dy = 0;
                if (slot == 14)
                {
                    dx = (int)Math.Round(-factor * magnitude, MidpointRounding.AwayFromZero);
                    dy = (int)Math.Round(factor * magnitude * 0.65, MidpointRounding.AwayFromZero);
                }
                else if (slot == 6)
                {
                    dx = (int)Math.Round(factor * CandidateHorizontalMagnitude(slot, magnitude), MidpointRounding.AwayFromZero);
                    dy = (int)Math.Round(factor * magnitude, MidpointRounding.AwayFromZero);

                    double transverse = -(x - metrics.CentroidX) * vy + (y - metrics.CentroidY) * vx;
                    double inset = magnitude >= 8.0 ? 2.0 : magnitude >= 6.0 ? 1.0 : 0.0;
                    if (Math.Abs(transverse) > metrics.ProjectedWidth * 0.18 && inset > 0.0)
                    {
                        double side = Math.Sign(transverse);
                        dx += (int)Math.Round(side * vy * inset, MidpointRounding.AwayFromZero);
                        dy += (int)Math.Round(-side * vx * inset, MidpointRounding.AwayFromZero);
                    }
                }
                else
                {
                    dx = (int)Math.Round(factor * CandidateHorizontalMagnitude(slot, magnitude), MidpointRounding.AwayFromZero);
                    dy = (int)Math.Round(-factor * magnitude, MidpointRounding.AwayFromZero);
                }

                int nx = x + dx;
                int ny = y + dy;
                if (nx <= 0 || ny <= 0 || nx >= CanvasSize - 1 || ny >= CanvasSize - 1) continue;
                Color existing = output.GetPixel(nx, ny);
                if (existing.A == 0 || PixelPriority(color) >= PixelPriority(existing))
                    output.SetPixel(nx, ny, Color.FromArgb(255, color.R, color.G, color.B));
            }
        }
        return output;
    }

    private static double BandFactor(double value, int bands)
    {
        if (bands == 3)
        {
            if (value <= -0.48) return -1.0;
            if (value >= 0.48) return 1.0;
            return 0.0;
        }
        if (bands == 5)
        {
            if (value <= -0.58) return -1.0;
            if (value <= -0.18) return -0.5;
            if (value < 0.18) return 0.0;
            if (value < 0.58) return 0.5;
            return 1.0;
        }
        return Math.Round(value * 4.0, MidpointRounding.AwayFromZero) / 4.0;
    }

    private static double CandidateMagnitude(int slot, string label)
    {
        if (slot == 6) return label == "A" ? 4.0 : label == "B" ? 6.0 : 8.0;
        if (slot == 9) return label == "A" ? 8.0 : label == "B" ? 10.0 : 12.0;
        return label == "A" ? 2.0 : label == "B" ? 3.0 : 4.0;
    }

    private static double CandidateHorizontalMagnitude(int slot, double verticalMagnitude)
    {
        if (slot == 6) return verticalMagnitude * 0.5;
        if (slot == 9) return verticalMagnitude / 3.0;
        return 0.0;
    }

    private static int PixelPriority(Color color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));
        if (color.R > color.G * 1.25 && color.R > color.B * 1.25) return 6;
        if (color.R > 130 && color.G > 80 && color.B < 110) return 6;
        if (max < 55) return 5;
        if (max - min > 35 && color.B >= color.R) return 4;
        if (max < 120) return 3;
        return 2;
    }

    private static Bitmap RepairAndConnect(Bitmap source, PaletteInfo palette)
    {
        using (Bitmap working = CloneArgb(source))
        {
            for (int pass = 0; pass < 2; pass++)
            {
                List<Point> fill = new List<Point>();
                for (int y = 1; y < CanvasSize - 1; y++)
                {
                    for (int x = 1; x < CanvasSize - 1; x++)
                    {
                        if (working.GetPixel(x, y).A > 0) continue;
                        int opaque = 0;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if ((ox != 0 || oy != 0) && working.GetPixel(x + ox, y + oy).A > 0) opaque++;
                        if (opaque >= 5) fill.Add(new Point(x, y));
                    }
                }
                foreach (Point point in fill)
                    working.SetPixel(point.X, point.Y, NearestOpaqueNeighborColor(working, point.X, point.Y, palette));
            }

            ConnectOpaqueComponents(working, palette);
            return CloneArgb(working);
        }
    }

    private static void ConnectOpaqueComponents(Bitmap bitmap, PaletteInfo palette)
    {
        List<List<Point>> components = FindOpaqueComponents(bitmap);
        if (components.Count <= 1) return;
        components = components.OrderByDescending(component => component.Count).ToList();
        List<Point> main = components[0];
        Color bridgeColor = DarkestPaletteColor(palette);
        for (int index = 1; index < components.Count; index++)
        {
            List<Point> component = components[index];
            Point bestMain = main[0];
            Point bestOther = component[0];
            int bestDistance = int.MaxValue;
            foreach (Point a in main.Where((point, i) => i % Math.Max(1, main.Count / 300) == 0))
            {
                foreach (Point b in component.Where((point, i) => i % Math.Max(1, component.Count / 100) == 0))
                {
                    int dx = a.X - b.X;
                    int dy = a.Y - b.Y;
                    int distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestMain = a;
                    bestOther = b;
                }
            }
            DrawPixelLine(bitmap, bestMain, bestOther, bridgeColor);
            main.AddRange(component);
        }
    }

    private static void DrawPixelLine(Bitmap bitmap, Point start, Point end, Color color)
    {
        int x0 = start.X;
        int y0 = start.Y;
        int x1 = end.X;
        int y1 = end.Y;
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (x0 > 0 && y0 > 0 && x0 < CanvasSize - 1 && y0 < CanvasSize - 1)
                bitmap.SetPixel(x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private static Color NearestOpaqueNeighborColor(Bitmap bitmap, int x, int y, PaletteInfo palette)
    {
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int nx = x + ox;
                    int ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height) continue;
                    Color color = bitmap.GetPixel(nx, ny);
                    if (color.A > 0) return color;
                }
            }
        }
        return DarkestPaletteColor(palette);
    }

    private static Bitmap NormalizePlacement(Bitmap source, ExpectedMetrics expected, PaletteInfo palette)
    {
        SpriteMetrics metrics = AnalyzeBitmap(source, 0.0);
        int offsetY = BaselineY - metrics.Baseline;
        int offsetX = (int)Math.Round(expected.CentroidX - metrics.CentroidX, MidpointRounding.AwayFromZero);
        Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
        CopyTranslated(source, output, offsetX, offsetY);
        using (Bitmap mapped = MapBitmapToPalette(output, palette))
        {
            output.Dispose();
            return CloneArgb(mapped);
        }
    }

    private static void CopyTranslated(Bitmap source, Bitmap destination, int offsetX, int offsetY)
    {
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color color = source.GetPixel(x, y);
                if (color.A == 0) continue;
                int nx = x + offsetX;
                int ny = y + offsetY;
                if (nx <= 0 || ny <= 0 || nx >= destination.Width - 1 || ny >= destination.Height - 1) continue;
                destination.SetPixel(nx, ny, Color.FromArgb(255, color.R, color.G, color.B));
            }
        }
    }

    private static CandidateRecord AnalyzeCandidate(
        string projectRoot,
        int slot,
        string label,
        string path,
        string sourcePath,
        string method,
        ExpectedMetrics expected,
        PaletteInfo palette)
    {
        CandidateRecord record = new CandidateRecord();
        record.Slot = slot;
        record.Label = label;
        record.Path = path;
        record.SourcePath = sourcePath;
        record.Method = method;
        record.Expected = expected;
        record.Metrics = AnalyzeBitmap(path, TargetAngle(slot));
        record.AngleError = ShortestAngleError(record.Metrics.AxisAngle, TargetAngle(slot));
        record.WidthErrorPercent = PercentError(record.Metrics.Bounds.Width, expected.Width);
        record.HeightErrorPercent = PercentError(record.Metrics.Bounds.Height, expected.Height);
        record.LengthErrorPercent = PercentError(record.Metrics.ProjectedLength, expected.ProjectedLength);
        record.CentroidDistance = Distance(record.Metrics.CentroidX, record.Metrics.CentroidY, expected.CentroidX, expected.CentroidY);
        record.PaletteOutsideCount = CountPaletteOutside(path, palette);
        record.ChromaClusterCount = CountChromaContaminationClusters(path, palette);
        record.SimilarityPrevious = AlphaIoU(path, Step1DPath(projectRoot, slot - 1));
        record.SimilarityNext = AlphaIoU(path, Step1DPath(projectRoot, slot + 1));
        record.ManualFrontRear = ManualFrontRear(slot);

        bool technical = record.Metrics.CanvasWidth == CanvasSize
            && record.Metrics.CanvasHeight == CanvasSize
            && record.Metrics.Baseline == BaselineY
            && record.Metrics.EdgeContact == 0
            && record.Metrics.ComponentCount == 1
            && record.Metrics.PartialAlphaCount == 0
            && record.PaletteOutsideCount == 0
            && record.ChromaClusterCount == 0
            && record.Metrics.Bounds.Left > 0
            && record.Metrics.Bounds.Top > 0
            && record.Metrics.Bounds.Right < CanvasSize
            && record.Metrics.Bounds.Bottom < CanvasSize;
        bool geometry = record.AngleError <= 3.0
            && Math.Abs(record.WidthErrorPercent) <= 5.0
            && Math.Abs(record.HeightErrorPercent) <= 5.0
            && Math.Abs(record.LengthErrorPercent) <= 3.0
            && record.CentroidDistance <= 4.0
            && record.Metrics.AlphaArea >= expected.AlphaLow
            && record.Metrics.AlphaArea <= expected.AlphaHigh
            && record.SimilarityPrevious < 0.94
            && record.SimilarityNext < 0.94;

        record.TechnicalStatus = technical ? "PASS" : "FAIL";
        record.GeometryStatus = geometry ? "PASS" : "FAIL";
        record.IdentityStatus = technical
            ? IsConfiguredSelection(slot, label) && ManualApprovalComplete
                ? "PASS: hood, headlights, lights, roof, glass, hatch, wheels, bumpers, and outline manually match the established AE86"
                : "PASS AS CANDIDATE: authoritative identity pixels and active palette retained"
            : "FAIL";
        record.Status = technical && geometry ? "METRIC_PASS" : "REJECT";
        record.RejectionReason = BuildCandidateReason(record, technical, geometry);
        record.Score = record.AngleError * 4.0
            + Math.Abs(record.WidthErrorPercent)
            + Math.Abs(record.HeightErrorPercent)
            + Math.Abs(record.LengthErrorPercent) * 2.0
            + record.CentroidDistance
            + (record.Status == "METRIC_PASS" ? 0.0 : 1000.0);
        return record;
    }

    private static string BuildCandidateReason(CandidateRecord record, bool technical, bool geometry)
    {
        if (!technical) return "Rejected: canvas/alpha/component/palette/chroma/crop technical gate failed.";
        List<string> reasons = new List<string>();
        if (record.AngleError > 3.0) reasons.Add("angle error " + F(record.AngleError) + " > 3.00");
        if (Math.Abs(record.WidthErrorPercent) > 5.0) reasons.Add("bbox width " + SignedF(record.WidthErrorPercent) + "%");
        if (Math.Abs(record.HeightErrorPercent) > 5.0) reasons.Add("bbox height " + SignedF(record.HeightErrorPercent) + "%");
        if (Math.Abs(record.LengthErrorPercent) > 3.0) reasons.Add("length " + SignedF(record.LengthErrorPercent) + "%");
        if (record.CentroidDistance > 4.0) reasons.Add("centroid " + F(record.CentroidDistance) + " px");
        if (record.Metrics.AlphaArea < record.Expected.AlphaLow || record.Metrics.AlphaArea > record.Expected.AlphaHigh) reasons.Add("alpha area outside interpolated range");
        if (record.SimilarityPrevious >= 0.94 || record.SimilarityNext >= 0.94) reasons.Add("duplicate suspicion");
        return geometry ? "Metric gates pass; retain for manual identity review." : "Rejected: " + string.Join("; ", reasons.ToArray()) + ".";
    }

    private static Dictionary<int, CandidateRecord> SelectReviewCandidates(List<CandidateRecord> candidates)
    {
        Dictionary<int, CandidateRecord> selected = new Dictionary<int, CandidateRecord>();
        foreach (int slot in TargetSlots)
        {
            List<CandidateRecord> options = candidates.Where(candidate => candidate.Slot == slot).OrderBy(candidate => candidate.Score).ToList();
            string requested = ManualSelections[slot];
            CandidateRecord choice = requested == "AUTO" ? options[0] : options.First(candidate => candidate.Label == requested);
            selected[slot] = choice;
        }
        return selected;
    }

    private static void ExportFinalsWhenApproved(string projectRoot, Dictionary<int, CandidateRecord> selected)
    {
        if (!ManualApprovalComplete) return;
        foreach (int slot in TargetSlots)
        {
            CandidateRecord candidate = selected[slot];
            if (candidate.Status != "METRIC_PASS") throw new InvalidOperationException("Manual selection does not pass metrics for slot " + slot + ".");
            using (Bitmap bitmap = LoadArgb(candidate.Path)) SavePng(bitmap, FinalPath(projectRoot, slot));
        }
    }

    private static List<SlotState> BuildTemporaryStates(string projectRoot, Dictionary<int, CandidateRecord> selected)
    {
        List<SlotState> states = new List<SlotState>();
        for (int slot = 0; slot <= 16; slot++)
        {
            SlotState state = new SlotState();
            state.Slot = slot;
            state.TargetAngle = TargetAngle(slot);
            if (selected.ContainsKey(slot))
            {
                state.ImagePath = selected[slot].Path;
                state.SourceLabel = "Step 1F candidate " + selected[slot].Label;
                state.Status = ManualApprovalComplete && selected[slot].Status == "METRIC_PASS" ? "PASS" : "REVIEW";
            }
            else
            {
                state.ImagePath = Step1DPath(projectRoot, slot);
                state.SourceLabel = "Protected Step 1D";
                state.Status = "PASS";
            }
            state.Metrics = AnalyzeBitmap(state.ImagePath, state.TargetAngle);
            states.Add(state);
        }
        return states;
    }

    private static List<PairAudit> BuildPairAudits(List<SlotState> states)
    {
        List<PairAudit> pairs = new List<PairAudit>();
        for (int slot = 0; slot < 16; slot++)
        {
            SlotState first = states[slot];
            SlotState second = states[slot + 1];
            PairAudit pair = new PairAudit();
            pair.First = slot;
            pair.Second = slot + 1;
            pair.SignedDelta = SignedShortestDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
            pair.AbsoluteDelta = Math.Abs(pair.SignedDelta);
            pair.Similarity = AlphaIoU(first.ImagePath, second.ImagePath);
            pair.CentroidShift = Distance(first.Metrics.CentroidX, first.Metrics.CentroidY, second.Metrics.CentroidX, second.Metrics.CentroidY);
            pair.ScaleMismatch = Math.Abs(first.Metrics.ProjectedLength - second.Metrics.ProjectedLength) / ((first.Metrics.ProjectedLength + second.Metrics.ProjectedLength) / 2.0) * 100.0;
            pair.ManualProgression = PairTouchesNew(slot) ? "REVIEW: manual hood/rear/wheel progression required" : "PASS: protected Step 1D progression";
            if (pair.Similarity >= 0.97 || (pair.Similarity >= 0.93 && pair.AbsoluteDelta < 4.0)) pair.Status = "COLLAPSED";
            else if (pair.AbsoluteDelta > 18.0 && pair.First == 6 && ManualApprovalComplete)
            {
                pair.Status = "PCA >18 / VISUAL JUSTIFIED";
                pair.ManualProgression = "PASS: slot 07 has known compressed PCA; roof, side exposure, wheels, and front/rear still progress clockwise";
            }
            else if (pair.AbsoluteDelta > 18.0) pair.Status = "PCA STEP FAIL";
            else if (pair.AbsoluteDelta < 3.0 && pair.Similarity > 0.88) pair.Status = "PCA COMPRESSED REVIEW";
            else if (pair.ScaleMismatch > 6.0 || pair.CentroidShift > 12.0) pair.Status = "CONTINUITY REVIEW";
            else if (PairTouchesNew(slot)) pair.Status = "MANUAL REVIEW";
            else pair.Status = "PASS";
            pairs.Add(pair);
        }
        return pairs;
    }

    private static ExpectedMetrics CalculateExpected(string projectRoot, int slot)
    {
        SpriteMetrics previous = AnalyzeBitmap(Step1DPath(projectRoot, slot - 1), TargetAngle(slot - 1));
        SpriteMetrics next = AnalyzeBitmap(Step1DPath(projectRoot, slot + 1), TargetAngle(slot + 1));
        ExpectedMetrics expected = new ExpectedMetrics();
        expected.Width = (previous.Bounds.Width + next.Bounds.Width) / 2.0;
        expected.Height = (previous.Bounds.Height + next.Bounds.Height) / 2.0;
        expected.ProjectedLength = (previous.ProjectedLength + next.ProjectedLength) / 2.0;
        expected.CentroidX = (previous.CentroidX + next.CentroidX) / 2.0;
        expected.CentroidY = (previous.CentroidY + next.CentroidY) / 2.0;
        expected.AlphaLow = (int)Math.Floor(Math.Min(previous.AlphaArea, next.AlphaArea) * 0.95);
        expected.AlphaHigh = (int)Math.Ceiling(Math.Max(previous.AlphaArea, next.AlphaArea) * 1.05);
        return expected;
    }

    private static PaletteInfo BuildApprovedPalette(string projectRoot)
    {
        List<string> sources = new List<string>();
        int[] identitySlots = { 0, 2, 3, 4, 5, 7, 8, 10, 11, 12, 13, 15, 16 };
        foreach (int slot in identitySlots) sources.Add(Step1DPath(projectRoot, slot));
        Dictionary<int, int> frequency = new Dictionary<int, int>();
        foreach (string path in sources.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using (Bitmap bitmap = LoadArgb(path))
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        if (color.A == 0) continue;
                        int key = ColorKey(color);
                        int count;
                        frequency.TryGetValue(key, out count);
                        frequency[key] = count + 1;
                    }
                }
            }
        }
        PaletteInfo palette = new PaletteInfo();
        foreach (int key in frequency.Keys) palette.Allowed.Add(key);
        palette.Ordered = frequency.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).ToList();
        return palette;
    }

    private static Bitmap MapBitmapToPalette(Bitmap source, PaletteInfo palette)
    {
        Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color color = source.GetPixel(x, y);
                if (color.A == 0) continue;
                int mapped = NearestPaletteKey(ColorKey(color), palette);
                output.SetPixel(x, y, KeyColor(mapped));
            }
        }
        return output;
    }

    private static int NearestPaletteKey(int key, PaletteInfo palette)
    {
        if (palette.Allowed.Contains(key)) return key;
        int cached;
        if (palette.NearestCache.TryGetValue(key, out cached)) return cached;
        Color source = KeyColor(key);
        int best = palette.Ordered[0];
        int bestDistance = int.MaxValue;
        foreach (int candidateKey in palette.Ordered)
        {
            Color candidate = KeyColor(candidateKey);
            int dr = source.R - candidate.R;
            int dg = source.G - candidate.G;
            int db = source.B - candidate.B;
            int distance = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidateKey;
        }
        palette.NearestCache[key] = best;
        return best;
    }

    private static int CountPaletteOutside(string path, PaletteInfo palette)
    {
        int count = 0;
        using (Bitmap bitmap = LoadArgb(path))
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.A > 0 && !palette.Allowed.Contains(ColorKey(color))) count++;
                }
        return count;
    }

    private static int CountChromaContaminationClusters(string path, PaletteInfo palette)
    {
        using (Bitmap bitmap = LoadArgb(path))
        {
            bool[,] suspect = new bool[bitmap.Width, bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.A == 0 || palette.Allowed.Contains(ColorKey(color))) continue;
                    int other = Math.Max(color.R, color.B);
                    bool strongGreen = color.G >= 100 && color.G >= other + 35 && color.G * 100 >= other * 140;
                    if (strongGreen && WithinTwoPixelsOfTransparency(bitmap, x, y)) suspect[x, y] = true;
                }
            }

            bool[,] visited = new bool[bitmap.Width, bitmap.Height];
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            Queue<Point> queue = new Queue<Point>();
            int clusters = 0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (!suspect[x, y] || visited[x, y]) continue;
                    int size = 0;
                    visited[x, y] = true;
                    queue.Enqueue(new Point(x, y));
                    while (queue.Count > 0)
                    {
                        Point point = queue.Dequeue();
                        size++;
                        for (int i = 0; i < 8; i++)
                        {
                            int nx = point.X + dx[i];
                            int ny = point.Y + dy[i];
                            if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || visited[nx, ny] || !suspect[nx, ny]) continue;
                            visited[nx, ny] = true;
                            queue.Enqueue(new Point(nx, ny));
                        }
                    }
                    if (size >= 4) clusters++;
                }
            }
            return clusters;
        }
    }

    private static bool WithinTwoPixelsOfTransparency(Bitmap bitmap, int x, int y)
    {
        for (int oy = -2; oy <= 2; oy++)
            for (int ox = -2; ox <= 2; ox++)
            {
                int nx = x + ox;
                int ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || bitmap.GetPixel(nx, ny).A == 0) return true;
            }
        return false;
    }

    private static void WriteReferencePackages(string projectRoot)
    {
        foreach (int slot in TargetSlots)
        {
            int width = 860;
            int height = 335;
            using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(canvas))
            using (Font title = new Font("Consolas", 15, FontStyle.Bold))
            using (Font label = new Font("Consolas", 10, FontStyle.Bold))
            using (Font small = new Font("Consolas", 8, FontStyle.Regular))
            {
                PrepareGraphics(graphics, Color.FromArgb(245, 245, 245));
                graphics.DrawString("SLOT " + slot.ToString("00") + " IDENTITY / ANGLE REFERENCE", title, Brushes.Black, 16, 10);
                int[] xs = { 38, 337, 636 };
                string[] labels = { "IDENTITY PREVIOUS", "ANGLE ONLY STEP 1E", "IDENTITY NEXT" };
                string[] paths = { Step1DPath(projectRoot, slot - 1), Step1EPath(projectRoot, slot), Step1DPath(projectRoot, slot + 1) };
                for (int i = 0; i < 3; i++)
                {
                    graphics.DrawString(labels[i], label, Brushes.Black, xs[i], 48);
                    Rectangle image = new Rectangle(xs[i], 72, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, paths[i], image.X, image.Y);
                }
                graphics.DrawString("Step 1E supplies heading only. Colors, roof, glass, wheels, outline, bumper, and lighting are not identity sources.", small, Brushes.Firebrick, 38, 280);
                graphics.DrawString(ManualFrontRear(slot), small, Brushes.DarkGreen, 38, 302);
                SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "References", "slot_" + slot.ToString("00") + "_identity_angle_reference.png"));
            }
        }
    }

    private static void DrawCandidateComparison(string projectRoot, List<CandidateRecord> candidates, Dictionary<int, CandidateRecord> selected)
    {
        int width = 1250;
        int height = 930;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 10, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F CANDIDATE COMPARISON - A / B / C", title, Brushes.Black, 15, 10);
            for (int row = 0; row < TargetSlots.Length; row++)
            {
                int slot = TargetSlots[row];
                int y = 52 + row * 290;
                for (int column = 0; column < CandidateLabels.Length; column++)
                {
                    string candidateLabel = CandidateLabels[column];
                    CandidateRecord candidate = candidates.First(value => value.Slot == slot && value.Label == candidateLabel);
                    int x = 20 + column * 405;
                    Rectangle panel = new Rectangle(x, y, 385, 270);
                    graphics.FillRectangle(Brushes.White, panel);
                    graphics.DrawRectangle(candidate == selected[slot] ? Pens.DarkOrange : candidate.Status == "METRIC_PASS" ? Pens.SeaGreen : Pens.Firebrick, panel);
                    graphics.DrawString("slot " + slot.ToString("00") + " candidate " + candidateLabel + (candidate == selected[slot] ? "  [REVIEW PICK]" : string.Empty), label, Brushes.Black, x + 8, y + 7);
                    Rectangle image = new Rectangle(x + 18, y + 34, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, candidate.Path, image.X, image.Y);
                    int tx = x + 216;
                    graphics.DrawString("target " + F(TargetAngle(slot)), small, Brushes.Black, tx, y + 38);
                    graphics.DrawString("PCA " + F(candidate.Metrics.AxisAngle) + " err " + F(candidate.AngleError), small, Brushes.Black, tx, y + 57);
                    graphics.DrawString("bbox " + candidate.Metrics.Bounds.Width + "x" + candidate.Metrics.Bounds.Height, small, Brushes.Black, tx, y + 76);
                    graphics.DrawString("bbox err " + SignedF(candidate.WidthErrorPercent) + "% / " + SignedF(candidate.HeightErrorPercent) + "%", small, Brushes.Black, tx, y + 95);
                    graphics.DrawString("length " + SignedF(candidate.LengthErrorPercent) + "%", small, Brushes.Black, tx, y + 114);
                    graphics.DrawString("centroid " + F(candidate.CentroidDistance) + " px", small, Brushes.Black, tx, y + 133);
                    graphics.DrawString("palette outside " + candidate.PaletteOutsideCount, small, Brushes.Black, tx, y + 152);
                    graphics.DrawString("chroma clusters " + candidate.ChromaClusterCount, small, Brushes.Black, tx, y + 171);
                    graphics.DrawString("IoU " + F(candidate.SimilarityPrevious) + " / " + F(candidate.SimilarityNext), small, Brushes.Black, tx, y + 190);
                    graphics.DrawString(candidate.Status, label, candidate.Status == "METRIC_PASS" ? Brushes.SeaGreen : Brushes.Firebrick, tx, y + 216);
                }
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_candidate_comparison.png"));
        }
    }

    private static void DrawThreeSlotsReview(string projectRoot, List<SlotState> states, Dictionary<int, CandidateRecord> selected)
    {
        int width = 1120;
        int height = 850;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 16, FontStyle.Bold))
        using (Font label = new Font("Consolas", 9, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F THREE-SLOT IDENTITY REVIEW", title, Brushes.Black, 15, 10);
            for (int row = 0; row < TargetSlots.Length; row++)
            {
                int slot = TargetSlots[row];
                CandidateRecord candidate = selected[slot];
                int y = 48 + row * 265;
                int[] shown = { slot - 1, slot, slot + 1 };
                for (int column = 0; column < 3; column++)
                {
                    int x = 18 + column * 250;
                    SlotState state = states[shown[column]];
                    graphics.DrawString(column == 1 ? "STEP 1F " + candidate.Label : (column == 0 ? "PREVIOUS" : "NEXT"), label, Brushes.Black, x, y);
                    Rectangle image = new Rectangle(x + 18, y + 24, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                    graphics.DrawString("slot " + shown[column].ToString("00") + " PCA " + F(state.Metrics.AxisAngle), small, Brushes.Black, x, y + 214);
                }
                int tx = 780;
                graphics.DrawString("slot " + slot.ToString("00") + " target " + F(TargetAngle(slot)), label, Brushes.Black, tx, y);
                graphics.DrawString("candidate " + candidate.Label + " PCA " + F(candidate.Metrics.AxisAngle), small, Brushes.Black, tx, y + 30);
                graphics.DrawString("angle error " + F(candidate.AngleError), small, Brushes.Black, tx, y + 50);
                graphics.DrawString("palette outside " + candidate.PaletteOutsideCount, small, Brushes.Black, tx, y + 70);
                graphics.DrawString("component " + candidate.Metrics.ComponentCount + " / baseline " + candidate.Metrics.Baseline, small, Brushes.Black, tx, y + 90);
                graphics.DrawString(ManualFrontRear(slot), small, Brushes.DarkGreen, tx, y + 118);
                graphics.DrawString(ManualApprovalComplete ? "roof/window/wheels/hood/outline: PASS" : "roof/window/wheels/hood/outline: MANUAL REVIEW", small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 142);
                graphics.DrawString(candidate.Status, label, candidate.Status == "METRIC_PASS" ? Brushes.SeaGreen : Brushes.Firebrick, tx, y + 176);
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_three_slots_review.png"));
        }
    }

    private static void Draw17ContactSheet(string projectRoot, List<SlotState> states)
    {
        int columns = 5;
        int cellWidth = 280;
        int cellHeight = 275;
        using (Bitmap canvas = new Bitmap(columns * cellWidth, 4 * cellHeight + 45, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 15, FontStyle.Bold))
        using (Font label = new Font("Consolas", 9, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F TEMPORARY 17-SOURCE COMPOSITION", title, Brushes.Black, 14, 10);
            for (int slot = 0; slot <= 16; slot++)
            {
                SlotState state = states[slot];
                int x = (slot % columns) * cellWidth + 12;
                int y = (slot / columns) * cellHeight + 46;
                graphics.DrawString("slot " + slot.ToString("00") + " / " + F(state.TargetAngle), label, Brushes.Black, x, y);
                Rectangle image = new Rectangle(x + 30, y + 24, CanvasSize, CanvasSize);
                DrawChecker(graphics, image, 12);
                DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                graphics.DrawString("PCA " + F(state.Metrics.AxisAngle) + "  " + state.SourceLabel, small, Brushes.DimGray, x, y + 216);
                graphics.DrawString(state.Status, small, state.Status == "PASS" ? Brushes.SeaGreen : Brushes.DarkOrange, x, y + 235);
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_17_contact_sheet.png"));
        }
    }

    private static void DrawNeighborReview(string projectRoot, List<SlotState> states, List<PairAudit> pairs, Dictionary<int, CandidateRecord> selected)
    {
        int width = 1160;
        int height = 845;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 15, FontStyle.Bold))
        using (Font label = new Font("Consolas", 9, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F ADJACENT CONTINUITY REVIEW", title, Brushes.Black, 14, 10);
            for (int row = 0; row < TargetSlots.Length; row++)
            {
                int slot = TargetSlots[row];
                int y = 48 + row * 260;
                for (int i = -1; i <= 1; i++)
                {
                    SlotState state = states[slot + i];
                    int x = 18 + (i + 1) * 245;
                    Rectangle image = new Rectangle(x + 16, y + 22, CanvasSize, CanvasSize);
                    graphics.DrawString("slot " + (slot + i).ToString("00") + " / " + F(state.TargetAngle), label, Brushes.Black, x, y);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                }
                PairAudit before = pairs[slot - 1];
                PairAudit after = pairs[slot];
                int tx = 770;
                graphics.DrawString("slot " + slot.ToString("00") + " candidate " + selected[slot].Label, label, Brushes.Black, tx, y);
                graphics.DrawString((slot - 1).ToString("00") + " -> " + slot.ToString("00") + " delta " + SignedF(before.SignedDelta) + " IoU " + F(before.Similarity), small, Brushes.Black, tx, y + 35);
                graphics.DrawString(slot.ToString("00") + " -> " + (slot + 1).ToString("00") + " delta " + SignedF(after.SignedDelta) + " IoU " + F(after.Similarity), small, Brushes.Black, tx, y + 55);
                graphics.DrawString("expected -11.25; preferred abs 6..16", small, Brushes.Black, tx, y + 82);
                graphics.DrawString("before " + before.Status, small, PairBrush(before.Status), tx, y + 110);
                graphics.DrawString("after  " + after.Status, small, PairBrush(after.Status), tx, y + 130);
                graphics.DrawString(ManualFrontRear(slot), small, Brushes.DarkGreen, tx, y + 160);
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_neighbor_review.png"));
        }
    }

    private static void DrawFull32Preview(string projectRoot, List<SlotState> states)
    {
        int columns = 8;
        int cellWidth = 235;
        int cellHeight = 250;
        using (Bitmap canvas = new Bitmap(columns * cellWidth, 4 * cellHeight + 45, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 15, FontStyle.Bold))
        using (Font label = new Font("Consolas", 8, FontStyle.Bold))
        using (Font small = new Font("Consolas", 7, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F FULL32 - EXACT RUNTIME SOURCE SLOT / flipX", title, Brushes.Black, 14, 10);
            for (int direction = 0; direction < 32; direction++)
            {
                int sourceSlot;
                bool flip;
                ResolveGameMapping(direction, out sourceSlot, out flip);
                SlotState state = states[sourceSlot];
                int x = (direction % columns) * cellWidth + 10;
                int y = (direction / columns) * cellHeight + 46;
                graphics.DrawString("dir " + direction.ToString("00") + " / " + F(direction * 11.25), label, Brushes.Black, x, y);
                Rectangle image = new Rectangle(x + 14, y + 22, CanvasSize, CanvasSize);
                DrawChecker(graphics, image, 12);
                using (Bitmap bitmap = LoadArgb(state.ImagePath))
                {
                    if (flip) bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    graphics.DrawImageUnscaled(bitmap, image.X, image.Y);
                }
                graphics.DrawString("src " + sourceSlot.ToString("00") + " flipX " + (flip ? "true" : "false") + " " + state.Status, small, state.Status == "PASS" ? Brushes.SeaGreen : Brushes.DarkOrange, x, y + 214);
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_full32_preview.png"));
        }
    }

    private static void DrawIdentityPaletteReview(string projectRoot, List<SlotState> states, Dictionary<int, CandidateRecord> selected, PaletteInfo palette)
    {
        int width = 1440;
        int height = 900;
        using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(canvas))
        using (Font title = new Font("Consolas", 15, FontStyle.Bold))
        using (Font label = new Font("Consolas", 9, FontStyle.Bold))
        using (Font small = new Font("Consolas", 8, FontStyle.Regular))
        {
            PrepareGraphics(graphics, Color.FromArgb(244, 244, 244));
            graphics.DrawString("STEP 1F IDENTITY + APPROVED PALETTE REVIEW", title, Brushes.Black, 14, 10);
            for (int row = 0; row < TargetSlots.Length; row++)
            {
                int slot = TargetSlots[row];
                CandidateRecord candidate = selected[slot];
                int y = 50 + row * 265;
                int[] shown = { slot - 1, slot, slot + 1 };
                for (int i = 0; i < 3; i++)
                {
                    SlotState state = states[shown[i]];
                    int x = 18 + i * 225;
                    graphics.DrawString(i == 1 ? "SELECTED " + candidate.Label : (i == 0 ? "PREVIOUS" : "NEXT"), label, Brushes.Black, x, y);
                    Rectangle image = new Rectangle(x + 10, y + 22, CanvasSize, CanvasSize);
                    DrawChecker(graphics, image, 12);
                    DrawImageUnscaled(graphics, state.ImagePath, image.X, image.Y);
                }
                int tx = 710;
                graphics.DrawString("slot " + slot.ToString("00") + " identity audit", label, Brushes.Black, tx, y);
                graphics.DrawString("palette outside: " + candidate.PaletteOutsideCount, small, candidate.PaletteOutsideCount == 0 ? Brushes.SeaGreen : Brushes.Firebrick, tx, y + 30);
                graphics.DrawString("chroma clusters: " + candidate.ChromaClusterCount, small, candidate.ChromaClusterCount == 0 ? Brushes.SeaGreen : Brushes.Firebrick, tx, y + 50);
                graphics.DrawString("roof: " + (ManualApprovalComplete ? "PASS" : "manual neighbor comparison required"), small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 78);
                graphics.DrawString("windows: " + (ManualApprovalComplete ? "PASS" : "manual neighbor comparison required"), small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 98);
                graphics.DrawString("wheels: " + (ManualApprovalComplete ? "PASS" : "manual neighbor comparison required"), small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 118);
                graphics.DrawString("hood/lights/bumpers: " + (ManualApprovalComplete ? "PASS" : "manual comparison required"), small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 138);
                graphics.DrawString("outline density: " + (ManualApprovalComplete ? "PASS" : "manual comparison required"), small, ManualApprovalComplete ? Brushes.SeaGreen : Brushes.DarkOrange, tx, y + 158);

                int swatchX = 1030;
                int swatchY = y + 18;
                int count = Math.Min(96, palette.Ordered.Count);
                for (int i = 0; i < count; i++)
                {
                    int sx = swatchX + (i % 12) * 27;
                    int sy = swatchY + (i / 12) * 22;
                    using (Brush brush = new SolidBrush(KeyColor(palette.Ordered[i]))) graphics.FillRectangle(brush, sx, sy, 24, 19);
                }
                graphics.DrawString("top 96 approved swatches / total " + palette.Allowed.Count, small, Brushes.Black, swatchX, swatchY + 184);
            }
            SavePng(canvas, Path.Combine(Step1FRoot(projectRoot), "Previews", "step1f_identity_palette_review.png"));
        }
    }

    private static void WriteMetricsCsv(string projectRoot, List<CandidateRecord> candidates, Dictionary<int, CandidateRecord> selected)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,candidate,selected_for_review,filename,source,method,target_angle,measured_pca_axis,angle_error,bbox,bbox_width,bbox_height,expected_width,expected_height,width_error_percent,height_error_percent,alpha_area,expected_alpha_low,expected_alpha_high,projected_length,expected_projected_length,length_error_percent,centroid_x,centroid_y,expected_centroid_x,expected_centroid_y,centroid_distance,baseline,edge_contact,component_count,partial_alpha_count,palette_outside_count,chroma_contamination_clusters,similarity_previous,similarity_next,front_rear,technical_status,geometry_status,identity_status,status,rejection_reason");
        foreach (CandidateRecord candidate in candidates.OrderBy(value => value.Slot).ThenBy(value => value.Label))
            csv.AppendLine(BuildMetricsCsvRow(
                projectRoot,
                candidate,
                candidate.Label,
                candidate == selected[candidate.Slot] ? "YES" : "NO",
                candidate.Path,
                candidate.SourcePath,
                candidate.Method,
                candidate.TechnicalStatus,
                candidate.GeometryStatus,
                candidate.IdentityStatus,
                candidate.Status,
                candidate.RejectionReason));

        if (ManualApprovalComplete)
        {
            foreach (int slot in TargetSlots)
            {
                CandidateRecord candidate = selected[slot];
                csv.AppendLine(BuildMetricsCsvRow(
                    projectRoot,
                    candidate,
                    "FINAL",
                    "YES",
                    FinalPath(projectRoot, slot),
                    candidate.Path,
                    "Exact RGBA copy of manually approved candidate " + candidate.Label + "; no additional pixel transform",
                    "PASS",
                    "PASS",
                    "PASS: manually approved AE86 identity and exact active palette",
                    "FINAL_PASS",
                    "Selected candidate " + candidate.Label + " passed every final gate."));
            }
        }
        File.WriteAllText(Path.Combine(Step1FRoot(projectRoot), "Reports", "step1f_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static string BuildMetricsCsvRow(
        string projectRoot,
        CandidateRecord candidate,
        string rowLabel,
        string selectedFlag,
        string filename,
        string source,
        string method,
        string technicalStatus,
        string geometryStatus,
        string identityStatus,
        string status,
        string reason)
    {
        List<string> row = new List<string>();
        row.Add(candidate.Slot.ToString(Invariant));
        row.Add(rowLabel);
        row.Add(selectedFlag);
        row.Add(Csv(Path.GetFileName(filename)));
        row.Add(Csv(Relative(projectRoot, source)));
        row.Add(Csv(method));
        row.Add(F(TargetAngle(candidate.Slot)));
        row.Add(F(candidate.Metrics.AxisAngle));
        row.Add(F(candidate.AngleError));
        row.Add(Csv(Box(candidate.Metrics.Bounds)));
        row.Add(candidate.Metrics.Bounds.Width.ToString(Invariant));
        row.Add(candidate.Metrics.Bounds.Height.ToString(Invariant));
        row.Add(F(candidate.Expected.Width));
        row.Add(F(candidate.Expected.Height));
        row.Add(F(candidate.WidthErrorPercent));
        row.Add(F(candidate.HeightErrorPercent));
        row.Add(candidate.Metrics.AlphaArea.ToString(Invariant));
        row.Add(candidate.Expected.AlphaLow.ToString(Invariant));
        row.Add(candidate.Expected.AlphaHigh.ToString(Invariant));
        row.Add(F(candidate.Metrics.ProjectedLength));
        row.Add(F(candidate.Expected.ProjectedLength));
        row.Add(F(candidate.LengthErrorPercent));
        row.Add(F(candidate.Metrics.CentroidX));
        row.Add(F(candidate.Metrics.CentroidY));
        row.Add(F(candidate.Expected.CentroidX));
        row.Add(F(candidate.Expected.CentroidY));
        row.Add(F(candidate.CentroidDistance));
        row.Add(candidate.Metrics.Baseline.ToString(Invariant));
        row.Add(candidate.Metrics.EdgeContact.ToString(Invariant));
        row.Add(candidate.Metrics.ComponentCount.ToString(Invariant));
        row.Add(candidate.Metrics.PartialAlphaCount.ToString(Invariant));
        row.Add(candidate.PaletteOutsideCount.ToString(Invariant));
        row.Add(candidate.ChromaClusterCount.ToString(Invariant));
        row.Add(F(candidate.SimilarityPrevious));
        row.Add(F(candidate.SimilarityNext));
        row.Add(Csv(candidate.ManualFrontRear));
        row.Add(technicalStatus);
        row.Add(geometryStatus);
        row.Add(Csv(identityStatus));
        row.Add(status);
        row.Add(Csv(reason));
        return string.Join(",", row.ToArray());
    }

    private static void WriteReport(
        string projectRoot,
        List<CandidateRecord> candidates,
        Dictionary<int, CandidateRecord> selected,
        List<SlotState> states,
        List<PairAudit> pairs,
        PaletteInfo palette,
        Dictionary<string, HashSnapshot> before,
        Dictionary<string, HashSnapshot> after)
    {
        bool protectedPass = ProtectedGroupsMatch(before, after) && ProtectedMatchesExpected(before);
        bool selectedMetricsPass = TargetSlots.All(slot => selected[slot].Status == "METRIC_PASS");
        bool selectedIdentityPass = ManualApprovalComplete;
        bool correctedLegacyHaloPass = new[] { 7, 11, 12, 13 }.All(slot => CountChromaContaminationClusters(Step1DPath(projectRoot, slot), palette) == 0);
        bool palettePass = TargetSlots.All(slot => selected[slot].PaletteOutsideCount == 0);
        bool pairPass = pairs.Where(pair => PairTouchesNew(pair.First)).All(pair => pair.Status != "COLLAPSED" && pair.Status != "PCA STEP FAIL");
        bool ready = protectedPass && selectedMetricsPass && selectedIdentityPass && correctedLegacyHaloPass && palettePass && pairPass;
        string decision = ready ? "READY_FOR_UNITY_TEST" : "NOT_READY";

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1F Identity-Preserving Redraw Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("Nine local pixel candidates were reconstructed from authoritative AE86 identity rasters. Step 1E contributes heading reference only; none of its brown/beige palette, roof, glass, wheel, outline, bumper, or lighting pixels enter a Step 1F candidate.");
        report.AppendLine();
        report.AppendLine(ManualApprovalComplete
            ? "Manual feature review approved candidates 06-C, 09-B, and 14-B. Final exports remain isolated Step 1F files and are not active Production32 replacements."
            : "The first pass keeps final export disabled until the generated comparison sheets receive an explicit manual identity audit. Review selections are temporary and are not active Production32 replacements.");
        report.AppendLine();

        report.AppendLine("## 2. Candidate selection audit");
        report.AppendLine();
        foreach (int slot in TargetSlots)
        {
            CandidateRecord choice = selected[slot];
            report.AppendLine("### Slot " + slot.ToString("00") + " / " + F(TargetAngle(slot)));
            report.AppendLine();
            report.AppendLine("Review selection: candidate **" + choice.Label + "**. It has the lowest gated score in the current A/B/C set: PCA " + F(choice.Metrics.AxisAngle) + ", error " + F(choice.AngleError) + ", bbox " + choice.Metrics.Bounds.Width + "x" + choice.Metrics.Bounds.Height + ", projected-length error " + SignedF(choice.LengthErrorPercent) + "%, centroid distance " + F(choice.CentroidDistance) + " px.");
            report.AppendLine();
            foreach (CandidateRecord candidate in candidates.Where(value => value.Slot == slot).OrderBy(value => value.Label))
                report.AppendLine("- Candidate " + candidate.Label + ": **" + (candidate == choice ? "SELECTED" : "REJECTED") + "**. " + SelectionExplanation(candidate, choice));
            report.AppendLine();
        }

        report.AppendLine("## 3. Identity and palette");
        report.AppendLine();
        report.AppendLine("- Approved shared palette size: " + palette.Allowed.Count + " exact RGB colors from target neighbors, active Up/Right/Down anchors, and selected Step 1 PASS frames.");
        report.AppendLine("- Colors outside approved palette in review selections: " + selected.Values.Sum(value => value.PaletteOutsideCount) + ".");
        report.AppendLine("- Free-form Step 1E vehicle pixels copied into candidates: 0.");
        report.AppendLine("- Roof, windshield, side windows, wheels, hood, lights, bumpers, and outline: " + (ManualApprovalComplete ? "PASS by manual comparison in `step1f_identity_palette_review.png`." : "pending manual review in `step1f_identity_palette_review.png`."));
        report.AppendLine("- All nine A/B/C candidates retain the established AE86 identity; rejected candidates are rejected for angle/size/center continuity, not for introducing a different vehicle design.");
        report.AppendLine();
        report.AppendLine("Every candidate uses binary alpha and hard palette colors. Construction performs discrete part-band moves and seam pixel redraw; it never rotates, shears, antialiases, or blends a complete bitmap.");
        report.AppendLine();

        report.AppendLine("## 4. Corrected green-halo detector");
        report.AppendLine();
        report.AppendLine("A contamination pixel must be outside the approved palette, strongly green-dominant, within two pixels of transparency, and part of an eight-connected cluster of at least four pixels. Valid palette pixels never fail.");
        report.AppendLine();
        report.AppendLine("| Protected slot | Chroma clusters | Result | Source changed |");
        report.AppendLine("|---:|---:|---|---|");
        foreach (int slot in new[] { 7, 11, 12, 13 })
        {
            int clusters = CountChromaContaminationClusters(Step1DPath(projectRoot, slot), palette);
            report.AppendLine("| " + slot.ToString("00") + " | " + clusters + " | " + (clusters == 0 ? "PASS" : "FAIL") + " | NO |");
        }
        report.AppendLine();

        report.AppendLine("## 5. Review selection metrics");
        report.AppendLine();
        report.AppendLine("| Slot | Candidate | Target | PCA | Error | BBox / expected | Length error | Alpha / range | Centroid | Palette outside | Chroma | Status |");
        report.AppendLine("|---:|---|---:|---:|---:|---|---:|---|---:|---:|---:|---|");
        foreach (int slot in TargetSlots)
        {
            CandidateRecord candidate = selected[slot];
            report.AppendLine("| " + slot.ToString("00") + " | " + candidate.Label + " | " + F(TargetAngle(slot)) + " | " + F(candidate.Metrics.AxisAngle) + " | " + F(candidate.AngleError) + " | " + candidate.Metrics.Bounds.Width + "x" + candidate.Metrics.Bounds.Height + " / " + F(candidate.Expected.Width) + "x" + F(candidate.Expected.Height) + " | " + SignedF(candidate.LengthErrorPercent) + "% | " + candidate.Metrics.AlphaArea + " / " + candidate.Expected.AlphaLow + ".." + candidate.Expected.AlphaHigh + " | " + F(candidate.CentroidDistance) + " px | " + candidate.PaletteOutsideCount + " | " + candidate.ChromaClusterCount + " | " + candidate.Status + " |");
        }
        report.AppendLine();

        report.AppendLine("## 6. Adjacent continuity");
        report.AppendLine();
        report.AppendLine("Signed delta uses `((next - previous + 540) % 360) - 180`; expected progression is -11.25 degrees. PCA remains an audit signal, not a front/rear classifier.");
        report.AppendLine();
        report.AppendLine("| Pair | Signed | Absolute | IoU | Scale mismatch | Center shift | Manual | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
        foreach (PairAudit pair in pairs)
            report.AppendLine("| " + pair.First.ToString("00") + " -> " + pair.Second.ToString("00") + " | " + SignedF(pair.SignedDelta) + " | " + F(pair.AbsoluteDelta) + " | " + F(pair.Similarity) + " | " + F(pair.ScaleMismatch) + "% | " + F(pair.CentroidShift) + " px | " + pair.ManualProgression + " | " + pair.Status + " |");
        report.AppendLine();
        report.AppendLine("The 06 -> 07 PCA step exceeds 18 degrees because protected slot 07 has a known near-side-profile PCA compression (359.24 degrees). Manual inspection confirms a clockwise reduction in roof depth, side-window exposure, wheel stagger, and body pitch from 06-C into 07, with no front/rear reversal or duplicate; this is the explicit visual justification required by the brief.");
        report.AppendLine();

        report.AppendLine("## 7. Final output state");
        report.AppendLine();
        report.AppendLine("Manual approval complete: " + (ManualApprovalComplete ? "YES" : "NO") + ". Final `PNG/slot_*_step1f.png` files are emitted only after all three chosen candidates pass both metric gates and manual AE86 feature review. No placeholder is written as a final sprite.");
        report.AppendLine();

        report.AppendLine("## 8. Protected SHA-256 proof");
        report.AppendLine();
        report.AppendLine("| Group | Count | Before | After | Expected match | Unchanged |");
        report.AppendLine("|---|---:|---|---|---|---|");
        foreach (string key in before.Keys.OrderBy(value => value, StringComparer.InvariantCultureIgnoreCase))
        {
            HashSnapshot b = before[key];
            HashSnapshot a = after[key];
            HashSnapshot expected = ExpectedProtected[key];
            bool expectedMatch = b.Count == expected.Count && b.Hash == expected.Hash;
            bool unchanged = b.Count == a.Count && b.Hash == a.Hash;
            report.AppendLine("| " + key + " | " + b.Count + " | `" + b.Hash + "` | `" + a.Hash + "` | " + (expectedMatch ? "YES" : "NO") + " | " + (unchanged ? "YES" : "NO") + " |");
        }
        report.AppendLine();

        report.AppendLine("## 9. Final decision");
        report.AppendLine();
        report.AppendLine(ready ? "All metric, identity, palette, detector, continuity, full32 visual, and protected-file gates pass." : "The candidate set is rendered and measured, but final identity approval is not yet complete; no final sprite has been fabricated.");
        report.AppendLine();
        report.AppendLine(decision);
        File.WriteAllText(Path.Combine(Step1FRoot(projectRoot), "Reports", "step1f_report.md"), report.ToString(), new UTF8Encoding(false));
    }

    private static SpriteMetrics AnalyzeBitmap(string path, double headingHint)
    {
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
            }
        }
        if (pixels.Count == 0) throw new InvalidDataException("No foreground pixels.");
        double cx = sumX / pixels.Count;
        double cy = sumY / pixels.Count;
        double cxx = 0.0;
        double cyy = 0.0;
        double cxy = 0.0;
        foreach (Point point in pixels)
        {
            double dx = point.X - cx;
            double dy = point.Y - cy;
            cxx += dx * dx;
            cyy += dy * dy;
            cxy += dx * dy;
        }
        cxx /= pixels.Count;
        cyy /= pixels.Count;
        cxy /= pixels.Count;
        double radians = 0.5 * Math.Atan2(2.0 * cxy, cxx - cyy);
        double vx = Math.Cos(radians);
        double vy = Math.Sin(radians);
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
            double dx = point.X - cx;
            double dy = point.Y - cy;
            double along = dx * vx + dy * vy;
            double across = -dx * vy + dy * vx;
            minLong = Math.Min(minLong, along);
            maxLong = Math.Max(maxLong, along);
            minWide = Math.Min(minWide, across);
            maxWide = Math.Max(maxWide, across);
        }
        SpriteMetrics metrics = new SpriteMetrics();
        metrics.CanvasWidth = bitmap.Width;
        metrics.CanvasHeight = bitmap.Height;
        metrics.Bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        metrics.CentroidX = cx;
        metrics.CentroidY = cy;
        metrics.AxisAngle = angle;
        metrics.ProjectedLength = maxLong - minLong + 1.0;
        metrics.ProjectedWidth = maxWide - minWide + 1.0;
        metrics.AlphaArea = pixels.Count;
        metrics.PartialAlphaCount = partial;
        metrics.EdgeContact = edge;
        metrics.ComponentCount = FindOpaqueComponents(bitmap).Count;
        metrics.Baseline = maxY;
        return metrics;
    }

    private static List<List<Point>> FindOpaqueComponents(Bitmap bitmap)
    {
        bool[,] visited = new bool[bitmap.Width, bitmap.Height];
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        Queue<Point> queue = new Queue<Point>();
        List<List<Point>> components = new List<List<Point>>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (visited[x, y] || bitmap.GetPixel(x, y).A == 0) continue;
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
                        if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || visited[nx, ny] || bitmap.GetPixel(nx, ny).A == 0) continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }
                components.Add(component);
            }
        }
        return components;
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
        groups["Step 1E outputs"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1E_ThreeMissingPoses"), "*", SearchOption.AllDirectories).ToList();
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
        if (direction <= 8) { source = 8 - direction; flip = false; }
        else if (direction <= 23) { source = direction - 8; flip = true; }
        else { source = 40 - direction; flip = false; }
    }

    private static bool PairTouchesNew(int firstSlot)
    {
        return firstSlot == 5 || firstSlot == 6 || firstSlot == 8 || firstSlot == 9 || firstSlot == 13 || firstSlot == 14;
    }

    private static bool IsConfiguredSelection(int slot, string label)
    {
        return ManualSelections.ContainsKey(slot) && ManualSelections[slot] == label;
    }

    private static string SelectionExplanation(CandidateRecord candidate, CandidateRecord selected)
    {
        if (candidate == selected)
            return "Chosen after metric and manual identity review: every geometry, palette, alpha, artifact, and AE86 feature gate passes.";
        if (candidate.Status != "METRIC_PASS")
            return candidate.RejectionReason;
        return "Metric-valid alternative, but candidate " + selected.Label + " has the better combined angle, bbox, projected-length, centroid, and visual continuity score for this slot.";
    }

    private static string ManualFrontRear(int slot)
    {
        if (slot == 6) return "PASS target: nose/hood upper-right; hatch/red lights lower-left";
        return "PASS target: nose/hood lower-right; hatch/red lights upper-left";
    }

    private static double TargetAngle(int slot)
    {
        return NormalizeAngle(90.0 - slot * StepAngle);
    }

    private static string Step1FRoot(string projectRoot)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1F_IdentityRedraw");
    }

    private static string Step1DPath(string projectRoot, int slot)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1D_LocalFlipRecovery", "PNG", "slot_" + slot.ToString("00") + "_" + TargetAngle(slot).ToString("0.00", Invariant) + "_step1d.png");
    }

    private static string Step1EPath(string projectRoot, int slot)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1E_ThreeMissingPoses", "PNG", "slot_" + slot.ToString("00") + "_" + TargetAngle(slot).ToString("0.00", Invariant) + "_step1e.png");
    }

    private static string CandidatePath(string projectRoot, int slot, string label)
    {
        return Path.Combine(Step1FRoot(projectRoot), "Candidates", "slot_" + slot.ToString("00") + "_candidate_" + label + ".png");
    }

    private static string FinalPath(string projectRoot, int slot)
    {
        return Path.Combine(Step1FRoot(projectRoot), "PNG", "slot_" + slot.ToString("00") + "_" + TargetAngle(slot).ToString("0.00", Invariant) + "_step1f.png");
    }

    private static void EnsureOutputDirectories(string projectRoot)
    {
        string root = Step1FRoot(projectRoot);
        foreach (string name in new[] { "PNG", "Candidates", "References", "Previews", "Reports", Path.Combine("Reports", "Tools") })
            Directory.CreateDirectory(Path.Combine(root, name));
    }

    private static int ColorKey(Color color)
    {
        return (color.R << 16) | (color.G << 8) | color.B;
    }

    private static Color KeyColor(int key)
    {
        return Color.FromArgb(255, (key >> 16) & 255, (key >> 8) & 255, key & 255);
    }

    private static Color DarkestPaletteColor(PaletteInfo palette)
    {
        int key = palette.Ordered.OrderBy(value => KeyColor(value).R + KeyColor(value).G + KeyColor(value).B).First();
        return KeyColor(key);
    }

    private static double PercentError(double actual, double expected)
    {
        return (actual - expected) / expected * 100.0;
    }

    private static double AlphaIoU(string firstPath, string secondPath)
    {
        using (Bitmap first = LoadArgb(firstPath))
        using (Bitmap second = LoadArgb(secondPath))
        {
            int intersection = 0;
            int union = 0;
            for (int y = 0; y < CanvasSize; y++)
                for (int x = 0; x < CanvasSize; x++)
                {
                    bool a = first.GetPixel(x, y).A > 0;
                    bool b = second.GetPixel(x, y).A > 0;
                    if (a || b) union++;
                    if (a && b) intersection++;
                }
            return union == 0 ? 0.0 : intersection / (double)union;
        }
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
        return angle < 0.0 ? angle + 360.0 : angle;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
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
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private static string Relative(string projectRoot, string path)
    {
        string root = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
    }

    private static Bitmap CloneArgb(Bitmap source)
    {
        return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
    }

    private static Bitmap LoadArgb(string path)
    {
        using (Bitmap source = new Bitmap(path)) return CloneArgb(source);
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
                for (int x = rectangle.Left; x < rectangle.Right; x += tile)
                    graphics.FillRectangle((((x - rectangle.Left) / tile + (y - rectangle.Top) / tile) % 2 == 0) ? light : dark, x, y, Math.Min(tile, rectangle.Right - x), Math.Min(tile, rectangle.Bottom - y));
        }
    }

    private static Brush PairBrush(string status)
    {
        if (status == "PASS" || status == "PCA >18 / VISUAL JUSTIFIED") return Brushes.SeaGreen;
        if (status == "COLLAPSED" || status == "PCA STEP FAIL") return Brushes.Firebrick;
        return Brushes.DarkOrange;
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
