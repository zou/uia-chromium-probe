// A UI Automation client that behaves like a capture watcher: it subscribes to focus events,
// launches a Chromium-based host showing page.html, types into a plain textarea and a
// contenteditable rich editor, then reads the tree to see whether the typed text is exposed.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

static class Program
{
    const string M1 = "HELLOTEXTAREA123";
    const string M2 = "HELLORICH456";

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly List<string> Log = new List<string>();
    static readonly List<string> FocusEvents = new List<string>();
    static string Name = "";

    static void L(string s)
    {
        lock (Log) Log.Add($"{Clock.ElapsedMilliseconds}ms {s}");
        Console.WriteLine($"[{Name}] {s}");
    }

    static string Cut(string s) => s == null ? null : (s.Length > 160 ? s.Substring(0, 160) : s);

    static string Title(IntPtr h)
    {
        var sb = new StringBuilder(512);
        GetWindowText(h, sb, 512);
        return sb.ToString();
    }

    static string Short(AutomationElement el)
    {
        try
        {
            var c = el.Current;
            return $"{c.ControlType.ProgrammaticName} class={c.ClassName} fw={c.FrameworkId} name={Cut(c.Name)}";
        }
        catch (Exception ex) { return "err " + ex.GetType().Name; }
    }

    // Everything a watcher could read from one element: Name, ValuePattern, TextPattern.
    static string Texts(AutomationElement el, Dictionary<string, object> via = null)
    {
        var sb = new StringBuilder();
        try { sb.Append(el.Current.Name).Append('\n'); } catch { }
        try
        {
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out object vp))
            {
                var v = ((ValuePattern)vp).Current.Value;
                sb.Append(v).Append('\n');
                if (via != null) via["value"] = Cut(v);
            }
        }
        catch { }
        try
        {
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out object tp))
            {
                var t = ((TextPattern)tp).DocumentRange.GetText(4000);
                sb.Append(t).Append('\n');
                if (via != null) via["text"] = Cut(t);
            }
        }
        catch { }
        return sb.ToString();
    }

    static int Write(Dictionary<string, object> r, string outPath)
    {
        lock (Log) r["log"] = Log.ToArray();
        lock (FocusEvents) r["focusEvents"] = FocusEvents.Count > 80 ? FocusEvents.GetRange(0, 80).ToArray() : FocusEvents.ToArray();
        File.WriteAllText(outPath, JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    [STAThread]
    static int Main(string[] a)
    {
        Name = a[0];
        string exe = a[1], args = a[2], outPath = a[3];
        var r = new Dictionary<string, object> { ["target"] = Name, ["exe"] = exe, ["args"] = args };

        Automation.AddAutomationFocusChangedEventHandler((s, e) =>
        {
            string d;
            try { d = Short((AutomationElement)s); } catch (Exception ex) { d = "err " + ex.GetType().Name; }
            lock (FocusEvents) FocusEvents.Add($"{Clock.ElapsedMilliseconds}ms {d}");
        });

        Process p = null;
        try
        {
            p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
            L($"started pid {p.Id}");

            AutomationElement win = null;
            for (int i = 0; i < 120 && win == null; i++)
            {
                foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition))
                {
                    string n = null;
                    try { n = w.Current.Name; } catch { }
                    if (n != null && n.StartsWith("UIA-PROBE|") && !n.Contains("wv2-loading")) { win = w; break; }
                }
                if (win == null) Thread.Sleep(500);
            }
            if (win == null) { r["error"] = "window not found"; return Write(r, outPath); }

            var h = new IntPtr(win.Current.NativeWindowHandle);
            L($"window found: {Title(h)}");
            Thread.Sleep(4000);

            // Alt tap lets a background process take the foreground.
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            SetForegroundWindow(h);
            Thread.Sleep(700);
            r["foregroundIsTarget"] = GetForegroundWindow() == h;

            SendKeys.SendWait(M1); Thread.Sleep(400);
            SendKeys.SendWait("{TAB}"); Thread.Sleep(400);
            SendKeys.SendWait(M2); Thread.Sleep(1200);
            L($"typed; ground-truth title now: {Title(h)}");

            bool f1 = false, f2 = false;
            var where = new List<string>();
            int nodes = 0, pass = 0;
            long deadline = Clock.ElapsedMilliseconds + 30000;
            while (Clock.ElapsedMilliseconds < deadline && !(f1 && f2))
            {
                pass++; nodes = 0; where.Clear(); f1 = f2 = false;
                try
                {
                    var fe = AutomationElement.FocusedElement;
                    var via = new Dictionary<string, object> { ["element"] = Short(fe) };
                    Texts(fe, via);
                    r["focused"] = via;
                }
                catch (Exception ex) { r["focused"] = "err " + ex.Message; }

                var q = new Queue<AutomationElement>();
                q.Enqueue(win);
                var walker = TreeWalker.RawViewWalker;
                while (q.Count > 0 && nodes < 6000)
                {
                    var el = q.Dequeue();
                    nodes++;
                    string text = Texts(el);
                    if (text.Contains(M1)) { f1 = true; where.Add("textarea@" + Short(el)); }
                    if (text.Contains(M2)) { f2 = true; where.Add("rich@" + Short(el)); }
                    try
                    {
                        var c = walker.GetFirstChild(el);
                        while (c != null) { q.Enqueue(c); c = walker.GetNextSibling(c); }
                    }
                    catch { }
                }
                L($"pass {pass}: nodes={nodes} textarea={f1} rich={f2} title={Title(h)}");
                if (!(f1 && f2)) Thread.Sleep(2000);
            }
            r["passes"] = pass;
            r["treeNodes"] = nodes;
            r["foundTextarea"] = f1;
            r["foundRich"] = f2;
            r["foundWhere"] = where.ToArray();
            r["windowTitleAfter"] = Title(h);
        }
        catch (Exception ex) { r["error"] = ex.ToString(); }
        finally { try { p?.Kill(true); } catch { } }
        return Write(r, outPath);
    }
}
