using System;
using System.Diagnostics;
using System.Linq;
using AutomacaoPromobTeste.Utils;
using FlaUI.Core.AutomationElements;

namespace AutomacaoPromobTeste.Utils{
    public static class Diagnostics{
        public static void Medir(string nome, Action acao){
            var sw = Stopwatch.StartNew();
            try { acao(); }
            finally { Logger.Log($"  [TEMPO] {nome}: {sw.ElapsedMilliseconds} ms", LogLevel.Debug); }
        }

        public static void ListarBotoesProject(Window janela, string automationIdHost){
            var processId = janela.Properties.ProcessId.ValueOrDefault;
            Console.WriteLine($"[INFO] Analisando estrutura para Processo: {processId}");

            var janelasDoProcesso = janela.Automation.GetDesktop().FindAllChildren()
                .Where(x => x.Properties.ProcessId.ValueOrDefault == processId)
                .ToList();

            if (janelasDoProcesso.Count > 1){
                Console.WriteLine($"[AVISO] Encontradas {janelasDoProcesso.Count} janelas para este processo:");
                foreach (var j in janelasDoProcesso)
                    Console.WriteLine($"  - Window: '{j.Name}' (ID: {j.Properties.AutomationId.ValueOrDefault})");
            }

            var host = janela.FindFirstDescendant(automationIdHost);
            if (host != null){
                Console.WriteLine("[OK] 'elementHost1' encontrado! Escaneando conteúdo profundo...");
                var items = host.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .ToList();

                foreach (var e in items)
                    Console.WriteLine($"  -> Tipo: {e.ControlType}, Nome: '{e.Name}', Id: '{e.Properties.AutomationId.ValueOrDefault}'");
            }
            else{
                Console.WriteLine("[AVISO] elementHost1 não encontrado. Escaneando janela toda...");
                var all = janela.FindAllDescendants()
                    .Where(e => !string.IsNullOrEmpty(e.Name))
                    .GroupBy(e => (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                    .Select(g => g.First())
                    .Take(50)
                    .ToList();

                foreach (var e in all)
                    Console.WriteLine($"  -> Tipo: {e.ControlType}, Nome: '{e.Name}', Id: '{e.Properties.AutomationId.ValueOrDefault}'");
            }

            Console.WriteLine("------------------------------------------");
        }
    }
}
