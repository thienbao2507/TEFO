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

public static class AE86Step1CHybridBuilder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] VisualFocusSlots = { 1, 6, 7, 9, 10, 14, 15 };

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
    }

    private sealed class SlotState
    {
        public int Slot;
        public double TargetAngle;
        public string Filename;
        public string ImagePath;
        public string SourceMethod;
        public string SourceLabel;
        public string Status;
        public string RecommendedAction;
        public string FrontRearCheck;
        public string ArtifactCheck;
        public string CropCheck;
        public SpriteMetrics Metrics;
        public double ExpectedLength;
        public double ScaleDifferencePercent;
        public double ExpectedCentroidX;
        public double ExpectedCentroidY;
        public double CenterOffsetX;
        public double CenterOffsetY;
        public double AngularError;
        public double? PreviousDelta;
        public double? NextDelta;
        public string VisualNotes;
    }

    private sealed class PairAudit
    {
        public int FirstSlot;
        public int SecondSlot;
        public double? ActualDelta;
        public double? Similarity;
        public double? ScaleMismatch;
        public double? CentroidShift;
        public string Status;
    }

    private sealed class HashSnapshot
    {
        public int Count;
        public string Hash;
    }

    public static void Run(string projectRoot)
    {
        projectRoot = Path.GetFullPath(projectRoot);
        string outputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_HybridLocal");
        string pngRoot = Path.Combine(outputRoot, "PNG");
        string reportRoot = Path.Combine(outputRoot, "Reports");
        string previewRoot = Path.Combine(outputRoot, "Previews");
        Directory.CreateDirectory(pngRoot);
        Directory.CreateDirectory(reportRoot);
        Directory.CreateDirectory(previewRoot);

        Dictionary<string, HashSnapshot> hashesBefore = CaptureProtectedHashes(projectRoot);
        SpriteMetrics up = AnalyzeBitmap(ActivePath(projectRoot, "ae86_090_00_up.png"), 90.0);
        SpriteMetrics right = AnalyzeBitmap(ActivePath(projectRoot, "ae86_000_00_right.png"), 0.0);
        SpriteMetrics down = AnalyzeBitmap(ActivePath(projectRoot, "ae86_270_00_down.png"), 270.0);

        Dictionary<int, string> selected = ExportHybridCandidates(projectRoot, pngRoot);
        string slot06Original = Path.Combine(pngRoot, "slot_06_22.50_original_local.png");
        string slot06Normalized = Path.Combine(pngRoot, "slot_06_22.50_scale_normalized.png");
        SpriteMetrics slot05Metrics = AnalyzeBitmap(selected[5], 33.75);
        SpriteMetrics slot07Metrics = AnalyzeBitmap(selected[7], 11.25);
        double slot06ExpectedLength = (slot05Metrics.ProjectedLength + slot07Metrics.ProjectedLength) / 2.0;
        CreateUniformScaleCandidate(slot06Original, slot06Normalized, slot06ExpectedLength, 22.50);
        selected[6] = slot06Normalized;

        List<SlotState> states = BuildStates(selected, up, right, down);
        List<PairAudit> pairs = BuildPairAudits(states);
        WriteMetricsCsv(reportRoot, states);
        DrawFinal17ContactSheet(previewRoot, states);
        DrawNeighborReview(previewRoot, states, pairs);
        DrawFull32Preview(previewRoot, states);
        DrawSlot06Comparison(previewRoot, states, slot06Original, slot06Normalized, slot06ExpectedLength);
        DrawSlot091011Comparison(projectRoot, previewRoot, states);

        Dictionary<string, HashSnapshot> hashesAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, outputRoot, states, pairs, slot06Original, slot06Normalized, slot06ExpectedLength, hashesBefore, hashesAfter);
    }

    private static Dictionary<int, string> ExportHybridCandidates(string projectRoot, string pngRoot)
    {
        Dictionary<int, string> source = new Dictionary<int, string>();
        source[0] = ActivePath(projectRoot, "ae86_090_00_up.png");
        source[1] = ActivePath(projectRoot, "ae86_078_75.png");
        source[2] = Step1Path(projectRoot, "slot_02_67.50_candidate.png");
        source[3] = Step1Path(projectRoot, "slot_03_56.25_candidate.png");
        source[4] = Step1Path(projectRoot, "slot_04_45.00_candidate.png");
        source[5] = Step1Path(projectRoot, "slot_05_33.75_candidate.png");
        source[6] = ActivePath(projectRoot, "ae86_022_50.png");
        source[7] = ActivePath(projectRoot, "ae86_011_25.png");
        source[8] = ActivePath(projectRoot, "ae86_000_00_right.png");
        source[9] = ActivePath(projectRoot, "ae86_348_75.png");
        source[10] = Step1Path(projectRoot, "slot_10_337.50_candidate.png");
        source[11] = Step1Path(projectRoot, "slot_11_326.25_candidate.png");
        source[12] = Step1Path(projectRoot, "slot_12_315.00_candidate.png");
        source[13] = Step1Path(projectRoot, "slot_13_303.75_candidate.png");
        source[14] = ActivePath(projectRoot, "ae86_292_50.png");
        source[15] = ActivePath(projectRoot, "ae86_281_25.png");
        source[16] = ActivePath(projectRoot, "ae86_270_00_down.png");

        Dictionary<int, string> output = new Dictionary<int, string>();
        for (int slot = 0; slot <= 16; slot++)
        {
            double angle = NormalizeAngle(90.0 - slot * StepAngle);
            string filename = slot == 6
                ? "slot_06_22.50_original_local.png"
                : "slot_" + slot.ToString("00") + "_" + AngleToken(angle) + "_hybrid.png";
            string destination = Path.Combine(pngRoot, filename);
            CopyPng(source[slot], destination);
            output[slot] = destination;
        }
        return output;
    }

    private static List<SlotState> BuildStates(Dictionary<int, string> selected, SpriteMetrics up, SpriteMetrics right, SpriteMetrics down)
    {
        List<SlotState> states = new List<SlotState>();
        for (int slot = 0; slot <= 16; slot++)
        {
            SlotState state = new SlotState();
            state.Slot = slot;
            state.TargetAngle = NormalizeAngle(90.0 - slot * StepAngle);
            state.ExpectedLength = InterpolateAnchorValue(slot, up.ProjectedLength, right.ProjectedLength, down.ProjectedLength);
            state.ExpectedCentroidX = InterpolateAnchorValue(slot, up.CentroidX, right.CentroidX, down.CentroidX);
            state.ExpectedCentroidY = InterpolateAnchorValue(slot, up.CentroidY, right.CentroidY, down.CentroidY);

            state.ImagePath = selected[slot];
            state.Filename = Path.GetFileName(state.ImagePath);
            state.SourceLabel = SourceLabelForSlot(slot);
            state.SourceMethod = SourceMethodForSlot(slot);

            ValidateSelectedState(state);
            states.Add(state);
        }
        return states;
    }

    private static void ValidateSelectedState(SlotState state)
    {
        try
        {
            state.Metrics = AnalyzeBitmap(state.ImagePath, state.TargetAngle);
        }
        catch (Exception exception)
        {
            state.Status = "FAIL";
            state.RecommendedAction = "Reject: " + exception.Message;
            state.FrontRearCheck = "Not completed";
            state.ArtifactCheck = "FAIL";
            state.CropCheck = "FAIL";
            return;
        }

        SpriteMetrics metrics = state.Metrics;
        state.AngularError = ShortestAngleError(metrics.AxisAngle, state.TargetAngle);
        state.ScaleDifferencePercent = ((metrics.ProjectedLength - state.ExpectedLength) / state.ExpectedLength) * 100.0;
        state.CenterOffsetX = metrics.CentroidX - state.ExpectedCentroidX;
        state.CenterOffsetY = metrics.CentroidY - state.ExpectedCentroidY;
        bool correctCanvas = metrics.CanvasWidth == CanvasSize && metrics.CanvasHeight == CanvasSize;
        bool alphaPass = metrics.PartialAlphaPixelCount == 0 && metrics.ComponentCount == 1;
        bool baselinePass = metrics.Baseline == BaselineY;
        bool edgePass = metrics.EdgeTouchCount == 0;
        bool cropPass = metrics.Bounds.Left > 0 && metrics.Bounds.Top > 0 && metrics.Bounds.Right < CanvasSize && metrics.Bounds.Bottom < CanvasSize;
        bool technicalPass = correctCanvas && alphaPass && baselinePass && edgePass && cropPass;
        state.FrontRearCheck = ManualFrontRearCheck(state.Slot);
        state.ArtifactCheck = alphaPass && edgePass ? "PASS" : "FAIL";
        state.CropCheck = cropPass ? "PASS" : "FAIL";

        if (!technicalPass)
        {
            state.Status = "FAIL";
            state.RecommendedAction = "Reject: canvas, alpha, baseline, edge, component, or crop validation failed.";
            state.VisualNotes = "Technical image requirements failed before visual review.";
        }
        else
        {
            state.Status = ManualVisualStatus(state.Slot);
            state.VisualNotes = ManualVisualNotes(state.Slot);
            if (Math.Abs(state.ScaleDifferencePercent) > 8.0)
            {
                state.Status = "FAIL";
                state.VisualNotes += " Apparent scale also exceeds the hybrid tolerance by more than 8 percent.";
            }
            else if (Math.Abs(state.ScaleDifferencePercent) > 5.0 && state.Status == "PASS")
            {
                state.Status = "REVIEW";
                state.VisualNotes += " Scale continuity needs gameplay review.";
            }
            state.RecommendedAction = state.Status == "PASS"
                ? "Retain for the hybrid Unity preview only."
                : state.Status == "REVIEW"
                    ? "Keep for runtime comparison, but do not approve as final replacement."
                    : "Do not promote; local-only visual orientation or sequence is invalid.";
        }
    }

    private static void CreateUniformScaleCandidate(string sourcePath, string outputPath, double expectedLength, double headingHint)
    {
        using (Bitmap source = LoadArgb(sourcePath))
        {
            SpriteMetrics sourceMetrics = AnalyzeBitmap(source, headingHint);
            double scale = expectedLength / sourceMetrics.ProjectedLength;
            Rectangle bounds = sourceMetrics.Bounds;
            int newWidth = Math.Max(1, (int)Math.Round(bounds.Width * scale, MidpointRounding.AwayFromZero));
            int newHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale, MidpointRounding.AwayFromZero));
            int destX = (CanvasSize - newWidth + 1) / 2;
            int destY = BaselineY - newHeight + 1;
            if (destX < 1 || destY < 1 || destX + newWidth >= CanvasSize - 1 || destY + newHeight >= CanvasSize - 1)
                throw new InvalidOperationException("Uniform scale correction does not fit the 186x186 canvas.");

            using (Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < newHeight; y++)
                {
                    int sourceOffsetY = Math.Min(bounds.Height - 1, (int)Math.Floor(((y + 0.5) * bounds.Height) / newHeight));
                    int sourceY = bounds.Y + sourceOffsetY;
                    for (int x = 0; x < newWidth; x++)
                    {
                        int sourceOffsetX = Math.Min(bounds.Width - 1, (int)Math.Floor(((x + 0.5) * bounds.Width) / newWidth));
                        Color pixel = source.GetPixel(bounds.X + sourceOffsetX, sourceY);
                        if (pixel.A > 0)
                            output.SetPixel(destX + x, destY + y, pixel);
                    }
                }
                SavePng(output, outputPath);
            }
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
            if (first.Metrics == null || second.Metrics == null)
            {
                audit.Status = "UNRESOLVED";
                audits.Add(audit);
                continue;
            }

            audit.ActualDelta = ClockwiseDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
            audit.Similarity = AlphaIoU(first.ImagePath, second.ImagePath);
            audit.ScaleMismatch = Math.Max(Math.Abs(first.ScaleDifferencePercent), Math.Abs(second.ScaleDifferencePercent));
            audit.CentroidShift = Distance(first.Metrics.CentroidX, first.Metrics.CentroidY, second.Metrics.CentroidX, second.Metrics.CentroidY);
            first.NextDelta = audit.ActualDelta;
            second.PreviousDelta = audit.ActualDelta;

            if (audit.ActualDelta.Value > 180.0)
                audit.Status = "REVERSED";
            else if (audit.ActualDelta.Value < 4.0)
                audit.Status = "COLLAPSED";
            else if (audit.ActualDelta.Value > 15.0)
                audit.Status = "LARGE JUMP";
            else if (audit.ScaleMismatch.Value > 3.0)
                audit.Status = "SCALE >3%";
            else if (first.Status == "FAIL" || second.Status == "FAIL")
                audit.Status = "VISUAL FAIL";
            else if (first.Status != "PASS" || second.Status != "PASS")
                audit.Status = "REVIEW SOURCE";
            else
                audit.Status = "PASS";
            audits.Add(audit);
        }
        return audits;
    }

    private static void WriteMetricsCsv(string reportRoot, List<SlotState> states)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,filename,target_angle,measured_angle,angular_error,source_label,source_method,canvas_width,canvas_height,bbox_x,bbox_y,bbox_width,bbox_height,centroid_x,centroid_y,bbox_center_x,bbox_center_y,projected_length,projected_width,alpha_pixel_count,partial_alpha_pixel_count,component_count,scale_difference_percent,center_offset_x,center_offset_y,baseline,edge_touch_count,crop_check,artifact_check,front_rear_check,visual_notes,previous_delta,next_delta,status,recommended_action");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            List<string> row = new List<string>();
            row.Add(state.Slot.ToString(Invariant));
            row.Add(Csv(state.Filename));
            row.Add(F(state.TargetAngle));
            row.Add(m == null ? string.Empty : F(m.AxisAngle));
            row.Add(m == null ? string.Empty : F(state.AngularError));
            row.Add(Csv(state.SourceLabel));
            row.Add(Csv(state.SourceMethod));
            row.Add(m == null ? string.Empty : m.CanvasWidth.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.CanvasHeight.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.X.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Y.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Width.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Height.ToString(Invariant));
            row.Add(m == null ? string.Empty : F(m.CentroidX));
            row.Add(m == null ? string.Empty : F(m.CentroidY));
            row.Add(m == null ? string.Empty : F(m.Bounds.X + (m.Bounds.Width - 1) / 2.0));
            row.Add(m == null ? string.Empty : F(m.Bounds.Y + (m.Bounds.Height - 1) / 2.0));
            row.Add(m == null ? string.Empty : F(m.ProjectedLength));
            row.Add(m == null ? string.Empty : F(m.ProjectedWidth));
            row.Add(m == null ? string.Empty : m.AlphaPixelCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.PartialAlphaPixelCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.ComponentCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : F(state.ScaleDifferencePercent));
            row.Add(m == null ? string.Empty : F(state.CenterOffsetX));
            row.Add(m == null ? string.Empty : F(state.CenterOffsetY));
            row.Add(m == null ? string.Empty : m.Baseline.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.EdgeTouchCount.ToString(Invariant));
            row.Add(Csv(state.CropCheck));
            row.Add(Csv(state.ArtifactCheck));
            row.Add(Csv(state.FrontRearCheck));
            row.Add(Csv(state.VisualNotes));
            row.Add(Nullable(state.PreviousDelta));
            row.Add(Nullable(state.NextDelta));
            row.Add(Csv(state.Status));
            row.Add(Csv(state.RecommendedAction));
            csv.AppendLine(string.Join(",", row.ToArray()));
        }
        File.WriteAllText(Path.Combine(reportRoot, "hybrid_local_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static void DrawFinal17ContactSheet(string previewRoot, List<SlotState> states)
    {
        const int columns = 5;
        const int cellWidth = 300;
        const int cellHeight = 300;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, 4 * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(238, 238, 238));
            foreach (SlotState state in states)
            {
                int column = state.Slot % columns;
                int row = state.Slot / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Rectangle imageArea = new Rectangle(cell.X + 57, cell.Y + 3, CanvasSize, CanvasSize);
                DrawSlot(graphics, state, imageArea, strong);
                using (Pen border = new Pen(StatusColor(state.Status), 4))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
                string actual = state.Metrics == null ? "n/a" : F(state.Metrics.AxisAngle);
                string error = state.Metrics == null ? "n/a" : F(state.AngularError);
                graphics.DrawString("slot " + state.Slot.ToString("00") + " target " + F(state.TargetAngle), strong, Brushes.Black, cell.X + 8, cell.Y + 193);
                graphics.DrawString("input: " + SourceFilenameForSlot(state.Slot), label, Brushes.Black, cell.X + 8, cell.Y + 214);
                graphics.DrawString("source: " + state.SourceLabel, label, Brushes.Black, cell.X + 8, cell.Y + 233);
                graphics.DrawString("PCA " + actual + "  baseline " + state.Metrics.Baseline, label, Brushes.Black, cell.X + 8, cell.Y + 252);
                graphics.DrawString("bbox " + state.Metrics.Bounds.Width + "x" + state.Metrics.Bounds.Height, label, Brushes.Black, cell.X + 8, cell.Y + 271);
                using (Brush brush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString(ShortStatus(state.Status), label, brush, cell.X + 205, cell.Y + 271);
            }
            SavePng(sheet, Path.Combine(previewRoot, "hybrid_local_17_contact_sheet.png"));
        }
    }

    private static void DrawNeighborReview(string previewRoot, List<SlotState> states, List<PairAudit> pairs)
    {
        const int columns = 2;
        const int cellWidth = 700;
        const int cellHeight = 275;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, 8 * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            foreach (PairAudit pair in pairs)
            {
                int index = pair.FirstSlot;
                int column = index % columns;
                int row = index / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                string actual = pair.ActualDelta.HasValue ? F(pair.ActualDelta.Value) : "n/a";
                graphics.DrawString("slot " + index.ToString("00") + " -> " + (index + 1).ToString("00") + " | expected 11.25 | actual " + actual + " | " + pair.Status, strong, Brushes.Black, cell.X + 10, cell.Y + 8);
                DrawSlot(graphics, states[index], new Rectangle(cell.X + 35, cell.Y + 37, CanvasSize, CanvasSize), strong);
                DrawSlot(graphics, states[index + 1], new Rectangle(cell.X + 475, cell.Y + 37, CanvasSize, CanvasSize), strong);
                string similarity = pair.Similarity.HasValue ? F(pair.Similarity.Value) : "n/a";
                string scale = pair.ScaleMismatch.HasValue ? F(pair.ScaleMismatch.Value) + "%" : "n/a";
                string center = pair.CentroidShift.HasValue ? F(pair.CentroidShift.Value) + "px" : "n/a";
                graphics.DrawString("similarity " + similarity + " | max scale " + scale + " | centroid shift " + center, label, Brushes.Black, cell.X + 135, cell.Y + 235);
                using (Pen border = new Pen(PairStatusColor(pair.Status), 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(previewRoot, "hybrid_local_neighbor_review.png"));
        }
    }

    private static void DrawFull32Preview(string previewRoot, List<SlotState> states)
    {
        const int columns = 8;
        const int cellWidth = 250;
        const int cellHeight = 245;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, 4 * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 9, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 10, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(238, 238, 238));
            for (int direction = 0; direction < 32; direction++)
            {
                int sourceSlot;
                bool flip;
                ResolveGameMapping(direction, out sourceSlot, out flip);
                SlotState source = states[sourceSlot];
                int column = direction % columns;
                int row = direction / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Rectangle imageArea = new Rectangle(cell.X + 32, cell.Y + 4, CanvasSize, CanvasSize);
                DrawChecker(graphics, imageArea, 12);
                if (source.ImagePath == null)
                    DrawPlaceholder(graphics, imageArea, "NEED INPUT", strong);
                else
                {
                    using (Bitmap image = LoadArgb(source.ImagePath))
                    {
                        if (flip) image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        graphics.DrawImage(image, imageArea);
                    }
                }
                graphics.DrawString("dir " + direction.ToString("00") + "  " + F(NormalizeAngle(direction * StepAngle)) + " deg", strong, Brushes.Black, cell.X + 8, cell.Y + 194);
                graphics.DrawString("source " + sourceSlot.ToString("00") + " flipX=" + (flip ? "true" : "false"), label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush brush = new SolidBrush(StatusColor(source.Status)))
                    graphics.DrawString(ShortStatus(source.Status), label, brush, cell.X + 8, cell.Y + 229);
                using (Pen border = new Pen(StatusColor(source.Status), 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(previewRoot, "hybrid_local_full32_preview.png"));
        }
    }

    private static void DrawSlot06Comparison(string previewRoot, List<SlotState> states, string originalPath, string normalizedPath, double expectedLength)
    {
        string[] paths = { states[5].ImagePath, originalPath, normalizedPath, states[7].ImagePath };
        string[] titles = { "SLOT 05 / STEP1", "SLOT 06 / ORIGINAL", "SLOT 06 / NORMALIZED", "SLOT 07 / LOCAL" };
        double[] hints = { 33.75, 22.50, 22.50, 11.25 };
        using (Bitmap sheet = new Bitmap(1200, 430, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font title = new Font("Consolas", 12, FontStyle.Bold))
        using (Font decision = new Font("Consolas", 12, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            for (int i = 0; i < paths.Length; i++)
            {
                int x = i * 300;
                SpriteMetrics metrics = AnalyzeBitmap(paths[i], hints[i]);
                Rectangle imageArea = new Rectangle(x + 57, 42, CanvasSize, CanvasSize);
                DrawChecker(graphics, imageArea, 12);
                using (Bitmap image = LoadArgb(paths[i])) graphics.DrawImage(image, imageArea);
                graphics.DrawString(titles[i], title, Brushes.Black, x + 28, 12);
                graphics.DrawString("bbox " + metrics.Bounds.Width + "x" + metrics.Bounds.Height, label, Brushes.Black, x + 24, 238);
                graphics.DrawString("alpha " + metrics.AlphaPixelCount, label, Brushes.Black, x + 24, 259);
                graphics.DrawString("length ratio " + F(metrics.ProjectedLength / expectedLength), label, Brushes.Black, x + 24, 280);
                graphics.DrawString("baseline " + metrics.Baseline, label, Brushes.Black, x + 24, 301);
                graphics.DrawString("centroid " + F(metrics.CentroidX) + "," + F(metrics.CentroidY), label, Brushes.Black, x + 24, 322);
                using (Pen border = new Pen(i == 2 ? Color.SeaGreen : Color.Gray, i == 2 ? 4 : 2))
                    graphics.DrawRectangle(border, x + 1, 1, 297, 355);
            }
            graphics.DrawString("SELECTED FOR HYBRID: normalized scale. It reduces the size pop without changing pixels, heading, or baseline; orientation remains a separate FAIL.", decision, Brushes.SeaGreen, new RectangleF(22, 374, 1150, 45));
            SavePng(sheet, Path.Combine(previewRoot, "hybrid_local_slot06_comparison.png"));
        }
    }

    private static void DrawSlot091011Comparison(string projectRoot, string previewRoot, List<SlotState> states)
    {
        string optionA = ActivePath(projectRoot, "ae86_337_50.png");
        string optionB = Step1Path(projectRoot, "slot_10_337.50_candidate.png");
        string[] paths = { states[9].ImagePath, optionA, optionB, states[11].ImagePath };
        string[] titles = { "SLOT 09 / LOCAL", "SLOT 10 A / LOCAL", "SLOT 10 B / STEP1", "SLOT 11 / STEP1" };
        double[] hints = { 348.75, 337.50, 337.50, 326.25 };
        using (Bitmap sheet = new Bitmap(1200, 410, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font title = new Font("Consolas", 12, FontStyle.Bold))
        using (Font decision = new Font("Consolas", 12, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            for (int i = 0; i < paths.Length; i++)
            {
                int x = i * 300;
                SpriteMetrics metrics = AnalyzeBitmap(paths[i], hints[i]);
                Rectangle imageArea = new Rectangle(x + 57, 42, CanvasSize, CanvasSize);
                DrawChecker(graphics, imageArea, 12);
                using (Bitmap image = LoadArgb(paths[i])) graphics.DrawImage(image, imageArea);
                graphics.DrawString(titles[i], title, Brushes.Black, x + 32, 12);
                graphics.DrawString("PCA " + F(metrics.AxisAngle), label, Brushes.Black, x + 24, 240);
                graphics.DrawString("bbox " + metrics.Bounds.Width + "x" + metrics.Bounds.Height, label, Brushes.Black, x + 24, 261);
                graphics.DrawString("centroid " + F(metrics.CentroidX) + "," + F(metrics.CentroidY), label, Brushes.Black, x + 24, 282);
                graphics.DrawString(i == 1 ? "front lower-left: reject" : i == 2 ? "front lower-right: SELECT" : string.Empty, label, i == 2 ? Brushes.SeaGreen : Brushes.Firebrick, x + 24, 303);
                using (Pen border = new Pen(i == 2 ? Color.SeaGreen : Color.Gray, i == 2 ? 4 : 2))
                    graphics.DrawRectangle(border, x + 1, 1, 297, 335);
            }
            graphics.DrawString("SELECTED: option B. It preserves the required lower-right front and continues into slot 11; option A points to the wrong horizontal side.", decision, Brushes.SeaGreen, new RectangleF(22, 354, 1150, 45));
            SavePng(sheet, Path.Combine(previewRoot, "hybrid_local_slot09_10_11_comparison.png"));
        }
    }

    private static void WriteReport(string projectRoot, string outputRoot, List<SlotState> states, List<PairAudit> pairs, string slot06OriginalPath, string slot06NormalizedPath, double slot06ExpectedLength, Dictionary<string, HashSnapshot> hashesBefore, Dictionary<string, HashSnapshot> hashesAfter)
    {
        int pass = states.Count(s => s.Status == "PASS");
        int review = states.Count(s => s.Status == "REVIEW");
        int fail = states.Count(s => s.Status == "FAIL");
        int runtimePass = CountRuntimeStatus(states, "PASS");
        int runtimeReview = CountRuntimeStatus(states, "REVIEW");
        int runtimeFail = CountRuntimeStatus(states, "FAIL");
        double maxMeasuredJump = pairs.Select(p => p.ActualDelta.Value).Max();
        double maxScale = states.Select(s => Math.Abs(s.ScaleDifferencePercent)).Max();
        SpriteMetrics slot06Original = AnalyzeBitmap(slot06OriginalPath, 22.50);
        SpriteMetrics slot06Normalized = AnalyzeBitmap(slot06NormalizedPath, 22.50);
        SpriteMetrics slot10A = AnalyzeBitmap(ActivePath(projectRoot, "ae86_337_50.png"), 337.50);
        SpriteMetrics slot10B = AnalyzeBitmap(Step1Path(projectRoot, "slot_10_337.50_candidate.png"), 337.50);
        bool ready = fail == 0 && review == 0 && pairs.All(p => p.Status == "PASS");

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1C Hybrid Local Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("A complete 17-source, local-only candidate set was exported without touching active gameplay assets. It contains **" + pass + " PASS**, **" + review + " REVIEW**, and **" + fail + " FAIL** sources after technical and manual front/rear review.");
        report.AppendLine();
        report.AppendLine("PCA is reported only as an axis signal. Manual hood/headlight versus hatch/taillight inspection overrides the PCA branch when the vehicle faces the wrong horizontal side.");
        report.AppendLine();
        report.AppendLine("## 2. Selected 17-source composition");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Source | PCA | BBox | Baseline | Scale | Visual result | Status |");
        report.AppendLine("|---:|---:|---|---:|---|---:|---:|---|---|");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            report.AppendLine("| " + state.Slot + " | " + F(state.TargetAngle) + " | " + state.SourceLabel + " `" + SourceFilenameForSlot(state.Slot) + "` | " + F(m.AxisAngle) + " | " + m.Bounds.Width + "x" + m.Bounds.Height + " | " + m.Baseline + " | " + F(state.ScaleDifferencePercent) + "% | " + state.VisualNotes + " | " + state.Status + " |");
        }
        report.AppendLine();
        report.AppendLine("## 3. Slot 06 original versus normalized");
        report.AppendLine();
        report.AppendLine("Only the complete vehicle raster was uniformly scaled with integer nearest-neighbor sampling and translated to baseline y=169. There was no rotation, shear, redraw, smoothing, or per-part resize.");
        report.AppendLine();
        report.AppendLine("| Variant | BBox | Alpha area | Length | Ratio to slots 05/07 target | Baseline | Centroid |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---|");
        report.AppendLine("| Original local | " + slot06Original.Bounds.Width + "x" + slot06Original.Bounds.Height + " | " + slot06Original.AlphaPixelCount + " | " + F(slot06Original.ProjectedLength) + " | " + F(slot06Original.ProjectedLength / slot06ExpectedLength) + " | " + slot06Original.Baseline + " | (" + F(slot06Original.CentroidX) + ", " + F(slot06Original.CentroidY) + ") |");
        report.AppendLine("| Scale-normalized | " + slot06Normalized.Bounds.Width + "x" + slot06Normalized.Bounds.Height + " | " + slot06Normalized.AlphaPixelCount + " | " + F(slot06Normalized.ProjectedLength) + " | " + F(slot06Normalized.ProjectedLength / slot06ExpectedLength) + " | " + slot06Normalized.Baseline + " | (" + F(slot06Normalized.CentroidX) + ", " + F(slot06Normalized.CentroidY) + ") |");
        report.AppendLine();
        report.AppendLine("**Visual recommendation:** select the normalized variant for the hybrid sheet because its whole-car size sits closer to slots 05 and 07 without appearing oversized. This does not repair the source's front/rear orientation: its taillights remain lower-right and its nose remains upper-left, so slot 06 is still FAIL.");
        report.AppendLine();
        report.AppendLine("## 4. Slot 10 option A versus B");
        report.AppendLine();
        report.AppendLine("Neither option was resized or rotated for this comparison.");
        report.AppendLine();
        report.AppendLine("| Option | Source | PCA | BBox | Centroid | Manual orientation | Decision |");
        report.AppendLine("|---|---|---:|---|---|---|---|");
        report.AppendLine("| A | local `ae86_337_50.png` | " + F(slot10A.AxisAngle) + " | " + slot10A.Bounds.Width + "x" + slot10A.Bounds.Height + " | (" + F(slot10A.CentroidX) + ", " + F(slot10A.CentroidY) + ") | Hood/headlights at lower-left, wrong side | Reject |");
        report.AppendLine("| B | Step 1 `slot_10_337.50_candidate.png` | " + F(slot10B.AxisAngle) + " | " + slot10B.Bounds.Width + "x" + slot10B.Bounds.Height + " | (" + F(slot10B.CentroidX) + ", " + F(slot10B.CentroidY) + ") | Hood/headlights at lower-right, continuous into slot 11 | **Selected** |");
        report.AppendLine();
        report.AppendLine("## 5. Required visual sequence review");
        report.AppendLine();
        report.AppendLine("- `00 -> 01 -> 02`: slot 01 is a real local intermediate but remains very close to Up; keep it REVIEW for gameplay comparison.");
        report.AppendLine("- `05 -> 06 -> 07 -> 08`: slot 07 is visibly distinct from Right through roof perspective, rear-hatch height, and wheel alignment. Slot 06 is horizontally reversed and also visually compresses the 05->06 step; FAIL.");
        report.AppendLine("- `08 -> 09 -> 10 -> 11`: slot 09 is visibly different from Right but points down-left instead of down-right. Slot 10 option B restores the required side, producing an obvious orientation reversal at 09->10; FAIL.");
        report.AppendLine("- `13 -> 14 -> 15 -> 16`: local slot 14 points down-left, not between the right-side slot 13 and slot 15. Slot 15 is mostly Down but its right bias is weak; slot 14 FAIL and slot 15 REVIEW.");
        report.AppendLine();
        report.AppendLine("## 6. Adjacent PCA and continuity audit");
        report.AppendLine();
        report.AppendLine("| Pair | Expected | PCA delta | Similarity | Scale mismatch | Centroid shift | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (PairAudit pair in pairs)
            report.AppendLine("| " + pair.FirstSlot + " -> " + pair.SecondSlot + " | 11.25 | " + F(pair.ActualDelta.Value) + " | " + F(pair.Similarity.Value) + " | " + F(pair.ScaleMismatch.Value) + "% | " + F(pair.CentroidShift.Value) + "px | " + pair.Status + " |");
        report.AppendLine();
        report.AppendLine("Maximum measured PCA jump is **" + F(maxMeasuredJump) + " degrees**; maximum absolute anchor-interpolated scale difference is **" + F(maxScale) + "%**. These figures do not override the manual orientation failures.");
        report.AppendLine();
        report.AppendLine("## 7. Technical image audit");
        report.AppendLine();
        report.AppendLine("All 17 selected PNGs are 186x186 RGBA, baseline y=169, zero edge contact, one connected alpha component, no partial-alpha halo, and no crop. Candidate images contain no checkerboard, labels, arrows, road, or shadow.");
        report.AppendLine();
        report.AppendLine("## 8. Full 32-direction runtime preview");
        report.AppendLine();
        report.AppendLine("The exact existing flipX mapping was used without code changes. Runtime propagation gives **" + runtimePass + " PASS**, **" + runtimeReview + " REVIEW**, and **" + runtimeFail + " FAIL** directions.");
        report.AppendLine();
        report.AppendLine("## 9. Exact files created");
        report.AppendLine();
        List<string> files = new List<string>();
        for (int slot = 0; slot <= 16; slot++)
        {
            double angle = NormalizeAngle(90.0 - slot * StepAngle);
            string filename = slot == 6 ? "slot_06_22.50_original_local.png" : "slot_" + slot.ToString("00") + "_" + AngleToken(angle) + "_hybrid.png";
            files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/" + filename);
        }
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_06_22.50_scale_normalized.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_17_contact_sheet.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_full32_preview.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_neighbor_review.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_slot06_comparison.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_slot09_10_11_comparison.png");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/hybrid_local_metrics.csv");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/hybrid_local_report.md");
        files.Add("Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/Tools/AE86Step1CHybridBuilder.cs");
        foreach (string file in files) report.AppendLine("- `" + file + "`");
        report.AppendLine();
        report.AppendLine("## 10. Protected-file SHA-256 proof");
        report.AppendLine();
        report.AppendLine("| Protected group | Count | Before | After | Identical |");
        report.AppendLine("|---|---:|---|---|---|");
        foreach (string key in hashesBefore.Keys.OrderBy(k => k, StringComparer.InvariantCultureIgnoreCase))
        {
            bool identical = hashesBefore[key].Count == hashesAfter[key].Count && hashesBefore[key].Hash == hashesAfter[key].Hash;
            report.AppendLine("| " + key + " | " + hashesBefore[key].Count + " | `" + hashesBefore[key].Hash + "` | `" + hashesAfter[key].Hash + "` | " + (identical ? "YES" : "NO") + " |");
        }
        report.AppendLine();
        report.AppendLine("Active Production32 PNGs, Assets .meta/.cs files, prefabs, and scenes remained read-only. No GUID, vehicle handling value, visualSteerLeadAngle, code, prefab, or scene was changed.");
        report.AppendLine();
        report.AppendLine("## 11. Final decision");
        report.AppendLine();
        report.AppendLine(ready ? "All local candidates form a technically and visually continuous sequence for non-destructive Unity testing." : "The 17 files are complete and can be inspected, but wrong-side orientations at slots 06, 09, and 14 plus REVIEW states at 01, 07, and 15 prevent approval for Unity runtime testing as a coherent 32-direction set.");
        report.AppendLine();
        report.Append(ready ? "READY_FOR_UNITY_TEST" : "NOT_READY");
        File.WriteAllText(Path.Combine(outputRoot, "Reports", "hybrid_local_report.md"), report.ToString(), new UTF8Encoding(false));
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
        double sumX = 0.0;
        double sumY = 0.0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                int alpha = bitmap.GetPixel(x, y).A;
                if (alpha == 0) continue;
                pixels.Add(new Point(x, y));
                sumX += x;
                sumY += y;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                if (alpha < 255) partial++;
                if (x == 0 || y == 0 || x == bitmap.Width - 1 || y == bitmap.Height - 1) edge++;
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
                        if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || visited[nx, ny]) continue;
                        if (bitmap.GetPixel(nx, ny).A == 0) continue;
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
        string production = Path.Combine(assets, "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32");
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        groups["active Production32 PNG"] = Directory.GetFiles(production, "*.png", SearchOption.TopDirectoryOnly).ToList();
        groups["all Assets .meta"] = Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories).ToList();
        groups["all Assets code"] = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories).ToList();
        groups["all prefabs"] = Directory.GetFiles(assets, "*.prefab", SearchOption.AllDirectories).ToList();
        groups["all scenes"] = Directory.GetFiles(assets, "*.unity", SearchOption.AllDirectories).ToList();

        Dictionary<string, HashSnapshot> result = new Dictionary<string, HashSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> group in groups)
        {
            List<string> files = group.Value.OrderBy(p => p, StringComparer.InvariantCultureIgnoreCase).ToList();
            StringBuilder joined = new StringBuilder();
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0) joined.Append('\n');
                joined.Append(Relative(projectRoot, files[i])).Append('|').Append(FileSha256(files[i]));
            }
            result[group.Key] = new HashSnapshot { Count = files.Count, Hash = Sha256(Encoding.UTF8.GetBytes(joined.ToString())) };
        }
        return result;
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

    private static int CountRuntimeStatus(List<SlotState> states, string status)
    {
        int count = 0;
        for (int direction = 0; direction < 32; direction++)
        {
            int source;
            bool flip;
            ResolveGameMapping(direction, out source, out flip);
            if (states[source].Status == status) count++;
        }
        return count;
    }

    private static void DrawSlot(Graphics graphics, SlotState state, Rectangle target, Font font)
    {
        DrawChecker(graphics, target, 12);
        if (state.ImagePath == null)
        {
            DrawPlaceholder(graphics, target, "NEED INPUT", font);
            return;
        }
        using (Bitmap image = LoadArgb(state.ImagePath)) graphics.DrawImage(image, target);
    }

    private static void DrawPlaceholder(Graphics graphics, Rectangle target, string text, Font font)
    {
        using (Brush fill = new SolidBrush(Color.FromArgb(238, 225, 225)))
        using (Pen cross = new Pen(Color.Firebrick, 5))
        {
            graphics.FillRectangle(fill, target);
            graphics.DrawRectangle(Pens.Firebrick, target);
            graphics.DrawLine(cross, target.Left + 18, target.Top + 18, target.Right - 18, target.Bottom - 18);
            graphics.DrawLine(cross, target.Right - 18, target.Top + 18, target.Left + 18, target.Bottom - 18);
            SizeF size = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, Brushes.Firebrick, target.X + (target.Width - size.Width) / 2f, target.Y + (target.Height - size.Height) / 2f);
        }
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

    private static void PrepareReviewGraphics(Graphics graphics, Color background)
    {
        graphics.Clear(background);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
    }

    private static Color StatusColor(string status)
    {
        if (status == "PASS") return Color.SeaGreen;
        if (status == "REVIEW") return Color.DarkOrange;
        return Color.Firebrick;
    }

    private static Color PairStatusColor(string status)
    {
        if (status == "PASS") return Color.SeaGreen;
        if (status == "REVIEW SOURCE" || status == "SCALE >3%") return Color.DarkOrange;
        return Color.Firebrick;
    }

    private static string ShortStatus(string status)
    {
        return status == "NEED_EXTERNAL_INPUT" ? "NEED INPUT" : status;
    }

    private static string SourceLabelForSlot(int slot)
    {
        if (slot == 0 || slot == 8 || slot == 16) return "Active anchor";
        if (slot == 2 || slot == 3 || slot == 4 || slot == 5 || (slot >= 10 && slot <= 13)) return "Step1";
        if (slot == 6) return "Local normalized";
        return "Original local";
    }

    private static string SourceFilenameForSlot(int slot)
    {
        switch (slot)
        {
            case 0: return "ae86_090_00_up.png";
            case 1: return "ae86_078_75.png";
            case 2: return "slot_02_67.50_candidate.png";
            case 3: return "slot_03_56.25_candidate.png";
            case 4: return "slot_04_45.00_candidate.png";
            case 5: return "slot_05_33.75_candidate.png";
            case 6: return "ae86_022_50.png";
            case 7: return "ae86_011_25.png";
            case 8: return "ae86_000_00_right.png";
            case 9: return "ae86_348_75.png";
            case 10: return "slot_10_337.50_candidate.png";
            case 11: return "slot_11_326.25_candidate.png";
            case 12: return "slot_12_315.00_candidate.png";
            case 13: return "slot_13_303.75_candidate.png";
            case 14: return "ae86_292_50.png";
            case 15: return "ae86_281_25.png";
            case 16: return "ae86_270_00_down.png";
            default: throw new ArgumentOutOfRangeException("slot");
        }
    }

    private static string SourceMethodForSlot(int slot)
    {
        if (slot == 0 || slot == 8 || slot == 16) return "Read-only active anchor copy";
        if (slot == 2 || slot == 3 || slot == 4 || slot == 5 || (slot >= 10 && slot <= 13)) return "Read-only Step 1 candidate copy";
        if (slot == 6) return "Local Production32 copy with uniform nearest-neighbor scale";
        return "Read-only local Production32 copy";
    }

    private static string ManualVisualStatus(int slot)
    {
        if (slot == 6 || slot == 9 || slot == 14) return "FAIL";
        if (slot == 1 || slot == 15) return "REVIEW";
        return "PASS";
    }

    private static string ManualVisualNotes(int slot)
    {
        if (slot == 1) return "Slight local intermediate is real but remains compressed toward the Up anchor.";
        if (slot == 6) return "Taillights sit lower-right and the nose points upper-left; wrong horizontal side and compressed against slot 05.";
        if (slot == 7) return "Distinct from Right through roof perspective, rear-hatch height, and wheel alignment while remaining shallow.";
        if (slot == 9) return "Distinct from Right, but hood/headlights point lower-left instead of the required lower-right.";
        if (slot == 10) return "Step 1 option B selected: hood/headlights point lower-right and continue smoothly toward slot 11.";
        if (slot == 14) return "Original local source points lower-left and therefore does not lie between right-side slots 13 and 15.";
        if (slot == 15) return "Mostly Down and technically clean, but the intended small Right bias is weak.";
        return "Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames.";
    }

    private static string ManualFrontRearCheck(int slot)
    {
        if (slot == 6) return "FAIL: front upper-left; red-taillight rear lower-right.";
        if (slot == 9) return "FAIL: headlight front lower-left; rear upper-right.";
        if (slot == 14) return "FAIL: headlight front lower-left; rear upper-right.";
        if (slot == 0) return "PASS: front top, rear bottom.";
        if (slot == 8) return "PASS: front right, rear left.";
        if (slot == 16) return "PASS: front bottom, rear top.";
        if (slot < 8) return "PASS: front/rear landmarks inspected for the Up-to-Right sequence.";
        return "PASS: front/rear landmarks inspected for the Right-to-Down sequence.";
    }

    private static string ActivePath(string projectRoot, string filename)
    {
        return Path.Combine(projectRoot, "Assets", "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32", filename);
    }

    private static string Step1Path(string projectRoot, string filename)
    {
        return Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates", "PNG", filename);
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

    private static double ClockwiseDelta(double previous, double current)
    {
        return NormalizeAngle(previous - current);
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

    private static string Nullable(double? value)
    {
        return value.HasValue ? F(value.Value) : string.Empty;
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
        using (Bitmap source = new Bitmap(path)) return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
    }

    private static void CopyPng(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Required local AE86 source was not found.", sourcePath);

        using (Bitmap source = LoadArgb(sourcePath))
        {
            if (source.Width != CanvasSize || source.Height != CanvasSize)
                throw new InvalidDataException("Expected a 186x186 source image: " + sourcePath);

            SavePng(source, destinationPath);
        }
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
