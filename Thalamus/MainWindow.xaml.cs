using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Thalamus
{
    public partial class MainWindow : Window
    {

        private string _compilerargs =
    "-shared -o payload.dll temp.cpp -O2 -s -static -lgdi32 -lcomdlg32";

        private IntPtr selectedhandle = IntPtr.Zero;
        private IntPtr _lastinjectedbase = IntPtr.Zero;
        private uint _lasttargetpid = 0;

        [DllImport("ThalamusCore.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetProcessList();

        [DllImport("ThalamusCore.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ManualMap(int pid, IntPtr buffer, bool browantstohijack);

        [DllImport("ThalamusCore.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool EraseHeaders(uint pid, IntPtr targetbase);

        private string _compilerpath = "";
        private MemoryMappedFile _speedFile;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            try
            {
                _speedFile = MemoryMappedFile.CreateOrOpen("ThalamusSharedMemory", 4);
            }
            catch { }


            var workarea = SystemParameters.WorkArea;

            this.Left = workarea.Left;
            this.Top = workarea.Top;
            this.Width = workarea.Width;
            this.Height = workarea.Height;
        }

        private void RefreshProcesses(object sender, RoutedEventArgs e)
        {
            ProcessCombo.Items.Clear();
            try
            {
                IntPtr ptr = GetProcessList();
                if (ptr != IntPtr.Zero)
                {
                    string raw = Marshal.PtrToStringAnsi(ptr);
                    string[] items = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items) ProcessCombo.Items.Add(item);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void SelectCompiler(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "g++.exe|*.exe" };
            if (ofd.ShowDialog() == true)
            {
                _compilerpath = ofd.FileName;
                CompilerStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                CompilerStatus.Text = "G++ set.";
            }
        }

        private async void StartInjection(object sender, RoutedEventArgs e)
        {
            comlog.Clear();

            if (string.IsNullOrEmpty(_compilerpath) || ProcessCombo.SelectedItem == null)
            { MessageBox.Show("Choose compiler and process!"); return; }

            int pid = int.Parse(ProcessCombo.SelectedItem.ToString().Split('|')[0]);

            string jsonresult = await MonacoView.ExecuteScriptAsync("GetText()");
            string code = System.Text.Json.JsonSerializer.Deserialize<string>(jsonresult);

            File.WriteAllText("temp.cpp", code);

            ProcessStartInfo psi = new ProcessStartInfo(_compilerpath)
            {
                Arguments = _compilerargs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process p = Process.Start(psi);
            string errors = p.StandardError.ReadToEnd();
            p.WaitForExit();
            File.Delete("temp.cpp");

            if (!string.IsNullOrEmpty(errors))
            {
                comlog.Text = errors;
                return;
            }

            if (File.Exists("payload.dll"))
            {
                byte[] buffer = File.ReadAllBytes("payload.dll");

                GCHandle pinnedarray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr pointer = pinnedarray.AddrOfPinnedObject();

                try
                {
                    bool hewantstohijack = checkmeforhijack.IsChecked ?? false;
                    IntPtr targetbaseaddress = ManualMap(pid, pointer, hewantstohijack);

                    if (targetbaseaddress != IntPtr.Zero)
                    {
                        _lastinjectedbase = targetbaseaddress;
                        _lasttargetpid = (uint)pid; 
                    }
                }
                finally
                {
                    pinnedarray.Free();
                }
            }
        }

        private void EditArgs(object sender, RoutedEventArgs e)
        {
            ArgEdit dlg = new ArgEdit(_compilerargs)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _compilerargs = dlg.CompilerArgs;
                comlog.Text = "Compiler arguments updated.";
            }
        }


        private void RemoveHeaders(object sender, RoutedEventArgs e)
        {
            if (_lastinjectedbase == IntPtr.Zero || _lasttargetpid == 0)
            {
                MessageBox.Show("You must inject!");
                return;
            }

            bool success = EraseHeaders(_lasttargetpid, _lastinjectedbase);

            if (success)
            {

            }
            else
            {
                MessageBox.Show("Cannot wipe headers! Did you wipe before?");
            }
        }


        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions();
            options.AdditionalBrowserArguments =
                "--disk-cache-size=1 " +
                "--media-cache-size=1 " +
                "--in-memory-storage " +
                "--disable-gpu-shader-disk-cache " +
                "--disable-application-cache";

            string temppath = Path.Combine(Path.GetTempPath(), "Thalamus_WebView2_Cache");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, temppath, options);

            await MonacoView.EnsureCoreWebView2Async(env);

            MonacoView.Source = new Uri(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Monaco",
                "monaco.html"
            ));

            MonacoView.CoreWebView2.NavigationCompleted += Monaco_NavigationCompleted;
        }

        private async void Monaco_NavigationCompleted(
    object sender,
    Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            string code = @"#include <windows.h>

void Main() {
    MessageBoxA(0, ""Hello, world!"", ""Hello"", MB_OK);
}










// --- IGNORE / DO NOT CHANGE THEM. ANY WRONG THING MIGHT MAKE CHEAT DOESNT WORK ---

DWORD WINAPI mainthread(LPVOID lpParam) {
    Main();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    if (fdwReason == DLL_PROCESS_ATTACH) 
    {
	CreateThread(0, 0, (LPTHREAD_START_ROUTINE)mainthread, 0, 0, 0);
    }
    return TRUE;
}";

            string json = System.Text.Json.JsonSerializer.Serialize(code);
            await MonacoView.ExecuteScriptAsync($"SetText({json})");
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeedText == null) return;

            float val = (float)e.NewValue;
            SpeedText.Text = val.ToString("F1") + "x";

            if (_speedFile != null)
            {
                using (var accessor = _speedFile.CreateViewAccessor())
                {
                    accessor.Write(0, val);
                }
            }
        }

        private void InjectSpeedDLL(object sender, RoutedEventArgs e)
        {
            if (ProcessCombo.SelectedItem == null) { MessageBox.Show("Choose a process!"); return; }
            int pid = int.Parse(ProcessCombo.SelectedItem.ToString().Split('|')[0]);

            if (File.Exists("ThalamusSpeed.dll"))
            {
                byte[] buffer = File.ReadAllBytes("ThalamusSpeed.dll");
                GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr pointer = pinned.AddrOfPinnedObject();

                try
                {
                    bool doesbrowanttohijack = checkmeforhijack.IsChecked ?? false;
                    IntPtr targetbaseaddress = ManualMap(pid, pointer, doesbrowanttohijack);
                    IntPtr res = ManualMap(pid, pointer, doesbrowanttohijack);
                    if (res != IntPtr.Zero)
                    {
                        _lastinjectedbase = res;
                        _lasttargetpid = (uint)pid;
                        CompilerStatus.Text = "SpeedHack DLL injected.";
                        CompilerStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                }
                finally { pinned.Free(); }
            }
            else { MessageBox.Show("Cannot find ThalamusSpeed.dll!"); }
        }

        private async void igotbrainrot(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string templateType = btn.Tag.ToString();
            string code = "";

            switch (templateType)
            {
                case "default":
                    code = @"#include <windows.h>

void Main() {
    MessageBoxA(0, ""Hello, world!"", ""Hello"", MB_OK);
}";
                    break;

                case "change_val":
                    code = @"#include <windows.h>
#include <iostream>

void Main() {
    uintptr_t address = 0x12345678; // The address you got from Cheat Engine or something
    int value = 9999;
    
    DWORD oldprotect;
    if (VirtualProtect((LPVOID)address, sizeof(int), PAGE_EXECUTE_READWRITE, &oldprotect)) {
        *(int*)address = value;
        VirtualProtect((LPVOID)address, sizeof(int), oldprotect, &oldprotect); // Revert back the protection
    }
}";
                    break;

                case "transparent":
                    code = @"#include <windows.h>

void Main() {
    HWND hwnd = GetActiveWindow();
    SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED);
    SetLayeredWindowAttributes(hwnd, 0, (255 * 70) / 100, LWA_ALPHA); // Change 70 to change transparency, its %70 right now
}";
                    break;

                case "pointer":
                    code = @"#include <windows.h>
#include <vector>

uintptr_t GetPointerAddress(uintptr_t ptr, std::vector<unsigned int> offsets) {
    uintptr_t addr = ptr;
    for (unsigned int i = 0; i < offsets.size(); ++i) {
        addr = *(uintptr_t*)addr;
        addr += offsets[i];
    }
    return addr;
}

void Main() {
    // --- IMPORTANT: CHANGE THIS ---
    // GetModuleHandle(NULL) retrieves the base address of the game.
    // 0x00123456 is the constant offset you see in green in Cheat Engine.
    // The sum of these two is not yet the bullet address, but the starting point of the path to the bullet.
    uintptr_t baseaddress = (uintptr_t)GetModuleHandle(NULL) + 0x00123456;
    uintptr_t finaladdress = GetPointerAddress(baseaddress, { 0x10, 0x20, 0x30 });
    // You can now use finaladdress for later
}
";
                    break;
            }


            string finalcode = code + @"


// --- IGNORE / DO NOT CHANGE THEM. ANY WRONG THING MIGHT MAKE CHEAT DOESNT WORK ---
DWORD WINAPI mainthread(LPVOID lpParam) {
    Main();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    if (fdwReason == DLL_PROCESS_ATTACH) {
        CreateThread(0, 0, (LPTHREAD_START_ROUTINE)mainthread, 0, 0, 0);
    }
    return TRUE;
}";

            string json = System.Text.Json.JsonSerializer.Serialize(finalcode);
            await MonacoView.ExecuteScriptAsync($"SetText({json})");
        }
    }
}