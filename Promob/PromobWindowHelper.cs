using System;
using System.Linq;
using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste.Promob{
    public static class PromobWindowHelper{
        public static int? CachedProcessIdPromob;

        public static Window? AguardarJanelaPromob(UIA3Automation automation, int timeoutMs = PromobConfig.TimeoutPadrao){
            Window? encontrada = null;

            var promobProc = System.Diagnostics.Process.GetProcesses()
                .FirstOrDefault(p => p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                   !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase));

            if (promobProc != null)
                CachedProcessIdPromob = promobProc.Id;

            InteractionHelper.EsperarAte(() =>{
                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    if (promobProc != null){
                        if (j.Properties.ProcessId.ValueOrDefault == promobProc.Id){
                            var name = j.Name ?? "";
                            if (EhJanelaPromob(name) || name.Contains("Promob", StringComparison.OrdinalIgnoreCase)){
                                encontrada = j.AsWindow();
                                return true;
                            }
                        }
                    }
                    else{
                        var fallbackName = j.Name ?? "";
                        if (EhJanelaPromob(fallbackName)){
                            encontrada = j.AsWindow();
                            try { CachedProcessIdPromob = encontrada.Properties.ProcessId.ValueOrDefault; } catch { }
                            return true;
                        }
                    }
                }

                return false;
            }, timeoutMs);

            if (encontrada != null)
                Logger.Log($"  [OK] Janela encontrada (PID: {CachedProcessIdPromob}): '{encontrada.Name}'");

            return encontrada;
        }

        public static bool EhJanelaPromob(string? nome){
            if (string.IsNullOrWhiteSpace(nome))
                return false;

            if (nome.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
                nome.Contains("VS Code", StringComparison.OrdinalIgnoreCase))
                return false;

            return nome.Contains("- Promob Studio", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Promob Studio Bartz", StringComparison.OrdinalIgnoreCase);
        }

        public static Window? EncontrarJanelaWizard(UIA3Automation automation, Window janelaPrincipal){
            var desktop = automation.GetDesktop();

            var wizardDesktop = desktop.FindFirstChild(cf => cf.ByName(PromobConfig.NomeJanelaWizardImportacao));
            if (wizardDesktop != null)
                return wizardDesktop.AsWindow();

            var wizardDesc = janelaPrincipal.FindFirstDescendant(cf => cf.ByName(PromobConfig.NomeJanelaWizardImportacao));
            return wizardDesc?.AsWindow();
        }

        public static Window? JanelaArquivoAberta(UIA3Automation automation, int? targetProcessId = null){
            var desktop = automation.GetDesktop();

            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            var consulta = janelas.AsEnumerable();
            if (targetProcessId.HasValue){
                consulta = consulta.Where(j =>{
                    try { return j.Properties.ProcessId.ValueOrDefault == targetProcessId.Value; }
                    catch { return false; }
                });
            }

            var dialogo = consulta.FirstOrDefault(j =>
                InteractionHelper.ContemQualquer(j.Name, "Abrir", "Open", "Salvar Como", "Save As"));

            if (dialogo != null)
                return dialogo.AsWindow();

            var profundo = desktop.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                  .And(cf.ByName("Abrir").Or(cf.ByName("Open")).Or(cf.ByName("Salvar Como")).Or(cf.ByName("Save As"))));

            return profundo?.AsWindow();
        }

        public static Window? EncontrarPopupAtencao(AutomationElement desktop, int? targetProcessId = null){
            var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

            var consulta = janelas.AsEnumerable();
            if (targetProcessId.HasValue){
                consulta = consulta.Where(j =>{
                    try { return j.Properties.ProcessId.ValueOrDefault == targetProcessId.Value; }
                    catch { return false; }
                });
            }

            var termosPrioritarios = new[] { "Atenção", "Atencao", "Atençao", "Confirmação", "Confirmacao", "Salvar", "Save" };
            var popup = consulta.FirstOrDefault(j => InteractionHelper.ContemQualquer(j.Name, termosPrioritarios));

            if (popup == null){
                // Somente como fallback, se soubermos o ProcessId
                popup = consulta.FirstOrDefault(j => (j.Name ?? "").Contains("Promob", StringComparison.OrdinalIgnoreCase));
            }

            if (popup != null)
                return popup.AsWindow();

            return null;
        }
    }
}
