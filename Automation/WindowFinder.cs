using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;

namespace AutomacaoPromobTeste.Automation{
    public static class WindowFinder{
        public static AutomationElement? CachedHost;

        public static AutomationElement ObterHostOuJanela(Window janela, string automationIdHost, int? processId){
            if (InteractionHelper.ElementoValido(CachedHost))
                return CachedHost!;

            Logger.Log($"    [DEBUG] Buscando {automationIdHost}...", LogLevel.Debug);
            var swHost = Stopwatch.StartNew();

            CachedHost = BuscarElementoComFallback(
                janela,
                cf => cf.ByAutomationId(automationIdHost),
                e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(automationIdHost, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: processId
            );
            swHost.Stop();

            if (CachedHost != null)
                Logger.Log($"    [OK] {automationIdHost} localizado ({swHost.ElapsedMilliseconds}ms) e cacheado.", LogLevel.Debug);
            else
                Logger.Log($"    [AVISO] {automationIdHost} não encontrado após {swHost.ElapsedMilliseconds}ms. Usando janela principal.", LogLevel.Debug);

            return CachedHost ?? janela;
        }

        public static AutomationElement? BuscarElementoComFallback(
            AutomationElement raiz,
            Func<ConditionFactory, ConditionBase> buscaPrincipal,
            Func<AutomationElement, bool>? filtroFallback = null,
            bool limitarAoMesmoProcesso = false,
            int? processId = null){
            // FASE 1: Varredura Rasa (BFS-like) — Muito mais rápido para elementos estruturais
            var swFase1 = Stopwatch.StartNew();
            if (filtroFallback != null){
                var todos = BuscarAteNivel(raiz, maxNivel: 4);
                var consulta = todos.AsEnumerable();

                if (limitarAoMesmoProcesso && processId.HasValue){
                    consulta = consulta.Where(e =>{
                        try { return e.Properties.ProcessId.ValueOrDefault == processId.Value; }
                        catch { return false; }
                    });
                }

                var resultado = consulta.FirstOrDefault(filtroFallback);
                swFase1.Stop();

                if (resultado != null){
                    Logger.Log($"      [PERF] Varredura Rasa encontrou o elemento em {swFase1.ElapsedMilliseconds}ms.", LogLevel.Debug);
                    return resultado;
                }
            }

            // FASE 2: Busca Profunda (UIA Nativo) — Fallback se a rasa falhar
            Logger.Log($"      [PERF] Varredura Rasa falhou ou ignorada ({swFase1.ElapsedMilliseconds}ms). Ativando Busca Profunda...", LogLevel.Debug);

            var swFase2 = Stopwatch.StartNew();
            var direto = raiz.FindFirstDescendant(buscaPrincipal);
            swFase2.Stop();

            if (direto != null){
                Logger.Log($"      [PERF] Busca Profunda (UIA) encontrou em {swFase2.ElapsedMilliseconds}ms.", LogLevel.Debug);
                return direto;
            }

            Logger.Log($"      [PERF] Ambas as buscas falharam (Total: {swFase1.ElapsedMilliseconds + swFase2.ElapsedMilliseconds}ms).", LogLevel.Debug);
            return null;
        }

        public static IEnumerable<AutomationElement> BuscarAteNivel(AutomationElement raiz, int maxNivel, int nivelAtual = 0){
            if (nivelAtual > maxNivel)
                yield break;

            AutomationElement[] filhos;
            try { filhos = raiz.FindAllChildren(); }
            catch { yield break; }

            foreach (var filho in filhos){
                yield return filho;
                foreach (var desc in BuscarAteNivel(filho, maxNivel, nivelAtual + 1))
                    yield return desc;
            }
        }
    }
}
