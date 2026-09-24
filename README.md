# uia-chromium-probe

Throwaway test for evolvey/evolvey#242, step 1: does a generic UI Automation client (not a named screen reader)
receive text typed into Chromium-based hosts on Windows, without any accessibility flag?

Hosts: Chrome (`--app`), Electron (latest), and a WebView2 WinForms app. Editors: a plain `<textarea>` and a
`contenteditable` `role="textbox"` rich editor (the shape Slack and Teams use). The `-forced` runs add
`--force-renderer-accessibility` as a positive control, so a miss can be told apart from a broken probe.

Ground truth that the typing landed is the window title (`UIA-PROBE|<textarea len>|<rich len>`), read with Win32
`GetWindowText`, not UIA. `|auto` means synthetic keystrokes never arrived and the page filled itself by script.
