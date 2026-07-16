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

public static class AE86Step1BBuilder
{
    private const int Canvas = 186;
    private const int Baseline = 169;
    private const double ExpectedStep = 11.25;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] TargetSlots = { 1, 6, 7, 9, 14, 15 };

    private sealed class Metric
    {
        public int Slot;
        public string Filename;
        public double Target;
        public double? Actual;
        public double? Error;
        public string Step1Method;
        public int CanvasWidth;
        public int CanvasHeight;
        public int? BboxX;
        public int? BboxY;
        public int? BboxWidth;
        public int? BboxHeight;
        public double? CentroidX;
        public double? CentroidY;
        public double? ProjectedLength;
        public double? ProjectedWidth;
        public int? AlphaPixelCount;
        public double? ScaleDifference;
        public double? CenterOffsetX;
        public double? CenterOffsetY;
        public int? EdgeTouchCount;
        public double? PreviousDelta;
        public double? NextDelta;
        public string Step1Status;
    }

    private sealed class SlotState
    {
        public int Slot;
        public double Target;
        public Metric Metric;
        public string SelectedImagePath;
        public string AuditImagePath;
        public string SourceMethod;
        public string Status;
        public string RecommendedAction;
        public bool ExternalRequired;
        public bool RejectedAuditImage;
        public string FrontDescription;
        public string RearDescription;
        public string CurrentProblem;
    }

    private sealed class PairAudit
    {
        public int FirstSlot;
        public int SecondSlot;
        public double? ActualDelta;
        public double? Similarity;
        public double? ScaleMismatch;
        public double? CenterShift;
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
        string outputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1B_FinalSix");
        string pngRoot = Path.Combine(outputRoot, "PNG");
        string promptRoot = Path.Combine(outputRoot, "Prompts");
        string referenceRoot = Path.Combine(outputRoot, "References");
        string reportRoot = Path.Combine(outputRoot, "Reports");
        Directory.CreateDirectory(outputRoot);
        Directory.CreateDirectory(pngRoot);
        Directory.CreateDirectory(promptRoot);
        Directory.CreateDirectory(referenceRoot);
        Directory.CreateDirectory(reportRoot);

        Dictionary<string, HashSnapshot> hashesBefore = CaptureProtectedHashes(projectRoot);
        Dictionary<int, Metric> metrics = ReadStep1Metrics(projectRoot);
        List<SlotState> states = BuildStates(projectRoot, metrics);

        WritePngReadme(pngRoot);
        WriteExternalPackages(projectRoot, promptRoot, referenceRoot, states);
        List<PairAudit> pairAudits = BuildPairAudits(states);
        WriteFinalSixMetrics(outputRoot, states);
        DrawFinalSixContactSheet(outputRoot, states);
        DrawFinal17ContactSheet(outputRoot, states);
        DrawNeighborReview(outputRoot, states, pairAudits);
        DrawFull32Preview(outputRoot, states);

        Dictionary<string, HashSnapshot> hashesAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, outputRoot, states, pairAudits, hashesBefore, hashesAfter);
    }

    private static Dictionary<int, Metric> ReadStep1Metrics(string projectRoot)
    {
        string path = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates", "candidate_metrics.csv");
        string[] lines = File.ReadAllLines(path);
        List<string> headers = ParseCsvLine(lines[0]);
        Dictionary<int, Metric> result = new Dictionary<int, Metric>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            List<string> values = ParseCsvLine(lines[i]);
            Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Count && c < values.Count; c++)
                row[headers[c]] = values[c];
            Metric metric = new Metric();
            metric.Slot = Int(row, "slot").Value;
            metric.Filename = Value(row, "filename");
            metric.Target = Double(row, "target_angle").Value;
            metric.Actual = Double(row, "estimated_actual_angle");
            metric.Error = Double(row, "angular_error");
            metric.Step1Method = Value(row, "source_method");
            metric.CanvasWidth = Int(row, "canvas_width") ?? Canvas;
            metric.CanvasHeight = Int(row, "canvas_height") ?? Canvas;
            metric.BboxX = Int(row, "bbox_x");
            metric.BboxY = Int(row, "bbox_y");
            metric.BboxWidth = Int(row, "bbox_width");
            metric.BboxHeight = Int(row, "bbox_height");
            metric.CentroidX = Double(row, "centroid_x");
            metric.CentroidY = Double(row, "centroid_y");
            metric.ProjectedLength = Double(row, "projected_length");
            metric.ProjectedWidth = Double(row, "projected_width");
            metric.AlphaPixelCount = Int(row, "alpha_pixel_count");
            metric.ScaleDifference = Double(row, "scale_difference_percent");
            metric.CenterOffsetX = Double(row, "center_offset_x");
            metric.CenterOffsetY = Double(row, "center_offset_y");
            metric.EdgeTouchCount = Int(row, "edge_touch_count");
            metric.PreviousDelta = Double(row, "previous_step_delta");
            metric.NextDelta = Double(row, "next_step_delta");
            metric.Step1Status = Value(row, "status");
            result[metric.Slot] = metric;
        }
        return result;
    }

    private static List<SlotState> BuildStates(string projectRoot, Dictionary<int, Metric> metrics)
    {
        string active = Path.Combine(projectRoot, "Assets", "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32");
        string step1 = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates", "PNG");
        List<SlotState> states = new List<SlotState>();
        for (int slot = 0; slot <= 16; slot++)
        {
            Metric metric = metrics[slot];
            SlotState state = new SlotState();
            state.Slot = slot;
            state.Target = metric.Target;
            state.Metric = metric;
            state.FrontDescription = FrontDescription(metric.Target);
            state.RearDescription = RearDescription(metric.Target);

            if (slot == 0)
                state.SelectedImagePath = Path.Combine(active, "ae86_090_00_up.png");
            else if (slot == 8)
                state.SelectedImagePath = Path.Combine(active, "ae86_000_00_right.png");
            else if (slot == 16)
                state.SelectedImagePath = Path.Combine(active, "ae86_270_00_down.png");
            else if (slot != 6 && slot != 7 && slot != 9)
                state.SelectedImagePath = Path.Combine(step1, CandidateFilename(slot, metric.Target));

            state.AuditImagePath = state.SelectedImagePath;
            state.SourceMethod = metric.Step1Method;
            state.Status = metric.Step1Status == "PASS" ? "PASS" : metric.Step1Status;
            state.RecommendedAction = "Retain inherited Step 1 result; outside the six-slot Step 1B edit scope.";

            if (slot == 1)
            {
                state.Status = "REVIEW";
                state.SourceMethod = "Step 1 re-extraction; external package prepared";
                state.RecommendedAction = "Generate a new 78.75-degree pose; current 74.37-degree frame is reference-only.";
                state.ExternalRequired = true;
                state.CurrentProblem = "Current pose is 4.38 degrees from target and makes pair 00->01 jump 15.28 degrees.";
            }
            else if (slot == 6)
            {
                state.Status = "UNRESOLVED";
                state.SourceMethod = "External generation required";
                state.RecommendedAction = "Generate a new exact 22.50-degree source.";
                state.ExternalRequired = true;
                state.CurrentProblem = "No local active-identity source exists at the required shallow Up bias.";
            }
            else if (slot == 7)
            {
                state.Status = "UNRESOLVED";
                state.SourceMethod = "External generation required";
                state.RecommendedAction = "Generate a new exact 11.25-degree source visibly distinct from Right.";
                state.ExternalRequired = true;
                state.CurrentProblem = "The prior shallow source collapsed into the 0-degree Right anchor.";
            }
            else if (slot == 9)
            {
                state.Status = "UNRESOLVED";
                state.SourceMethod = "External generation required";
                state.RecommendedAction = "Generate a new exact 348.75-degree source visibly distinct from Right.";
                state.ExternalRequired = true;
                state.CurrentProblem = "No local active-identity source exists at the required shallow Down bias.";
            }
            else if (slot == 14)
            {
                state.Status = "UNRESOLVED";
                state.SourceMethod = "Step 1 candidate rejected; external package prepared";
                state.RecommendedAction = "Reject the 299.20-degree attempt and generate a new 292.50-degree pose.";
                state.ExternalRequired = true;
                state.RejectedAuditImage = true;
                state.CurrentProblem = "Current pose has 6.70-degree error, above the Step 1B FAIL threshold.";
                state.SelectedImagePath = null;
            }
            else if (slot == 15)
            {
                state.Status = "REVIEW";
                state.SourceMethod = "Step 1 flip; external package prepared";
                state.RecommendedAction = "Generate a new 281.25-degree pose; current 286.15-degree frame is reference-only.";
                state.ExternalRequired = true;
                state.CurrentProblem = "Current pose is 4.90 degrees from target and leaves a 16.18-degree jump to Down.";
            }
            states.Add(state);
        }
        return states;
    }

    private static string CandidateFilename(int slot, double angle)
    {
        return "slot_" + slot.ToString("00") + "_" + Angle(angle) + "_candidate.png";
    }

    private static void WritePngReadme(string pngRoot)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Step 1B PNG Status");
        text.AppendLine();
        text.AppendLine("No replacement PNG is accepted in this folder.");
        text.AppendLine();
        text.AppendLine("- Slots 01 and 15 remain REVIEW references, not PASS replacements.");
        text.AppendLine("- Slots 06, 07, 09, and 14 are unresolved and require external generation.");
        text.AppendLine("- The Step 1 slot 14 image failed the stricter Step 1B threshold and was not copied here.");
        text.AppendLine("- Active Production32 and Step 1 candidate files remain untouched.");
        File.WriteAllText(Path.Combine(pngRoot, "README.md"), text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteExternalPackages(string projectRoot, string promptRoot, string referenceRoot, List<SlotState> states)
    {
        foreach (int slot in TargetSlots)
        {
            SlotState state = states[slot];
            SlotState immediatePrevious = states[slot - 1];
            SlotState immediateNext = states[slot + 1];
            int approvedPreviousSlot;
            int approvedNextSlot;
            ResolveNearestApproved(slot, out approvedPreviousSlot, out approvedNextSlot);
            SlotState approvedPrevious = states[approvedPreviousSlot];
            SlotState approvedNext = states[approvedNextSlot];
            string stem = "slot_" + slot.ToString("00") + "_" + Angle(state.Target);
            string promptPath = Path.Combine(promptRoot, stem + "_prompt.txt");
            string referencePath = Path.Combine(referenceRoot, stem + "_reference.png");
            string tripletPath = Path.Combine(referenceRoot, stem + "_neighbor_triplet.png");

            File.WriteAllText(promptPath, BuildPrompt(projectRoot, state, immediatePrevious, immediateNext, approvedPrevious, approvedNext), new UTF8Encoding(false));
            DrawAngleReference(referencePath, state, immediatePrevious, immediateNext, approvedPrevious, approvedNext);
            DrawNeighborTriplet(tripletPath, state, immediatePrevious, immediateNext, approvedPrevious, approvedNext);
        }
    }

    private static string BuildPrompt(string projectRoot, SlotState target, SlotState immediatePrevious, SlotState immediateNext, SlotState approvedPrevious, SlotState approvedNext)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("AE86 Production32 Step 1B exact-angle external generation prompt");
        text.AppendLine();
        text.AppendLine("TARGET");
        text.AppendLine("- sourceSprites17 slot: " + target.Slot);
        text.AppendLine("- exact Unity heading: " + F(target.Target) + " degrees (Right=0, Up=90, Down=270)");
        text.AppendLine("- plain direction: " + DirectionDescription(target.Target));
        text.AppendLine("- immediate previous source slot: " + immediatePrevious.Slot + " at " + F(immediatePrevious.Target) + " degrees, status " + immediatePrevious.Status);
        text.AppendLine("- immediate next source slot: " + immediateNext.Slot + " at " + F(immediateNext.Target) + " degrees, status " + immediateNext.Status);
        text.AppendLine("- nearest approved previous visual reference: slot " + approvedPrevious.Slot + " at " + F(approvedPrevious.Target) + " degrees");
        text.AppendLine("- nearest approved next visual reference: slot " + approvedNext.Slot + " at " + F(approvedNext.Target) + " degrees");
        text.AppendLine();
        text.AppendLine("REFERENCE FILES");
        text.AppendLine("- previous approved sprite: " + Relative(projectRoot, approvedPrevious.SelectedImagePath).Replace('\\', '/'));
        text.AppendLine("- next approved sprite: " + Relative(projectRoot, approvedNext.SelectedImagePath).Replace('\\', '/'));
        text.AppendLine("- angle diagram: References/slot_" + target.Slot.ToString("00") + "_" + Angle(target.Target) + "_reference.png");
        text.AppendLine("- neighbor triplet: References/slot_" + target.Slot.ToString("00") + "_" + Angle(target.Target) + "_neighbor_triplet.png");
        text.AppendLine();
        text.AppendLine("CURRENT PROBLEM");
        text.AppendLine(target.CurrentProblem);
        text.AppendLine();
        text.AppendLine("FRONT AND REAR PLACEMENT");
        text.AppendLine("- front bumper, black hood, pop-up headlights, and yellow fog lights: " + target.FrontDescription);
        text.AppendLine("- rear hatch window, rear bumper, and red taillights: " + target.RearDescription);
        text.AppendLine("The hood/headlights must resolve the front and the hatch/taillights must resolve the rear. Do not infer direction from silhouette or PCA alone.");
        text.AppendLine();
        text.AppendLine("PERSPECTIVE RELATIONSHIP");
        text.AppendLine("Render a genuinely new intermediate pose whose hood centerline, windshield slope, roof plane, side-window exposure, wheel alignment, front bumper height, and rear hatch height all sit between the immediate canonical neighbors. It must not duplicate either neighbor, and it must remain visibly distinct at 186px gameplay scale.");
        text.AppendLine();
        text.AppendLine("IDENTITY LOCK");
        text.AppendLine("Preserve the exact active Production32 AE86-inspired hatchback identity: light gray body, black hood, black lower trim, dark windows, black wheels, pop-up headlights, yellow fog lights, red rear taillights, crisp black/purple pixel outline, and identical roof, hood, windshield, hatch, bumper, and wheel proportions. Match the approved references rather than the unrelated 1254px legacy Body images.");
        text.AppendLine();
        text.AppendLine("TECHNICAL OUTPUT");
        text.AppendLine("Create exactly one 186x186 RGBA PNG with true transparency. Align tire contact to baseline y=169 and center the projected vehicle without forcing an identical raw bounding box. Projected length must be within 3 percent of interpolated neighbors. Use hard Point-compatible pixel edges, fully opaque foreground pixels, zero edge-touching pixels, one connected foreground component, no crop, and no detached alpha pixels.");
        text.AppendLine();
        text.AppendLine("NEGATIVE CONSTRAINTS");
        text.AppendLine("No arbitrary bitmap rotation, shear-only fake, smooth resampling, blur, anti-aliased halo, white background, shadow, road, scenery, environment, text, watermark, duplicate neighbor pose, reversed front/rear, UpLeft/DownLeft heading, isolated pixels, or independent resizing of wheels/body/windows/hood.");
        text.AppendLine();
        text.AppendLine("ACCEPTANCE");
        text.AppendLine("Measured heading error must be 3.00 degrees or less after PCA axis resolution by manual front/rear inspection. Both adjacent clockwise steps should be 8-14 degrees. The final frame must pass manual visual identity, scale, center, baseline, alpha, and artifact review before gameplay replacement.");
        return text.ToString();
    }

    private static void ResolveNearestApproved(int targetSlot, out int previous, out int next)
    {
        if (targetSlot == 1) { previous = 0; next = 2; return; }
        if (targetSlot == 6) { previous = 5; next = 8; return; }
        if (targetSlot == 7) { previous = 5; next = 8; return; }
        if (targetSlot == 9) { previous = 8; next = 10; return; }
        if (targetSlot == 14) { previous = 13; next = 16; return; }
        previous = 13; next = 16;
    }

    private static void DrawAngleReference(string path, SlotState target, SlotState immediatePrevious, SlotState immediateNext, SlotState approvedPrevious, SlotState approvedNext)
    {
        using (Bitmap bitmap = new Bitmap(1000, 720, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Font title = new Font("Consolas", 22, FontStyle.Bold))
        using (Font body = new Font("Consolas", 14, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 15, FontStyle.Bold))
        using (Pen circle = new Pen(Color.FromArgb(90, 90, 90), 2))
        using (Pen neighbor = new Pen(Color.SteelBlue, 5))
        using (Pen targetPen = new Pen(Color.Firebrick, 8))
        {
            graphics.Clear(Color.White);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawString("Slot " + target.Slot.ToString("00") + " target " + F(target.Target) + " degrees", title, Brushes.Black, 35, 25);
            graphics.DrawString(DirectionDescription(target.Target), strong, Brushes.Black, 35, 68);
            graphics.DrawString("Immediate neighbors: " + F(immediatePrevious.Target) + " / " + F(immediateNext.Target), body, Brushes.Black, 35, 104);
            PointF center = new PointF(500, 390);
            float radius = 220;
            graphics.DrawEllipse(circle, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            DrawArrow(graphics, neighbor, center, immediatePrevious.Target, radius * 0.82f);
            DrawArrow(graphics, neighbor, center, immediateNext.Target, radius * 0.82f);
            DrawArrow(graphics, targetPen, center, target.Target, radius);
            graphics.FillEllipse(Brushes.Black, center.X - 5, center.Y - 5, 10, 10);
            graphics.DrawString("previous " + F(immediatePrevious.Target), body, Brushes.SteelBlue, 55, 620);
            graphics.DrawString("TARGET " + F(target.Target), strong, Brushes.Firebrick, 405, 620);
            graphics.DrawString("next " + F(immediateNext.Target), body, Brushes.SteelBlue, 785, 620);
            graphics.DrawString("Front follows red arrow; rear is exactly opposite.", body, Brushes.Black, 250, 662);
            graphics.DrawString("Nearest approved art: slots " + approvedPrevious.Slot + " and " + approvedNext.Slot, body, Brushes.Black, 325, 690);
            SavePng(bitmap, path);
        }
    }

    private static void DrawNeighborTriplet(string path, SlotState target, SlotState immediatePrevious, SlotState immediateNext, SlotState approvedPrevious, SlotState approvedNext)
    {
        const int width = 1050;
        const int height = 340;
        using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Font title = new Font("Consolas", 15, FontStyle.Bold))
        using (Font body = new Font("Consolas", 11, FontStyle.Regular))
        using (Pen arrow = new Pen(Color.Firebrick, 7))
        {
            graphics.Clear(Color.FromArgb(240, 240, 240));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            DrawTripletCell(graphics, new Rectangle(0, 0, 350, height), approvedPrevious, "PREVIOUS APPROVED", title, body);
            Rectangle targetCell = new Rectangle(350, 0, 350, height);
            DrawChecker(graphics, new Rectangle(targetCell.X + 82, 46, Canvas, Canvas), 12);
            PointF center = new PointF(targetCell.X + 175, 139);
            DrawArrow(graphics, arrow, center, target.Target, 78);
            graphics.DrawString("BLANK TARGET", title, Brushes.Firebrick, targetCell.X + 102, 12);
            graphics.DrawString("slot " + target.Slot.ToString("00") + " / " + F(target.Target) + " deg", title, Brushes.Black, targetCell.X + 78, 245);
            graphics.DrawString(DirectionDescription(target.Target), body, Brushes.Black, new RectangleF(targetCell.X + 25, 277, 300, 48));
            using (Pen border = new Pen(Color.Firebrick, 3))
                graphics.DrawRectangle(border, targetCell.X + 1, targetCell.Y + 1, targetCell.Width - 3, targetCell.Height - 3);
            DrawTripletCell(graphics, new Rectangle(700, 0, 350, height), approvedNext, "NEXT APPROVED", title, body);
            graphics.DrawString("Immediate canonical neighbors: slot " + immediatePrevious.Slot + " (" + immediatePrevious.Status + ") and slot " + immediateNext.Slot + " (" + immediateNext.Status + ")", body, Brushes.Black, 225, 321);
            SavePng(bitmap, path);
        }
    }

    private static void DrawTripletCell(Graphics graphics, Rectangle cell, SlotState state, string heading, Font title, Font body)
    {
        DrawChecker(graphics, new Rectangle(cell.X + 82, 46, Canvas, Canvas), 12);
        if (state.SelectedImagePath != null)
        {
            using (Bitmap image = LoadArgb(state.SelectedImagePath))
                graphics.DrawImage(image, cell.X + 82, 46, Canvas, Canvas);
        }
        graphics.DrawString(heading, title, Brushes.SeaGreen, cell.X + 92, 12);
        graphics.DrawString("slot " + state.Slot.ToString("00") + " / " + F(state.Target) + " deg", title, Brushes.Black, cell.X + 82, 245);
        graphics.DrawString("source: " + ShortMethod(state.SourceMethod), body, Brushes.Black, new RectangleF(cell.X + 30, 277, 290, 42));
        using (Pen border = new Pen(Color.SeaGreen, 3))
            graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
    }

    private static void WriteFinalSixMetrics(string outputRoot, List<SlotState> states)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,target_angle,estimated_actual_angle,angular_error,source_method,canvas_width,canvas_height,bbox_x,bbox_y,bbox_width,bbox_height,centroid_x,centroid_y,projected_length,projected_width,alpha_pixel_count,scale_difference_percent,center_offset_x,center_offset_y,baseline,edge_touch_count,previous_delta,next_delta,status,recommended_action");
        foreach (int slot in TargetSlots)
        {
            SlotState state = states[slot];
            Metric metric = state.Metric;
            List<string> row = new List<string>();
            row.Add(slot.ToString(Invariant));
            row.Add(F(state.Target));
            row.Add(Nullable(metric.Actual));
            row.Add(Nullable(metric.Error));
            row.Add(Csv(state.SourceMethod));
            row.Add(metric.CanvasWidth.ToString(Invariant));
            row.Add(metric.CanvasHeight.ToString(Invariant));
            row.Add(Nullable(metric.BboxX));
            row.Add(Nullable(metric.BboxY));
            row.Add(Nullable(metric.BboxWidth));
            row.Add(Nullable(metric.BboxHeight));
            row.Add(Nullable(metric.CentroidX));
            row.Add(Nullable(metric.CentroidY));
            row.Add(Nullable(metric.ProjectedLength));
            row.Add(Nullable(metric.ProjectedWidth));
            row.Add(Nullable(metric.AlphaPixelCount));
            row.Add(Nullable(metric.ScaleDifference));
            row.Add(Nullable(metric.CenterOffsetX));
            row.Add(Nullable(metric.CenterOffsetY));
            row.Add(metric.BboxY.HasValue && metric.BboxHeight.HasValue ? (metric.BboxY.Value + metric.BboxHeight.Value - 1).ToString(Invariant) : string.Empty);
            row.Add(Nullable(metric.EdgeTouchCount));
            row.Add(Nullable(metric.PreviousDelta));
            row.Add(Nullable(metric.NextDelta));
            row.Add(Csv(state.Status));
            row.Add(Csv(state.RecommendedAction));
            csv.AppendLine(string.Join(",", row.ToArray()));
        }
        File.WriteAllText(Path.Combine(outputRoot, "final_six_metrics.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static void DrawFinalSixContactSheet(string outputRoot, List<SlotState> states)
    {
        const int rowHeight = 285;
        const int sheetWidth = 1200;
        using (Bitmap sheet = new Bitmap(sheetWidth, rowHeight * TargetSlots.Length, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font title = new Font("Consolas", 13, FontStyle.Bold))
        using (Font body = new Font("Consolas", 10, FontStyle.Regular))
        using (Font placeholder = new Font("Consolas", 11, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(240, 240, 240));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int row = 0; row < TargetSlots.Length; row++)
            {
                int slot = TargetSlots[row];
                SlotState state = states[slot];
                SlotState previous = states[slot - 1];
                SlotState next = states[slot + 1];
                int y = row * rowHeight;
                DrawCompactSlot(graphics, previous, new Rectangle(20, y + 38, 186, 186), false, placeholder);
                DrawTargetAuditSlot(graphics, state, new Rectangle(260, y + 38, 186, 186), placeholder);
                DrawCompactSlot(graphics, next, new Rectangle(500, y + 38, 186, 186), false, placeholder);
                graphics.DrawString("PREV #" + previous.Slot.ToString("00") + " / " + F(previous.Target), title, Brushes.Black, 20, y + 9);
                graphics.DrawString("TARGET #" + state.Slot.ToString("00") + " / " + F(state.Target), title, Brushes.Black, 260, y + 9);
                graphics.DrawString("NEXT #" + next.Slot.ToString("00") + " / " + F(next.Target), title, Brushes.Black, 500, y + 9);

                string actual = state.Metric.Actual.HasValue ? F(state.Metric.Actual.Value) : "n/a";
                string error = state.Metric.Error.HasValue ? F(state.Metric.Error.Value) : "n/a";
                graphics.DrawString("actual: " + actual + " deg", body, Brushes.Black, 725, y + 44);
                graphics.DrawString("error: " + error + " deg", body, Brushes.Black, 725, y + 68);
                graphics.DrawString("method: " + state.SourceMethod, body, Brushes.Black, new RectangleF(725, y + 94, 445, 48));
                using (Brush statusBrush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString("status: " + state.Status, title, statusBrush, 725, y + 150);
                graphics.DrawString(state.CurrentProblem, body, Brushes.Black, new RectangleF(725, y + 181, 445, 66));
                graphics.DrawString("external package: Prompts + References", body, Brushes.Firebrick, 725, y + 253);
                using (Pen border = new Pen(StatusColor(state.Status), 3))
                    graphics.DrawRectangle(border, 1, y + 1, sheetWidth - 3, rowHeight - 3);
            }
            SavePng(sheet, Path.Combine(outputRoot, "final_six_contact_sheet.png"));
        }
    }

    private static void DrawTargetAuditSlot(Graphics graphics, SlotState state, Rectangle target, Font font)
    {
        DrawChecker(graphics, target, 12);
        string path = state.AuditImagePath;
        if (path == null)
        {
            DrawPlaceholder(graphics, target, "EXTERNAL\nREQUIRED", font);
            return;
        }
        using (Bitmap image = LoadArgb(path))
            graphics.DrawImage(image, target);
        if (state.RejectedAuditImage)
        {
            using (Pen cross = new Pen(Color.Firebrick, 5))
            {
                graphics.DrawLine(cross, target.Left + 8, target.Top + 8, target.Right - 8, target.Bottom - 8);
                graphics.DrawLine(cross, target.Right - 8, target.Top + 8, target.Left + 8, target.Bottom - 8);
            }
            graphics.DrawString("REJECTED", font, Brushes.Firebrick, target.X + 56, target.Y + 82);
        }
    }

    private static void DrawFinal17ContactSheet(string outputRoot, List<SlotState> states)
    {
        const int columns = 5;
        const int cellWidth = 300;
        const int cellHeight = 255;
        int rows = 4;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(238, 238, 238));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            foreach (SlotState state in states)
            {
                int column = state.Slot % columns;
                int row = state.Slot / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Rectangle imageArea = new Rectangle(cell.X + 57, cell.Y + 3, Canvas, Canvas);
                DrawCompactSlot(graphics, state, imageArea, false, strong);
                using (Pen border = new Pen(StatusColor(state.Status), 4))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
                string actual = state.Metric.Actual.HasValue ? F(state.Metric.Actual.Value) : "n/a";
                string error = state.Metric.Error.HasValue ? F(state.Metric.Error.Value) : "n/a";
                graphics.DrawString("slot " + state.Slot.ToString("00") + " target " + F(state.Target), strong, Brushes.Black, cell.X + 8, cell.Y + 193);
                graphics.DrawString("actual " + actual + "  error " + error, label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush statusBrush = new SolidBrush(StatusColor(state.Status)))
                    graphics.DrawString(ShortMethod(state.SourceMethod) + " | " + state.Status, label, statusBrush, cell.X + 8, cell.Y + 233);
            }
            SavePng(sheet, Path.Combine(outputRoot, "final_candidates17_contact_sheet.png"));
        }
    }

    private static List<PairAudit> BuildPairAudits(List<SlotState> states)
    {
        List<PairAudit> audits = new List<PairAudit>();
        for (int i = 0; i < 16; i++)
        {
            SlotState first = states[i];
            SlotState second = states[i + 1];
            PairAudit audit = new PairAudit();
            audit.FirstSlot = i;
            audit.SecondSlot = i + 1;
            if (first.SelectedImagePath == null || second.SelectedImagePath == null || !first.Metric.Actual.HasValue || !second.Metric.Actual.HasValue)
            {
                audit.Status = "UNRESOLVED";
                audits.Add(audit);
                continue;
            }
            audit.ActualDelta = ClockwiseDelta(first.Metric.Actual.Value, second.Metric.Actual.Value);
            audit.Similarity = AlphaIoU(first.SelectedImagePath, second.SelectedImagePath);
            audit.ScaleMismatch = Math.Max(Math.Abs(first.Metric.ScaleDifference ?? 0.0), Math.Abs(second.Metric.ScaleDifference ?? 0.0));
            audit.CenterShift = Distance(first.Metric.CentroidX, first.Metric.CentroidY, second.Metric.CentroidX, second.Metric.CentroidY);
            if (audit.ActualDelta.Value > 180.0)
                audit.Status = "REVERSED";
            else if (audit.ActualDelta.Value < 4.0)
                audit.Status = "COLLAPSED";
            else if (audit.ActualDelta.Value > 15.0)
                audit.Status = "LARGE JUMP";
            else if (audit.ScaleMismatch.Value > 3.0)
                audit.Status = "SCALE >3%";
            else if (first.Status == "REVIEW" || second.Status == "REVIEW")
                audit.Status = "REVIEW SOURCE";
            else
                audit.Status = "PASS";
            audits.Add(audit);
        }
        return audits;
    }

    private static void DrawNeighborReview(string outputRoot, List<SlotState> states, List<PairAudit> audits)
    {
        const int columns = 2;
        const int cellWidth = 700;
        const int cellHeight = 275;
        const int rows = 8;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(242, 242, 242));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            foreach (PairAudit audit in audits)
            {
                int index = audit.FirstSlot;
                int column = index % columns;
                int row = index / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                SlotState first = states[index];
                SlotState second = states[index + 1];
                string actual = audit.ActualDelta.HasValue ? F(audit.ActualDelta.Value) : "n/a";
                graphics.DrawString("slot " + index.ToString("00") + " -> " + (index + 1).ToString("00") + " | expected 11.25 | actual " + actual + " | " + audit.Status, strong, Brushes.Black, cell.X + 10, cell.Y + 8);
                DrawCompactSlot(graphics, first, new Rectangle(cell.X + 35, cell.Y + 37, Canvas, Canvas), false, strong);
                DrawCompactSlot(graphics, second, new Rectangle(cell.X + 475, cell.Y + 37, Canvas, Canvas), false, strong);
                string similarity = audit.Similarity.HasValue ? F(audit.Similarity.Value) : "n/a";
                string scale = audit.ScaleMismatch.HasValue ? F(audit.ScaleMismatch.Value) + "%" : "n/a";
                string center = audit.CenterShift.HasValue ? F(audit.CenterShift.Value) + "px" : "n/a";
                graphics.DrawString("similarity " + similarity + " | max scale mismatch " + scale + " | centroid shift " + center, label, Brushes.Black, cell.X + 135, cell.Y + 235);
                Color color = PairStatusColor(audit.Status);
                using (Pen border = new Pen(color, 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(outputRoot, "final_candidate_neighbor_review.png"));
        }
    }

    private static void DrawFull32Preview(string outputRoot, List<SlotState> states)
    {
        const int columns = 8;
        const int cellWidth = 250;
        const int cellHeight = 245;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, 4 * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 9, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 10, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(238, 238, 238));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int direction = 0; direction < 32; direction++)
            {
                int sourceSlot;
                bool flip;
                ResolveGameMapping(direction, out sourceSlot, out flip);
                SlotState source = states[sourceSlot];
                int column = direction % columns;
                int row = direction / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Rectangle imageArea = new Rectangle(cell.X + 32, cell.Y + 4, Canvas, Canvas);
                DrawChecker(graphics, imageArea, 12);
                if (source.SelectedImagePath == null)
                {
                    DrawPlaceholder(graphics, imageArea, "EXTERNAL", strong);
                }
                else
                {
                    using (Bitmap image = LoadArgb(source.SelectedImagePath))
                    {
                        if (flip)
                            image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        graphics.DrawImage(image, imageArea);
                    }
                }
                graphics.DrawString("dir " + direction.ToString("00") + "  " + F(NormalizeAngle(direction * ExpectedStep)) + " deg", strong, Brushes.Black, cell.X + 8, cell.Y + 194);
                graphics.DrawString("source " + sourceSlot.ToString("00") + " flipX=" + (flip ? "true" : "false"), label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush statusBrush = new SolidBrush(StatusColor(source.Status)))
                    graphics.DrawString(source.Status, label, statusBrush, cell.X + 8, cell.Y + 229);
                using (Pen border = new Pen(StatusColor(source.Status), 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(outputRoot, "final_candidate_full32_preview.png"));
        }
    }

    private static void WriteReport(string projectRoot, string outputRoot, List<SlotState> states, List<PairAudit> pairAudits, Dictionary<string, HashSnapshot> before, Dictionary<string, HashSnapshot> after)
    {
        int sixPass = TargetSlots.Count(slot => states[slot].Status == "PASS");
        int sixReview = TargetSlots.Count(slot => states[slot].Status == "REVIEW");
        int sixUnresolved = TargetSlots.Count(slot => states[slot].Status == "UNRESOLVED");
        int fullPass = states.Count(s => s.Status == "PASS");
        int fullReview = states.Count(s => s.Status == "REVIEW");
        int fullUnresolved = states.Count(s => s.Status == "UNRESOLVED");
        int full32Pass = CountRuntimeStatus(states, "PASS");
        int full32Review = CountRuntimeStatus(states, "REVIEW");
        int full32Unresolved = CountRuntimeStatus(states, "UNRESOLVED");
        double maxJump = pairAudits.Where(p => p.ActualDelta.HasValue).Max(p => p.ActualDelta.Value);
        double maxScale = states.Where(s => s.SelectedImagePath != null && s.Metric.ScaleDifference.HasValue).Max(s => Math.Abs(s.Metric.ScaleDifference.Value));

        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1B Final Six Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("Step 1B is **NOT_READY**. Of the six target slots, **" + sixPass + " PASS**, **" + sixReview + " REVIEW**, and **" + sixUnresolved + " unresolved** remain. No local source or deterministic reconstruction met the <=3-degree angle limit while preserving front/rear structure and the active 186px identity. External-generation packages were therefore created for all six slots, and no replacement PNG was accepted.");
        report.AppendLine();
        report.AppendLine("## 2. Exact six-slot results");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Current actual | Error | Result | Action |");
        report.AppendLine("|---:|---:|---:|---:|---|---|");
        foreach (int slot in TargetSlots)
        {
            SlotState state = states[slot];
            report.AppendLine("| " + slot + " | " + F(state.Target) + " | " + Nullable(state.Metric.Actual) + " | " + Nullable(state.Metric.Error) + " | " + state.Status + " | " + state.RecommendedAction + " |");
        }
        report.AppendLine();
        report.AppendLine("Slot 14's 299.20-degree Step 1 image is retained only in its original protected Step 1 location and may appear crossed out in audit imagery. It is not selected in the final 17-source candidate sequence.");
        report.AppendLine();
        report.AppendLine("## 3. Source method by target slot");
        report.AppendLine();
        foreach (int slot in TargetSlots)
            report.AppendLine("- Slot `" + slot.ToString("00") + "`: " + states[slot].SourceMethod + ". " + states[slot].CurrentProblem);
        report.AppendLine();
        report.AppendLine("The authoritative strip, all 17 extractions, the nine 1254px legacy images, active Production32, Step 1 outputs, deprecated prefabs, Docs, and Temp were rechecked. No unused exact-angle active-identity source was found. Mechanical shear, arbitrary rotation, smooth interpolation, and downscaled legacy art were rejected as final artwork strategies.");
        report.AppendLine();
        report.AppendLine("## 4. Angle audit");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Actual | Error | Step 1B threshold | Front/rear result |");
        report.AppendLine("|---:|---:|---:|---:|---|---|");
        foreach (int slot in TargetSlots)
        {
            SlotState state = states[slot];
            string threshold = !state.Metric.Error.HasValue ? "Unmeasured" : state.Metric.Error.Value <= 3.0 ? "PASS" : state.Metric.Error.Value <= 5.0 ? "REVIEW" : "FAIL / rejected";
            report.AppendLine("| " + slot + " | " + F(state.Target) + " | " + Nullable(state.Metric.Actual) + " | " + Nullable(state.Metric.Error) + " | " + threshold + " | " + state.FrontDescription + "; " + state.RearDescription + " |");
        }
        report.AppendLine();
        report.AppendLine("PCA figures remain audit measurements only. The 180-degree branch was resolved from black hood/pop-up headlights/yellow fog lights versus hatch window/red taillights. No placeholder is marked PASS.");
        report.AppendLine();
        report.AppendLine("## 5. Adjacent-step audit");
        report.AppendLine();
        report.AppendLine("| Pair | Expected | Actual | Similarity | Max scale mismatch | Centroid shift | Status |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|");
        foreach (PairAudit pair in pairAudits)
        {
            report.AppendLine("| " + pair.FirstSlot + " -> " + pair.SecondSlot + " | 11.25 | " + Nullable(pair.ActualDelta) + " | " + Nullable(pair.Similarity) + " | " + (pair.ScaleMismatch.HasValue ? F(pair.ScaleMismatch.Value) + "%" : string.Empty) + " | " + (pair.CenterShift.HasValue ? F(pair.CenterShift.Value) + "px" : string.Empty) + " | " + pair.Status + " |");
        }
        report.AppendLine();
        report.AppendLine("Maximum measured adjacent jump is **" + F(maxJump) + " degrees** at slot 15->16. Pairs touching unresolved slots cannot be sequence-approved.");
        report.AppendLine();
        report.AppendLine("## 6. Scale audit");
        report.AppendLine();
        report.AppendLine("| Slot | Length | Width | Alpha area | Scale delta | Baseline | Edge touches |");
        report.AppendLine("|---:|---:|---:|---:|---:|---:|---:|");
        foreach (SlotState state in states.Where(s => s.SelectedImagePath != null))
        {
            Metric m = state.Metric;
            int baseline = m.BboxY.HasValue && m.BboxHeight.HasValue ? m.BboxY.Value + m.BboxHeight.Value - 1 : -1;
            report.AppendLine("| " + state.Slot + " | " + Nullable(m.ProjectedLength) + " | " + Nullable(m.ProjectedWidth) + " | " + Nullable(m.AlphaPixelCount) + " | " + Nullable(m.ScaleDifference) + "% | " + baseline + " | " + Nullable(m.EdgeTouchCount) + " |");
        }
        report.AppendLine();
        report.AppendLine("Maximum absolute scale difference among selected source images is **" + F(maxScale) + "%** at inherited slot 10, which exceeds the Step 1B READY limit of 3%. It is outside the six-slot edit scope and was not changed.");
        report.AppendLine();
        report.AppendLine("## 7. Center and baseline audit");
        report.AppendLine();
        report.AppendLine("| Slot | Centroid | BBox center | Center offset | Baseline | Selected |");
        report.AppendLine("|---:|---|---|---|---:|---|");
        foreach (SlotState state in states)
        {
            Metric m = state.Metric;
            string bboxCenter = m.BboxX.HasValue && m.BboxWidth.HasValue && m.BboxY.HasValue && m.BboxHeight.HasValue ? "(" + F(m.BboxX.Value + (m.BboxWidth.Value - 1) / 2.0) + ", " + F(m.BboxY.Value + (m.BboxHeight.Value - 1) / 2.0) + ")" : string.Empty;
            string centroid = m.CentroidX.HasValue ? "(" + F(m.CentroidX.Value) + ", " + F(m.CentroidY.Value) + ")" : string.Empty;
            string offset = m.CenterOffsetX.HasValue ? "(" + F(m.CenterOffsetX.Value) + ", " + F(m.CenterOffsetY.Value) + ")" : string.Empty;
            int baseline = m.BboxY.HasValue && m.BboxHeight.HasValue ? m.BboxY.Value + m.BboxHeight.Value - 1 : -1;
            report.AppendLine("| " + state.Slot + " | " + centroid + " | " + bboxCenter + " | " + offset + " | " + (baseline >= 0 ? baseline.ToString() : string.Empty) + " | " + (state.SelectedImagePath != null ? "Yes" : "No") + " |");
        }
        report.AppendLine();
        report.AppendLine("All selected rasters retain baseline y=169 and zero edge contact, but continuity cannot be approved across four missing source positions. No generated frame was available for wheel/roof/hood/windshield semantic measurements in the unresolved cells.");
        report.AppendLine();
        report.AppendLine("## 8. Visual identity review");
        report.AppendLine();
        report.AppendLine("Existing Step 1 images retain the active light-gray body, black hood/lower trim, dark windows, black wheels, pop-up headlights, yellow front lighting, red taillights, and pixel outline. Slot 1 and 15 remain visually coherent but angle-inaccurate REVIEW references. Slot 14 is directionally too close to slot 13 and is rejected. No image-generation output was accepted because exact 186px identity, alpha, scale, center, and <=3-degree heading could not be guaranteed locally.");
        report.AppendLine();
        report.AppendLine("## 9. Remaining unresolved slots");
        report.AppendLine();
        report.AppendLine("Hard unresolved slots: `6 (22.50)`, `7 (11.25)`, `9 (348.75)`, and `14 (292.50)`. Slots `1 (78.75)` and `15 (281.25)` remain REVIEW and still require new PASS artwork before replacement.");
        report.AppendLine();
        report.AppendLine("## 10. Full 17-source status");
        report.AppendLine();
        report.AppendLine("Inherited Step 1 status plus Step 1B decisions: **" + fullPass + " PASS**, **" + fullReview + " REVIEW**, **" + fullUnresolved + " unresolved**.");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Selected status | Selected source |");
        report.AppendLine("|---:|---:|---|---|");
        foreach (SlotState state in states)
            report.AppendLine("| " + state.Slot + " | " + F(state.Target) + " | " + state.Status + " | " + (state.SelectedImagePath == null ? "Placeholder / external required" : Relative(projectRoot, state.SelectedImagePath).Replace('\\', '/')) + " |");
        report.AppendLine();
        report.AppendLine("## 11. Full 32-direction status");
        report.AppendLine();
        report.AppendLine("Using the exact runtime flipX mapping: **" + full32Pass + " PASS directions**, **" + full32Review + " REVIEW directions**, and **" + full32Unresolved + " unresolved directions**. Each unresolved non-vertical source propagates to its mirrored runtime counterpart.");
        report.AppendLine();
        report.AppendLine("## 12. Exact files created");
        report.AppendLine();
        List<string> files = Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).Select(p => Relative(projectRoot, p).Replace('\\', '/')).ToList();
        string reportPath = Relative(projectRoot, Path.Combine(outputRoot, "step1b_final_six_report.md")).Replace('\\', '/');
        if (!files.Contains(reportPath, StringComparer.OrdinalIgnoreCase))
            files.Add(reportPath);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            report.AppendLine("- `" + file + "`");
        report.AppendLine();
        report.AppendLine("## 13. Protected-file SHA-256 proof");
        report.AppendLine();
        report.AppendLine("| Protected group | Count | Before | After | Identical |");
        report.AppendLine("|---|---:|---|---|---|");
        foreach (string key in before.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            bool identical = before[key].Count == after[key].Count && before[key].Hash == after[key].Hash;
            report.AppendLine("| " + key + " | " + before[key].Count + " | `" + before[key].Hash + "` | `" + after[key].Hash + "` | " + (identical ? "YES" : "NO") + " |");
        }
        report.AppendLine();
        report.AppendLine("Protected groups include active Production32 PNGs, every Assets `.meta`, every Assets `.cs`, all prefabs, all scenes, and all 25 Step 1 candidate files.");
        report.AppendLine();
        report.AppendLine("## 14. Recommendation");
        report.AppendLine();
        report.AppendLine("**NOT_READY**");
        report.AppendLine();
        report.AppendLine("Reasons: zero of six Step 1B targets are PASS; four source positions are unresolved; two remain REVIEW; unresolved pairs prevent a complete clockwise sequence; maximum measured jump is " + F(maxJump) + " degrees (>15); inherited maximum scale difference is " + F(maxScale) + "% (>3); and center continuity cannot be validated across missing frames. Do not replace active Production32 yet.");
        File.WriteAllText(Path.Combine(outputRoot, "step1b_final_six_report.md"), report.ToString(), new UTF8Encoding(false));
    }

    private static int CountRuntimeStatus(List<SlotState> states, string status)
    {
        int count = 0;
        for (int direction = 0; direction < 32; direction++)
        {
            int source;
            bool flip;
            ResolveGameMapping(direction, out source, out flip);
            if (states[source].Status == status)
                count++;
        }
        return count;
    }

    private static Dictionary<string, HashSnapshot> CaptureProtectedHashes(string projectRoot)
    {
        string assets = Path.Combine(projectRoot, "Assets");
        string production = Path.Combine(assets, "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32");
        string step1 = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates");
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        groups["active Production32 PNG"] = Directory.GetFiles(production, "*.png", SearchOption.TopDirectoryOnly).ToList();
        groups["all Assets .meta"] = Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories).ToList();
        groups["all Assets code"] = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories).ToList();
        groups["all prefabs"] = Directory.GetFiles(assets, "*.prefab", SearchOption.AllDirectories).ToList();
        groups["all scenes"] = Directory.GetFiles(assets, "*.unity", SearchOption.AllDirectories).ToList();
        groups["Step 1 candidates"] = Directory.GetFiles(step1, "*", SearchOption.AllDirectories).ToList();
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

    private static void DrawCompactSlot(Graphics graphics, SlotState state, Rectangle target, bool showRejected, Font font)
    {
        DrawChecker(graphics, target, 12);
        string imagePath = state.SelectedImagePath;
        if (showRejected && state.RejectedAuditImage)
            imagePath = state.AuditImagePath;
        if (imagePath == null)
        {
            DrawPlaceholder(graphics, target, "EXTERNAL", font);
            return;
        }
        using (Bitmap image = LoadArgb(imagePath))
            graphics.DrawImage(image, target);
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
            graphics.DrawString(text, font, Brushes.Firebrick, target.X + (target.Width - size.Width) / 2, target.Y + (target.Height - size.Height) / 2);
        }
    }

    private static void DrawChecker(Graphics graphics, Rectangle area, int size)
    {
        for (int y = area.Top; y < area.Bottom; y += size)
        {
            for (int x = area.Left; x < area.Right; x += size)
            {
                bool dark = (((x - area.Left) / size) + ((y - area.Top) / size)) % 2 == 0;
                using (Brush brush = new SolidBrush(dark ? Color.FromArgb(220, 220, 220) : Color.White))
                    graphics.FillRectangle(brush, x, y, Math.Min(size, area.Right - x), Math.Min(size, area.Bottom - y));
            }
        }
    }

    private static void DrawArrow(Graphics graphics, Pen pen, PointF center, double angle, float length)
    {
        double radians = angle * Math.PI / 180.0;
        PointF end = new PointF(center.X + (float)(Math.Cos(radians) * length), center.Y - (float)(Math.Sin(radians) * length));
        LineCap original = pen.EndCap;
        pen.EndCap = LineCap.ArrowAnchor;
        graphics.DrawLine(pen, center, end);
        pen.EndCap = original;
    }

    private static double AlphaIoU(string firstPath, string secondPath)
    {
        using (Bitmap first = LoadArgb(firstPath))
        using (Bitmap second = LoadArgb(secondPath))
        {
            int intersection = 0;
            int union = 0;
            for (int y = 0; y < Canvas; y++)
            {
                for (int x = 0; x < Canvas; x++)
                {
                    bool a = first.GetPixel(x, y).A > 0;
                    bool b = second.GetPixel(x, y).A > 0;
                    if (a && b) intersection++;
                    if (a || b) union++;
                }
            }
            return union == 0 ? 0.0 : intersection / (double)union;
        }
    }

    private static double Distance(double? ax, double? ay, double? bx, double? by)
    {
        if (!ax.HasValue || !ay.HasValue || !bx.HasValue || !by.HasValue)
            return 0.0;
        double dx = bx.Value - ax.Value;
        double dy = by.Value - ay.Value;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string FrontDescription(double angle)
    {
        if (angle > 0 && angle < 90)
            return "upper-right, with the exact Up/Right bias shown by the target arrow";
        if (angle > 270 && angle < 360)
            return "lower-right, with the exact Down/Right bias shown by the target arrow";
        if (Math.Abs(angle - 90) < 0.01)
            return "top of canvas";
        if (Math.Abs(angle - 270) < 0.01)
            return "bottom of canvas";
        return "right side of canvas";
    }

    private static string RearDescription(double angle)
    {
        if (angle > 0 && angle < 90)
            return "lower-left, exactly opposite the front";
        if (angle > 270 && angle < 360)
            return "upper-left, exactly opposite the front";
        if (Math.Abs(angle - 90) < 0.01)
            return "bottom of canvas";
        if (Math.Abs(angle - 270) < 0.01)
            return "top of canvas";
        return "left side of canvas";
    }

    private static string DirectionDescription(double angle)
    {
        if (Math.Abs(angle - 78.75) < 0.01) return "mostly Up with a small Right bias";
        if (Math.Abs(angle - 22.50) < 0.01) return "mostly Right with a clear Up bias";
        if (Math.Abs(angle - 11.25) < 0.01) return "almost Right with a very small Up bias";
        if (Math.Abs(angle - 348.75) < 0.01) return "almost Right with a very small Down bias";
        if (Math.Abs(angle - 292.50) < 0.01) return "mostly Down with a noticeable Right bias";
        if (Math.Abs(angle - 281.25) < 0.01) return "almost Down with a small Right bias";
        return "canonical intermediate direction";
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

    private static string ShortMethod(string method)
    {
        if (method.StartsWith("Step 1 candidate rejected", StringComparison.OrdinalIgnoreCase)) return "Step1 rejected";
        if (method.StartsWith("Step 1 re-extraction", StringComparison.OrdinalIgnoreCase)) return "Step1 re-extracted";
        if (method.StartsWith("Step 1 flip", StringComparison.OrdinalIgnoreCase)) return "Step1 flipped";
        if (method.StartsWith("External", StringComparison.OrdinalIgnoreCase)) return "External required";
        return method;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder current = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    private static string Value(Dictionary<string, string> row, string key)
    {
        string value;
        return row.TryGetValue(key, out value) ? value : string.Empty;
    }

    private static double? Double(Dictionary<string, string> row, string key)
    {
        double value;
        return double.TryParse(Value(row, key), NumberStyles.Float, Invariant, out value) ? (double?)value : null;
    }

    private static int? Int(Dictionary<string, string> row, string key)
    {
        int value;
        return int.TryParse(Value(row, key), NumberStyles.Integer, Invariant, out value) ? (int?)value : null;
    }

    private static string Nullable(double? value)
    {
        return value.HasValue ? F(value.Value) : string.Empty;
    }

    private static string Nullable(int? value)
    {
        return value.HasValue ? value.Value.ToString(Invariant) : string.Empty;
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    private static string F(double value)
    {
        return value.ToString("0.00", Invariant);
    }

    private static string Angle(double value)
    {
        return value.ToString("0.00", Invariant);
    }

    private static double ClockwiseDelta(double previous, double current)
    {
        return NormalizeAngle(previous - current);
    }

    private static double NormalizeAngle(double value)
    {
        value %= 360.0;
        if (value < 0) value += 360.0;
        return value;
    }

    private static Bitmap LoadArgb(string path)
    {
        using (Bitmap source = new Bitmap(path))
            return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
    }

    private static void SavePng(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string Relative(string root, string path)
    {
        Uri rootUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        Uri pathUri = new Uri(Path.GetFullPath(path));
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string FileSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
            return Hex(sha.ComputeHash(stream));
    }

    private static string Sha256(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return Hex(sha.ComputeHash(bytes));
    }

    private static string Hex(byte[] bytes)
    {
        StringBuilder text = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes) text.Append(value.ToString("X2", Invariant));
        return text.ToString();
    }
}
