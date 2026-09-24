const { app, BrowserWindow } = require('electron');
const path = require('path');
app.whenReady().then(() => {
  const w = new BrowserWindow({ width: 900, height: 600, show: true });
  w.loadFile(path.join(__dirname, '..', 'page.html'));
  w.focus();
});
app.on('window-all-closed', () => app.quit());
