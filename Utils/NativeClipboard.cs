using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutomacaoPromobTeste.Utils{
    public static class NativeClipboard{
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr hMem);

        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        public static string? ObterTexto(){
            try{
                for (int tentativa = 0; tentativa < 5; tentativa++){
                    if (OpenClipboard(IntPtr.Zero)){
                        try{
                            if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;

                            IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
                            if (hGlobal == IntPtr.Zero) return null;

                            IntPtr pGlobal = GlobalLock(hGlobal);
                            if (pGlobal == IntPtr.Zero) return null;

                            try{
                                return Marshal.PtrToStringUni(pGlobal);
                            }
                            finally{
                                GlobalUnlock(hGlobal);
                            }
                        }
                        finally{
                            CloseClipboard();
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex){
                Logger.Log($"  [AVISO] Falha ao ler do clipboard: {ex.Message}", LogLevel.Debug);
            }
            return null;
        }

        public static void CopiarParaClipboardNativo(string texto){
            try{
                for (int tentativa = 0; tentativa < 5; tentativa++){
                    if (OpenClipboard(IntPtr.Zero)){
                        try{
                            EmptyClipboard();

                            // Aloca memória global para o texto Unicode (UTF-16 + null terminator)
                            int byteCount = (texto.Length + 1) * 2;
                            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                            if (hGlobal == IntPtr.Zero)
                                throw new Exception("GlobalAlloc falhou");

                            var pGlobal = GlobalLock(hGlobal);
                            try
                            {
                                Marshal.Copy(texto.ToCharArray(), 0, pGlobal, texto.Length);
                                Marshal.WriteInt16(pGlobal, texto.Length * 2, 0); // null terminator
                            }
                            finally
                            {
                                GlobalUnlock(hGlobal);
                            }

                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                            Logger.Log("  [OK] Caminho copiado para o Clipboard (Win32).", LogLevel.Debug);
                            return;
                        }
                        finally{
                            CloseClipboard();
                        }
                    }

                    Thread.Sleep(50);
                }

                CopiarParaClipboardPowerShell(texto);
            }
            catch (Exception ex){
                Logger.Log($"  [AVISO] Falha no clipboard nativo: {ex.Message}. Tentando PowerShell...", LogLevel.Warn);
                CopiarParaClipboardPowerShell(texto);
            }
        }

        private static void CopiarParaClipboardPowerShell(string texto){
            try{
                var escapado = texto.Replace("'", "''");
                var startInfo = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"Set-Clipboard -Value '{escapado}'\""){
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = Process.Start(startInfo);
                p?.WaitForExit(2000);
                Logger.Log("  [OK] Caminho copiado via PowerShell.", LogLevel.Debug);
            }
            catch (Exception ex){
                Logger.Log($"  [AVISO] Falha ao copiar para Clipboard: {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
