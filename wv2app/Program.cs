using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

static class Program
{
    [STAThread]
    static void Main(string[] a)
    {
        Application.EnableVisualStyles();
        var f = new Form { Text = "UIA-PROBE|wv2-loading", Width = 900, Height = 600 };
        var wv = new WebView2 { Dock = DockStyle.Fill };
        f.Controls.Add(wv);
        f.Shown += async (s, e) =>
        {
            await wv.EnsureCoreWebView2Async();
            wv.CoreWebView2.DocumentTitleChanged += (s2, e2) => f.Text = wv.CoreWebView2.DocumentTitle;
            wv.CoreWebView2.NavigationCompleted += (s3, e3) => wv.Focus();
            wv.CoreWebView2.Navigate(a[0]);
        };
        Application.Run(f);
    }
}
