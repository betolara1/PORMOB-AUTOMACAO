using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace AutomacaoPromobTeste.Automation{
    public static class InteractionHelper{
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        public static void AtivarJanela(Window janela){
            if (janela == null) return;

            try{
                // Se já estiver no topo, não faz nada
                var top = GetForegroundWindow();
                if (top != IntPtr.Zero && top == (IntPtr)janela.Properties.NativeWindowHandle.ValueOrDefault)
                    return;

                if (janela.Patterns.Window.IsSupported){
                    var estadoVisual = janela.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault;
                    if (estadoVisual == FlaUI.Core.Definitions.WindowVisualState.Minimized){
                        janela.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
                        EsperarUiRespirar(400);
                    }
                }
            }
            catch { }

            try{
                janela.SetForeground();
                janela.Focus();
                EsperarUiRespirar(200);
            }
            catch{
                try { janela.Focus(); } catch { }
            }
        }

        public static void Focar(AutomationElement el){
            try { el.Focus(); } catch { }
        }

        public static void ClicarComFallback(AutomationElement el){
            if (el == null) return;

            try{
                if (el.Patterns.Invoke.IsSupported){
                    el.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
            }
            catch { }

            try{
                el.Click();
                return;
            }
            catch { }

            Focar(el);
            Keyboard.Type(VirtualKeyShort.SPACE);
        }

        public static void SelecionarOuClicar(AutomationElement el){
            try{
                if (el.Patterns.SelectionItem.IsSupported){
                    el.Patterns.SelectionItem.Pattern.Select();
                    return;
                }
            }
            catch { }

            ClicarComFallback(el);
        }

        public static bool TentarDefinirValor(AutomationElement el, string valor){
            try{
                if (el.Patterns.Value.IsSupported){
                    el.Patterns.Value.Pattern.SetValue(valor);
                    return true;
                }
            }
            catch { }

            try{
                if (el.ControlType == FlaUI.Core.Definitions.ControlType.ComboBox){
                    el.AsComboBox().Value = valor;
                    return true;
                }
            }
            catch { }

            try{
                if (el.Patterns.Text.IsSupported){
                    Focar(el);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    EsperarUiRespirar(80);
                    Keyboard.Type(valor);
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static bool EsperarAte(Func<bool> condicao, int timeoutMs = 5000, int intervaloMs = 200){
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs){
                try{
                    if (condicao())
                        return true;
                }
                catch { }

                Thread.Sleep(intervaloMs);
            }

            return false;
        }

        public static T? EsperarAteRetorno<T>(Func<T?> produtor, int timeoutMs = 5000, int intervaloMs = 200) where T : class{
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs){
                try{
                    var valor = produtor();
                    if (valor != null)
                        return valor;
                }
                catch { }

                Thread.Sleep(intervaloMs);
            }

            return null;
        }

        public static void EsperarUiRespirar(int ms = 150){
            Thread.Sleep(ms);
        }

        public static bool ContemQualquer(string? texto, params string[] valores){
            if (string.IsNullOrWhiteSpace(texto))
                return false;

            return valores.Any(v => texto.Contains(v, StringComparison.OrdinalIgnoreCase));
        }
        
        public static bool ElementoValido(AutomationElement? el){
            if (el == null)
                return false;

            try{
                _ = el.Name;
                _ = el.ControlType;
                return true;
            }
            catch{
                return false;
            }
        }
    }
}
