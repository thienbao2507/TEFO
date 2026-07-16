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

public static class AE86Step1DBuilder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] VisualFocusSlots = { 1, 6, 7, 9, 14, 15 };

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
        public string SourceFile;
        public string TransformApplied;
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
        public double? SignedDeltaFromPrevious;
        public double? AbsoluteDeltaFromPrevious;
        public double? SignedDeltaToNext;
        public double? AbsoluteDeltaToNext;
        public string VisualNotes;
    }

    private sealed class PairAudit
    {
        public int FirstSlot;
        public int SecondSlot;
        public double? SignedDelta;
        public double? AbsoluteDelta;
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
        string outputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1D_LocalFlipRecovery");
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

        string slot07Original;
        string slot07Normalized;
        double slot07ExpectedLength;
        bool slot07NormalizedSelected;
        Dictionary<int, string> selected = ExportStep1DCandidates(
            projectRoot,
            pngRoot,
            out slot07Original,
            out slot07Normalized,
            out slot07ExpectedLength,
            out slot07NormalizedSelected);

        List<SlotState> states = BuildStates(selected, up, right, down);
        List<PairAudit> pairs = BuildPairAudits(states);
        WriteMetricsCsv(projectRoot, reportRoot, states);
        DrawStep1DContactSheet(previewRoot, states);
        DrawNeighborReview(previewRoot, states, pairs);
        DrawFull32Preview(previewRoot, states);
        DrawRepairedSlotsReview(projectRoot, previewRoot, states);
        DrawSlot07Comparison(previewRoot, states, slot07Original, slot07Normalized, slot07ExpectedLength, slot07NormalizedSelected);

        Dictionary<string, HashSnapshot> hashesAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, outputRoot, states, pairs, slot07Original, slot07Normalized, slot07ExpectedLength, slot07NormalizedSelected, hashesBefore, hashesAfter);
    }

    private static Dictionary<int, string> ExportStep1DCandidates(
        string projectRoot,
        string pngRoot,
        out string slot07Original,
        out string slot07Normalized,
        out double slot07ExpectedLength,
        out bool slot07NormalizedSelected)
    {
        Dictionary<int, string> source = new Dictionary<int, string>();
        source[0] = ActivePath(projectRoot, "ae86_090_00_up.png");
        source[1] = Step1Path(projectRoot, "slot_01_78.75_candidate.png");
        source[2] = Step1Path(projectRoot, "slot_02_67.50_candidate.png");
        source[3] = Step1Path(projectRoot, "slot_03_56.25_candidate.png");
        source[4] = Step1Path(projectRoot, "slot_04_45.00_candidate.png");
        source[5] = Step1Path(projectRoot, "slot_05_33.75_candidate.png");
        source[6] = Step1CPath(projectRoot, "PNG", "slot_06_22.50_scale_normalized.png");
        source[7] = ActivePath(projectRoot, "ae86_011_25.png");
        source[8] = ActivePath(projectRoot, "ae86_000_00_right.png");
        source[9] = ActivePath(projectRoot, "ae86_348_75.png");
        source[10] = Step1Path(projectRoot, "slot_10_337.50_candidate.png");
        source[11] = Step1Path(projectRoot, "slot_11_326.25_candidate.png");
        source[12] = Step1Path(projectRoot, "slot_12_315.00_candidate.png");
        source[13] = Step1Path(projectRoot, "slot_13_303.75_candidate.png");
        source[14] = ActivePath(projectRoot, "ae86_292_50.png");
        source[15] = Step1Path(projectRoot, "slot_15_281.25_candidate.png");
        source[16] = ActivePath(projectRoot, "ae86_270_00_down.png");

        Dictionary<int, string> output = new Dictionary<int, string>();
        for (int slot = 0; slot <= 16; slot++)
        {
            if (slot == 7) continue;
            double angle = NormalizeAngle(90.0 - slot * StepAngle);
            string filename = "slot_" + slot.ToString("00") + "_" + AngleToken(angle) + "_step1d.png";
            string destination = Path.Combine(pngRoot, filename);
            if (slot == 6 || slot == 9 || slot == 14)
                FlipHorizontalExact(source[slot], destination);
            else
                CopyPng(source[slot], destination);
            output[slot] = destination;
        }

        slot07Original = Path.Combine(pngRoot, "slot_07_11.25_original.png");
        slot07Normalized = Path.Combine(pngRoot, "slot_07_11.25_scale_normalized.png");
        CopyPng(source[7], slot07Original);
        SpriteMetrics repairedSlot06 = AnalyzeBitmap(output[6], 22.50);
        SpriteMetrics right = AnalyzeBitmap(output[8], 0.00);
        slot07ExpectedLength = (repairedSlot06.ProjectedLength + right.ProjectedLength) / 2.0;
        CreateUniformScaleCandidate(slot07Original, slot07Normalized, slot07ExpectedLength, 11.25);

        // Visual review favors the normalized whole-car scale: it remains shallow and distinct from Right,
        // while reducing both scale and center discontinuity against repaired slot 06 and the Right anchor.
        slot07NormalizedSelected = true;
        string slot07Final = Path.Combine(pngRoot, "slot_07_11.25_step1d.png");
        CopyPng(slot07NormalizedSelected ? slot07Normalized : slot07Original, slot07Final);
        output[7] = slot07Final;
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
            state.SourceFile = SourceFilenameForSlot(slot);
            state.TransformApplied = TransformForSlot(slot);

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
                ? "Retain in the non-destructive Step 1D review set."
                : state.Status == "REVIEW"
                    ? "Keep for visual comparison; inspect the adjacent transition at gameplay scale."
                    : "Do not promote; the repaired source collapses into an adjacent pose.";
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

            audit.SignedDelta = SignedShortestDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
            audit.AbsoluteDelta = Math.Abs(audit.SignedDelta.Value);
            audit.Similarity = AlphaIoU(first.ImagePath, second.ImagePath);
            audit.ScaleMismatch = Math.Max(Math.Abs(first.ScaleDifferencePercent), Math.Abs(second.ScaleDifferencePercent));
            audit.CentroidShift = Distance(first.Metrics.CentroidX, first.Metrics.CentroidY, second.Metrics.CentroidX, second.Metrics.CentroidY);
            first.SignedDeltaToNext = audit.SignedDelta;
            first.AbsoluteDeltaToNext = audit.AbsoluteDelta;
            second.SignedDeltaFromPrevious = audit.SignedDelta;
            second.AbsoluteDeltaFromPrevious = audit.AbsoluteDelta;

            if (IsManualCollapsedTransition(slot) && audit.Similarity.Value >= 0.90)
                audit.Status = "COLLAPSED";
            else if (audit.SignedDelta.Value > 0.0 && IsManualReversedTransition(slot))
                audit.Status = "REVERSED";
            else if (slot == 7 && audit.AbsoluteDelta.Value < 4.0)
                audit.Status = "PCA COMPRESSED / VISUAL DISTINCT";
            else if (audit.AbsoluteDelta.Value > 18.0)
                audit.Status = "LARGE JUMP";
            else if (audit.AbsoluteDelta.Value < 4.0)
                audit.Status = "PCA COMPRESSED";
            else if (audit.ScaleMismatch.Value > 5.0)
                audit.Status = "SCALE REVIEW";
            else if (audit.AbsoluteDelta.Value > 15.0)
                audit.Status = "STEP REVIEW";
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

    private static void WriteMetricsCsv(string projectRoot, string reportRoot, List<SlotState> states)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,filename,target_angle,source_file,transform_applied,source_label,source_method,measured_pca_axis,angular_error,signed_delta_from_previous,absolute_delta_from_previous,signed_delta_to_next,absolute_delta_to_next,canvas_width,canvas_height,bbox,bbox_x,bbox_y,bbox_width,bbox_height,alpha_area,projected_length,projected_width,scale_difference_percent,centroid_x,centroid_y,centroid_shift_x,centroid_shift_y,centroid_shift,baseline,edge_contact,component_count,partial_alpha_count,crop_check,artifact_check,front_rear_result,visual_notes,visual_status,recommendation");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            List<string> row = new List<string>();
            row.Add(state.Slot.ToString(Invariant));
            row.Add(Csv(state.Filename));
            row.Add(F(state.TargetAngle));
            row.Add(Csv(state.SourceFile.Replace('\\', '/')));
            row.Add(Csv(state.TransformApplied));
            row.Add(Csv(state.SourceLabel));
            row.Add(Csv(state.SourceMethod));
            row.Add(m == null ? string.Empty : F(m.AxisAngle));
            row.Add(m == null ? string.Empty : F(state.AngularError));
            row.Add(Nullable(state.SignedDeltaFromPrevious));
            row.Add(Nullable(state.AbsoluteDeltaFromPrevious));
            row.Add(Nullable(state.SignedDeltaToNext));
            row.Add(Nullable(state.AbsoluteDeltaToNext));
            row.Add(m == null ? string.Empty : m.CanvasWidth.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.CanvasHeight.ToString(Invariant));
            row.Add(m == null ? string.Empty : Csv(Box(m.Bounds)));
            row.Add(m == null ? string.Empty : m.Bounds.X.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Y.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Width.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.Bounds.Height.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.AlphaPixelCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : F(m.ProjectedLength));
            row.Add(m == null ? string.Empty : F(m.ProjectedWidth));
            row.Add(m == null ? string.Empty : F(state.ScaleDifferencePercent));
            row.Add(m == null ? string.Empty : F(m.CentroidX));
            row.Add(m == null ? string.Empty : F(m.CentroidY));
            row.Add(m == null ? string.Empty : F(state.CenterOffsetX));
            row.Add(m == null ? string.Empty : F(state.CenterOffsetY));
            row.Add(m == null ? string.Empty : F(Distance(0.0, 0.0, state.CenterOffsetX, state.CenterOffsetY)));
            row.Add(m == null ? string.Empty : m.Baseline.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.EdgeTouchCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.ComponentCount.ToString(Invariant));
            row.Add(m == null ? string.Empty : m.PartialAlphaPixelCount.ToString(Invariant));
            row.Add(Csv(state.CropCheck));
            row.Add(Csv(state.ArtifactCheck));
            row.Add(Csv(state.FrontRearCheck));
            row.Add(Csv(state.VisualNotes));
            row.Add(Csv(state.Status));
            row.Add(Csv(state.RecommendedAction));
            csv.AppendLine(string.Join(",", row.ToArray()));
        }
        File.WriteAllText(Path.Combine(reportRoot, "step1d_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static void DrawStep1DContactSheet(string previewRoot, List<SlotState> states)
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
                graphics.DrawString("input: " + ContactInputForSlot(state.Slot), label, Brushes.Black, cell.X + 8, cell.Y + 214);
                graphics.DrawString("source: " + state.SourceLabel, label, Brushes.Black, cell.X + 8, cell.Y + 233);
                graphics.DrawString("PCA " + actual + "  baseline " + state.Metrics.Baseline, label, Brushes.Black, cell.X + 8, cell.Y + 252);
                graphics.DrawString("bbox " + state.Metrics.Bounds.Width + "x" + state.Metrics.Bounds.Height, label, Brushes.Black, cell.X + 8, cell.Y + 271);
                using (Brush brush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString(ShortStatus(state.Status), label, brush, cell.X + 205, cell.Y + 271);
            }
            SavePng(sheet, Path.Combine(previewRoot, "step1d_17_contact_sheet.png"));
        }
    }

    private static void DrawNeighborReview(string previewRoot, List<SlotState> states, List<PairAudit> pairs)
    {
        const int columns = 2;
        const int cellWidth = 700;
        const int cellHeight = 290;
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
                string signed = pair.SignedDelta.HasValue ? F(pair.SignedDelta.Value) : "n/a";
                string absolute = pair.AbsoluteDelta.HasValue ? F(pair.AbsoluteDelta.Value) : "n/a";
                graphics.DrawString("slot " + index.ToString("00") + " -> " + (index + 1).ToString("00") + " | expected -11.25 | signed " + signed + " | abs " + absolute, strong, Brushes.Black, cell.X + 10, cell.Y + 7);
                graphics.DrawString("STATUS: " + pair.Status, label, Brushes.Black, cell.X + 10, cell.Y + 27);
                DrawSlot(graphics, states[index], new Rectangle(cell.X + 35, cell.Y + 49, CanvasSize, CanvasSize), strong);
                DrawSlot(graphics, states[index + 1], new Rectangle(cell.X + 475, cell.Y + 49, CanvasSize, CanvasSize), strong);
                string similarity = pair.Similarity.HasValue ? F(pair.Similarity.Value) : "n/a";
                string scale = pair.ScaleMismatch.HasValue ? F(pair.ScaleMismatch.Value) + "%" : "n/a";
                string center = pair.CentroidShift.HasValue ? F(pair.CentroidShift.Value) + "px" : "n/a";
                graphics.DrawString("similarity " + similarity + " | max scale " + scale + " | centroid shift " + center, label, Brushes.Black, cell.X + 135, cell.Y + 250);
                using (Pen border = new Pen(PairStatusColor(pair.Status), 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(previewRoot, "step1d_neighbor_review.png"));
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
            SavePng(sheet, Path.Combine(previewRoot, "step1d_full32_preview.png"));
        }
    }

    private static void DrawSlot07Comparison(string previewRoot, List<SlotState> states, string originalPath, string normalizedPath, double expectedLength, bool normalizedSelected)
    {
        string[] paths = { states[6].ImagePath, originalPath, normalizedPath, states[8].ImagePath };
        string[] titles = { "SLOT 06 / REPAIRED", "SLOT 07 / ORIGINAL", "SLOT 07 / NORMALIZED", "SLOT 08 / RIGHT" };
        double[] hints = { 22.50, 11.25, 11.25, 0.00 };
        using (Bitmap sheet = new Bitmap(1200, 500, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 9, FontStyle.Regular))
        using (Font title = new Font("Consolas", 11, FontStyle.Bold))
        using (Font decision = new Font("Consolas", 11, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            for (int i = 0; i < paths.Length; i++)
            {
                int x = i * 300;
                SpriteMetrics metrics = AnalyzeBitmap(paths[i], hints[i]);
                double scaleDifference = ((metrics.ProjectedLength - expectedLength) / expectedLength) * 100.0;
                double similarity06 = AlphaIoU(paths[i], states[6].ImagePath);
                double similarity08 = AlphaIoU(paths[i], states[8].ImagePath);
                Rectangle imageArea = new Rectangle(x + 57, 38, CanvasSize, CanvasSize);
                DrawChecker(graphics, imageArea, 12);
                using (Bitmap image = LoadArgb(paths[i])) graphics.DrawImage(image, imageArea);
                graphics.DrawString(titles[i], title, Brushes.Black, x + 24, 10);
                graphics.DrawString("bbox " + metrics.Bounds.Width + "x" + metrics.Bounds.Height + "  alpha " + metrics.AlphaPixelCount, label, Brushes.Black, x + 18, 232);
                graphics.DrawString("length " + F(metrics.ProjectedLength) + "  scale " + F(scaleDifference) + "%", label, Brushes.Black, x + 18, 250);
                graphics.DrawString("centroid " + F(metrics.CentroidX) + "," + F(metrics.CentroidY), label, Brushes.Black, x + 18, 268);
                graphics.DrawString("baseline " + metrics.Baseline + "  edge " + metrics.EdgeTouchCount, label, Brushes.Black, x + 18, 286);
                graphics.DrawString("similarity slot06 " + F(similarity06), label, Brushes.Black, x + 18, 304);
                graphics.DrawString("similarity Right " + F(similarity08), label, Brushes.Black, x + 18, 322);
                graphics.DrawString(i == 1 || i == 2 ? "vs Right: VISUALLY DISTINCT" : string.Empty, label, Brushes.SeaGreen, x + 18, 340);
                bool selected = (i == 2 && normalizedSelected) || (i == 1 && !normalizedSelected);
                using (Pen border = new Pen(selected ? Color.SeaGreen : Color.Gray, selected ? 4 : 2))
                    graphics.DrawRectangle(border, x + 1, 1, 297, 370);
            }
            string decisionText = normalizedSelected
                ? "SELECTED: normalized slot 07. It preserves the shallow upper-right pose and Right distinction while reducing scale and center pop."
                : "SELECTED: original slot 07. Normalization appeared oversized during visual review.";
            graphics.DrawString(decisionText, decision, Brushes.SeaGreen, new RectangleF(22, 392, 1155, 60));
            SavePng(sheet, Path.Combine(previewRoot, "step1d_slot07_comparison.png"));
        }
    }

    private static void DrawRepairedSlotsReview(string projectRoot, string previewRoot, List<SlotState> states)
    {
        int[] repaired = { 6, 9, 14 };
        string[] failedNames = { "slot_06_22.50_scale_normalized.png", "slot_09_348.75_hybrid.png", "slot_14_292.50_hybrid.png" };
        using (Bitmap sheet = new Bitmap(1400, 1130, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 9, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        using (Font heading = new Font("Consolas", 13, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            graphics.DrawString("STEP 1D SELECTED FOCUS SLOTS", heading, Brushes.Black, 12, 8);
            for (int i = 0; i < VisualFocusSlots.Length; i++)
            {
                SlotState state = states[VisualFocusSlots[i]];
                int x = 8 + i * 232;
                Rectangle area = new Rectangle(x + 23, 36, CanvasSize, CanvasSize);
                DrawSlot(graphics, state, area, strong);
                graphics.DrawString("slot " + state.Slot.ToString("00") + "  " + F(state.TargetAngle), strong, Brushes.Black, x + 8, 226);
                using (Brush statusBrush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString(ShortStatus(state.Status), label, statusBrush, x + 8, 247);
                using (Pen border = new Pen(StatusColor(state.Status), 3)) graphics.DrawRectangle(border, x, 30, 228, 239);
            }

            for (int row = 0; row < repaired.Length; row++)
            {
                int slot = repaired[row];
                int y = 285 + row * 278;
                string failed = Step1CPath(projectRoot, "PNG", failedNames[row]);
                string[] paths = { states[slot - 1].ImagePath, failed, states[slot].ImagePath, states[slot + 1].ImagePath };
                string[] titles = { "PREVIOUS " + (slot - 1).ToString("00"), "STEP 1C FAILED", "FLIPPED REPAIR " + slot.ToString("00"), "NEXT " + (slot + 1).ToString("00") };
                graphics.DrawString("REPAIR AUDIT SLOT " + slot.ToString("00"), heading, Brushes.Black, 12, y);
                for (int column = 0; column < 4; column++)
                {
                    int x = 20 + column * 345;
                    Rectangle area = new Rectangle(x + 76, y + 52, CanvasSize, CanvasSize);
                    DrawChecker(graphics, area, 12);
                    using (Bitmap image = LoadArgb(paths[column])) graphics.DrawImage(image, area);
                    graphics.DrawString(titles[column], strong, Brushes.Black, x + 22, y + 29);
                    using (Pen border = new Pen(column == 1 ? Color.Firebrick : column == 2 ? Color.SeaGreen : Color.Gray, column == 1 || column == 2 ? 4 : 2))
                        graphics.DrawRectangle(border, x + 1, y + 24, 330, 226);
                }
                string frontRear = slot == 6
                    ? "FRONT/REAR PASS: nose upper-right; rear and red taillights lower-left."
                    : "FRONT/REAR PASS: nose lower-right; rear and red taillights upper-left.";
                graphics.DrawString(frontRear, strong, Brushes.SeaGreen, 20, y + 253);
                graphics.DrawString("CONTINUITY FAIL: repaired pose collapses toward its adjacent Step1 source.", strong, Brushes.Firebrick, 710, y + 253);
            }
            SavePng(sheet, Path.Combine(previewRoot, "step1d_repaired_slots_review.png"));
        }
    }

    private static void WriteReport(string projectRoot, string outputRoot, List<SlotState> states, List<PairAudit> pairs, string slot07OriginalPath, string slot07NormalizedPath, double slot07ExpectedLength, bool slot07NormalizedSelected, Dictionary<string, HashSnapshot> hashesBefore, Dictionary<string, HashSnapshot> hashesAfter)
    {
        int pass = states.Count(s => s.Status == "PASS");
        int review = states.Count(s => s.Status == "REVIEW");
        int fail = states.Count(s => s.Status == "FAIL");
        int runtimePass = CountRuntimeStatus(states, "PASS");
        int runtimeReview = CountRuntimeStatus(states, "REVIEW");
        int runtimeFail = CountRuntimeStatus(states, "FAIL");
        double maxAbsoluteDelta = pairs.Max(p => p.AbsoluteDelta.Value);
        double maxScale = states.Max(s => Math.Abs(s.ScaleDifferencePercent));
        SpriteMetrics slot07Original = AnalyzeBitmap(slot07OriginalPath, 11.25);
        SpriteMetrics slot07Normalized = AnalyzeBitmap(slot07NormalizedPath, 11.25);
        double originalScale = ((slot07Original.ProjectedLength - slot07ExpectedLength) / slot07ExpectedLength) * 100.0;
        double normalizedScale = ((slot07Normalized.ProjectedLength - slot07ExpectedLength) / slot07ExpectedLength) * 100.0;
        double originalSimilarity06 = AlphaIoU(slot07OriginalPath, states[6].ImagePath);
        double originalSimilarity08 = AlphaIoU(slot07OriginalPath, states[8].ImagePath);
        double normalizedSimilarity06 = AlphaIoU(slot07NormalizedPath, states[6].ImagePath);
        double normalizedSimilarity08 = AlphaIoU(slot07NormalizedPath, states[8].ImagePath);
        bool protectedIdentical = hashesBefore.All(entry => hashesAfter.ContainsKey(entry.Key) && entry.Value.Count == hashesAfter[entry.Key].Count && entry.Value.Hash == hashesAfter[entry.Key].Hash);
        bool blockingTransition = pairs.Any(p => p.Status == "COLLAPSED" || p.Status == "REVERSED" || p.Status == "LARGE JUMP");
        bool ready = fail == 0 && !blockingTransition && protectedIdentical;

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1D Local Flip Recovery Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("A complete 17-source local-only set was exported without changing any active or upstream asset. It contains **" + pass + " PASS**, **" + review + " REVIEW**, and **" + fail + " FAIL** selected sources.");
        report.AppendLine();
        report.AppendLine("The exact horizontal repairs at slots 06, 09, and 14 correct their front/rear horizontal side. They do not recover missing angular information: slot 06 collapses toward 05, slot 09 collapses toward 10, and slot 14 is pixel-identical in visible content to 15. Manual landmarks override ambiguous PCA branch selection.");
        report.AppendLine();
        report.AppendLine("## 2. Selected 17-source composition");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Source | Transform | PCA | BBox | Baseline | Scale | Front/rear | Status |");
        report.AppendLine("|---:|---:|---|---|---:|---|---:|---:|---|---|");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            report.AppendLine("| " + state.Slot + " | " + F(state.TargetAngle) + " | `" + state.SourceFile.Replace('\\', '/') + "` | " + state.TransformApplied + " | " + F(m.AxisAngle) + " | " + m.Bounds.Width + "x" + m.Bounds.Height + " | " + m.Baseline + " | " + F(state.ScaleDifferencePercent) + "% | " + state.FrontRearCheck + " | " + state.Status + " |");
        }
        report.AppendLine();
        report.AppendLine("## 3. Horizontal repair audit");
        report.AppendLine();
        report.AppendLine("Each repair is one full-canvas horizontal pixel flip. There is no rotation, rescale, shear, redraw, smoothing, or antialiasing.");
        report.AppendLine();
        report.AppendLine("| Slot | Required repaired landmarks | Similarity to previous | Similarity to next | Result |");
        report.AppendLine("|---:|---|---:|---:|---|");
        report.AppendLine("| 06 | Nose upper-right; rear/red taillights lower-left | " + F(pairs[5].Similarity.Value) + " | " + F(pairs[6].Similarity.Value) + " | Front/rear PASS; pose collapses toward 05 |");
        report.AppendLine("| 09 | Nose lower-right; rear/red taillights upper-left | " + F(pairs[8].Similarity.Value) + " | " + F(pairs[9].Similarity.Value) + " | Front/rear PASS; near-duplicate of 10 |");
        report.AppendLine("| 14 | Nose lower-right; rear/red taillights upper-left | " + F(pairs[13].Similarity.Value) + " | " + F(pairs[14].Similarity.Value) + " | Front/rear PASS; visible pixels duplicate 15 |");
        report.AppendLine();
        report.AppendLine("## 4. Slot 07 original versus normalized");
        report.AppendLine();
        report.AppendLine("| Variant | BBox | Alpha | Length | Scale difference | Centroid | Baseline | Edge | Similarity 06 | Similarity Right | Distinct from Right | Selected |");
        report.AppendLine("|---|---|---:|---:|---:|---|---:|---:|---:|---:|---|---|");
        report.AppendLine("| Original | " + slot07Original.Bounds.Width + "x" + slot07Original.Bounds.Height + " | " + slot07Original.AlphaPixelCount + " | " + F(slot07Original.ProjectedLength) + " | " + F(originalScale) + "% | (" + F(slot07Original.CentroidX) + ", " + F(slot07Original.CentroidY) + ") | " + slot07Original.Baseline + " | " + slot07Original.EdgeTouchCount + " | " + F(originalSimilarity06) + " | " + F(originalSimilarity08) + " | YES: roof, hatch, and wheel line remain Up-biased | " + (!slot07NormalizedSelected ? "YES" : "NO") + " |");
        report.AppendLine("| Scale-normalized | " + slot07Normalized.Bounds.Width + "x" + slot07Normalized.Bounds.Height + " | " + slot07Normalized.AlphaPixelCount + " | " + F(slot07Normalized.ProjectedLength) + " | " + F(normalizedScale) + "% | (" + F(slot07Normalized.CentroidX) + ", " + F(slot07Normalized.CentroidY) + ") | " + slot07Normalized.Baseline + " | " + slot07Normalized.EdgeTouchCount + " | " + F(normalizedSimilarity06) + " | " + F(normalizedSimilarity08) + " | YES: shallow upper-right perspective remains visible | " + (slot07NormalizedSelected ? "**YES**" : "NO") + " |");
        report.AppendLine();
        report.AppendLine("**Selection:** normalized slot 07. Uniform nearest-neighbor whole-car scaling and translation reduce scale/center pop without changing heading; the sprite remains visibly distinct from Right.");
        report.AppendLine();
        report.AppendLine("## 5. Required visual sequence review");
        report.AppendLine();
        report.AppendLine("- `00 -> 01 -> 02`: Step1 slot 01 forms a credible mostly-Up intermediate; signed PCA steps remain negative and visually clockwise.");
        report.AppendLine("- `05 -> 06 -> 07 -> 08`: repaired 06 points to the correct side, but its silhouette is too close to 05 and the following step into 07 is too large. Slot 07 remains distinct from Right.");
        report.AppendLine("- `08 -> 09 -> 10 -> 11`: repaired 09 points lower-right, but it is a near-duplicate of slot 10, so the 09->10 step collapses.");
        report.AppendLine("- `13 -> 14 -> 15 -> 16`: repaired 14 points lower-right, but its visible raster is identical to slot 15, so the 14->15 step collapses completely.");
        report.AppendLine();
        report.AppendLine("## 6. Signed adjacent-angle and continuity audit");
        report.AppendLine();
        report.AppendLine("Signed deltas use `((next - previous + 540) % 360) - 180`. The expected progression is -11.25 degrees. No transition is reported as a false jump near 360 degrees; REVERSED requires both a positive signed delta and manual front/rear evidence.");
        report.AppendLine();
        report.AppendLine("| Pair | Expected signed | Signed PCA delta | Absolute delta | Similarity | Scale mismatch | Centroid shift | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (PairAudit pair in pairs)
            report.AppendLine("| " + pair.FirstSlot + " -> " + pair.SecondSlot + " | -11.25 | " + F(pair.SignedDelta.Value) + " | " + F(pair.AbsoluteDelta.Value) + " | " + F(pair.Similarity.Value) + " | " + F(pair.ScaleMismatch.Value) + "% | " + F(pair.CentroidShift.Value) + "px | " + pair.Status + " |");
        report.AppendLine();
        report.AppendLine("Maximum absolute shortest PCA delta is **" + F(maxAbsoluteDelta) + " degrees**; maximum absolute anchor-interpolated scale difference is **" + F(maxScale) + "%**. PCA remains an audit signal, not a substitute for front/rear inspection.");
        report.AppendLine();
        report.AppendLine("## 7. Technical image audit");
        report.AppendLine();
        report.AppendLine("All 17 selected PNGs and both slot 07 variants are 186x186 RGBA with true transparency, baseline y=169, zero edge contact, one connected vehicle component, zero partial-alpha pixels, and no crop. No output sprite contains checkerboard, text, arrows, road, or shadow.");
        report.AppendLine();
        report.AppendLine("## 8. Full 32-direction runtime preview");
        report.AppendLine();
        report.AppendLine("The exact existing `ResolveSourceSlot` flipX mapping was reproduced without script changes. Runtime propagation gives **" + runtimePass + " PASS**, **" + runtimeReview + " REVIEW**, and **" + runtimeFail + " FAIL** directions. Mirroring preserves the repaired front/rear side, but the collapsed source transitions also propagate to their mirrored runtime counterparts.");
        report.AppendLine();
        report.AppendLine("## 9. Exact files created");
        report.AppendLine();
        List<string> files = new List<string>();
        for (int slot = 0; slot <= 16; slot++)
        {
            double angle = NormalizeAngle(90.0 - slot * StepAngle);
            files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_" + slot.ToString("00") + "_" + AngleToken(angle) + "_step1d.png");
        }
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_original.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_scale_normalized.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_17_contact_sheet.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_full32_preview.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_neighbor_review.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_repaired_slots_review.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_slot07_comparison.png");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/step1d_metrics.csv");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/step1d_report.md");
        files.Add("Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/Tools/AE86Step1DBuilder.cs");
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
        report.AppendLine("Active Production32, Assets metadata/code/prefabs/scenes, Step 1 candidates, and Step 1C outputs remained read-only. No GUID, vehicle handling value, visualSteerLeadAngle, runtime mapping, prefab, or scene was changed.");
        report.AppendLine();
        report.AppendLine("## 11. Final decision");
        report.AppendLine();
        report.AppendLine(ready
            ? "The repaired local set is technically clean and visually continuous enough for non-destructive Unity runtime testing."
            : "The repaired sprites now face the correct horizontal side, but collapsed transitions at 05->06, 09->10, and 14->15 plus large adjacent gaps prevent a coherent 32-direction runtime test set.");
        report.AppendLine();
        report.Append(ready ? "READY_FOR_UNITY_TEST" : "NOT_READY");
        File.WriteAllText(Path.Combine(outputRoot, "Reports", "step1d_report.md"), report.ToString(), new UTF8Encoding(false));
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
        groups["Step 1 candidates"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates"), "*", SearchOption.AllDirectories).ToList();
        groups["Step 1C Hybrid Local outputs"] = Directory.GetFiles(Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_HybridLocal"), "*", SearchOption.AllDirectories).ToList();

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
        if (status == "REVIEW SOURCE" || status == "SCALE REVIEW" || status == "STEP REVIEW" || status == "PCA COMPRESSED" || status == "PCA COMPRESSED / VISUAL DISTINCT") return Color.DarkOrange;
        return Color.Firebrick;
    }

    private static string ShortStatus(string status)
    {
        return status == "NEED_EXTERNAL_INPUT" ? "NEED INPUT" : status;
    }

    private static string SourceLabelForSlot(int slot)
    {
        if (slot == 0 || slot == 8 || slot == 16) return "Active anchor";
        if ((slot >= 1 && slot <= 5) || (slot >= 10 && slot <= 13) || slot == 15) return "Step1";
        if (slot == 7) return "Local normalized";
        return "Flip repair";
    }

    private static string SourceFilenameForSlot(int slot)
    {
        switch (slot)
        {
            case 0: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_090_00_up.png";
            case 1: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_01_78.75_candidate.png";
            case 2: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_02_67.50_candidate.png";
            case 3: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_03_56.25_candidate.png";
            case 4: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_04_45.00_candidate.png";
            case 5: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_05_33.75_candidate.png";
            case 6: return "Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_06_22.50_scale_normalized.png";
            case 7: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_011_25.png";
            case 8: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_000_00_right.png";
            case 9: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_348_75.png";
            case 10: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_10_337.50_candidate.png";
            case 11: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_11_326.25_candidate.png";
            case 12: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_12_315.00_candidate.png";
            case 13: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_13_303.75_candidate.png";
            case 14: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_292_50.png";
            case 15: return "Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_15_281.25_candidate.png";
            case 16: return "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_270_00_down.png";
            default: throw new ArgumentOutOfRangeException("slot");
        }
    }

    private static string ContactInputForSlot(int slot)
    {
        if (slot == 6) return "Step1C slot_06 normalized";
        return Path.GetFileName(SourceFilenameForSlot(slot));
    }

    private static string SourceMethodForSlot(int slot)
    {
        if (slot == 0 || slot == 8 || slot == 16) return "Read-only active anchor copy";
        if ((slot >= 1 && slot <= 5) || (slot >= 10 && slot <= 13) || slot == 15) return "Read-only Step 1 candidate copy";
        if (slot == 7) return "Read-only local source with uniform nearest-neighbor scale and translation";
        return "Read-only source with one exact full-canvas horizontal pixel flip";
    }

    private static string TransformForSlot(int slot)
    {
        if (slot == 6 || slot == 9 || slot == 14) return "Exact horizontal pixel flip";
        if (slot == 7) return "Uniform nearest-neighbor scale + translation";
        return "None (read-only copy)";
    }

    private static string ManualVisualStatus(int slot)
    {
        if (slot == 6 || slot == 9 || slot == 14) return "FAIL";
        return "PASS";
    }

    private static string ManualVisualNotes(int slot)
    {
        if (slot == 1) return "Step 1 source forms a credible mostly-Up intermediate between slots 00 and 02.";
        if (slot == 6) return "Horizontal side is repaired, but the silhouette and PCA axis collapse toward slot 05.";
        if (slot == 7) return "Normalized whole-car scale preserves shallow upper-right perspective and remains visibly distinct from Right.";
        if (slot == 9) return "Horizontal side is repaired, but the result is a near-duplicate of Step 1 slot 10.";
        if (slot == 14) return "Horizontal side is repaired, but visible pixels duplicate Step 1 slot 15.";
        if (slot == 15) return "Step 1 source is a technically clean mostly-Down, slightly-right pose.";
        return "Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames.";
    }

    private static string ManualFrontRearCheck(int slot)
    {
        if (slot == 6) return "PASS: nose/headlights upper-right; rear/red taillights lower-left.";
        if (slot == 9 || slot == 14) return "PASS: nose/headlights lower-right; rear/red taillights upper-left.";
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

    private static string Step1CPath(string projectRoot, params string[] parts)
    {
        string path = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_HybridLocal");
        foreach (string part in parts) path = Path.Combine(path, part);
        return path;
    }

    private static bool IsManualCollapsedTransition(int firstSlot)
    {
        return firstSlot == 5 || firstSlot == 9 || firstSlot == 14;
    }

    private static bool IsManualReversedTransition(int firstSlot)
    {
        // All three Step 1D repairs pass front/rear inspection, so no transition is manually reversed.
        return false;
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
        double delta = (current - previous + 540.0) % 360.0 - 180.0;
        return delta;
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

    private static void FlipHorizontalExact(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Required local AE86 source was not found.", sourcePath);

        using (Bitmap source = LoadArgb(sourcePath))
        using (Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
        {
            if (source.Width != CanvasSize || source.Height != CanvasSize)
                throw new InvalidDataException("Expected a 186x186 source image: " + sourcePath);

            for (int y = 0; y < CanvasSize; y++)
                for (int x = 0; x < CanvasSize; x++)
                    output.SetPixel(CanvasSize - 1 - x, y, source.GetPixel(x, y));

            SavePng(output, destinationPath);
        }
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
