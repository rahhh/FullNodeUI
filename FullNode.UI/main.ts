import { app, BrowserWindow, ipcMain, Menu, nativeImage, screen, Tray } from 'electron';
import * as path from 'path';
import * as url from 'url';
import * as os from 'os';

let serve;
let testnet;
const args = process.argv.slice(1);
serve = args.some(val => val === "--serve" || val === "-serve");
testnet = args.some(val => val === "--testnet" || val === "-testnet");

let apiPort;
if (testnet) {
  apiPort = 38221;
} else {
  apiPort = 37221;
}

ipcMain.on('get-port', (event, arg) => {
  event.returnValue = apiPort;
});

try {
  require('dotenv').config();
} catch {
  console.log('asar');
}

require('electron-context-menu')({
  showInspectElement: serve
});

// Keep a global reference of the window object, if you don't, the window will
// be closed automatically when the JavaScript object is garbage collected.
let mainWindow = null;

function createWindow() {
  // Create the browser window.
  mainWindow = new BrowserWindow({
    width: 1150,
    height: 650,
    frame: true,
    minWidth: 1150,
    minHeight: 650,
    title: "Xels Core"
  });

  if (serve) {
    require('electron-reload')(__dirname, {
    });
    mainWindow.loadURL('http://localhost:4200');
  } else {
    mainWindow.loadURL(url.format({
      pathname: path.join(__dirname, 'dist/index.html'),
      protocol: 'file:',
      slashes: true
    }));
  }

  if (serve) {
    mainWindow.webContents.openDevTools();
  }

  // Emitted when the window is going to close.
  mainWindow.on('close', () => {
  })

  // Emitted when the window is closed.
  mainWindow.on('closed', () => {
    // Dereference the window object, usually you would store window
    // in an array if your app supports multi windows, this is the time
    // when you should delete the corresponding element.
    mainWindow = null;
  });

};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', () => {
  if (serve) {
    console.log("Xels UI was started in development mode. This requires the user to be running the Xels Full Node Daemon himself.")
  }
  else {
    startXelsApi();
  }
  createTray();
  createWindow();
  if (os.platform() === 'darwin'){
    createMenu();
  }
});

app.on('before-quit', () => {
  closeXelsApi();
});

// Quit when all windows are closed.
app.on('window-all-closed', () => {
  // On OS X it is common for applications and their menu bar
  // to stay active until the user quits explicitly with Cmd + Q
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  if (mainWindow === null) {
    createWindow();
  }
});

function closeXelsApi() {
  // if (process.platform !== 'darwin' && !serve) {
    if (process.platform !== 'darwin' && !serve && !testnet) {
    var http2 = require('http');
    const options1 = {
      hostname: 'localhost',
      port: 37221,
      path: '/api/node/shutdown',
      method: 'POST'
    };

   const req = http2.request(options1, (res) => {});
   req.write('');
   req.end();

   } else if (process.platform !== 'darwin' && !serve && testnet) {
     var http2 = require('http');
     const options2 = {
       hostname: 'localhost',
       port: 38221,
       path: '/api/node/shutdown',
       method: 'POST'
     };

   const req = http2.request(options2, (res) => {});
   req.write('');
   req.end();
   }
};

function startXelsApi() {
  var xelsProcess;
  const spawnXels = require('child_process').spawn;

  //Start Xels Daemon
  let apiPath = path.resolve(__dirname, 'assets//daemon//Xels.XelsD');
  if (os.platform() === 'win32') {
    apiPath = path.resolve(__dirname, '..\\..\\resources\\daemon\\Xels.XelsD.exe');
  } else if(os.platform() === 'linux') {
	  apiPath = path.resolve(__dirname, '..//..//resources//daemon//Xels.XelsD');
  } else {
	  apiPath = path.resolve(__dirname, '..//..//resources//daemon//Xels.XelsD');
  }

  if (!testnet) {
    xelsProcess = spawnXels(apiPath, {
      detached: true
    });
  } else if (testnet) {
    xelsProcess = spawnXels(apiPath, ['-testnet'], {
      detached: true
    });
  }

  xelsProcess.stdout.on('data', (data) => {
    writeLog(`Xels: ${data}`);
  });
}

function createTray() {
  //Put the app in system tray
  let trayIcon;
  if (serve) {
    trayIcon = nativeImage.createFromPath('./src/assets/images/icon-tray.png');
  } else {
    trayIcon = nativeImage.createFromPath(path.resolve(__dirname, '../../resources/src/assets/images/icon-tray.png'));
  }

  let systemTray = new Tray(trayIcon);
  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Hide/Show',
      click: function() {
        mainWindow.isVisible() ? mainWindow.hide() : mainWindow.show();
      }
    },
    {
      label: 'Exit',
      click: function() {
        app.quit();
      }
    }
  ]);
  systemTray.setToolTip('Xels Core');
  systemTray.setContextMenu(contextMenu);
  systemTray.on('click', function() {
    if (!mainWindow.isVisible()) {
      mainWindow.show();
    }

    if (!mainWindow.isFocused()) {
      mainWindow.focus();
    }
  });

  app.on('window-all-closed', function () {
    if (systemTray) systemTray.destroy();
  });
};

function writeLog(msg) {
  console.log(msg);
};

function createMenu() {
  var menuTemplate = [{
    label: app.getName(),
    submenu: [
      { label: "About " + app.getName(), selector: "orderFrontStandardAboutPanel:" },
      { label: "Quit", accelerator: "Command+Q", click: function() { app.quit(); }}
    ]}, {
    label: "Edit",
    submenu: [
      { label: "Undo", accelerator: "CmdOrCtrl+Z", selector: "undo:" },
      { label: "Redo", accelerator: "Shift+CmdOrCtrl+Z", selector: "redo:" },
      { label: "Cut", accelerator: "CmdOrCtrl+X", selector: "cut:" },
      { label: "Copy", accelerator: "CmdOrCtrl+C", selector: "copy:" },
      { label: "Paste", accelerator: "CmdOrCtrl+V", selector: "paste:" },
      { label: "Select All", accelerator: "CmdOrCtrl+A", selector: "selectAll:" }
    ]}
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(menuTemplate));
};
