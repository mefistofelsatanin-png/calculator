using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

// ─── Expression Evaluator (Recursive Descent Parser) ──────────────────────────
class Parser
{
    private string s;
    private int p;
    private bool degrees; // true = sin/cos/tan take degrees

    public Parser(string input, bool useDegrees)
    {
        s = input;
        p = 0;
        degrees = useDegrees;
    }

    public double Parse()
    {
        double v = ParseExpr();
        Skip();
        if (p < s.Length) throw new Exception("Неочікуваний символ: " + s[p]);
        return v;
    }

    // expr = term (('+' | '-') term)*
    private double ParseExpr()
    {
        double left = ParseTerm();
        while (true)
        {
            Skip();
            if (p < s.Length && s[p] == '+') { p++; left += ParseTerm(); }
            else if (p < s.Length && s[p] == '-') { p++; left -= ParseTerm(); }
            else break;
        }
        return left;
    }

    // term = power (('*'|'/') power)*
    private double ParseTerm()
    {
        double left = ParsePower();
        while (true)
        {
            Skip();
            if (p < s.Length && s[p] == '*') { p++; left *= ParsePower(); }
            else if (p < s.Length && s[p] == '/')
            {
                p++;
                double d = ParsePower();
                if (d == 0.0) throw new DivideByZeroException("Ділення на нуль");
                left /= d;
            }
            else break;
        }
        return left;
    }

    // power = unary ('^' power)?  right-associative
    private double ParsePower()
    {
        double left = ParseUnary();
        Skip();
        if (p < s.Length && s[p] == '^')
        {
            p++;
            double right = ParsePower();
            left = Math.Pow(left, right);
        }
        return left;
    }

    // unary = '-' unary | atom
    private double ParseUnary()
    {
        Skip();
        if (p < s.Length && s[p] == '-') { p++; return -ParseUnary(); }
        if (p < s.Length && s[p] == '+') { p++; return ParseUnary(); }
        return ParseAtom();
    }

    // atom = function '(' expr ')' | '(' expr ')' | number
    private double ParseAtom()
    {
        Skip();
        if (p >= s.Length) throw new Exception("Очікувалось число або вираз");

        // Check for named functions
        string[] fns = { "sqrt", "sin", "cos", "tan", "abs", "log", "ln" };
        foreach (string fn in fns)
        {
            if (p + fn.Length <= s.Length &&
                s.Substring(p, fn.Length).ToLowerInvariant() == fn)
            {
                p += fn.Length;
                Skip();
                if (p >= s.Length || s[p] != '(') throw new Exception("Очікувалась ( після " + fn);
                p++; // (
                double arg = ParseExpr();
                Skip();
                if (p >= s.Length || s[p] != ')') throw new Exception("Очікувалась )");
                p++; // )
                return ApplyFunc(fn, arg);
            }
        }

        // Parentheses
        if (s[p] == '(')
        {
            p++;
            double v = ParseExpr();
            Skip();
            if (p >= s.Length || s[p] != ')') throw new Exception("Очікувалась )");
            p++;
            return v;
        }

        // Number
        return ParseNumber();
    }

    private double ApplyFunc(string fn, double arg)
    {
        double rad = degrees ? arg * Math.PI / 180.0 : arg;
        switch (fn)
        {
            case "sqrt":
                if (arg < 0) throw new Exception("Корінь з від'ємного числа");
                return Math.Sqrt(arg);
            case "sin": return Math.Round(Math.Sin(rad), 10);
            case "cos": return Math.Round(Math.Cos(rad), 10);
            case "tan":
                double t = Math.Tan(rad);
                if (double.IsInfinity(t)) throw new Exception("tan невизначений");
                return Math.Round(t, 10);
            case "abs": return Math.Abs(arg);
            case "log": return Math.Log10(arg);
            case "ln":  return Math.Log(arg);
        }
        return arg;
    }

    private double ParseNumber()
    {
        Skip();
        int start = p;
        bool hasDot = false;
        while (p < s.Length && (char.IsDigit(s[p]) || (s[p] == '.' && !hasDot)))
        {
            if (s[p] == '.') hasDot = true;
            p++;
        }
        if (p == start) throw new Exception("Очікувалось число на позиції " + p);
        string numStr = s.Substring(start, p - start);
        double result;
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            throw new Exception("Невірне число: " + numStr);
        return result;
    }

    private void Skip()
    {
        while (p < s.Length && s[p] == ' ') p++;
    }
}

// ─── Main Form ─────────────────────────────────────────────────────────────────
public class Calculator : Form
{
    private Label lblEquation;
    private Label lblResult;
    private ListBox lstHistory;
    private Label lblDegRad;
    private bool useDegrees = true;

    private string expression = "";
    private bool justCalculated = false;

    // Map display symbols to actual chars for the expression evaluator
    private static string NormalizeExpr(string expr)
    {
        return expr
            .Replace("×", "*")
            .Replace("÷", "/")
            .Replace("−", "-")
            .Replace("π", Math.PI.ToString("R", CultureInfo.InvariantCulture))
            .Replace("e", Math.E.ToString("R", CultureInfo.InvariantCulture));
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Calculator());
    }

    public Calculator()
    {
        BuildUI();
        this.KeyPreview = true;
        this.KeyDown += OnKeyDown;
    }

    private void BuildUI()
    {
        this.Text = "Калькулятор";
        this.Size = new Size(640, 620);
        this.MinimumSize = new Size(640, 620);
        this.BackColor = Color.FromArgb(28, 28, 28);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI", 10f);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        // ── Display panel ──────────────────────────────────────────────────────
        Panel pnlDisplay = new Panel();
        pnlDisplay.BackColor = Color.FromArgb(18, 18, 18);
        pnlDisplay.Bounds = new Rectangle(10, 10, 388, 115);

        lblEquation = new Label();
        lblEquation.Bounds = new Rectangle(5, 4, 375, 42);
        lblEquation.ForeColor = Color.FromArgb(150, 150, 150);
        lblEquation.Font = new Font("Segoe UI", 11f);
        lblEquation.TextAlign = ContentAlignment.MiddleRight;
        lblEquation.AutoEllipsis = true;
        pnlDisplay.Controls.Add(lblEquation);

        lblResult = new Label();
        lblResult.Bounds = new Rectangle(5, 50, 375, 60);
        lblResult.ForeColor = Color.White;
        lblResult.Font = new Font("Segoe UI", 26f, FontStyle.Bold);
        lblResult.TextAlign = ContentAlignment.MiddleRight;
        lblResult.AutoEllipsis = true;
        lblResult.Text = "0";
        pnlDisplay.Controls.Add(lblResult);

        this.Controls.Add(pnlDisplay);

        // DEG/RAD toggle (small, top-left of display)
        lblDegRad = new Label();
        lblDegRad.Bounds = new Rectangle(15, 15, 50, 22);
        lblDegRad.Text = "DEG";
        lblDegRad.ForeColor = Color.FromArgb(100, 200, 100);
        lblDegRad.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        lblDegRad.TextAlign = ContentAlignment.MiddleCenter;
        lblDegRad.BackColor = Color.FromArgb(30, 60, 30);
        lblDegRad.Cursor = Cursors.Hand;
        lblDegRad.Click += OnDegRadToggle;
        this.Controls.Add(lblDegRad);
        this.Controls.SetChildIndex(lblDegRad, 0);

        // ── History panel ───────────────────────────────────────────────────────
        Label lblHistTitle = new Label();
        lblHistTitle.Text = "Історія";
        lblHistTitle.Bounds = new Rectangle(408, 10, 210, 25);
        lblHistTitle.ForeColor = Color.FromArgb(150, 150, 150);
        lblHistTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        this.Controls.Add(lblHistTitle);

        Button btnClearHist = new Button();
        btnClearHist.Text = "Очистити";
        btnClearHist.Bounds = new Rectangle(500, 8, 112, 24);
        btnClearHist.FlatStyle = FlatStyle.Flat;
        btnClearHist.FlatAppearance.BorderSize = 0;
        btnClearHist.BackColor = Color.FromArgb(50, 30, 30);
        btnClearHist.ForeColor = Color.FromArgb(200, 80, 80);
        btnClearHist.Font = new Font("Segoe UI", 8f);
        btnClearHist.Cursor = Cursors.Hand;
        btnClearHist.TabStop = false;
        btnClearHist.Click += (s, e) => lstHistory.Items.Clear();
        this.Controls.Add(btnClearHist);

        lstHistory = new ListBox();
        lstHistory.Bounds = new Rectangle(408, 36, 212, 555);
        lstHistory.BackColor = Color.FromArgb(22, 22, 22);
        lstHistory.ForeColor = Color.FromArgb(210, 210, 210);
        lstHistory.Font = new Font("Segoe UI", 9f);
        lstHistory.BorderStyle = BorderStyle.None;
        lstHistory.HorizontalScrollbar = false;
        lstHistory.DoubleClick += OnHistoryDoubleClick;
        this.Controls.Add(lstHistory);

        // ── Buttons ─────────────────────────────────────────────────────────────
        // 5 columns, 7 rows
        // Col width: 72, Row height: 58, gap: 4
        // Total button area width: 5*72 + 4*4 = 376
        int BW = 72, BH = 58, GAP = 4, SX = 10, SY = 135;

        // Row 0: scientific functions
        MakeBtn("sin",  "sin",  0, 0, BW, BH, GAP, SX, SY, "func");
        MakeBtn("cos",  "cos",  1, 0, BW, BH, GAP, SX, SY, "func");
        MakeBtn("tan",  "tan",  2, 0, BW, BH, GAP, SX, SY, "func");
        MakeBtn("√x",   "sqrt", 3, 0, BW, BH, GAP, SX, SY, "func");
        MakeBtn("π",    "π",    4, 0, BW, BH, GAP, SX, SY, "const");

        // Row 1: extra ops + clear
        MakeBtn("x²",  "x²",  0, 1, BW, BH, GAP, SX, SY, "func");
        MakeBtn("xʸ",  "xʸ",  1, 1, BW, BH, GAP, SX, SY, "func");
        MakeBtn("(",   "(",   2, 1, BW, BH, GAP, SX, SY, "paren");
        MakeBtn(")",   ")",   3, 1, BW, BH, GAP, SX, SY, "paren");
        MakeBtn("C",   "C",   4, 1, BW, BH, GAP, SX, SY, "clear");

        // Row 2
        MakeBtn("7",   "7",   0, 2, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("8",   "8",   1, 2, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("9",   "9",   2, 2, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("÷",   "÷",   3, 2, BW, BH, GAP, SX, SY, "op");
        MakeBtn("⌫",   "⌫",   4, 2, BW, BH, GAP, SX, SY, "clear");

        // Row 3
        MakeBtn("4",   "4",   0, 3, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("5",   "5",   1, 3, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("6",   "6",   2, 3, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("×",   "×",   3, 3, BW, BH, GAP, SX, SY, "op");
        MakeBtn("CE",  "CE",  4, 3, BW, BH, GAP, SX, SY, "clear");

        // Row 4
        MakeBtn("1",   "1",   0, 4, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("2",   "2",   1, 4, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("3",   "3",   2, 4, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("−",   "−",   3, 4, BW, BH, GAP, SX, SY, "op");
        // = spans rows 4 and 5
        MakeBtnRect("=", "=",
            new Rectangle(SX + 4 * (BW + GAP), SY + 4 * (BH + GAP),
                          BW, BH * 2 + GAP), "equals");

        // Row 5
        MakeBtn("+/−", "+/−", 0, 5, BW, BH, GAP, SX, SY, "func");
        MakeBtn("0",   "0",   1, 5, BW, BH, GAP, SX, SY, "digit");
        MakeBtn(".",   ".",   2, 5, BW, BH, GAP, SX, SY, "digit");
        MakeBtn("+",   "+",   3, 5, BW, BH, GAP, SX, SY, "op");
    }

    private Dictionary<Button, Color> _origColors = new Dictionary<Button, Color>();

    private void MakeBtn(string label, string tag, int col, int row, int bw, int bh, int gap, int sx, int sy, string kind)
    {
        MakeBtnRect(label, tag,
            new Rectangle(sx + col * (bw + gap), sy + row * (bh + gap), bw, bh), kind);
    }

    private void MakeBtnRect(string label, string tag, Rectangle bounds, string kind)
    {
        Button btn = new Button();
        btn.Text = label;
        btn.Tag = tag;
        btn.Bounds = bounds;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
        btn.TabStop = false;

        switch (kind)
        {
            case "equals":
                btn.BackColor = Color.FromArgb(0, 120, 215);
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
                break;
            case "op":
                btn.BackColor = Color.FromArgb(42, 42, 72);
                btn.ForeColor = Color.FromArgb(110, 160, 255);
                btn.Font = new Font("Segoe UI", 15f);
                break;
            case "clear":
                btn.BackColor = Color.FromArgb(58, 30, 30);
                btn.ForeColor = Color.FromArgb(255, 100, 100);
                btn.Font = new Font("Segoe UI", 13f);
                break;
            case "func":
                btn.BackColor = Color.FromArgb(30, 48, 30);
                btn.ForeColor = Color.FromArgb(90, 220, 120);
                btn.Font = new Font("Segoe UI", 12f);
                break;
            case "paren":
                btn.BackColor = Color.FromArgb(40, 40, 65);
                btn.ForeColor = Color.FromArgb(190, 170, 255);
                btn.Font = new Font("Segoe UI", 15f);
                break;
            case "const":
                btn.BackColor = Color.FromArgb(30, 48, 30);
                btn.ForeColor = Color.FromArgb(90, 220, 120);
                btn.Font = new Font("Segoe UI", 14f);
                break;
            default: // digit
                btn.BackColor = Color.FromArgb(44, 44, 44);
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 15f);
                break;
        }

        _origColors[btn] = btn.BackColor;
        btn.MouseEnter += OnBtnEnter;
        btn.MouseLeave += OnBtnLeave;
        btn.Click += OnButtonClick;
        this.Controls.Add(btn);
    }

    private void OnBtnEnter(object sender, EventArgs e)
    {
        Button btn = (Button)sender;
        Color orig = _origColors[btn];
        btn.BackColor = Color.FromArgb(
            Math.Min(255, orig.R + 22),
            Math.Min(255, orig.G + 22),
            Math.Min(255, orig.B + 22));
    }

    private void OnBtnLeave(object sender, EventArgs e)
    {
        Button btn = (Button)sender;
        btn.BackColor = _origColors[btn];
    }

    private void OnButtonClick(object sender, EventArgs e)
    {
        ProcessInput(((Button)sender).Tag.ToString());
    }

    private void OnDegRadToggle(object sender, EventArgs e)
    {
        useDegrees = !useDegrees;
        lblDegRad.Text = useDegrees ? "DEG" : "RAD";
    }

    private void OnHistoryDoubleClick(object sender, EventArgs e)
    {
        // Paste the expression part from history (before " = ")
        if (lstHistory.SelectedItem == null) return;
        string item = lstHistory.SelectedItem.ToString();
        int eqIdx = item.LastIndexOf(" = ");
        if (eqIdx >= 0)
            expression = item.Substring(0, eqIdx);
        else
            expression = item;
        justCalculated = false;
        RefreshDisplay();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        bool shift = (e.Modifiers & Keys.Shift) != 0;

        if (!shift && e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
        {
            ProcessInput(((int)(e.KeyCode - Keys.D0)).ToString());
            e.Handled = true; e.SuppressKeyPress = true; return;
        }
        if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
        {
            ProcessInput(((int)(e.KeyCode - Keys.NumPad0)).ToString());
            e.Handled = true; e.SuppressKeyPress = true; return;
        }

        if (e.KeyCode == Keys.OemPeriod || e.KeyCode == Keys.Decimal) { ProcessInput("."); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Add) { ProcessInput("+"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Subtract) { ProcessInput("−"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Multiply) { ProcessInput("×"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Divide) { ProcessInput("÷"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Return) { ProcessInput("="); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Back) { ProcessInput("⌫"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape) { ProcessInput("C"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Delete) { ProcessInput("CE"); e.SuppressKeyPress = true; }
        // Shift+6 = ^
        else if (shift && e.KeyCode == Keys.D6) { ProcessInput("xʸ"); e.SuppressKeyPress = true; }
        // Shift+8 = *
        else if (shift && e.KeyCode == Keys.D8) { ProcessInput("×"); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.OemOpenBrackets || (shift && e.KeyCode == Keys.D9))
        { ProcessInput("("); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.OemCloseBrackets || (shift && e.KeyCode == Keys.D0))
        { ProcessInput(")"); e.SuppressKeyPress = true; }

        e.Handled = true;
    }

    private void ProcessInput(string val)
    {
        if (val == "=")
        {
            Calculate();
            return;
        }

        // After "=" was pressed — determine what to do with next input
        if (justCalculated)
        {
            bool isOp = (val == "+" || val == "−" || val == "×" || val == "÷");
            if (!isOp && val != "CE" && val != "⌫")
                expression = ""; // start fresh for new entry
            justCalculated = false;
        }

        switch (val)
        {
            case "C":
                expression = "";
                break;

            case "CE":
                RemoveLastToken();
                break;

            case "⌫":
                Backspace();
                break;

            case "+/−":
                ToggleSign();
                break;

            case "sin": expression += "sin("; break;
            case "cos": expression += "cos("; break;
            case "tan": expression += "tan("; break;
            case "sqrt": expression += "sqrt("; break;
            case "x²": expression += "^2"; break;
            case "xʸ": expression += "^"; break;
            case "π": expression += "π"; break;

            default:
                expression += val;
                break;
        }

        RefreshDisplay();
    }

    private void Backspace()
    {
        if (expression.Length == 0) return;
        // Remove whole function name with (
        string[] fns = { "sin(", "cos(", "tan(", "sqrt(" };
        foreach (string fn in fns)
        {
            if (expression.EndsWith(fn))
            {
                expression = expression.Substring(0, expression.Length - fn.Length);
                return;
            }
        }
        expression = expression.Substring(0, expression.Length - 1);
    }

    private void RemoveLastToken()
    {
        if (expression.Length == 0) return;
        // Remove whole function
        string[] fns = { "sin(", "cos(", "tan(", "sqrt(" };
        foreach (string fn in fns)
        {
            if (expression.EndsWith(fn))
            {
                expression = expression.Substring(0, expression.Length - fn.Length);
                return;
            }
        }
        // Walk back over a number
        int i = expression.Length - 1;
        char last = expression[i];
        if (char.IsDigit(last) || last == '.')
        {
            while (i >= 0 && (char.IsDigit(expression[i]) || expression[i] == '.'))
                i--;
            expression = expression.Substring(0, i + 1);
        }
        else
        {
            // Single-char operator or paren
            expression = expression.Substring(0, expression.Length - 1);
        }
    }

    private void ToggleSign()
    {
        if (expression.Length == 0) return;
        int end = expression.Length;
        int i = end - 1;
        // Walk back over digits and dot
        while (i >= 0 && (char.IsDigit(expression[i]) || expression[i] == '.'))
            i--;
        int numStart = i + 1;
        if (numStart >= end) return;
        // Check if there's a leading '−' right before
        if (numStart > 0 && expression[numStart - 1] == '−')
            expression = expression.Substring(0, numStart - 1) + expression.Substring(numStart);
        else
            expression = expression.Substring(0, numStart) + "−" + expression.Substring(numStart);
    }

    private void Calculate()
    {
        if (expression.Length == 0) return;
        try
        {
            string normalized = NormalizeExpr(expression);
            Parser parser = new Parser(normalized, useDegrees);
            double result = parser.Parse();
            string resStr = FormatNum(result);
            string entry = expression + " = " + resStr;
            lstHistory.Items.Insert(0, entry);
            if (lstHistory.Items.Count > 100) lstHistory.Items.RemoveAt(lstHistory.Items.Count - 1);
            lblEquation.Text = expression + " =";
            lblResult.Text = resStr;
            expression = resStr;
            justCalculated = true;
        }
        catch (DivideByZeroException)
        {
            lblResult.Text = "Ділення на нуль!";
            lblEquation.Text = expression;
        }
        catch (Exception ex)
        {
            lblResult.Text = "Помилка";
            lblEquation.Text = ex.Message;
        }
    }

    private void RefreshDisplay()
    {
        lblEquation.Text = expression;
        if (expression.Length == 0)
        {
            lblResult.Text = "0";
            return;
        }
        // Show last number token being typed
        string last = GetLastNumToken(expression);
        lblResult.Text = (last.Length > 0) ? last : expression;
    }

    private string GetLastNumToken(string expr)
    {
        int i = expr.Length - 1;
        if (i < 0) return "0";
        if (!char.IsDigit(expr[i]) && expr[i] != '.') return "";
        while (i >= 0 && (char.IsDigit(expr[i]) || expr[i] == '.')) i--;
        return expr.Substring(i + 1);
    }

    private string FormatNum(double n)
    {
        if (double.IsNaN(n)) return "Не число";
        if (double.IsInfinity(n)) return "∞";
        double rounded = Math.Round(n, 10);
        if (rounded == Math.Floor(rounded) && Math.Abs(rounded) < 1e15)
            return ((long)rounded).ToString(CultureInfo.InvariantCulture);
        return rounded.ToString("G10", CultureInfo.InvariantCulture);
    }
}
