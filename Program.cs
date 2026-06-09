using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting; 

namespace RCSizerWinForms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show("CRITICAL APPLICATION ERROR ON STARTUP: \n\n" + ex.ToString(), "Micro-FAST Diagnostic Hook");
            }
        }
    }

    public class MainForm : Form
    {
        private TextBox txtMaxTod, txtWPayload, txtWBat, txtPBat, txtSpan, txtPropD, txtNaca, txtOutput, txtMathDisplay, txtAssumptionsDisplay;
        private ComboBox cbConfigType;
        private Button btnRun;
        private Panel renderPanel;
        private System.Windows.Forms.Timer animationTimer;
        private TabControl sidebarTabControl;
        private TabPage tabInputs, tabMath, tabGraphs, tabAssumptions;
        private Chart chartLd, chartTod;

        // Interactive 3D Canvas Orbit Controls
        private double angleX = 0.5;
        private double angleY = 0.6;
        private Point lastMousePos;
        private bool isDragging = false;
        private double propSpinAngle = 0.0;

        // Global Dynamic Aerodynamic Matrix Values
        private double currentSpan = 1.9;
        private double currentChord = 0.28;
        private double currentPropD = 11.0;
        private double currentSweepDeg = 0.0;
        private double currentDihedralDeg = 0.0;
        private double currentThicknessRatio = 0.12; 
        private double currentFuseLength = 1.33;     
        private string activeAirframeType = "Traditional High-Wing";

        // NACA Airfoil Geometry Storage Coords
        private List<PointF> airfoilProfileUpper = new List<PointF>();
        private List<PointF> airfoilProfileLower = new List<PointF>();

        // Stability Calculations Outputs
        private double wingZLocation = 0.0; 
        private double calcBalsaWeight = 0.0;
        private double calculatedHStabSpan = 0.0;
        private double calculatedHStabChord = 0.0;
        private double calculatedVStabHeight = 0.0;
        private double calculatedVStabChord = 0.0;
        private double calculatedHStabArea = 0.0;
        private double calculatedVStabArea = 0.0;

        public class Configuration
        {
            public double Chord { get; set; }
            public double TotalWeight { get; set; }
            public double TakeoffDist { get; set; }
            public double StallSpeed { get; set; }
            public double AspectRatio { get; set; }
            public double SweepAngle { get; set; }
            public double DihedralAngle { get; set; }
            public double ThicknessRatio { get; set; }
            public double FuselageLength { get; set; }
            public double CalculatedBalsaMass { get; set; }
            public double WingZPos { get; set; }
            public double HStabArea { get; set; }
            public double HStabSpan { get; set; }
            public double HStabChord { get; set; }
            public double VStabArea { get; set; }
            public double VStabHeight { get; set; }
            public double VStabChord { get; set; }
            public double MaxCl { get; set; }
            public List<Tuple<double, double>> LdCurveData { get; set; } = new List<Tuple<double, double>>();
            public List<Tuple<double, double>> TodSensitivityData { get; set; } = new List<Tuple<double, double>>();
        }

        private struct Vector3D
        {
            public double X, Y, Z;
            public Vector3D(double x, double y, double z) { X = x; Y = y; Z = z; }
        }

        private class Face3D
        {
            public int[] VertexIndices;
            public Color BaseColor;
            public double AverageZ;
            public Vector3D Normal;
            public Face3D(int[] indices, Color color) { VertexIndices = indices; BaseColor = color; }
        }

        public MainForm()
        {
            this.Text = "Micro-Sizer 3D Proportional Engine";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable; 
            this.BackColor = Color.FromArgb(24, 24, 26);

            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            sidebarTabControl = new TabControl
            {
                Width = 440,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(24, 24, 28)
            };

            tabInputs = new TabPage { Text = "Configuration", BackColor = Color.FromArgb(24, 24, 28) };
            tabMath = new TabPage { Text = "Aerodynamic Math", BackColor = Color.FromArgb(24, 24, 28) };
            tabGraphs = new TabPage { Text = "Performance Graphs", BackColor = Color.FromArgb(24, 24, 28) };
            tabAssumptions = new TabPage { Text = "Design Assumptions", BackColor = Color.FromArgb(24, 24, 28) };

            sidebarTabControl.TabPages.Add(tabInputs);
            sidebarTabControl.TabPages.Add(tabMath);
            sidebarTabControl.TabPages.Add(tabGraphs);
            sidebarTabControl.TabPages.Add(tabAssumptions);

            FlowLayoutPanel layoutContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(15),
                AutoScroll = true
            };

            Label lblTitle = new Label { Text = "PROPORTIONAL FLIGHT OPTIMIZER", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0,0,0,10) };
            layoutContainer.Controls.Add(lblTitle);

            txtMaxTod = CreateInput(layoutContainer, "Max Takeoff Distance (m):", "15.0");
            txtWPayload = CreateInput(layoutContainer, "Payload Mass (kg):", "0.75");
            txtWBat = CreateInput(layoutContainer, "Battery Mass (kg):", "0.45");
            txtPBat = CreateInput(layoutContainer, "Available Power (Watts):", "650");
            txtSpan = CreateInput(layoutContainer, "Target Wingspan (m):", "1.90");
            txtPropD = CreateInput(layoutContainer, "Propeller Diameter (in):", "11.0");
            txtNaca = CreateInput(layoutContainer, "NACA 4-Digit Airfoil Profile:", "2412");

            Label lblType = new Label { Text = "Configuration Template:", ForeColor = Color.DarkGray, AutoSize = true, Margin = new Padding(0, 6, 0, 2) };
            cbConfigType = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };
            cbConfigType.Items.AddRange(new string[] { "Traditional High-Wing", "Traditional Mid-Wing", "Traditional Low-Wing", "Delta Flying Wing", "Swept Wing" });
            cbConfigType.SelectedIndex = 0;
            layoutContainer.Controls.Add(lblType);
            layoutContainer.Controls.Add(cbConfigType);

            btnRun = new Button
            {
                Text = "RUN PROPORTIONAL OPTIMIZATION LOOP",
                Size = new Size(380, 40),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 15, 0, 15)
            };
            btnRun.Click += BtnRun_Click;
            layoutContainer.Controls.Add(btnRun);

            Label lblOut = new Label { Text = "STABILITY FLIGHT REPORT MATRIX:", ForeColor = Color.FromArgb(46, 204, 113), AutoSize = true };
            layoutContainer.Controls.Add(lblOut);

            txtOutput = new TextBox
            {
                Multiline = true,
                Size = new Size(380, 260),
                BackColor = Color.FromArgb(32, 32, 36),
                ForeColor = Color.FromArgb(0, 255, 180),
                Font = new Font("Consolas", 8.5f),
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None
            };
            layoutContainer.Controls.Add(txtOutput);
            tabInputs.Controls.Add(layoutContainer);

            txtMathDisplay = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 22),
                ForeColor = Color.FromArgb(230, 230, 240),
                Font = new Font("Consolas", 9.5f),
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };
            tabMath.Controls.Add(txtMathDisplay);

            txtAssumptionsDisplay = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 22),
                ForeColor = Color.FromArgb(200, 220, 200),
                Font = new Font("Consolas", 9.5f),
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10)
            };
            tabAssumptions.Controls.Add(txtAssumptionsDisplay);
            PopulateStaticAssumptionsText();

            TableLayoutPanel graphLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.FromArgb(24, 24, 28),
                Padding = new Padding(10)
            };
            graphLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            graphLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            chartLd = CreateEmbeddedChart("Lift-to-Drag Ratio (L/D) vs Speed", "Airspeed (m/s)", "L/D Ratio", SeriesChartType.Spline, Color.DeepSkyBlue);
            chartTod = CreateEmbeddedChart("Takeoff Distance Sensitivity", "Total Weight (kg)", "Takeoff Run (m)", SeriesChartType.Spline, Color.OrangeRed);

            graphLayout.Controls.Add(chartLd, 0, 0);
            graphLayout.Controls.Add(chartTod, 0, 1);
            tabGraphs.Controls.Add(graphLayout);

            this.Controls.Add(sidebarTabControl);

            renderPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 12, 14) };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(renderPanel, true, null);

            renderPanel.Paint += RenderPanel_Paint;
            renderPanel.MouseDown += (s, e) => { if(e.Button == MouseButtons.Left) { isDragging = true; lastMousePos = e.Location; } };
            renderPanel.MouseMove += (s, e) => {
                if (isDragging) {
                    angleY += (e.X - lastMousePos.X) * 0.007;
                    angleX += (e.Y - lastMousePos.Y) * 0.007;
                    lastMousePos = e.Location;
                    renderPanel.Invalidate();
                }
            };
            renderPanel.MouseUp += (s, e) => { if(e.Button == MouseButtons.Left) isDragging = false; };
            
            this.Controls.Add(renderPanel);

            animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            animationTimer.Tick += (s, e) => { propSpinAngle += 0.45; renderPanel.Invalidate(); };
            animationTimer.Start();

            BtnRun_Click(this, EventArgs.Empty);
        }

        private TextBox CreateInput(FlowLayoutPanel p, string labelText, string defaultVal)
        {
            Label lbl = new Label { Text = labelText, ForeColor = Color.LightGray, AutoSize = true, Margin = new Padding(0, 4, 0, 1) };
            TextBox tb = new TextBox { Text = defaultVal, Width = 380, BackColor = Color.FromArgb(42, 42, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            p.Controls.Add(lbl);
            p.Controls.Add(tb);
            return tb;
        }

        private Chart CreateEmbeddedChart(string title, string xAxisTitle, string yAxisTitle, SeriesChartType type, Color lineStyleColor)
        {
            Chart chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 36), Margin = new Padding(0, 5, 0, 10) };
            ChartArea area = new ChartArea("MainArea") { BackColor = Color.FromArgb(24, 24, 26) };
            area.AxisX.LabelStyle.ForeColor = Color.White;
            area.AxisY.LabelStyle.ForeColor = Color.White;
            area.AxisX.TitleForeColor = Color.LightGray;
            area.AxisY.TitleForeColor = Color.LightGray;
            area.AxisX.Title = xAxisTitle;
            area.AxisY.Title = yAxisTitle;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(50, 50, 55);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(50, 50, 55);
            chart.ChartAreas.Add(area);
            
            Series series = new Series("DataSeries")
            {
                ChartType = type,
                XValueType = ChartValueType.Double,
                YValueType = ChartValueType.Double,
                Color = lineStyleColor,
                BorderWidth = 3
            };
            chart.Series.Add(series);

            Title chartTitle = new Title(title, Docking.Top, new Font("Segoe UI", 9.5f, FontStyle.Bold), Color.White);
            chart.Titles.Add(chartTitle);
            return chart;
        }

        private void PopulateStaticAssumptionsText()
        {
            txtAssumptionsDisplay.Text = 
                "=== CORE SIMULATION ENGINE ASSUMPTIONS ===\r\n\r\n" +
                "1. ATMOSPHERE & ENVIRONMENT\r\n" +
                "   - Standard Air Density (rho): Fixed at 1.225 kg/m³\r\n" +
                "     (Assumes operation at standard sea-level bounds).\r\n" +
                "   - Ground Friction Coeff (mu): Constant 0.06\r\n" +
                "     (Models standard short-grass/rough runway conditions).\r\n\r\n" +
                "2. AERODYNAMIC PROPERTIES\r\n" +
                "   - Oswald Efficiency Factor (e): Evaluated at 0.76.\r\n" +
                "   - Baseline Max Lift (Cl_max): Derived as 1.1 + (Camber * 5).\r\n" +
                "   - Parasitic Drag Profile (Cd0):\r\n" +
                "     Calculated via skin build-up estimates penalizing profile thickness\r\n" +
                "     ratios up to the 4th power (t/c^4).\r\n\r\n" +
                "3. STABILITY BOUNDS (SCALE PROPORTIONS)\r\n" +
                "   - Target Horizontal Tail Coeff (Vh): Fixed to 0.38\r\n" +
                "   - Target Vertical Tail Coeff (Vv): Fixed to 0.025\r\n" +
                "   - Moment Arms (Lh, Lv): Calculated at exactly 55% of total\r\n" +
                "     fuselage length to enforce realistic RC structural distributions.\r\n" +
                "   - Fuselage Sizing Window: Limited strictly between 70% and 95%\r\n" +
                "     of the current targeted wingspan layout.\r\n\r\n" +
                "4. MASS MATRIX DISTRIBUTION\r\n" +
                "   - Structural Framework Density: Evaluated at 130 kg/m³\r\n" +
                "     (Standard cured balsa framework construction layout).\r\n" +
                "   - Constant Structural Overhead: 220g offset tracking lines,\r\n" +
                "     linkages, servos, surface covering film, and receiver hardware.";
        }

        private void GenerateNacaCoordinates(string code, double thicknessScale)
        {
            airfoilProfileUpper.Clear();
            airfoilProfileLower.Clear();

            if (code.Length != 4 || !int.TryParse(code, out int val)) { code = "0012"; }

            double m = (code[0] - '0') / 100.0;  
            double p = (code[1] - '0') / 10.0;   
            double t = thicknessScale;

            int stations = 12; 
            for (int i = 0; i <= stations; i++)
            {
                double x = (double)i / stations;
                double yt = 5.0 * t * (0.2969 * Math.Sqrt(x) - 0.1260 * x - 0.3516 * x * x + 0.2843 * Math.Pow(x, 3) - 0.1015 * Math.Pow(x, 4));
                double yc = 0; double slope = 0;

                if (p > 0)
                {
                    if (x <= p) { yc = (m / (p * p)) * (2.0 * p * x - x * x); slope = ((2.0 * m) / (p * p)) * (p - x); }
                    else { yc = (m / (Math.Pow(1.0 - p, 2))) * ((1.0 - 2.0 * p) + 2.0 * p * x - x * x); slope = ((2.0 * m) / (Math.Pow(1.0 - p, 2))) * (p - x); }
                }

                double theta = Math.Atan(slope);
                double xu = x - yt * Math.Sin(theta); double yu = yc + yt * Math.Cos(theta);
                double xl = x + yt * Math.Sin(theta); double yl = yc - yt * Math.Cos(theta);

                airfoilProfileUpper.Add(new PointF((float)(xu - 0.5), (float)yu));
                airfoilProfileLower.Add(new PointF((float)(xl - 0.5), (float)yl));
            }
        }

        private void BtnRun_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(txtMaxTod.Text, out double maxTod) ||
                !double.TryParse(txtWPayload.Text, out double wPayload) ||
                !double.TryParse(txtWBat.Text, out double wBat) ||
                !double.TryParse(txtPBat.Text, out double pBat) ||
                !double.TryParse(txtSpan.Text, out double span) ||
                !double.TryParse(txtPropD.Text, out double propD))
            {
                txtOutput.Text = "Parameter processing configuration error.";
                return;
            }

            string nacaInput = txtNaca.Text.Trim();
            if (nacaInput.Length != 4) nacaInput = "2412";

            double inputCamber = (nacaInput[0] - '0') / 100.0;
            double structuralClBase = 1.1 + (inputCamber * 5.0); 

            activeAirframeType = cbConfigType.SelectedItem?.ToString() ?? "Traditional High-Wing";
            List<Configuration> validConfigs = new List<Configuration>();
            double propArea = Math.PI * Math.Pow((propD * 0.0254) / 2.0, 2);
            double tStatic = Math.Pow(1.225 * propArea * Math.Pow(pBat, 2), 1.0 / 3.0) * 0.9;

            double sweepMin = 0; double sweepMax = 5;
            double targetDihedral = 0;

            if (activeAirframeType == "Swept Wing") { sweepMin = 10; sweepMax = 45; targetDihedral = 2.0; }
            else if (activeAirframeType == "Delta Flying Wing") { sweepMin = 35; sweepMax = 60; targetDihedral = 0.0; }
            else if (activeAirframeType == "Traditional Low-Wing") { sweepMin = 0; sweepMax = 8; targetDihedral = 5.5; }
            else if (activeAirframeType == "Traditional High-Wing") { sweepMin = 0; sweepMax = 8; targetDihedral = -2.5; }
            else { sweepMin = 0; sweepMax = 8; targetDihedral = 1.0; }

            long totalIterationsChecked = 0;

            for (double c = 0.12; c <= 0.55; c += 0.01) 
            {
                for (double tRatio = 0.05; tRatio <= 0.22; tRatio += 0.01) 
                {
                    for (double fLenScale = 0.70; fLenScale <= 0.95; fLenScale += 0.02) 
                    {
                        for (double sweep = sweepMin; sweep <= sweepMax; sweep += 2.5) 
                        {
                            totalIterationsChecked++;
                            double fLen = span * fLenScale;
                            double totalStrutLength = (fLen * 4.0) + (c * 0.28 * 14.0); 
                            double balsaVolume = totalStrutLength * (0.006 * 0.006); 
                            double balsaMass = balsaVolume * 130.0; 

                            double wTotal = balsaMass + wPayload + wBat + 0.22; 
                            double weightN = wTotal * 9.81;
                            double s = span * c;
                            double ar = Math.Pow(span, 2) / s;

                            double radSweep = sweep * Math.PI / 180.0;
                            double effectiveClMax = structuralClBase * Math.Cos(radSweep);

                            double vStall = Math.Sqrt((2.0 * weightN) / (1.225 * s * effectiveClMax));
                            double vLO = 1.2 * vStall;
                            double vAvg = 0.7 * vLO;

                            double cLAvg = weightN / (0.5 * 1.225 * Math.Pow(vAvg, 2) * s);
                            double camberPenalty = 1.0 + (inputCamber * 2.5);
                            double cD0 = 0.022 * (1.0 + 2.0 * tRatio + 60.0 * Math.Pow(tRatio, 4)) * camberPenalty;
                            double cD = cD0 + (1.0 / (Math.PI * ar * 0.76)) * Math.Pow(cLAvg, 2);

                            double drag = 0.5 * 1.225 * Math.Pow(vAvg, 2) * s * cD;
                            double lift = 0.5 * 1.225 * Math.Pow(vAvg, 2) * s * cLAvg;

                            double fAccel = tStatic - drag - 0.06 * (weightN - lift);
                            if (fAccel <= 0) continue;

                            double tod = (wTotal * Math.Pow(vLO, 2)) / (2.0 * fAccel);
                            if (tod <= maxTod)
                            {
                                double optWingZ = fLen * 0.15; 
                                if (activeAirframeType == "Delta Flying Wing") optWingZ = 0.0;

                                double momentArmH = fLen * 0.55; 
                                double momentArmV = fLen * 0.55; 

                                double sHStab = (0.38 * s * c) / momentArmH;
                                double sVStab = (0.025 * s * span) / momentArmV;

                                double hSpan = Math.Sqrt(sHStab * 4.2); 
                                double hChord = sHStab / hSpan;

                                double vHeight = Math.Sqrt(sVStab * 1.6);
                                double vChord = sVStab / vHeight;

                                var config = new Configuration
                                {
                                    Chord = c, TotalWeight = wTotal, TakeoffDist = tod, StallSpeed = vStall,
                                    AspectRatio = ar, SweepAngle = sweep, DihedralAngle = targetDihedral,
                                    ThicknessRatio = tRatio, FuselageLength = fLen, CalculatedBalsaMass = balsaMass,
                                    WingZPos = optWingZ,
                                    HStabArea = sHStab, HStabSpan = hSpan, HStabChord = hChord,
                                    VStabArea = sVStab, VStabHeight = vHeight, VStabChord = vChord,
                                    MaxCl = effectiveClMax
                                };

                                for (double v = Math.Ceiling(vStall); v <= vStall + 25; v += 1.5)
                                {
                                    double localCl = weightN / (0.5 * 1.225 * Math.Pow(v, 2) * s);
                                    double localCd = cD0 + (1.0 / (Math.PI * ar * 0.76)) * Math.Pow(localCl, 2);
                                    config.LdCurveData.Add(new Tuple<double, double>(v, localCl / localCd));
                                }

                                for (double weightScale = wTotal - 0.5; weightScale <= wTotal + 1.0; weightScale += 0.1)
                                {
                                    double wN = weightScale * 9.81;
                                    double vS = Math.Sqrt((2.0 * wN) / (1.225 * s * effectiveClMax));
                                    double vL = 1.2 * vS; double vA = 0.7 * vL;
                                    double cLA = wN / (0.5 * 1.225 * Math.Pow(vA, 2) * s);
                                    double cDA = cD0 + (1.0 / (Math.PI * ar * 0.76)) * Math.Pow(cLA, 2);
                                    double fAc = tStatic - (0.5 * 1.225 * Math.Pow(vA, 2) * s * cDA) - 0.06 * (wN - (0.5 * 1.225 * Math.Pow(vA, 2) * s * cLA));
                                    if (fAc > 0) config.TodSensitivityData.Add(new Tuple<double, double>(weightScale, (weightScale * Math.Pow(vL, 2)) / (2.0 * fAc)));
                                }

                                validConfigs.Add(config);
                            }
                        }
                    }
                }
            }

            if (!validConfigs.Any())
            {
                txtOutput.Text = $"No configurations survived optimization matrix limits.";
                return;
            }

            var best = validConfigs.OrderBy(x => x.StallSpeed).First();
            string dihedralLabel = best.DihedralAngle < 0 ? $"Anhedral Angle     : {Math.Abs(best.DihedralAngle):F1}° (Stabilizing)" : $"Dihedral Angle     : {best.DihedralAngle:F1}°";

            // Spatial alignment definitions
            double calcTailZ = best.FuselageLength * 0.72;
            double fuseW = best.Chord * 0.28;
            double wingYVerticalOffset = activeAirframeType.Contains("High") ? -fuseW / 2 : (activeAirframeType.Contains("Low") ? fuseW / 2 : 0);
            if (activeAirframeType == "Delta Flying Wing") wingYVerticalOffset = 0;

            txtOutput.Text = $"SUCCESS: Scale-Proportional Optimization Engine\r\n" +
                             $"Permutations Tested: {totalIterationsChecked:N0}\r\n\r\n" +
                             $"--- Core Wing Dimensions ---\r\n" +
                             $"Wing Mean Chord    : {best.Chord:F3} m\r\n" +
                             $"Wing Thickness ratio: {best.ThicknessRatio * 100:F0}% (t/c)\r\n" +
                             $"Wing Quarter Sweep : {best.SweepAngle:F1}°\r\n" +
                             $"{dihedralLabel}\r\n\r\n" +
                             $"--- Tail Unit Proportional Specs ---\r\n" +
                             $"H-Stab Target Area : {best.HStabArea:F4} m²\r\n" +
                             $"H-Stab Total Span  : {best.HStabSpan:F3} m\r\n" +
                             $"H-Stab Root Chord  : {best.HStabChord:F3} m\r\n" +
                             $"V-Stab Target Area : {best.VStabArea:F4} m²\r\n" +
                             $"V-Stab Total Height: {best.VStabHeight:F3} m\r\n" +
                             $"V-Stab Root Chord  : {best.VStabChord:F3} m\r\n\r\n" +
                             $"--- Spatial Airframe Locations (From Nose 0,0,0) ---\r\n" +
                             $"Wing Quarter-Chord Ref Point  : [X: 0.000m, Y: {wingYVerticalOffset:F3}m, Z: {best.WingZPos:F3}m]\r\n" +
                             $"H-Stab Center Reference Point : [X: 0.000m, Y: 0.000m, Z: {calcTailZ:F3}m]\r\n" +
                             $"V-Stab Base Reference Point   : [X: 0.000m, Y: {-fuseW / 2:F3}m, Z: {calcTailZ:F3}m]\r\n\r\n" +
                             $"--- Performance Matrices ---\r\n" +
                             $"Stall Velocity     : {best.StallSpeed:F1} m/s\r\n" +
                             $"Fuselage Length    : {best.FuselageLength:F2} m\r\n" +
                             $"Est. Balsa Strut Framework Wt : {best.CalculatedBalsaMass * 1000:F0} g\r\n" +
                             $"Total Calculated Gross Weight : {best.TotalWeight:F2} kg\r\n" +
                             $"Takeoff Ground Run : {best.TakeoffDist:F1} m";

            double sweepRad = best.SweepAngle * Math.PI / 180.0;
            double inducedDihedralEffect = -best.MaxCl * Math.Sin(2 * sweepRad) * 0.25;

            txtMathDisplay.Text = $"=== SYSTEM STABILITY EQUATIONS APPLIED ===\r\n" +
                                  $"Total States Checked: {totalIterationsChecked:N0}\r\n\r\n" +
                                  $"1. PROPORTIONAL TAIL COEFFICIENT ASSIGNMENTS\r\n" +
                                  $"   Applied Standard Vh Coeff = 0.38 (Normalized scale profile)\r\n" +
                                  $"   Applied Standard Vv Coeff = 0.025 (Normalized scale profile)\r\n" +
                                  $"   Effective Moment Tail Arm = {best.FuselageLength * 0.55:F3} m\r\n\r\n" +
                                  $"2. TAIL DIMENSIONAL DESIGN VALUES\r\n" +
                                  $"   Horizontal Area (Sh) = {best.HStabArea:F4} m² -> Span: {best.HStabSpan:F3} m | Chord: {best.HStabChord:F3} m\r\n" +
                                  $"   Vertical Area (Sv)   = {best.VStabArea:F4} m² -> Height: {best.VStabHeight:F3} m | Chord: {best.VStabChord:F3} m\r\n\r\n" +
                                  $"3. AIRFRAME SPATIAL LOCATION MATRIX (NOSE DATUM)\r\n" +
                                  $"   Fuselage Reference Point 0,0,0 coordinates at extreme nose boundaries.\r\n" +
                                  $"   Wing Center Leading edge Alignment Y-Axis = {wingYVerticalOffset:F3} m | Z-Axis = {best.WingZPos:F3} m\r\n" +
                                  $"   Horizontal Stabilizer Offset Location Z = {calcTailZ:F3} m\r\n" +
                                  $"   Vertical Stabilizer Base Alignment Y-Axis = {-fuseW / 2:F3} m | Z-Axis = {calcTailZ:F3} m\r\n\r\n" +
                                  $"4. AIRFRAME PERFORMANCE TARGETS\r\n" +
                                  $"   Wing Area (S) = {span * best.Chord:F3} m²\r\n" +
                                  $"   Estimated Material Mass (Balsa) = {best.CalculatedBalsaMass:F3} kg\r\n" +
                                  $"   V_stall = {best.StallSpeed:F2} m/s";

            chartLd.Series["DataSeries"].Points.Clear();
            foreach (var pt in best.LdCurveData) chartLd.Series["DataSeries"].Points.AddXY(pt.Item1, pt.Item2);

            chartTod.Series["DataSeries"].Points.Clear();
            foreach (var pt in best.TodSensitivityData) chartTod.Series["DataSeries"].Points.AddXY(pt.Item1, pt.Item2);

            currentSpan = span;
            currentChord = best.Chord;
            currentPropD = propD;
            currentSweepDeg = best.SweepAngle;
            currentDihedralDeg = best.DihedralAngle;
            currentThicknessRatio = best.ThicknessRatio;
            currentFuseLength = best.FuselageLength;
            
            calcBalsaWeight = best.CalculatedBalsaMass;
            wingZLocation = best.WingZPos;
            calculatedHStabSpan = best.HStabSpan;
            calculatedHStabChord = best.HStabChord;
            calculatedVStabHeight = best.VStabHeight;
            calculatedVStabChord = best.VStabChord;
            calculatedHStabArea = best.HStabArea;
            calculatedVStabArea = best.VStabArea;

            GenerateNacaCoordinates(nacaInput, currentThicknessRatio);
            renderPanel.Invalidate(); 
        }

        private void RenderPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            double scale = 175.0;
            int cx = (renderPanel.Width + 440) / 2; 
            int cy = renderPanel.Height / 2;

            double fuseW = currentChord * 0.28;
            double propRadius = (currentPropD * 0.0254) / 2.0;

            var rawVertices = new List<Vector3D>();
            var faces = new List<Face3D>();

            double radSweep = currentSweepDeg * Math.PI / 180.0;
            double radDih = currentDihedralDeg * Math.PI / 180.0;
            
            double tipZOffset = (currentSpan / 2.0) * Math.Sin(radSweep);
            double tipYOffset = -(currentSpan / 2.0) * Math.Sin(radDih);

            Color wingColor = Color.FromArgb(46, 204, 113);
            Color vStabColor = Color.Cyan; 

            if (activeAirframeType == "Delta Flying Wing")
            {
                rawVertices.Add(new Vector3D(0,  currentChord * 0.1, -currentChord)); 
                rawVertices.Add(new Vector3D(0, -currentChord * 0.1, -currentChord)); 
                rawVertices.Add(new Vector3D(0,  currentChord * 0.1,  0));            
                rawVertices.Add(new Vector3D(0, -currentChord * 0.1,  0));            

                rawVertices.Add(new Vector3D(-currentSpan/2, tipYOffset + currentChord*0.05, tipZOffset - currentChord/3));
                rawVertices.Add(new Vector3D(-currentSpan/2, tipYOffset - currentChord*0.05, tipZOffset - currentChord/3));
                rawVertices.Add(new Vector3D(currentSpan/2,  tipYOffset + currentChord*0.05, tipZOffset - currentChord/3));
                rawVertices.Add(new Vector3D(currentSpan/2,  tipYOffset - currentChord*0.05, tipZOffset - currentChord/3));

                int finIdx = rawVertices.Count;
                double hFin = (calculatedVStabHeight > 0) ? calculatedVStabHeight : currentSpan * 0.16;
                rawVertices.Add(new Vector3D(0, -currentChord * 0.1, 0.0));
                rawVertices.Add(new Vector3D(0, -hFin,               currentChord * 0.2));
                rawVertices.Add(new Vector3D(0, -hFin,               currentChord * 0.5));
                rawVertices.Add(new Vector3D(0, -currentChord * 0.1, 0.5 * currentChord));

                faces.Add(new Face3D(new int[]{0, 4, 6}, wingColor)); faces.Add(new Face3D(new int[]{0, 6, 2}, wingColor));
                faces.Add(new Face3D(new int[]{1, 7, 5}, Color.FromArgb(39, 174, 96))); faces.Add(new Face3D(new int[]{1, 3, 7}, Color.FromArgb(39, 174, 96)));
                faces.Add(new Face3D(new int[]{0, 1, 5, 4}, Color.FromArgb(34, 150, 80))); faces.Add(new Face3D(new int[]{0, 6, 7, 1}, Color.FromArgb(34, 150, 80)));
                faces.Add(new Face3D(new int[]{finIdx, finIdx+1, finIdx+2, finIdx+3}, vStabColor));
            }
            else
            {
                // 1. Fuselage
                rawVertices.Add(new Vector3D(-fuseW/2,  fuseW/2, -currentFuseLength * 0.25)); 
                rawVertices.Add(new Vector3D(fuseW/2,  fuseW/2, -currentFuseLength * 0.25));
                rawVertices.Add(new Vector3D(fuseW/2, -fuseW/2, -currentFuseLength * 0.25)); 
                rawVertices.Add(new Vector3D(-fuseW/2, -fuseW/2, -currentFuseLength * 0.25));
                rawVertices.Add(new Vector3D(-fuseW/2,  fuseW/2,  currentFuseLength * 0.75)); 
                rawVertices.Add(new Vector3D(fuseW/2,  fuseW/2,  currentFuseLength * 0.75));
                rawVertices.Add(new Vector3D(fuseW/2, -fuseW/2,  currentFuseLength * 0.75)); 
                rawVertices.Add(new Vector3D(-fuseW/2, -fuseW/2,  currentFuseLength * 0.75));
                AddBoxFaces(faces, 0, 1, 2, 3, 4, 5, 6, 7, Color.FromArgb(52, 152, 219));

                // 2. Wing Mapping
                int wingS = rawVertices.Count;
                double yOff = activeAirframeType.Contains("High") ? -fuseW/2 : (activeAirframeType.Contains("Low") ? fuseW/2 : 0);
                double zOff = wingZLocation; 
                int stations = airfoilProfileUpper.Count;

                for (int i = 0; i < stations; i++)
                {
                    rawVertices.Add(new Vector3D(0, yOff - (airfoilProfileUpper[i].Y * currentChord), zOff + (airfoilProfileUpper[i].X * currentChord)));
                    rawVertices.Add(new Vector3D(0, yOff - (airfoilProfileLower[i].Y * currentChord), zOff + (airfoilProfileLower[i].X * currentChord)));
                }
                for (int i = 0; i < stations; i++)
                {
                    rawVertices.Add(new Vector3D(-currentSpan/2, yOff + tipYOffset - (airfoilProfileUpper[i].Y * currentChord), zOff + tipZOffset + (airfoilProfileUpper[i].X * currentChord)));
                    rawVertices.Add(new Vector3D(-currentSpan/2, yOff + tipYOffset - (airfoilProfileLower[i].Y * currentChord), zOff + tipZOffset + (airfoilProfileLower[i].X * currentChord)));
                }
                for (int i = 0; i < stations; i++)
                {
                    rawVertices.Add(new Vector3D(currentSpan/2, yOff + tipYOffset - (airfoilProfileUpper[i].Y * currentChord), zOff + tipZOffset + (airfoilProfileUpper[i].X * currentChord)));
                    rawVertices.Add(new Vector3D(currentSpan/2, yOff + tipYOffset - (airfoilProfileLower[i].Y * currentChord), zOff + tipZOffset + (airfoilProfileLower[i].X * currentChord)));
                }

                for (int i = 0; i < stations - 1; i++)
                {
                    int cUpperCur = wingS + (i * 2); int cUpperNext = wingS + ((i + 1) * 2);
                    int lUpperCur = wingS + (stations * 2) + (i * 2); int lUpperNext = wingS + (stations * 2) + ((i + 1) * 2);
                    int cLowerCur = wingS + (i * 2) + 1; int cLowerNext = wingS + ((i + 1) * 2) + 1;
                    int lLowerCur = wingS + (stations * 2) + (i * 2) + 1; int lLowerNext = wingS + (stations * 2) + ((i + 1) * 2) + 1;
                    int rUpperCur = wingS + (stations * 4) + (i * 2); int rUpperNext = wingS + (stations * 4) + ((i + 1) * 2);
                    int rLowerCur = wingS + (stations * 4) + (i * 2) + 1; int rLowerNext = wingS + (stations * 4) + ((i + 1) * 2) + 1;

                    faces.Add(new Face3D(new int[] { cUpperCur, lUpperCur, lUpperNext, cUpperNext }, wingColor));
                    faces.Add(new Face3D(new int[] { lLowerCur, cLowerCur, cLowerNext, lLowerNext }, Color.FromArgb(39, 174, 96)));
                    faces.Add(new Face3D(new int[] { rUpperCur, cUpperCur, cUpperNext, rUpperNext }, wingColor));
                    faces.Add(new Face3D(new int[] { cLowerCur, rLowerCur, rLowerNext, cLowerNext }, Color.FromArgb(39, 174, 96)));
                }

                // 3. Tail Components Setup
                int tailStart = rawVertices.Count;
                double tailZ = currentFuseLength * 0.72;
                double hSpan = (calculatedHStabSpan > 0) ? calculatedHStabSpan : currentSpan * 0.3;
                double hChord = (calculatedHStabChord > 0) ? calculatedHStabChord : currentChord * 0.5;
                double vHeight = (calculatedVStabHeight > 0) ? calculatedVStabHeight : currentSpan * 0.18;
                double vChord = (calculatedVStabChord > 0) ? calculatedVStabChord : hChord;

                rawVertices.Add(new Vector3D(-hSpan/2,  0.004, tailZ - hChord)); rawVertices.Add(new Vector3D(hSpan/2,  0.004, tailZ - hChord));
                rawVertices.Add(new Vector3D(hSpan/2,  0.004, tailZ));          rawVertices.Add(new Vector3D(-hSpan/2,  0.004, tailZ));
                rawVertices.Add(new Vector3D(-hSpan/2, -0.004, tailZ - hChord)); rawVertices.Add(new Vector3D(hSpan/2, -0.004, tailZ - hChord));
                rawVertices.Add(new Vector3D(hSpan/2, -0.004, tailZ));          rawVertices.Add(new Vector3D(-hSpan/2, -0.004, tailZ));
                
                rawVertices.Add(new Vector3D(-0.004, -vHeight, tailZ - vChord)); rawVertices.Add(new Vector3D(0.004, -vHeight, tailZ - vChord));
                rawVertices.Add(new Vector3D(0.004, -vHeight, tailZ));          rawVertices.Add(new Vector3D(-0.004, -vHeight, tailZ));
                rawVertices.Add(new Vector3D(-0.004, -fuseW/2,  tailZ - vChord)); rawVertices.Add(new Vector3D(0.004, -fuseW/2,  tailZ - vChord));
                rawVertices.Add(new Vector3D(0.004, -fuseW/2,  tailZ));          rawVertices.Add(new Vector3D(-0.004, -fuseW/2,  tailZ));

                faces.Add(new Face3D(new int[]{tailStart+3, tailStart+2, tailStart+1, tailStart+0}, Color.FromArgb(231, 76, 60)));
                faces.Add(new Face3D(new int[]{tailStart+4, tailStart+5, tailStart+6, tailStart+7}, Color.FromArgb(192, 57, 43)));
                
                faces.Add(new Face3D(new int[]{tailStart+8, tailStart+9, tailStart+13, tailStart+12}, vStabColor));
                faces.Add(new Face3D(new int[]{tailStart+9, tailStart+10, tailStart+14, tailStart+13}, vStabColor));
                faces.Add(new Face3D(new int[]{tailStart+10, tailStart+11, tailStart+15, tailStart+14}, Color.DarkCyan));
                faces.Add(new Face3D(new int[]{tailStart+11, tailStart+8, tailStart+12, tailStart+15}, Color.DarkCyan));
            }

            // 4. Nose Propeller Nodes
            int propStart = rawVertices.Count;
            double noseZ = (activeAirframeType == "Delta Flying Wing") ? -currentChord : -currentFuseLength * 0.25;
            rawVertices.Add(new Vector3D(propRadius * Math.Cos(propSpinAngle), propRadius * Math.Sin(propSpinAngle), noseZ - 0.01));
            rawVertices.Add(new Vector3D(0, 0, noseZ));
            rawVertices.Add(new Vector3D(-propRadius * Math.Cos(propSpinAngle), -propRadius * Math.Sin(propSpinAngle), noseZ - 0.01));
            rawVertices.Add(new Vector3D(0, 0, noseZ));

            // Viewport Projections Pipeline
            PointF[] projPoints = new PointF[rawVertices.Count];
            double[] transZ = new double[rawVertices.Count];
            for (int i = 0; i < rawVertices.Count; i++)
            {
                var pt = rawVertices[i];
                double x1 = pt.X * Math.Cos(angleY) - pt.Z * Math.Sin(angleY);
                double z1 = pt.X * Math.Sin(angleY) + pt.Z * Math.Cos(angleY);
                double y2 = pt.Y * Math.Cos(angleX) - z1 * Math.Sin(angleX);
                double z2 = pt.Y * Math.Sin(angleX) + z1 * Math.Cos(angleX);
                projPoints[i] = new PointF((float)(x1 * scale) + cx, (float)(y2 * scale) + cy);
                transZ[i] = z2;
            }

            Vector3D lightDir = new Vector3D(0.5, 1.0, -0.7);
            double lightLen = Math.Sqrt(lightDir.X * lightDir.X + lightDir.Y * lightDir.Y + lightDir.Z * lightDir.Z);
            lightDir = new Vector3D(lightDir.X / lightLen, lightDir.Y / lightLen, lightDir.Z / lightLen);

            foreach (var face in faces)
            {
                face.AverageZ = face.VertexIndices.Average(idx => transZ[idx]);
                var v0 = rawVertices[face.VertexIndices[0]]; var v1 = rawVertices[face.VertexIndices[1]]; var v2 = rawVertices[face.VertexIndices[2]];
                double nx = (v1.Y - v0.Y) * (v2.Z - v0.Z) - (v1.Z - v0.Z) * (v2.Y - v0.Y);
                double ny = (v1.Z - v0.Z) * (v2.X - v0.X) - (v1.X - v0.X) * (v2.Z - v0.Z);
                double nz = (v1.X - v0.X) * (v2.Y - v0.Y) - (v1.Y - v0.Y) * (v2.X - v0.X);
                double nLen = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                face.Normal = new Vector3D(nx / nLen, ny / nLen, nz / nLen);
            }

            var sortedFaces = faces.OrderBy(f => f.AverageZ).ToList();
            foreach (var face in sortedFaces)
            {
                PointF[] polyPts = face.VertexIndices.Select(idx => projPoints[idx]).ToArray();
                double dot = face.Normal.X * lightDir.X + face.Normal.Y * lightDir.Y + face.Normal.Z * lightDir.Z;
                double intensity = Math.Max(0.18, Math.Min(1.0, (dot + 1.0) / 2.0));

                Color shaded = Color.FromArgb((int)(face.BaseColor.R * intensity), (int)(face.BaseColor.G * intensity), (int)(face.BaseColor.B * intensity));
                using (SolidBrush brush = new SolidBrush(shaded))
                using (Pen edgePen = new Pen(Color.FromArgb(30, 30, 34), 0.4f))
                {
                    g.FillPolygon(brush, polyPts); g.DrawPolygon(edgePen, polyPts);
                }
            }

            using (Pen propPen = new Pen(Color.FromArgb(241, 196, 15), 3f))
            {
                g.DrawLine(propPen, projPoints[propStart], projPoints[propStart + 1]);
                g.DrawLine(propPen, projPoints[propStart + 2], propStart + 3 < projPoints.Length ? projPoints[propStart + 3] : projPoints[propStart + 1]);
            }
        }

        private void AddBoxFaces(List<Face3D> faces, int v0, int v1, int v2, int v3, int v4, int v5, int v6, int v7, Color c)
        {
            faces.Add(new Face3D(new int[] { v0, v1, v5, v4 }, c)); faces.Add(new Face3D(new int[] { v2, v3, v7, v6 }, c));
            faces.Add(new Face3D(new int[] { v1, v2, v6, v5 }, c)); faces.Add(new Face3D(new int[] { v3, v0, v4, v7 }, c));
        }
    }
}