using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PromobAutomacao.Utils{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe utilitária que gerencia acesso de baixo nível (Win32 API / PInvoke) e PowerShell à Área de Transferência (Clipboard) do Windows.
        /// Permite ler e gravar dados de forma 100% resiliente em modo headless sem a necessidade de threads STA de WPF ou Windows Forms.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class NativeClipboard{
        
        // Chamadas nativas de baixo nível (P/Invoke) para gerenciar o Clipboard do Windows.
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        
        // Chamadas nativas do Kernel32 para gerenciar alocação física de ponteiros e memória no Windows.
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr hMem);

        // Constantes da API do Windows:
        // CF_UNICODETEXT indica codificação UTF-16 (Unicode padrão do C# / Windows).
        // GMEM_MOVEABLE permite que o Windows gerencie e mova o bloco de memória dinamicamente no heap do sistema.
        const uint CF_UNICODETEXT = 13;
        const uint GMEM_MOVEABLE = 0x0002;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Recupera o texto atualmente armazenado na Área de Transferência (Clipboard) do Windows usando chamadas de baixo nível da Win32.
            /// </summary>
            /// <returns>O texto recuperado da Área de Transferência, ou <c>null</c> se o formato for incompatível ou falhar.</returns>
        //--------------------------------------------------------------------------------------
        public static string? ObterTexto(){
            try{
                // Como o Clipboard é um recurso global compartilhado por todo o Windows, outro programa
                // (como navegadores ou editores) pode estar acessando ele no mesmo milissegundo.
                // Criamos um loop de 5 tentativas com um delay de 50ms para evitar falhas de "Acesso Negado".
                for (int tentativa = 0; tentativa < 5; tentativa++){
                    if (OpenClipboard(IntPtr.Zero)){
                        try{
                            // Valida se o conteúdo copiado na memória é interpretável como texto Unicode
                            if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;

                            // Localiza o bloco de dados no clipboard
                            IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
                            if (hGlobal == IntPtr.Zero) return null;

                            // Bloqueia a memória temporariamente para leitura segura através do ponteiro
                            IntPtr pGlobal = GlobalLock(hGlobal);
                            if (pGlobal == IntPtr.Zero) return null;

                            try{
                                // Transforma o ponteiro Unicode nativo (UTF-16) de volta em uma string gerenciada do C#
                                return Marshal.PtrToStringUni(pGlobal);
                            }
                            finally{
                                // Libera a memória bloqueada
                                GlobalUnlock(hGlobal);
                            }
                        }
                        finally{
                            // Sempre fecha o clipboard para não deixar o recurso travado para o resto do sistema operacional
                            CloseClipboard();
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex){
                AppLogs.LogClipboardReadFailure(ex.Message);
            }
            return null;
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Copia um texto para a Área de Transferência do Windows de forma resiliente usando APIs nativas,
            /// caindo para o PowerShell como plano de fallback caso o acesso seja negado por bloqueios do SO.
            /// </summary>
            /// <param name="texto">O texto de caminho de arquivo ou string a ser copiado.</param>
        //--------------------------------------------------------------------------------------
        public static void CopiarParaClipboardNativo(string texto){
            try{
                // Loop de tolerância a falhas para garantir acesso exclusivo ao Clipboard
                for (int tentativa = 0; tentativa < 5; tentativa++){
                    if (OpenClipboard(IntPtr.Zero)){
                        try{
                            // Esvazia os dados antigos do clipboard para carregar o novo texto
                            EmptyClipboard();

                            // Aloca memória global de baixo nível para armazenar a string em formato UTF-16
                            // Adicionamos +1 caractere ao tamanho para o caractere nulo terminador '\0' (padrão de strings C/C++)
                            // Multiplicamos por 2 porque cada caractere Unicode consome 2 bytes na memória.
                            int byteCount = (texto.Length + 1) * 2;
                            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                            if (hGlobal == IntPtr.Zero)
                                throw new Exception("GlobalAlloc falhou");

                            // Bloqueia a memória global recém-criada para obter o ponteiro de gravação física
                            var pGlobal = GlobalLock(hGlobal);
                            try
                            {
                                // Copia os caracteres da string do C# para a memória do Windows
                                Marshal.Copy(texto.ToCharArray(), 0, pGlobal, texto.Length);
                                
                                // Insere manualmente o caractere terminador nulo (\0) ao final da string na memória
                                Marshal.WriteInt16(pGlobal, texto.Length * 2, 0); 
                            }
                            finally
                            {
                                // Destrava o bloco de memória global permitindo que o Windows assuma o controle dele
                                GlobalUnlock(hGlobal);
                            }

                            // Define o bloco de memória como o dado oficial do clipboard
                            SetClipboardData(CF_UNICODETEXT, hGlobal);
                            AppLogs.LogClipboardCopiedWin32();
                            return;
                        }
                        finally{
                            // Sempre libera o clipboard do Windows
                            CloseClipboard();
                        }
                    }

                    Thread.Sleep(50);
                }

                // Fallback primário se as tentativas diretas falharem
                CopiarParaClipboardPowerShell(texto);
            }
            catch (Exception ex){
                AppLogs.LogClipboardNativeFallback(ex.Message);
                CopiarParaClipboardPowerShell(texto);
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Copia o texto para o Clipboard invocando silenciosamente um processo secundário do PowerShell.
            /// Funciona como um plano B perfeito quando o acesso direto via P/Invoke é bloqueado.
            /// </summary>
            /// <param name="texto">O texto a ser copiado.</param>
        //--------------------------------------------------------------------------------------
        private static void CopiarParaClipboardPowerShell(string texto){
            try{
                // Escapa aspas simples para não corromper o script de comando no terminal do Windows
                var escapado = texto.Replace("'", "''");
                var startInfo = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -Command \"Set-Clipboard -Value '{escapado}'\""){
                    CreateNoWindow = true, // Roda oculto em segundo plano (sem piscar tela preta do cmd)
                    UseShellExecute = false
                };
                
                using var p = Process.Start(startInfo);
                p?.WaitForExit(2000); // Aguarda até 2 segundos para o PowerShell completar
                AppLogs.LogClipboardCopiedPowerShell();
            }
            catch (Exception ex){
                AppLogs.LogClipboardCopyFailure(ex.Message);
            }
        }
    }
}
