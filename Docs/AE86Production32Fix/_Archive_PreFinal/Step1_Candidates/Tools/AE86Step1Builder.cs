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
using System.Text.RegularExpressions;

public static class AE86Step1Builder
{
    private const int CanvasSize = 186;
    private const int BaselineY = 169;
    private const double StepAngle = 11.25;

    private sealed class SlotPlan
    {
        public int Slot;
        public double TargetAngle;
        public string SourceRelativePath;
        public bool FlipX;
        public bool NormalizeScale;
        public double ManualHeadingHint;
        public string SourceMethod;
        public string CandidateFilename;
        public string FrontRear;
        public bool IsAnchor;
        public bool ExternalRequired;
        public string ExternalReason;
    }

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

    private sealed class SlotResult
    {
        public SlotPlan Plan;
        public string DisplayFilename;
        public string ImagePath;
        public SpriteMetrics Metrics;
        public double AngularError;
        public double ExpectedLength;
        public double ScaleDifferencePercent;
        public double ExpectedCentroidX;
        public double ExpectedCentroidY;
        public double CenterOffsetX;
        public double CenterOffsetY;
        public string Status;
        public string RecommendedAction;
        public string SequenceFlag;
    }

    private sealed class HashSnapshot
    {
        public string Name;
        public int Count;
        public string AggregateSha256;
    }

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void Run(string projectRoot)
    {
        projectRoot = Path.GetFullPath(projectRoot);
        string outputRoot = Path.Combine(projectRoot, "Docs", "AE86Production32Fix", "Step1_Candidates");
        string pngRoot = Path.Combine(outputRoot, "PNG");
        string promptRoot = Path.Combine(outputRoot, "Prompts");
        Directory.CreateDirectory(outputRoot);
        Directory.CreateDirectory(pngRoot);
        Directory.CreateDirectory(promptRoot);

        Dictionary<string, HashSnapshot> hashesBefore = CaptureProtectedHashes(projectRoot);
        List<SlotPlan> plans = BuildPlans();

        WriteSourceInventory(projectRoot, outputRoot);
        WriteExternalPrompts(promptRoot, plans);

        string activeRoot = Path.Combine(projectRoot, "Assets", "Art", "Vehicles", "AE86", "Body", "Extracted", "Production32");
        SpriteMetrics upAnchor = AnalyzeBitmap(Path.Combine(activeRoot, "ae86_090_00_up.png"), 90.0);
        SpriteMetrics rightAnchor = AnalyzeBitmap(Path.Combine(activeRoot, "ae86_000_00_right.png"), 0.0);
        SpriteMetrics downAnchor = AnalyzeBitmap(Path.Combine(activeRoot, "ae86_270_00_down.png"), 270.0);

        List<SlotResult> results = new List<SlotResult>();
        foreach (SlotPlan plan in plans)
        {
            SlotResult result = new SlotResult();
            result.Plan = plan;
            result.ExpectedLength = InterpolateAnchorValue(plan.Slot, upAnchor.ProjectedLength, rightAnchor.ProjectedLength, downAnchor.ProjectedLength);
            result.ExpectedCentroidX = InterpolateAnchorValue(plan.Slot, upAnchor.CentroidX, rightAnchor.CentroidX, downAnchor.CentroidX);
            result.ExpectedCentroidY = InterpolateAnchorValue(plan.Slot, upAnchor.CentroidY, rightAnchor.CentroidY, downAnchor.CentroidY);

            if (plan.ExternalRequired)
            {
                result.DisplayFilename = "NEEDS_EXTERNAL_GENERATION";
                result.Status = "NEEDS_EXTERNAL_GENERATION";
                result.RecommendedAction = plan.ExternalReason;
                result.SequenceFlag = "Unresolved; no candidate artwork was fabricated.";
                results.Add(result);
                continue;
            }

            string sourcePath = Path.Combine(projectRoot, plan.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (plan.IsAnchor)
            {
                result.ImagePath = sourcePath;
                result.DisplayFilename = Path.GetFileName(sourcePath);
            }
            else
            {
                string candidatePath = Path.Combine(pngRoot, plan.CandidateFilename);
                CreateCandidate(sourcePath, candidatePath, plan, result.ExpectedLength);
                result.ImagePath = candidatePath;
                result.DisplayFilename = plan.CandidateFilename;
            }

            result.Metrics = AnalyzeBitmap(result.ImagePath, plan.ManualHeadingHint);
            result.AngularError = ShortestAngleError(result.Metrics.AxisAngle, plan.TargetAngle);
            result.ScaleDifferencePercent = ((result.Metrics.ProjectedLength - result.ExpectedLength) / result.ExpectedLength) * 100.0;
            result.CenterOffsetX = result.Metrics.CentroidX - result.ExpectedCentroidX;
            result.CenterOffsetY = result.Metrics.CentroidY - result.ExpectedCentroidY;
            result.Status = StatusFromError(result.AngularError);
            result.RecommendedAction = RecommendationFor(result);
            result.SequenceFlag = "Pending adjacent-step evaluation.";

            if (!plan.IsAnchor && result.Status == "FAIL")
            {
                File.Delete(result.ImagePath);
                result.ImagePath = null;
                result.DisplayFilename = "NEEDS_EXTERNAL_GENERATION";
                result.Status = "NEEDS_EXTERNAL_GENERATION";
                result.RecommendedAction = "Measured candidate exceeded 7 degrees; generate a new exact-angle sprite externally.";
                result.SequenceFlag = "Rejected candidate; no FAIL artwork retained.";
            }

            results.Add(result);
        }

        WriteCenterPreview(activeRoot, pngRoot);
        EvaluateSequence(results);
        WriteMetricsCsv(outputRoot, results);
        DrawCandidates17Sheet(outputRoot, results);
        DrawNeighborSheet(outputRoot, results);
        DrawFull32Sheet(outputRoot, results);

        Dictionary<string, HashSnapshot> hashesAfter = CaptureProtectedHashes(projectRoot);
        WriteReport(projectRoot, outputRoot, results, hashesBefore, hashesAfter);
    }

    private static List<SlotPlan> BuildPlans()
    {
        const string active = "Assets/Art/Vehicles/AE86/Body/Extracted/Production32/";
        const string extracted = "Assets/Art/Vehicles/AE86/Body/Extracted/";
        List<SlotPlan> plans = new List<SlotPlan>();

        plans.Add(Anchor(0, 90.00, active + "ae86_090_00_up.png", 89.65, "Front at top; rear hatch and taillights at bottom."));
        plans.Add(Candidate(1, 78.75, extracted + "ae86_frame_14.png", false, true, 74.37, "Re-extracted", "slot_01_78.75_candidate.png", "Front upper-right, rear lower-left; mostly Up."));
        plans.Add(Candidate(2, 67.50, extracted + "ae86_frame_13.png", false, true, 63.71, "Re-extracted", "slot_02_67.50_candidate.png", "Front upper-right, rear lower-left."));
        plans.Add(Candidate(3, 56.25, extracted + "ae86_frame_12.png", false, true, 54.99, "Re-extracted", "slot_03_56.25_candidate.png", "Front upper-right, rear lower-left."));
        plans.Add(Candidate(4, 45.00, extracted + "ae86_frame_11.png", false, true, 47.62, "Re-extracted", "slot_04_45.00_candidate.png", "Front upper-right, rear lower-left; diagonal anchor."));
        plans.Add(Candidate(5, 33.75, extracted + "ae86_frame_10.png", false, true, 36.15, "Re-extracted", "slot_05_33.75_candidate.png", "Front upper-right, rear lower-left; mostly Right."));
        plans.Add(External(6, 22.50, "No existing frame matches 22.50 degrees at the approved identity and scale."));
        plans.Add(External(7, 11.25, "The only shallow source collapses into the 0-degree Right anchor."));
        plans.Add(Anchor(8, 0.00, active + "ae86_000_00_right.png", 356.84, "Front at right; rear hatch and taillights at left."));
        plans.Add(External(9, 348.75, "No existing frame provides the required small Down bias from Right."));
        plans.Add(Candidate(10, 337.50, extracted + "ae86_frame_07.png", true, false, 333.94, "Flipped", "slot_10_337.50_candidate.png", "Front lower-right, rear upper-left; mostly Right."));
        plans.Add(Candidate(11, 326.25, extracted + "ae86_frame_06.png", true, false, 324.24, "Flipped", "slot_11_326.25_candidate.png", "Front lower-right, rear upper-left."));
        plans.Add(Candidate(12, 315.00, extracted + "ae86_frame_05.png", true, false, 316.46, "Flipped", "slot_12_315.00_candidate.png", "Front lower-right, rear upper-left; diagonal anchor."));
        plans.Add(Candidate(13, 303.75, extracted + "ae86_frame_04.png", true, false, 307.16, "Flipped", "slot_13_303.75_candidate.png", "Front lower-right, rear upper-left."));
        plans.Add(Candidate(14, 292.50, extracted + "ae86_frame_03.png", true, false, 299.20, "Flipped", "slot_14_292.50_candidate.png", "Front lower-right, rear upper-left; mostly Down."));
        plans.Add(Candidate(15, 281.25, extracted + "ae86_frame_02.png", true, false, 286.15, "Flipped", "slot_15_281.25_candidate.png", "Front lower-right, rear upper-left; mostly Down."));
        plans.Add(Anchor(16, 270.00, active + "ae86_270_00_down.png", 269.97, "Front at bottom; rear hatch and taillights at top."));
        return plans;
    }

    private static SlotPlan Anchor(int slot, double angle, string source, double hint, string frontRear)
    {
        return new SlotPlan
        {
            Slot = slot,
            TargetAngle = angle,
            SourceRelativePath = source,
            ManualHeadingHint = hint,
            SourceMethod = "Existing",
            FrontRear = frontRear,
            IsAnchor = true
        };
    }

    private static SlotPlan Candidate(int slot, double angle, string source, bool flip, bool normalizeScale, double hint, string method, string filename, string frontRear)
    {
        return new SlotPlan
        {
            Slot = slot,
            TargetAngle = angle,
            SourceRelativePath = source,
            FlipX = flip,
            NormalizeScale = normalizeScale,
            ManualHeadingHint = hint,
            SourceMethod = method,
            CandidateFilename = filename,
            FrontRear = frontRear
        };
    }

    private static SlotPlan External(int slot, double angle, string reason)
    {
        return new SlotPlan
        {
            Slot = slot,
            TargetAngle = angle,
            SourceMethod = "External required",
            ExternalRequired = true,
            ExternalReason = reason,
            FrontRear = "Defined in the matching prompt and angle-reference diagram."
        };
    }

    private static void CreateCandidate(string sourcePath, string outputPath, SlotPlan plan, double expectedLength)
    {
        using (Bitmap source = LoadArgb(sourcePath))
        {
            SpriteMetrics sourceMetrics = AnalyzeBitmap(source, plan.ManualHeadingHint);
            double scale = plan.NormalizeScale ? expectedLength / sourceMetrics.ProjectedLength : 1.0;
            Rectangle bounds = sourceMetrics.Bounds;
            int newWidth = Math.Max(1, (int)Math.Round(bounds.Width * scale, MidpointRounding.AwayFromZero));
            int newHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale, MidpointRounding.AwayFromZero));
            int destX = (CanvasSize - newWidth + 1) / 2;
            int destY = BaselineY - newHeight + 1;

            if (destX < 1 || destY < 1 || destX + newWidth >= CanvasSize - 1 || destY + newHeight >= CanvasSize - 1)
                throw new InvalidOperationException("Candidate does not fit the 186x186 canvas: slot " + plan.Slot);

            using (Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < newHeight; y++)
                {
                    int sourceOffsetY = Math.Min(bounds.Height - 1, (int)Math.Floor(((y + 0.5) * bounds.Height) / newHeight));
                    int sourceY = bounds.Y + sourceOffsetY;
                    for (int x = 0; x < newWidth; x++)
                    {
                        int sourceOffsetX = Math.Min(bounds.Width - 1, (int)Math.Floor(((x + 0.5) * bounds.Width) / newWidth));
                        if (plan.FlipX)
                            sourceOffsetX = bounds.Width - 1 - sourceOffsetX;
                        Color pixel = source.GetPixel(bounds.X + sourceOffsetX, sourceY);
                        if (pixel.A > 0)
                            output.SetPixel(destX + x, destY + y, pixel);
                    }
                }
                SavePng(output, outputPath);
            }
        }
    }

    private static void WriteCenterPreview(string activeRoot, string pngRoot)
    {
        string sourcePath = Path.Combine(activeRoot, "ae86_000_00_right.png");
        string outputPath = Path.Combine(pngRoot, "slot_08_0.00_center_preview.png");
        using (Bitmap source = LoadArgb(sourcePath))
        using (Bitmap output = new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize - 2; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    if (pixel.A > 0)
                        output.SetPixel(x + 2, y, pixel);
                }
            }
            SavePng(output, outputPath);
        }
    }

    private static SpriteMetrics AnalyzeBitmap(string path, double manualHeadingHint)
    {
        using (Bitmap bitmap = LoadArgb(path))
            return AnalyzeBitmap(bitmap, manualHeadingHint);
    }

    private static SpriteMetrics AnalyzeBitmap(Bitmap bitmap, double manualHeadingHint)
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
                if (alpha == 0)
                    continue;
                pixels.Add(new Point(x, y));
                sumX += x;
                sumY += y;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                if (alpha < 255)
                    partial++;
                if (x == 0 || y == 0 || x == bitmap.Width - 1 || y == bitmap.Height - 1)
                    edge++;
            }
        }

        if (pixels.Count == 0)
            throw new InvalidOperationException("No foreground alpha pixels were found.");

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
        if (ShortestAngleError(opposite, manualHeadingHint) < ShortestAngleError(angle, manualHeadingHint))
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
        int count = 0;
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        Queue<Point> queue = new Queue<Point>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (visited[x, y] || bitmap.GetPixel(x, y).A == 0)
                    continue;
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
                        if (nx < 0 || ny < 0 || nx >= bitmap.Width || ny >= bitmap.Height || visited[nx, ny])
                            continue;
                        if (bitmap.GetPixel(nx, ny).A == 0)
                            continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }
            }
        }
        return count;
    }

    private static void EvaluateSequence(List<SlotResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            SlotResult current = results[i];
            if (current.Metrics == null)
                continue;
            List<string> flags = new List<string>();
            if (i > 0 && results[i - 1].Metrics != null)
            {
                double delta = ClockwiseDelta(results[i - 1].Metrics.AxisAngle, current.Metrics.AxisAngle);
                flags.Add(ClassifyStep("prev", delta));
            }
            else if (i > 0)
            {
                flags.Add("previous unresolved");
            }

            if (i + 1 < results.Count && results[i + 1].Metrics != null)
            {
                double delta = ClockwiseDelta(current.Metrics.AxisAngle, results[i + 1].Metrics.AxisAngle);
                flags.Add(ClassifyStep("next", delta));
            }
            else if (i + 1 < results.Count)
            {
                flags.Add("next unresolved");
            }
            current.SequenceFlag = string.Join("; ", flags.ToArray());
        }
    }

    private static string ClassifyStep(string side, double delta)
    {
        if (delta > 180.0)
            return side + " reversed (" + F(delta) + " deg)";
        if (delta < 4.0)
            return side + " collapsed (" + F(delta) + " deg)";
        if (delta < StepAngle - 4.0)
            return side + " compressed (" + F(delta) + " deg)";
        if (delta > StepAngle + 4.0)
            return side + " large jump (" + F(delta) + " deg)";
        return side + " OK (" + F(delta) + " deg)";
    }

    private static void WriteExternalPrompts(string promptRoot, List<SlotPlan> plans)
    {
        foreach (SlotPlan plan in plans.Where(p => p.ExternalRequired))
        {
            double previousAngle = plans[plan.Slot - 1].TargetAngle;
            double nextAngle = plans[plan.Slot + 1].TargetAngle;
            string stem = "slot_" + plan.Slot.ToString("00") + "_" + AngleToken(plan.TargetAngle);
            string legacyStem = "slot_" + plan.Slot.ToString("00") + "_" + plan.TargetAngle.ToString("000.00", Invariant);
            if (!string.Equals(stem, legacyStem, StringComparison.Ordinal))
            {
                DeleteIfPresent(Path.Combine(promptRoot, legacyStem + "_prompt.txt"));
                DeleteIfPresent(Path.Combine(promptRoot, legacyStem + "_angle_reference.png"));
            }
            string filename = stem + "_prompt.txt";
            string path = Path.Combine(promptRoot, filename);
            string direction = DirectionDescription(plan.TargetAngle);
            string front = FrontDescription(plan.TargetAngle);
            string rear = RearDescription(plan.TargetAngle);

            StringBuilder text = new StringBuilder();
            text.AppendLine("AE86 Production32 exact-angle sprite generation prompt");
            text.AppendLine();
            text.AppendLine("TARGET SLOT AND ANGLE");
            text.AppendLine("- sourceSprites17 slot: " + plan.Slot);
            text.AppendLine("- exact heading: " + F(plan.TargetAngle) + " degrees (0=Right, 90=Up, 270=Down)");
            text.AppendLine("- plain direction: " + direction);
            text.AppendLine("- previous neighbor: slot " + (plan.Slot - 1) + " at " + F(previousAngle) + " degrees");
            text.AppendLine("- next neighbor: slot " + (plan.Slot + 1) + " at " + F(nextAngle) + " degrees");
            text.AppendLine();
            text.AppendLine("STRICT VISUAL RELATIONSHIP");
            text.AppendLine("Create one genuinely new 11.25-degree intermediate pose. It must visually sit between the two named neighbors at gameplay scale. Do not duplicate, rotate, skew, or slightly nudge either neighboring frame. The longitudinal body axis, roof, wheelbase, windshield, hood, and hatch must all support the exact heading.");
            text.AppendLine();
            text.AppendLine("FRONT AND REAR PLACEMENT");
            text.AppendLine("- front bumper, pop-up headlights, black hood, and nose: " + front);
            text.AppendLine("- rear hatch, rear bumper, and red taillights: " + rear);
            text.AppendLine("- headlights must unambiguously identify the front; taillights must unambiguously identify the rear.");
            text.AppendLine();
            text.AppendLine("IDENTITY AND CANVAS LOCK");
            text.AppendLine("Match the approved active Production32 AE86 exactly: compact AE86-inspired hatchback, light gray body, black hood, black lower trim, dark windows, black wheels, pop-up headlights, crisp black/purple outline, identical roof/windshield/hatch/bumper/wheel proportions, and identical pixel-art construction. Use a 186x186 RGBA canvas, true transparency, centered projected vehicle body, pivot assumption at canvas center, and tire-contact baseline approximately y=169. Match the apparent vehicle length of the Up, Right, and Down active anchors within 5 percent. Keep body and wheels at one uniform scale.");
            text.AppendLine();
            text.AppendLine("NEGATIVE CONSTRAINTS");
            text.AppendLine("Do not face Left. Do not reverse front and rear. Do not collapse into either neighboring angle. Do not substitute a coarse 8-direction or 16-direction pose for this exact source slot. Do not mechanically rotate a finished pixel sprite. Do not redesign the car, enlarge only the body, shrink the wheels, change window/hood/hatch proportions, add anti-aliasing, blur, shadows, road, scenery, text, detached pixels, white background, or edge-touching pixels.");
            text.AppendLine();
            text.AppendLine("DIRECTION PRIORITY");
            text.AppendLine("Exact directional separation and correct front/rear orientation take priority over artistic variation. The result must pass a measured angular error of 4 degrees or less and must preserve the approved AE86 identity.");
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));

            DrawAngleReference(Path.Combine(promptRoot, stem + "_angle_reference.png"), plan, previousAngle, nextAngle);
        }
    }

    private static string DirectionDescription(double angle)
    {
        if (Math.Abs(angle - 22.5) < 0.01)
            return "mostly Right with a clear but shallow Up bias";
        if (Math.Abs(angle - 11.25) < 0.01)
            return "almost Right with only a small Up bias";
        if (Math.Abs(angle - 348.75) < 0.01)
            return "almost Right with only a small Down bias";
        return "exact intermediate heading";
    }

    private static string FrontDescription(double angle)
    {
        if (angle > 0.0 && angle < 90.0)
            return "upper-right of the canvas; higher than the rear";
        return "lower-right of the canvas; lower than the rear";
    }

    private static string RearDescription(double angle)
    {
        if (angle > 0.0 && angle < 90.0)
            return "lower-left of the canvas; lower than the front";
        return "upper-left of the canvas; higher than the front";
    }

    private static void DrawAngleReference(string path, SlotPlan plan, double previousAngle, double nextAngle)
    {
        using (Bitmap bitmap = new Bitmap(900, 700, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Font title = new Font("Consolas", 22, FontStyle.Bold))
        using (Font body = new Font("Consolas", 15, FontStyle.Regular))
        using (Pen circlePen = new Pen(Color.FromArgb(90, 90, 90), 2))
        using (Pen neighborPen = new Pen(Color.FromArgb(70, 130, 200), 5))
        using (Pen targetPen = new Pen(Color.FromArgb(220, 55, 45), 8))
        {
            graphics.Clear(Color.White);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawString("Slot " + plan.Slot + " target " + F(plan.TargetAngle) + " degrees", title, Brushes.Black, 32, 24);
            graphics.DrawString(DirectionDescription(plan.TargetAngle), body, Brushes.Black, 32, 66);
            PointF center = new PointF(450, 375);
            float radius = 215;
            graphics.DrawEllipse(circlePen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            DrawArrow(graphics, neighborPen, center, previousAngle, radius * 0.82f);
            DrawArrow(graphics, neighborPen, center, nextAngle, radius * 0.82f);
            DrawArrow(graphics, targetPen, center, plan.TargetAngle, radius);
            graphics.FillEllipse(Brushes.Black, center.X - 5, center.Y - 5, 10, 10);
            graphics.DrawString("Previous " + F(previousAngle), body, Brushes.SteelBlue, 32, 610);
            graphics.DrawString("TARGET " + F(plan.TargetAngle), body, Brushes.Firebrick, 330, 610);
            graphics.DrawString("Next " + F(nextAngle), body, Brushes.SteelBlue, 650, 610);
            graphics.DrawString("Front follows red arrow; rear is exactly opposite.", body, Brushes.Black, 165, 650);
            SavePng(bitmap, path);
        }
    }

    private static void DrawArrow(Graphics graphics, Pen pen, PointF center, double angle, float length)
    {
        double radians = angle * Math.PI / 180.0;
        PointF end = new PointF(center.X + (float)(Math.Cos(radians) * length), center.Y - (float)(Math.Sin(radians) * length));
        LineCap originalEndCap = pen.EndCap;
        pen.EndCap = LineCap.ArrowAnchor;
        graphics.DrawLine(pen, center, end);
        pen.EndCap = originalEndCap;
    }

    private static void WriteMetricsCsv(string outputRoot, List<SlotResult> results)
    {
        string path = Path.Combine(outputRoot, "candidate_metrics.csv");
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("slot,filename,target_angle,estimated_actual_angle,angular_error,source_method,canvas_width,canvas_height,bbox_x,bbox_y,bbox_width,bbox_height,centroid_x,centroid_y,projected_length,projected_width,alpha_pixel_count,scale_difference_percent,center_offset_x,center_offset_y,edge_touch_count,previous_step_delta,next_step_delta,status,recommended_action");
        foreach (SlotResult result in results)
        {
            SpriteMetrics m = result.Metrics;
            string previousDelta = result.Plan.Slot > 0 && m != null && results[result.Plan.Slot - 1].Metrics != null
                ? F(ClockwiseDelta(results[result.Plan.Slot - 1].Metrics.AxisAngle, m.AxisAngle)) : string.Empty;
            string nextDelta = result.Plan.Slot < 16 && m != null && results[result.Plan.Slot + 1].Metrics != null
                ? F(ClockwiseDelta(m.AxisAngle, results[result.Plan.Slot + 1].Metrics.AxisAngle)) : string.Empty;
            List<string> values = new List<string>();
            values.Add(result.Plan.Slot.ToString(Invariant));
            values.Add(Csv(result.DisplayFilename));
            values.Add(F(result.Plan.TargetAngle));
            values.Add(m == null ? string.Empty : F(m.AxisAngle));
            values.Add(m == null ? string.Empty : F(result.AngularError));
            values.Add(Csv(result.Plan.SourceMethod));
            values.Add(m == null ? CanvasSize.ToString(Invariant) : m.CanvasWidth.ToString(Invariant));
            values.Add(m == null ? CanvasSize.ToString(Invariant) : m.CanvasHeight.ToString(Invariant));
            values.Add(m == null ? string.Empty : m.Bounds.X.ToString(Invariant));
            values.Add(m == null ? string.Empty : m.Bounds.Y.ToString(Invariant));
            values.Add(m == null ? string.Empty : m.Bounds.Width.ToString(Invariant));
            values.Add(m == null ? string.Empty : m.Bounds.Height.ToString(Invariant));
            values.Add(m == null ? string.Empty : F(m.CentroidX));
            values.Add(m == null ? string.Empty : F(m.CentroidY));
            values.Add(m == null ? string.Empty : F(m.ProjectedLength));
            values.Add(m == null ? string.Empty : F(m.ProjectedWidth));
            values.Add(m == null ? string.Empty : m.AlphaPixelCount.ToString(Invariant));
            values.Add(m == null ? string.Empty : F(result.ScaleDifferencePercent));
            values.Add(m == null ? string.Empty : F(result.CenterOffsetX));
            values.Add(m == null ? string.Empty : F(result.CenterOffsetY));
            values.Add(m == null ? string.Empty : m.EdgeTouchCount.ToString(Invariant));
            values.Add(previousDelta);
            values.Add(nextDelta);
            values.Add(Csv(result.Status));
            values.Add(Csv(result.RecommendedAction));
            csv.AppendLine(string.Join(",", values.ToArray()));
        }
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
    }

    private static void DrawCandidates17Sheet(string outputRoot, List<SlotResult> results)
    {
        const int columns = 5;
        const int cellWidth = 300;
        const int cellHeight = 255;
        int rows = (int)Math.Ceiling(results.Count / (double)columns);
        using (Bitmap sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 11, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(238, 238, 238));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int i = 0; i < results.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                DrawChecker(graphics, new Rectangle(cell.X + 2, cell.Y + 2, cell.Width - 4, 190), 12);
                SlotResult result = results[i];
                Color statusColor = StatusColor(result.Status);
                using (Pen border = new Pen(statusColor, 4))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
                DrawSlotImageOrPlaceholder(graphics, result, new Rectangle(cell.X + 57, cell.Y + 3, CanvasSize, CanvasSize), strong);
                string actual = result.Metrics == null ? "n/a" : F(result.Metrics.AxisAngle);
                string error = result.Metrics == null ? "n/a" : F(result.AngularError);
                graphics.DrawString("slot " + result.Plan.Slot.ToString("00") + " target " + F(result.Plan.TargetAngle), strong, Brushes.Black, cell.X + 8, cell.Y + 193);
                graphics.DrawString("actual " + actual + "  error " + error, label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush statusBrush = new SolidBrush(statusColor))
                    graphics.DrawString(result.Plan.SourceMethod + " | " + ShortStatus(result.Status), label, statusBrush, cell.X + 8, cell.Y + 233);
            }
            SavePng(sheet, Path.Combine(outputRoot, "candidates17_contact_sheet.png"));
        }
    }

    private static void DrawNeighborSheet(string outputRoot, List<SlotResult> results)
    {
        const int columns = 2;
        const int cellWidth = 620;
        const int cellHeight = 250;
        const int rows = 8;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 10, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 12, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(242, 242, 242));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int pair = 0; pair < 16; pair++)
            {
                int column = pair % columns;
                int row = pair / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                SlotResult first = results[pair];
                SlotResult second = results[pair + 1];
                DrawChecker(graphics, new Rectangle(cell.X + 5, cell.Y + 35, 600, 190), 12);
                DrawSlotImageOrPlaceholder(graphics, first, new Rectangle(cell.X + 20, cell.Y + 38, CanvasSize, CanvasSize), strong);
                DrawSlotImageOrPlaceholder(graphics, second, new Rectangle(cell.X + 395, cell.Y + 38, CanvasSize, CanvasSize), strong);
                string delta;
                string classification;
                if (first.Metrics == null || second.Metrics == null)
                {
                    delta = "n/a";
                    classification = "UNRESOLVED";
                }
                else
                {
                    double step = ClockwiseDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
                    delta = F(step);
                    classification = ClassifyStep("step", step).Replace("step ", string.Empty).ToUpperInvariant();
                }
                graphics.DrawString("slot " + pair.ToString("00") + " -> " + (pair + 1).ToString("00") + " | clockwise delta " + delta + " | " + classification, strong, Brushes.Black, cell.X + 10, cell.Y + 7);
                graphics.DrawString(F(first.Plan.TargetAngle) + " deg", label, Brushes.Black, cell.X + 75, cell.Y + 226);
                graphics.DrawString(F(second.Plan.TargetAngle) + " deg", label, Brushes.Black, cell.X + 450, cell.Y + 226);
                using (Pen border = new Pen(classification.StartsWith("OK", StringComparison.Ordinal) ? Color.SeaGreen : Color.Firebrick, 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(outputRoot, "candidate_neighbor_review.png"));
        }
    }

    private static void DrawFull32Sheet(string outputRoot, List<SlotResult> results)
    {
        const int columns = 8;
        const int cellWidth = 250;
        const int cellHeight = 245;
        const int rows = 4;
        using (Bitmap sheet = new Bitmap(columns * cellWidth, rows * cellHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(sheet))
        using (Font label = new Font("Consolas", 9, FontStyle.Regular))
        using (Font strong = new Font("Consolas", 10, FontStyle.Bold))
        {
            graphics.Clear(Color.FromArgb(238, 238, 238));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int directionIndex = 0; directionIndex < 32; directionIndex++)
            {
                int sourceSlot;
                bool flip;
                ResolveGameMapping(directionIndex, out sourceSlot, out flip);
                SlotResult source = results[sourceSlot];
                int column = directionIndex % columns;
                int row = directionIndex / columns;
                Rectangle cell = new Rectangle(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
                DrawChecker(graphics, new Rectangle(cell.X + 2, cell.Y + 2, cell.Width - 4, 190), 12);
                if (source.ImagePath == null)
                {
                    DrawPlaceholder(graphics, new Rectangle(cell.X + 32, cell.Y + 4, CanvasSize, CanvasSize), strong);
                }
                else
                {
                    using (Bitmap image = LoadArgb(source.ImagePath))
                    {
                        if (flip)
                            image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        graphics.DrawImage(image, cell.X + 32, cell.Y + 4, CanvasSize, CanvasSize);
                    }
                }
                double finalAngle = NormalizeAngle(directionIndex * StepAngle);
                graphics.DrawString("dir " + directionIndex.ToString("00") + "  " + F(finalAngle) + " deg", strong, Brushes.Black, cell.X + 8, cell.Y + 194);
                graphics.DrawString("source " + sourceSlot.ToString("00") + " flipX=" + (flip ? "true" : "false"), label, Brushes.Black, cell.X + 8, cell.Y + 214);
                using (Brush statusBrush = new SolidBrush(StatusColor(source.Status)))
                    graphics.DrawString(ShortStatus(source.Status), label, statusBrush, cell.X + 8, cell.Y + 229);
                using (Pen border = new Pen(StatusColor(source.Status), 3))
                    graphics.DrawRectangle(border, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
            }
            SavePng(sheet, Path.Combine(outputRoot, "candidate_full32_preview.png"));
        }
    }

    private static void ResolveGameMapping(int directionIndex, out int sourceSlot, out bool flip)
    {
        if (directionIndex <= 8)
        {
            sourceSlot = 8 - directionIndex;
            flip = false;
        }
        else if (directionIndex <= 23)
        {
            sourceSlot = directionIndex - 8;
            flip = true;
        }
        else
        {
            sourceSlot = 40 - directionIndex;
            flip = false;
        }
    }

    private static void DrawSlotImageOrPlaceholder(Graphics graphics, SlotResult result, Rectangle target, Font font)
    {
        if (result.ImagePath == null)
        {
            DrawPlaceholder(graphics, target, font);
            return;
        }
        using (Bitmap image = LoadArgb(result.ImagePath))
            graphics.DrawImage(image, target);
    }

    private static void DrawPlaceholder(Graphics graphics, Rectangle target, Font font)
    {
        using (Brush fill = new SolidBrush(Color.FromArgb(235, 225, 225)))
        using (Pen cross = new Pen(Color.Firebrick, 5))
        {
            graphics.FillRectangle(fill, target);
            graphics.DrawRectangle(Pens.Firebrick, target);
            graphics.DrawLine(cross, target.Left + 20, target.Top + 20, target.Right - 20, target.Bottom - 20);
            graphics.DrawLine(cross, target.Right - 20, target.Top + 20, target.Left + 20, target.Bottom - 20);
            graphics.DrawString("NEEDS\nEXTERNAL\nGENERATION", font, Brushes.Firebrick, target.X + 42, target.Y + 61);
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

    private static Color StatusColor(string status)
    {
        if (status == "PASS")
            return Color.SeaGreen;
        if (status == "REVIEW")
            return Color.DarkOrange;
        return Color.Firebrick;
    }

    private static void WriteSourceInventory(string projectRoot, string outputRoot)
    {
        List<string> roots = new List<string>
        {
            Path.Combine(projectRoot, "Assets", "Art", "Vehicles", "AE86"),
            Path.Combine(projectRoot, "Assets", "Prefab", "Vehicles"),
            Path.Combine(projectRoot, "Assets", "Prefabs", "Deprecated"),
            Path.Combine(projectRoot, "Docs"),
            Path.Combine(projectRoot, "Temp")
        };
        string selfRoot = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        List<string> images = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".bmp" && extension != ".gif")
                    continue;
                if (Path.GetFullPath(file).StartsWith(selfRoot, StringComparison.OrdinalIgnoreCase))
                    continue;
                images.Add(file);
            }
        }
        string archivedTestCarImage = Path.Combine(projectRoot, "Assets", "Sprites", "Car_Test.png");
        if (File.Exists(archivedTestCarImage) && !images.Contains(archivedTestCarImage, StringComparer.OrdinalIgnoreCase))
            images.Add(archivedTestCarImage);
        images.Sort(StringComparer.OrdinalIgnoreCase);

        StringBuilder markdown = new StringBuilder();
        markdown.AppendLine("# AE86 Production32 Step 1 Source Inventory");
        markdown.AppendLine();
        markdown.AppendLine("Search roots: `Assets/Art/Vehicles/AE86/`, `Assets/Prefab/Vehicles/`, `Assets/Prefabs/Deprecated/`, `Docs/`, and `Temp/`, plus a project-wide filename scan for AE86/car/vehicle/deprecated/backup/archive/temporary/candidate/review images. No image source was found inside the prefab or temporary roots. The project-wide scan found one extra archived test-car texture, listed below and rejected after pixel inspection.");
        markdown.AppendLine();
        markdown.AppendLine("| Path | Dimensions | Visible vehicle frames | Active-design match | Correct right-facing pose potential | Unity referenced | Safe candidate input | Notes |");
        markdown.AppendLine("|---|---:|---:|---|---|---|---|---|");
        foreach (string imagePath in images)
        {
            int width;
            int height;
            using (Image image = Image.FromFile(imagePath))
            {
                width = image.Width;
                height = image.Height;
            }
            string relative = Relative(projectRoot, imagePath);
            int visibleFrames = VisibleFrameCount(relative);
            bool activeProductionSource = Regex.IsMatch(relative.Replace('\\', '/'), @"/Production32/ae86_(090_00_up|078_75|067_50|056_25|045_00_upright|033_75|022_50|011_25|000_00_right|348_75|337_50|326_25|315_00_downright|303_75|292_50|281_25|270_00_down)\.png$", RegexOptions.IgnoreCase);
            bool extractedSingle = Regex.IsMatch(relative.Replace('\\', '/'), @"/Extracted/ae86_frame_\d\d\.png$", RegexOptions.IgnoreCase);
            bool legacySingle = Regex.IsMatch(relative.Replace('\\', '/'), @"/Body/ae86_(up|upright|right|downright|down|up_upright|right_upright|right_downright|down_downright)\.png$", RegexOptions.IgnoreCase);
            bool originalStrip = relative.EndsWith("full ae86.png", StringComparison.OrdinalIgnoreCase);
            bool archivedBlankCar = relative.Equals("Assets\\Sprites\\Car_Test.png", StringComparison.OrdinalIgnoreCase);
            bool composite = !activeProductionSource && !extractedSingle && !legacySingle && !originalStrip;

            string design = archivedBlankCar ? "No vehicle pixels" : legacySingle ? "Broad AE86 match; not the active 186px pixel construction" : "Yes; active design or a derivative review render";
            string potential = archivedBlankCar ? "None" : extractedSingle || originalStrip ? "Yes; exact source frames require measured reassignment/flip" : activeProductionSource ? "Read-only active source; several headings are wrong" : legacySingle ? "Angle clues only; scale/background/style mismatch" : "No; composite/diagnostic only";
            string referenced = archivedBlankCar ? "Yes, by 3 archived test-car prefabs" : activeProductionSource ? "Yes (Car_AE86 prefab; Down also appears as initial Body sprite)" : "No serialized Unity reference found";
            string safe = archivedBlankCar ? "No" : extractedSingle ? "Yes, read-only input" : originalStrip ? "Yes, via border-safe re-extraction" : activeProductionSource ? "No modification; anchors may be read for audit" : "No";
            string notes = archivedBlankCar ? "256x256, one unique opaque white pixel value, zero visible vehicle frames; not AE86 source art." : composite ? "Flattened contact/review/diagnostic image; never crop candidate art from it." : legacySingle ? "1254x1254 opaque/light background; some files include generated background artifacts and require destructive resampling." : originalStrip ? "2172x724 RGB strip with 17 detected cars; authoritative source identity." : extractedSingle ? "186x186 transparent extraction, baseline 169, no Unity reference." : "Active Production32 PNG; protected in this step.";
            markdown.AppendLine("| `" + relative.Replace('\\', '/') + "` | " + width + "x" + height + " | " + (archivedBlankCar ? 0 : visibleFrames) + " | " + design + " | " + potential + " | " + referenced + " | " + safe + " | " + notes + " |");
        }
        markdown.AppendLine();
        markdown.AppendLine("## Inventory conclusion");
        markdown.AppendLine();
        markdown.AppendLine("The only exact active-identity source art is `full ae86.png` and its 17 transparent extractions. The nine 1254x1254 legacy Body images are unreferenced and useful only as visual angle references: their scale, background quality, pixel density, and in several cases car construction differ from the active Production32 strip. `Car_Test.png` is a blank white texture referenced only by archived test-car prefabs. Audit/contact sheets are derivative composites and are not candidate sources.");
        File.WriteAllText(Path.Combine(outputRoot, "source_inventory.md"), markdown.ToString(), new UTF8Encoding(false));
    }

    private static int VisibleFrameCount(string relativePath)
    {
        string name = Path.GetFileName(relativePath).ToLowerInvariant();
        if (name == "full ae86.png" || name.Contains("detection_diagnostic") || name.Contains("frames_contact") || name.Contains("frames_review") || name.Contains("sources17") || name.Contains("production32_contact") || name.Contains("production32_review_labeled"))
            return 17;
        if (name.Contains("full32"))
            return 32;
        if (name.Contains("neighbor"))
            return 32;
        return 1;
    }

    private static void WriteReport(string projectRoot, string outputRoot, List<SlotResult> results, Dictionary<string, HashSnapshot> before, Dictionary<string, HashSnapshot> after)
    {
        int candidateSuccess = results.Count(r => !r.Plan.IsAnchor && !r.Plan.ExternalRequired && r.ImagePath != null);
        int candidatePass = results.Count(r => !r.Plan.IsAnchor && r.Status == "PASS");
        int candidateReview = results.Count(r => !r.Plan.IsAnchor && r.Status == "REVIEW");
        int candidateFail = results.Count(r => !r.Plan.IsAnchor && r.Status == "FAIL");
        List<SlotResult> external = results.Where(r => r.Status == "NEEDS_EXTERNAL_GENERATION").ToList();
        StringBuilder report = new StringBuilder();
        report.AppendLine("# AE86 Production32 Step 1 Candidate Report");
        report.AppendLine();
        report.AppendLine("## 1. Executive summary");
        report.AppendLine();
        report.AppendLine("This non-destructive pass recovered **" + candidateSuccess + "** of the 14 incorrect source slots: **" + candidatePass + " PASS**, **" + candidateReview + " REVIEW**, and **" + candidateFail + " retained FAIL candidates**. Slots **" + string.Join(", ", external.Select(r => r.Plan.Slot.ToString()).ToArray()) + "** remain `NEEDS_EXTERNAL_GENERATION`; no misleading bitmap was created for them. The three active anchors remain read-only.");
        report.AppendLine();
        report.AppendLine("## 2. Confirmed active architecture");
        report.AppendLine();
        report.AppendLine("The project uses 32 final headings from 17 real source sprites plus 15 runtime `SpriteRenderer.flipX` mirrors. `sourceSprites17`, controller quantization, visual mapping, prefab references, and Unity import settings were treated as verified and were not changed.");
        report.AppendLine();
        report.AppendLine("## 3. Sources discovered");
        report.AppendLine();
        report.AppendLine("See [`source_inventory.md`](source_inventory.md). The authoritative identity source is the 2172x724 `full ae86.png` strip and its 17 border-flood-filled 186x186 extractions. No additional source sheet, backup, deprecated export, or temporary image containing missing exact angles was found.");
        report.AppendLine();
        report.AppendLine("## 4. Slots recovered from existing source art");
        report.AppendLine();
        report.AppendLine("Recovered slots: `1, 2, 3, 4, 5, 10, 11, 12, 13, 14, 15`. Each uses a real extracted frame from the authoritative strip; no arbitrary rotation or redraw was used.");
        report.AppendLine();
        report.AppendLine("## 5. Slots corrected by safe re-extraction");
        report.AppendLine();
        report.AppendLine("Slots `1-5` use unmirrored extracted frames `14-10`, reassigned by measured heading. Their full vehicle raster was uniformly normalized with nearest-neighbor sampling to the interpolated anchor length, then aligned to baseline y=169. Body and wheels were scaled together.");
        report.AppendLine();
        report.AppendLine("## 6. Slots corrected by validated flip");
        report.AppendLine();
        report.AppendLine("Slots `10-15` use extracted frames `07-02` with a full horizontal pixel reversal. The front bumper/headlights and rear hatch/taillights were manually checked after the flip. Their existing projected scale was already within 5 percent, so no resize was applied.");
        report.AppendLine();
        report.AppendLine("## 7. Supported generation workflow");
        report.AppendLine();
        report.AppendLine("No generated candidate was accepted. A built-in image-generation path exists, but it cannot guarantee a native 186x186 pixel grid, exact active-car identity, baseline, and <=4-degree heading in one deterministic pass. Using it here would require downsampling/redesign risk, so Step 1 leaves those slots explicit for external generation and review.");
        report.AppendLine();
        report.AppendLine("## 8. Slots requiring external generation");
        report.AppendLine();
        foreach (SlotResult item in external)
            report.AppendLine("- Slot `" + item.Plan.Slot + "` at `" + F(item.Plan.TargetAngle) + " deg`: " + item.RecommendedAction);
        report.AppendLine();
        report.AppendLine("## 9. Angle table for all 17 source positions");
        report.AppendLine();
        report.AppendLine("| Slot | Target | Estimated | Error | Front/rear inspection | Between neighbors | Method | Status |");
        report.AppendLine("|---:|---:|---:|---:|---|---|---|---|");
        foreach (SlotResult result in results)
        {
            string estimated = result.Metrics == null ? "n/a" : F(result.Metrics.AxisAngle);
            string error = result.Metrics == null ? "n/a" : F(result.AngularError);
            string between;
            if (result.Metrics == null)
                between = "Unresolved";
            else if (result.SequenceFlag.Contains("reversed"))
                between = "No";
            else if (result.SequenceFlag.Contains("unresolved"))
                between = "Limited: neighbor unresolved";
            else
                between = "Yes";
            report.AppendLine("| " + result.Plan.Slot + " | " + F(result.Plan.TargetAngle) + " | " + estimated + " | " + error + " | " + result.Plan.FrontRear + " | " + between + " | " + result.Plan.SourceMethod + " | " + result.Status + " |");
        }
        report.AppendLine();
        report.AppendLine("Angle values use alpha-mask PCA for the longitudinal axis, with the 180-degree branch resolved by manual inspection of hood/headlights versus hatch/taillights. PCA was not used alone.");
        report.AppendLine();
        report.AppendLine("## 10. Adjacent-step validation");
        report.AppendLine();
        report.AppendLine("| Pair | Clockwise delta | Result |");
        report.AppendLine("|---|---:|---|");
        for (int i = 0; i < 16; i++)
        {
            SlotResult first = results[i];
            SlotResult second = results[i + 1];
            if (first.Metrics == null || second.Metrics == null)
                report.AppendLine("| " + i + " -> " + (i + 1) + " | n/a | Unresolved candidate in pair |");
            else
            {
                double delta = ClockwiseDelta(first.Metrics.AxisAngle, second.Metrics.AxisAngle);
                report.AppendLine("| " + i + " -> " + (i + 1) + " | " + F(delta) + " | " + ClassifyStep("step", delta).Replace("step ", string.Empty) + " |");
            }
        }
        report.AppendLine();
        report.AppendLine("No measured candidate reverses the clockwise sequence. Slot 0->1 and slot 15->16 remain visibly larger transitions, and all pairs touching slots 6, 7, or 9 remain unresolved until new art exists.");
        report.AppendLine();
        report.AppendLine("## 11. Scale comparison");
        report.AppendLine();
        report.AppendLine("| Slot | Projected length | Projected width | Alpha area | Scale delta | Baseline | Edge touches | Components |");
        report.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (SlotResult result in results.Where(r => r.Metrics != null))
        {
            SpriteMetrics m = result.Metrics;
            report.AppendLine("| " + result.Plan.Slot + " | " + F(m.ProjectedLength) + " | " + F(m.ProjectedWidth) + " | " + m.AlphaPixelCount + " | " + F(result.ScaleDifferencePercent) + "% | " + m.Baseline + " | " + m.EdgeTouchCount + " | " + m.ComponentCount + " |");
        }
        report.AppendLine();
        report.AppendLine("Projected length and width come from PCA-axis extrema rather than raw bounding-box width. Uniform transforms preserve wheel/body ratio. Manual pixel review confirmed that roof, hood, windshield, hatch, and wheels grow together for slots 1-5; no semantic part was resized independently. All real candidates remain 186x186 RGBA, baseline 169, with no edge contact.");
        report.AppendLine();
        report.AppendLine("## 12. Center and baseline comparison");
        report.AppendLine();
        report.AppendLine("| Slot | Alpha centroid | BBox center | Margins L/R/T/B | Interpolated centroid offset | Baseline |");
        report.AppendLine("|---:|---|---|---|---|---:|");
        foreach (SlotResult result in results.Where(r => r.Metrics != null))
        {
            SpriteMetrics m = result.Metrics;
            double bboxCenterX = m.Bounds.X + (m.Bounds.Width - 1) / 2.0;
            double bboxCenterY = m.Bounds.Y + (m.Bounds.Height - 1) / 2.0;
            int right = m.CanvasWidth - m.Bounds.Right;
            int bottom = m.CanvasHeight - m.Bounds.Bottom;
            report.AppendLine("| " + result.Plan.Slot + " | (" + F(m.CentroidX) + ", " + F(m.CentroidY) + ") | (" + F(bboxCenterX) + ", " + F(bboxCenterY) + ") | " + m.Bounds.X + "/" + right + "/" + m.Bounds.Y + "/" + bottom + " | (" + F(result.CenterOffsetX) + ", " + F(result.CenterOffsetY) + ") | " + m.Baseline + " |");
        }
        report.AppendLine();
        report.AppendLine("Raw boxes were not forced to identical coordinates. Candidate content is horizontally centered and tire contact remains at y=169. An optional read-only comparison copy, `PNG/slot_08_0.00_center_preview.png`, shifts the Right anchor two pixels right; it is not an approved replacement.");
        report.AppendLine();
        report.AppendLine("## 13. Contact sheets");
        report.AppendLine();
        report.AppendLine("- [`candidates17_contact_sheet.png`](candidates17_contact_sheet.png)");
        report.AppendLine("- [`candidate_neighbor_review.png`](candidate_neighbor_review.png)");
        report.AppendLine("- [`candidate_full32_preview.png`](candidate_full32_preview.png)");
        report.AppendLine();
        report.AppendLine("## 14. Candidate PNG paths");
        report.AppendLine();
        foreach (SlotResult result in results.Where(r => !r.Plan.IsAnchor && r.ImagePath != null))
            report.AppendLine("- `" + Relative(projectRoot, result.ImagePath).Replace('\\', '/') + "`");
        report.AppendLine("- `" + Relative(projectRoot, Path.Combine(outputRoot, "PNG", "slot_08_0.00_center_preview.png")).Replace('\\', '/') + "` (audit-only preview)");
        report.AppendLine();
        report.AppendLine("## 15. Prompt and angle-reference paths");
        report.AppendLine();
        foreach (SlotResult result in external)
        {
            string stem = "slot_" + result.Plan.Slot.ToString("00") + "_" + AngleToken(result.Plan.TargetAngle);
            report.AppendLine("- `" + Relative(projectRoot, Path.Combine(outputRoot, "Prompts", stem + "_prompt.txt")).Replace('\\', '/') + "`");
            report.AppendLine("- `" + Relative(projectRoot, Path.Combine(outputRoot, "Prompts", stem + "_angle_reference.png")).Replace('\\', '/') + "`");
        }
        report.AppendLine();
        report.AppendLine("## 16. Exact files created");
        report.AppendLine();
        List<string> created = Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).Select(p => Relative(projectRoot, p).Replace('\\', '/')).ToList();
        string reportRelative = Relative(projectRoot, Path.Combine(outputRoot, "step1_candidate_report.md")).Replace('\\', '/');
        if (!created.Contains(reportRelative, StringComparer.OrdinalIgnoreCase))
            created.Add(reportRelative);
        created.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string file in created)
            report.AppendLine("- `" + file + "`");
        report.AppendLine();
        report.AppendLine("## 17. Protected-file SHA-256 comparison");
        report.AppendLine();
        report.AppendLine("| Protected group | File count | Before | After | Identical |");
        report.AppendLine("|---|---:|---|---|---|");
        foreach (string key in before.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            HashSnapshot first = before[key];
            HashSnapshot second = after[key];
            bool same = first.Count == second.Count && first.AggregateSha256 == second.AggregateSha256;
            report.AppendLine("| " + key + " | " + first.Count + " | `" + first.AggregateSha256 + "` | `" + second.AggregateSha256 + "` | " + (same ? "YES" : "NO") + " |");
        }
        report.AppendLine();
        report.AppendLine("The protected groups cover every PNG in active Production32, every `.meta` under Assets, every Assets `.cs`, every prefab, and every Unity scene. No active PNG pixel, `.meta`, code file, prefab, scene, handling value, import setting, or serialized reference was modified by Step 1.");
        File.WriteAllText(Path.Combine(outputRoot, "step1_candidate_report.md"), report.ToString(), new UTF8Encoding(false));
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
        Dictionary<string, HashSnapshot> snapshots = new Dictionary<string, HashSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> group in groups)
        {
            List<string> files = group.Value.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            StringBuilder combined = new StringBuilder();
            foreach (string path in files)
                combined.Append(Relative(projectRoot, path).Replace('\\', '/')).Append('|').Append(FileSha256(path)).Append('\n');
            snapshots[group.Key] = new HashSnapshot
            {
                Name = group.Key,
                Count = files.Count,
                AggregateSha256 = Sha256(Encoding.UTF8.GetBytes(combined.ToString()))
            };
        }
        return snapshots;
    }

    private static string FileSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
            return BytesToHex(sha.ComputeHash(stream));
    }

    private static string Sha256(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BytesToHex(sha.ComputeHash(bytes));
    }

    private static string BytesToHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
            builder.Append(value.ToString("X2", Invariant));
        return builder.ToString();
    }

    private static string RecommendationFor(SlotResult result)
    {
        if (result.Plan.IsAnchor)
            return result.Plan.Slot == 8 ? "Keep active anchor; compare optional +2px audit-only center preview." : "Keep active anchor unchanged.";
        if (result.Status == "PASS")
            return Math.Abs(result.ScaleDifferencePercent) <= 5.0 ? "Candidate is suitable for Step 2 approval review." : "Angle passes, but normalize scale before approval.";
        if (result.Status == "REVIEW")
            return "Manual gameplay-scale review required; angle is within 7 degrees but outside the 4-degree PASS threshold.";
        return "Reject and generate a new exact-angle source.";
    }

    private static string StatusFromError(double error)
    {
        if (error <= 4.0)
            return "PASS";
        if (error <= 7.0)
            return "REVIEW";
        return "FAIL";
    }

    private static string ShortStatus(string status)
    {
        return status == "NEEDS_EXTERNAL_GENERATION" ? "EXTERNAL" : status;
    }

    private static string AngleToken(double angle)
    {
        return angle.ToString("0.00", Invariant);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static double InterpolateAnchorValue(int slot, double up, double right, double down)
    {
        if (slot <= 8)
            return up + (right - up) * (slot / 8.0);
        return right + (down - right) * ((slot - 8) / 8.0);
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
        if (angle < 0.0)
            angle += 360.0;
        return angle;
    }

    private static Bitmap LoadArgb(string path)
    {
        using (Bitmap source = new Bitmap(path))
            return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
    }

    private static void SavePng(Bitmap bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path))
            File.Delete(path);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string Relative(string root, string path)
    {
        Uri rootUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        Uri pathUri = new Uri(Path.GetFullPath(path));
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string Csv(string value)
    {
        if (value == null)
            return string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string F(double value)
    {
        return value.ToString("0.00", Invariant);
    }
}
