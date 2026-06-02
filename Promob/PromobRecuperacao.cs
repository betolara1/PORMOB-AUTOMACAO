using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using PromobAutomacao.Automation;
using PromobAutomacao.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace PromobAutomacao.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Componente responsável por gerenciar a rotina de auto-recuperação (Self-Healing)
        /// do robô quando ocorrem timeouts ou exceções no fluxo principal.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobRecuperacao{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Rotina de auto-recuperação (Self-Healing) disparada quando ocorrem timeouts ou falhas inesperadas no fluxo principal.
            /// Tenta desobstruir a UI do Promob fechando modais travados e retornando a aplicação ao estado inicial seguro.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void TentarRecuperar(UIA3Automation automation){
            AppLogs.LogRecoveryIniciando();

            try{
                WindowFinder.CachedHost = null; // Invalida cache de UI

                var janelaBase = PromobWindowHelper.AguardarJanelaPromob(automation, 1000);
                if (janelaBase != null) InteractionHelper.AtivarJanela(janelaBase);
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                InteractionHelper.EsperarUiRespirar(250);

                if (janelaBase != null) InteractionHelper.AtivarJanela(janelaBase);
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                InteractionHelper.EsperarUiRespirar(250);

                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    var titulo = j.Name ?? "";
                    if (InteractionHelper.ContemQualquer(titulo, PromobConfig.TitulosAviso)){
                        try{
                            var popup = j.AsWindow();
                            popup.SetForeground();
                            
                            // Verifica se é o popup de "Deseja cancelar a operação?" (Busca rasa ultra rápida com FindAllChildren)
                            var textElement = popup.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)).FirstOrDefault();
                            var texto = textElement?.Properties.Name.ValueOrDefault ?? "";

                            if (texto.Contains(PromobConfig.MsgConfirmarCancelamento, StringComparison.OrdinalIgnoreCase)){
                                AppLogs.LogRecoveryPopupCancelamentoDetectado(PromobConfig.BtnNao);
                                
                                // Busca rasa (FindAllChildren) filtrada em memória para evitar COM hangs
                                var btnNao = popup.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                                    .FirstOrDefault(b => b.Name == PromobConfig.BtnNao || b.Name == PromobConfig.BtnNaoAlt);
                                
                                if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                                else {
                                    InteractionHelper.AtivarJanela(popup);
                                    Keyboard.Type("n");
                                }
                            }
                            else {
                                InteractionHelper.AtivarJanela(popup);
                                Keyboard.Press(VirtualKeyShort.ESCAPE);
                            }
                            InteractionHelper.EsperarUiRespirar(200);
                        }
                        catch{
                            // ignora
                        }
                    }
                }

                var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 2500);
                if (janela != null){
                    FecharProjetoEIgnorarSalvar(janela);
                }
            }
            catch (Exception ex){
                AppLogs.LogRecoveryFalhou(ex.Message);
            }

            InteractionHelper.EsperarUiRespirar(1000);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Tenta agressivamente fechar o projeto ativo no Promob durante a execução da rotina de recuperação,
            /// forçando a negação de qualquer diálogo de salvamento de arquivos pendente.
            /// </summary>
            /// <param name="janelaPromob">A janela principal do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void FecharProjetoEIgnorarSalvar(Window janelaPromob){
            AppLogs.LogRecoveryTentandoFecharProjeto();
            InteractionHelper.AtivarJanela(janelaPromob);

            var raizBusca = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);
            var btnFechar = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByAutomationId(PromobConfig.IdProjectClose).Or(cf.ByName(PromobConfig.BtnFechar))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button && ((e.Properties.AutomationId.ValueOrDefault ?? "") == PromobConfig.IdProjectClose || (e.Properties.Name.ValueOrDefault ?? "") == PromobConfig.BtnFechar),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnFechar != null) {
                AppLogs.LogRecoveryFechandoProjetoUia();
                InteractionHelper.ClicarComFallback(btnFechar);
            } 
            else {
                AppLogs.LogRecoveryFallbackTecladoFecharProjeto();
                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_A);
                InteractionHelper.EsperarUiRespirar(300);

                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.Type("f");
            }

            AppLogs.LogRecoveryTratandoPopupSalvamento();
            bool fechou = InteractionHelper.EsperarAte(() => {
                var popup = PromobWindowHelper.EncontrarPopupAtencao(janelaPromob.Automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                if (popup != null) {
                    // Busca rasa rápida com FindAllChildren filtrando em memória
                    var btnNao = popup.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                        .FirstOrDefault(b => b.Name == PromobConfig.BtnNao || b.Name == PromobConfig.BtnNaoAlt);
                    
                    if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                    else {
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        Keyboard.Type("n");
                    }
                    return true;
                }
                return false;
            }, 3000, 200);

            if (!fechou) {
                 InteractionHelper.AtivarJanela(janelaPromob);
                 Keyboard.Type("n");
            }
        }
    }
}
