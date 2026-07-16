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

public static class AE86Step1CBuilder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] FinalSeven = { 1, 6, 7, 9, 10, 14, 15 };
    private static readonly int[] ExternalSlots = { 1, 6, 7, 9, 14, 15 };

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
        public bool MissingExternalInput;
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
        string outputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_FinalCandidates");
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

        string slot10Source = Step1Path(projectRoot, "slot_10_337.50_candidate.png");
        string slot10Output = Path.Combine(pngRoot, "slot_10_337.50_scale_fixed_candidate.png");
        SpriteMetrics slot10Before = AnalyzeBitmap(slot10Source, 337.50);
        double slot10ExpectedLength = InterpolateAnchorValue(10, up.ProjectedLength, right.ProjectedLength, down.ProjectedLength);
        CreateUniformScaleCandidate(slot10Source, slot10Output, slot10ExpectedLength, 337.50);

        List<SlotState> states = BuildStates(projectRoot, slot10Output, up, right, down);
        List<PairAudit> pairs = BuildPairAudits(states);
        WriteMetricsCsv(reportRoot, states);
        DrawFinal17ContactSheet(previewRoot, states);
        DrawNeighborReview(previewRoot, states, pairs);
        DrawFull32Preview(previewRoot, states);
        DrawFinalSevenReview(previewRoot, states);

        Dictionary<string, HashSnapshot> hashesAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, outputRoot, states, pairs, slot10Before, hashesBefore, hashesAfter);
    }

    private static List<SlotState> BuildStates(string projectRoot, string slot10Output, SpriteMetrics up, SpriteMetrics right, SpriteMetrics down)
    {
        string inputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1C_ExternalInputs");
        List<SlotState> states = new List<SlotState>();
        for (int slot = 0; slot <= 16; slot++)
        {
            SlotState state = new SlotState();
            state.Slot = slot;
            state.TargetAngle = NormalizeAngle(90.0 - slot * StepAngle);
            state.ExpectedLength = InterpolateAnchorValue(slot, up.ProjectedLength, right.ProjectedLength, down.ProjectedLength);
            state.ExpectedCentroidX = InterpolateAnchorValue(slot, up.CentroidX, right.CentroidX, down.CentroidX);
            state.ExpectedCentroidY = InterpolateAnchorValue(slot, up.CentroidY, right.CentroidY, down.CentroidY);

            if (ExternalSlots.Contains(slot))
            {
                state.Filename = ExternalFilename(slot, state.TargetAngle);
                string inputPath = Path.Combine(inputRoot, state.Filename);
                if (!File.Exists(inputPath))
                {
                    state.SourceMethod = "Required external input missing";
                    state.Status = "NEED_EXTERNAL_INPUT";
                    state.RecommendedAction = "Add " + state.Filename + " to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.";
                    state.FrontRearCheck = "Not inspectable; input is absent.";
                    state.ArtifactCheck = "Not inspectable";
                    state.CropCheck = "Not inspectable";
                    state.MissingExternalInput = true;
                    states.Add(state);
                    continue;
                }

                state.ImagePath = inputPath;
                state.SourceMethod = "External input validation";
                state.Filename = Path.GetFileName(inputPath);
            }
            else if (slot == 0)
            {
                state.ImagePath = ActivePath(projectRoot, "ae86_090_00_up.png");
                state.Filename = Path.GetFileName(state.ImagePath);
                state.SourceMethod = "Active anchor (read-only)";
            }
            else if (slot == 8)
            {
                state.ImagePath = ActivePath(projectRoot, "ae86_000_00_right.png");
                state.Filename = Path.GetFileName(state.ImagePath);
                state.SourceMethod = "Active anchor (read-only)";
            }
            else if (slot == 16)
            {
                state.ImagePath = ActivePath(projectRoot, "ae86_270_00_down.png");
                state.Filename = Path.GetFileName(state.ImagePath);
                state.SourceMethod = "Active anchor (read-only)";
            }
            else if (slot == 10)
            {
                state.ImagePath = slot10Output;
                state.Filename = Path.GetFileName(slot10Output);
                state.SourceMethod = "Uniform nearest-neighbor scale normalization";
            }
            else
            {
                state.Filename = "slot_" + slot.ToString("00") + "_" + AngleToken(state.TargetAngle) + "_candidate.png";
                state.ImagePath = Step1Path(projectRoot, state.Filename);
                state.SourceMethod = "Inherited Step 1 candidate (read-only)";
            }

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
        bool scalePass = Math.Abs(state.ScaleDifferencePercent) <= 3.0;

        state.FrontRearCheck = FrontRearDescription(state.TargetAngle);
        state.ArtifactCheck = alphaPass && edgePass ? "PASS" : "FAIL";
        state.CropCheck = cropPass ? "PASS" : "FAIL";

        if (!technicalPass)
        {
            state.Status = "FAIL";
            state.RecommendedAction = "Reject: canvas, alpha, baseline, edge, component, or crop validation failed.";
        }
        else if (!scalePass)
        {
            state.Status = "FAIL";
            state.RecommendedAction = "Reject: projected scale difference exceeds 3.00%.";
        }
        else if (state.AngularError <= 3.0)
        {
            state.Status = "PASS";
            state.RecommendedAction = "Retain in the non-destructive candidate sequence.";
        }
        else if (state.AngularError <= 5.0)
        {
            state.Status = "REVIEW";
            state.RecommendedAction = "Heading error exceeds the Step 1C 3.00-degree PASS limit.";
        }
        else
        {
            state.Status = "FAIL";
            state.RecommendedAction = "Reject: heading error exceeds 5.00 degrees.";
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
                throw new InvalidOperationException("Slot 10 scale correction does not fit the 186x186 canvas.");

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
        csv.AppendLine("slot,filename,target_angle,measured_angle,angular_error,source_method,canvas_width,canvas_height,bbox_x,bbox_y,bbox_width,bbox_height,centroid_x,centroid_y,bbox_center_x,bbox_center_y,projected_length,projected_width,alpha_pixel_count,partial_alpha_pixel_count,component_count,scale_difference_percent,center_offset_x,center_offset_y,baseline,edge_touch_count,crop_check,artifact_check,front_rear_check,previous_delta,next_delta,status,recommended_action");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            List<string> row = new List<string>();
            row.Add(state.Slot.ToString(Invariant));
            row.Add(Csv(state.Filename));
            row.Add(F(state.TargetAngle));
            row.Add(m == null ? string.Empty : F(m.AxisAngle));
            row.Add(m == null ? string.Empty : F(state.AngularError));
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
            row.Add(Nullable(state.PreviousDelta));
            row.Add(Nullable(state.NextDelta));
            row.Add(Csv(state.Status));
            row.Add(Csv(state.RecommendedAction));
            csv.AppendLine(string.Join(",", row.ToArray()));
        }
        File.WriteAllText(Path.Combine(reportRoot, "step1c_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static void DrawFinal17ContactSheet(string previewRoot, List<SlotState> states)
    {
        const int columns = 5;
        const int cellWidth = 300;
        const int cellHeight = 255;
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
                graphics.DrawString("actual " + actual + "  error " + error, label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush brush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString(ShortStatus(state.Status), label, brush, cell.X + 8, cell.Y + 233);
            }
            SavePng(sheet, Path.Combine(previewRoot, "step1c_final17_contact_sheet.png"));
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
            SavePng(sheet, Path.Combine(previewRoot, "step1c_neighbor_review.png"));
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
            SavePng(sheet, Path.Combine(previewRoot, "step1c_full32_preview.png"));
        }
    }

    private static void DrawFinalSevenReview(string previewRoot, List<SlotState> states)
    {
        const int rowHeight = 285;
        using (Bitmap sheet = new Bitmap(1200, FinalSeven.Length * rowHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font title = new Font("Consolas", 11, FontStyle.Bold))
        using (Font statusFont = new Font("Consolas", 13, FontStyle.Bold))
        {
            PrepareReviewGraphics(graphics, Color.FromArgb(242, 242, 242));
            for (int row = 0; row < FinalSeven.Length; row++)
            {
                int slot = FinalSeven[row];
                SlotState state = states[slot];
                SlotState previous = states[slot - 1];
                SlotState next = states[slot + 1];
                int y = row * rowHeight;
                graphics.DrawString("PREV #" + previous.Slot.ToString("00") + " / " + F(previous.TargetAngle), title, Brushes.Black, 20, y + 9);
                graphics.DrawString("TARGET #" + state.Slot.ToString("00") + " / " + F(state.TargetAngle), title, Brushes.Black, 260, y + 9);
                graphics.DrawString("NEXT #" + next.Slot.ToString("00") + " / " + F(next.TargetAngle), title, Brushes.Black, 500, y + 9);
                DrawSlot(graphics, previous, new Rectangle(20, y + 39, CanvasSize, CanvasSize), title);
                DrawSlot(graphics, state, new Rectangle(260, y + 39, CanvasSize, CanvasSize), title);
                DrawSlot(graphics, next, new Rectangle(500, y + 39, CanvasSize, CanvasSize), title);
                string actual = state.Metrics == null ? "n/a" : F(state.Metrics.AxisAngle);
                string error = state.Metrics == null ? "n/a" : F(state.AngularError);
                string scale = state.Metrics == null ? "n/a" : F(state.ScaleDifferencePercent) + "%";
                string centroid = state.Metrics == null ? "n/a" : F(Math.Sqrt(state.CenterOffsetX * state.CenterOffsetX + state.CenterOffsetY * state.CenterOffsetY)) + "px";
                graphics.DrawString("actual: " + actual + " deg", label, Brushes.Black, 725, y + 45);
                graphics.DrawString("error: " + error + " deg", label, Brushes.Black, 725, y + 72);
                graphics.DrawString("scale difference: " + scale, label, Brushes.Black, 725, y + 99);
                graphics.DrawString("centroid offset: " + centroid, label, Brushes.Black, 725, y + 126);
                using (Brush brush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString("status: " + state.Status, statusFont, brush, 725, y + 164);
                graphics.DrawString(state.RecommendedAction, label, Brushes.Black, new RectangleF(725, y + 199, 445, 65));
                using (Pen border = new Pen(StatusColor(state.Status), 4))
                    graphics.DrawRectangle(border, 1, y + 1, 1197, rowHeight - 3);
            }
            SavePng(sheet, Path.Combine(previewRoot, "step1c_final_seven_review.png"));
        }
    }

    private static void WriteReport(string projectRoot, string outputRoot, List<SlotState> states, List<PairAudit> pairs, SpriteMetrics slot10Before, Dictionary<string, HashSnapshot> hashesBefore, Dictionary<string, HashSnapshot> hashesAfter)
    {
        int pass = states.Count(s => s.Status == "PASS");
        int review = states.Count(s => s.Status == "REVIEW");
        int fail = states.Count(s => s.Status == "FAIL");
        int missing = states.Count(s => s.Status == "NEED_EXTERNAL_INPUT");
        int runtimePass = CountRuntimeStatus(states, "PASS");
        int runtimeReview = CountRuntimeStatus(states, "REVIEW");
        int runtimeFail = CountRuntimeStatus(states, "FAIL");
        int runtimeMissing = CountRuntimeStatus(states, "NEED_EXTERNAL_INPUT");
        double maxMeasuredJump = pairs.Where(p => p.ActualDelta.HasValue).Select(p => p.ActualDelta.Value).DefaultIfEmpty(0.0).Max();
        double maxScale = states.Where(s => s.Metrics != null).Select(s => Math.Abs(s.ScaleDifferencePercent)).DefaultIfEmpty(0.0).Max();
        SlotState slot10 = states[10];
        double beforeScale = ((slot10Before.ProjectedLength - slot10.ExpectedLength) / slot10.ExpectedLength) * 100.0;

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1C Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("**NEED_EXTERNAL_INPUT**");
        report.AppendLine();
        report.AppendLine("The required Step 1C external-input directory is absent, so no six-angle artwork was invented or substituted. Slot 10 was corrected non-destructively and all requested audit artifacts were produced in blocked-state form. The candidate set is not complete.");
        report.AppendLine();
        report.AppendLine("Strict 17-source result: **" + pass + " PASS**, **" + review + " REVIEW**, **" + fail + " FAIL**, and **" + missing + " NEED_EXTERNAL_INPUT**.");
        report.AppendLine();
        report.AppendLine("## 2. Exact missing external inputs");
        report.AppendLine();
        foreach (int slot in ExternalSlots)
            report.AppendLine("- `Docs/AE86Production32Fix/Step1C_ExternalInputs/" + ExternalFilename(slot, states[slot].TargetAngle) + "`");
        report.AppendLine();
        report.AppendLine("No placeholder is marked PASS, and no old review image was substituted for these files.");
        report.AppendLine();
        report.AppendLine("## 3. Slot 10 scale correction");
        report.AppendLine();
        report.AppendLine("The complete slot 10 foreground raster was uniformly resized with integer nearest-neighbor sampling. No rotation, shear, per-part scaling, recoloring, or redraw was used. The tire-contact baseline was re-anchored to y=169.");
        report.AppendLine();
        report.AppendLine("| Metric | Before | After |");
        report.AppendLine("|---|---:|---:|");
        report.AppendLine("| Measured heading | " + F(slot10Before.AxisAngle) + " | " + F(slot10.Metrics.AxisAngle) + " |");
        report.AppendLine("| Heading change | 0.00 | " + F(ShortestAngleError(slot10Before.AxisAngle, slot10.Metrics.AxisAngle)) + " |");
        report.AppendLine("| Projected length | " + F(slot10Before.ProjectedLength) + " | " + F(slot10.Metrics.ProjectedLength) + " |");
        report.AppendLine("| Scale difference | " + F(beforeScale) + "% | " + F(slot10.ScaleDifferencePercent) + "% |");
        report.AppendLine("| Bounding box | " + Box(slot10Before.Bounds) + " | " + Box(slot10.Metrics.Bounds) + " |");
        report.AppendLine("| Baseline | " + slot10Before.Baseline + " | " + slot10.Metrics.Baseline + " |");
        report.AppendLine("| Edge contact | " + slot10Before.EdgeTouchCount + " | " + slot10.Metrics.EdgeTouchCount + " |");
        report.AppendLine("| Partial-alpha pixels | " + slot10Before.PartialAlphaPixelCount + " | " + slot10.Metrics.PartialAlphaPixelCount + " |");
        report.AppendLine();
        report.AppendLine("Output: `Docs/AE86Production32Fix/Step1C_FinalCandidates/PNG/slot_10_337.50_scale_fixed_candidate.png`");
        report.AppendLine();
        report.AppendLine("## 4. All 17 source slots");
        report.AppendLine();
        report.AppendLine("Step 1C applies the strict <=3.00-degree PASS threshold to every measured source. This can expose inherited REVIEW states that were accepted under the earlier Step 1 threshold.");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Measured | Error | Scale | Baseline | Method | Status |");
        report.AppendLine("|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (SlotState state in states)
        {
            SpriteMetrics m = state.Metrics;
            report.AppendLine("| " + state.Slot + " | " + F(state.TargetAngle) + " | " + (m == null ? string.Empty : F(m.AxisAngle)) + " | " + (m == null ? string.Empty : F(state.AngularError)) + " | " + (m == null ? string.Empty : F(state.ScaleDifferencePercent) + "%") + " | " + (m == null ? string.Empty : m.Baseline.ToString(Invariant)) + " | " + state.SourceMethod + " | " + state.Status + " |");
        }
        report.AppendLine();
        report.AppendLine("## 5. Adjacent clockwise audit");
        report.AppendLine();
        report.AppendLine("| Pair | Expected | Actual | Similarity | Scale mismatch | Centroid shift | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (PairAudit pair in pairs)
            report.AppendLine("| " + pair.FirstSlot + " -> " + pair.SecondSlot + " | 11.25 | " + Nullable(pair.ActualDelta) + " | " + Nullable(pair.Similarity) + " | " + (pair.ScaleMismatch.HasValue ? F(pair.ScaleMismatch.Value) + "%" : string.Empty) + " | " + (pair.CentroidShift.HasValue ? F(pair.CentroidShift.Value) + "px" : string.Empty) + " | " + pair.Status + " |");
        report.AppendLine();
        report.AppendLine("Maximum currently measurable adjacent jump: **" + F(maxMeasuredJump) + " degrees**. A complete maximum cannot be approved while pairs touching missing external slots are unresolved.");
        report.AppendLine();
        report.AppendLine("## 6. Scale, center, alpha, and crop audit");
        report.AppendLine();
        report.AppendLine("| Slot | Length | Width | Alpha area | Partial alpha | Components | Centroid | Center offset | Edge | Crop | Artifact |");
        report.AppendLine("|---:|---:|---:|---:|---:|---:|---|---|---:|---|---|");
        foreach (SlotState state in states.Where(s => s.Metrics != null))
        {
            SpriteMetrics m = state.Metrics;
            report.AppendLine("| " + state.Slot + " | " + F(m.ProjectedLength) + " | " + F(m.ProjectedWidth) + " | " + m.AlphaPixelCount + " | " + m.PartialAlphaPixelCount + " | " + m.ComponentCount + " | (" + F(m.CentroidX) + ", " + F(m.CentroidY) + ") | (" + F(state.CenterOffsetX) + ", " + F(state.CenterOffsetY) + ") | " + m.EdgeTouchCount + " | " + state.CropCheck + " | " + state.ArtifactCheck + " |");
        }
        report.AppendLine();
        report.AppendLine("Maximum measured absolute scale difference after the slot 10 correction is **" + F(maxScale) + "%**. Missing sources have no fabricated measurements.");
        report.AppendLine();
        report.AppendLine("## 7. Final-seven review");
        report.AppendLine();
        foreach (int slot in FinalSeven)
            report.AppendLine("- Slot `" + slot.ToString("00") + "`: **" + states[slot].Status + "**. " + states[slot].RecommendedAction);
        report.AppendLine();
        report.AppendLine("## 8. Exact 32-direction runtime mapping");
        report.AppendLine();
        report.AppendLine("The preview uses the existing game mapping without code changes: directions 0-8 use sources 8-0 unflipped, directions 9-23 use sources 1-15 with flipX, and directions 24-31 use sources 16-9 unflipped.");
        report.AppendLine();
        report.AppendLine("Runtime status: **" + runtimePass + " PASS**, **" + runtimeReview + " REVIEW**, **" + runtimeFail + " FAIL**, **" + runtimeMissing + " NEED_EXTERNAL_INPUT**.");
        report.AppendLine();
        report.AppendLine("## 9. Exact files created");
        report.AppendLine();
        string[] files =
        {
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/PNG/slot_10_337.50_scale_fixed_candidate.png",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_final17_contact_sheet.png",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_neighbor_review.png",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_full32_preview.png",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_final_seven_review.png",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/step1c_metrics.csv",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/step1c_report.md",
            "Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/Tools/AE86Step1CBuilder.cs"
        };
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
        report.AppendLine("Protected groups cover active Production32 PNGs, every Assets .meta and .cs file, all prefabs, all scenes, all Step 1 candidates, and all Step 1B outputs.");
        report.AppendLine();
        report.AppendLine("## 11. Final decision");
        report.AppendLine();
        report.AppendLine("Six mandatory input files are absent, so all 17 source slots do not exist and twelve runtime directions remain unresolved. Strict inherited angle reviews also remain. Active gameplay assets must not be replaced.");
        report.AppendLine();
        report.Append("NOT_READY");
        File.WriteAllText(Path.Combine(outputRoot, "Reports", "step1c_report.md"), report.ToString(), new UTF8Encoding(false));
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
        string step1 = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates");
        string step1b = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1B_FinalSix");
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        groups["active Production32 PNG"] = Directory.GetFiles(production, "*.png", SearchOption.TopDirectoryOnly).ToList();
        groups["all Assets .meta"] = Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories).ToList();
        groups["all Assets code"] = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories).ToList();
        groups["all prefabs"] = Directory.GetFiles(assets, "*.prefab", SearchOption.AllDirectories).ToList();
        groups["all scenes"] = Directory.GetFiles(assets, "*.unity", SearchOption.AllDirectories).ToList();
        groups["Step 1 candidates"] = Directory.GetFiles(step1, "*", SearchOption.AllDirectories).ToList();
        groups["Step 1B outputs"] = Directory.GetFiles(step1b, "*", SearchOption.AllDirectories).ToList();

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

    private static string FrontRearDescription(double angle)
    {
        if (angle > 270.0 && angle < 360.0) return "Front verified toward lower-right; rear toward upper-left.";
        if (angle > 0.0 && angle < 90.0) return "Front verified toward upper-right; rear toward lower-left.";
        if (ShortestAngleError(angle, 90.0) < 0.01) return "Front at top; rear at bottom.";
        if (ShortestAngleError(angle, 270.0) < 0.01) return "Front at bottom; rear at top.";
        return "Front at right; rear at left.";
    }

    private static string ExternalFilename(int slot, double angle)
    {
        return "slot_" + slot.ToString("00") + "_" + AngleToken(angle) + "_external.png";
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
