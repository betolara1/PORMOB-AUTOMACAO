using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

// ═══════════════════════════════════════════════════════════════════════════════
//  AUTOMAÇÃO PROMOB — AGENTE IA VISION (OpenRouter)
//  A IA tira print da tela e age como um humano para cada etapa.
//  Requer: dotnet run  (como Administrador)
// ═══════════════════════════════════════════════════════════════════════════════
namespace AutomacaoPromobTeste
{
    // ── Resposta da IA ───────────────────────────────────────────────────────
    class AcaoIA
    {
        [JsonPropertyName("acao")]
        public string acao   { get; set; } = "wait";   // click | wait | done | error

        [JsonPropertyName("x_pct")]
        public double x_pct  { get; set; } = 0;        // 0.0 a 100.0

        [JsonPropertyName("y_pct")]
        public double y_pct  { get; set; } = 0;        // 0.0 a 100.0

        [JsonPropertyName("motivo")]
        public string motivo { get; set; } = "";
    }

    // ── Etapas do fluxo ──────────────────────────────────────────────────────
    enum Etapa
    {
        ClicarImportarProjeto,      // 1 - Clicar no botão "Importar Projeto"
        ClicarBotaoTresPontos,      // 2 - Clicar no "..." para abrir diálogo de arquivo
        SelecionarArquivoEAbrir,    // 3 - Selecionar primeiro arquivo e clicar "Abrir"
        ClicarAvancar,              // 4a - Clicar no botão "Avançar"
        TratarPopupCancelar,        // 4b - Clicar "Cancelar" no popup de atenção
        Concluido,
        Erro
    }

    class Program
    {
        // ── Configurações ────────────────────────────────────────────────────
        static string API_KEY = "";
        const string  MODELO  = "google/gemini-2.0-flash-001";

        static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        static readonly string PastaPromob = Path.Combine(DesktopPath, "promob");
        static readonly string PastaDebug  = Path.Combine(DesktopPath, "promob_debug");

        const int MAX_TENTATIVAS = 15;
        const int DELAY_ENTRE    = 1500;
        const int DELAY_SCREENSHOT = 1500; // Aumentado para 1.5s conforme solicitado

        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };

        static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Win32 API ────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, int extra);
        [DllImport("user32.dll")] static extern int  GetSystemMetrics(int n);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        const uint MOUSE_DOWN = 0x0002;
        const uint MOUSE_UP   = 0x0004;
        const int  SW_MAXIMIZE = 3;

        // ════════════════════════════════════════════════════════════════════
        //  MAIN
        // ════════════════════════════════════════════════════════════════════
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            CarregarEnv();

            Banner("Automação Promob — Agente IA Vision (OpenRouter)");

            if (string.IsNullOrEmpty(API_KEY))
            {
                Erro("API_KEY não encontrada no arquivo .env!");
                Info("Crie o arquivo .env com: GEMINI_API_KEY=sua_chave");
                Console.ReadKey(); return;
            }

            if (!Directory.Exists(PastaPromob))
            {
                Erro($"Pasta não encontrada: {PastaPromob}");
                Console.ReadKey(); return;
            }

            Directory.CreateDirectory(PastaDebug);

            var arquivos = Directory.GetFiles(PastaPromob, "*.promob")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (arquivos.Count == 0)
            {
                Erro("Nenhum arquivo .promob encontrado.");
                Console.ReadKey(); return;
            }

            Info($"{arquivos.Count} arquivo(s) encontrado(s).");

            // ── Encontrar o Promob via FlaUI ──
            Info("\nProcurando janela do Promob...");

            using var automation = new UIA3Automation();
            var janela = AguardarJanelaPromob(automation, 10000);

            if (janela == null)
            {
                Erro("Janela do Promob NÃO encontrada!");
                Info("Certifique-se que:");
                Info("  1. O Promob Studio Bartz está aberto");
                Info("  2. Este programa roda como Administrador");
                Console.ReadKey(); return;
            }

            Ok($"Promob encontrado: \"{janela.Name}\"");
            SalvarScreenshotDebug("00_inicio");

            // ── Processar cada arquivo ──
            int processados = 0, erros = 0;

            foreach (var arquivo in arquivos)
            {
                var nome = Path.GetFileName(arquivo);
                Console.WriteLine($"\n{'═',0}{'═',44}");
                Console.WriteLine($"[{processados + erros + 1}/{arquivos.Count}] {nome}");
                Console.WriteLine($"{'═',0}{'═',44}");

                try
                {
                    AtivarJanela(janela);
                    Thread.Sleep(500);

                    ProcessarArquivo(janela, arquivo);
                    processados++;
                    Ok($"{nome} processado com sucesso!");
                }
                catch (Exception ex)
                {
                    erros++;
                    Erro($"Falha: {ex.Message}");
                    SalvarScreenshotDebug($"erro_{processados + erros}");

                    Keyboard.Press(VirtualKeyShort.ESCAPE); Thread.Sleep(400);
                    Keyboard.Press(VirtualKeyShort.ESCAPE); Thread.Sleep(400);
                    Keyboard.Press(VirtualKeyShort.ESCAPE); Thread.Sleep(800);
                }
            }

            Console.WriteLine();
            Banner($"✅ {processados} processados  |  ❌ {erros} erros");
            Console.ReadKey();
        }

        // ════════════════════════════════════════════════════════════════════
        //  LOOP PRINCIPAL — 4 ETAPAS COM IA VISION
        // ════════════════════════════════════════════════════════════════════
        static void ProcessarArquivo(Window janela, string caminhoArquivo)
        {
            var etapa     = Etapa.ClicarImportarProjeto;
            int tentativa = 0;

            Info($"Iniciando fluxo para: {Path.GetFileName(caminhoArquivo)}");

            while (etapa != Etapa.Concluido && etapa != Etapa.Erro)
            {
                if (tentativa >= MAX_TENTATIVAS)
                    throw new Exception($"Etapa '{etapa}' excedeu {MAX_TENTATIVAS} tentativas.");

                // 1. Screenshot da janela específica
                Thread.Sleep(DELAY_SCREENSHOT);
                IntPtr hwnd = janela.Properties.NativeWindowHandle.ValueOrDefault;
                string b64 = TirarScreenshot(hwnd);

                // 2. Prompt da etapa
                string prompt = MontarPrompt(etapa);

                // 3. Consultar IA
                Console.WriteLine($"\n  📸 Etapa: {etapa,-35} tentativa {tentativa + 1}");
                var acao = ConsultarIA(b64, prompt);
                Console.WriteLine($"  🤖 [{acao.acao.ToUpper(),-5}] {acao.motivo}");

                // 4. Executar
                ExecutarAcao(acao, hwnd);

                // 5. Transição de etapa
                var etapaAnterior = etapa;

                if (acao.acao == "error")
                {
                    etapa = Etapa.Erro;
                }
                else if (acao.acao == "done")
                {
                    etapa = ProximaEtapa(etapa);
                    tentativa = 0;
                    Ok($"Etapa {etapaAnterior} concluída! → Próxima: {etapa}");
                }
                else
                {
                    tentativa++;
                }

                Thread.Sleep(DELAY_ENTRE);
            }

            if (etapa == Etapa.Erro)
                throw new Exception("IA reportou erro irrecuperável.");

            Ok("Todas as etapas concluídas com sucesso!");
        }

        static Etapa ProximaEtapa(Etapa atual) => atual switch
        {
            Etapa.ClicarImportarProjeto   => Etapa.ClicarBotaoTresPontos,
            Etapa.ClicarBotaoTresPontos   => Etapa.SelecionarArquivoEAbrir,
            Etapa.SelecionarArquivoEAbrir => Etapa.ClicarAvancar,
            Etapa.ClicarAvancar           => Etapa.TratarPopupCancelar,
            Etapa.TratarPopupCancelar     => Etapa.Concluido,
            _                            => Etapa.Concluido
        };

        // ════════════════════════════════════════════════════════════════════
        //  PROMPTS POR ETAPA
        // ════════════════════════════════════════════════════════════════════
        static string MontarPrompt(Etapa etapa)
        {
            string instrucaoBase = """
Você é um agente de automação controlando o software "Promob Studio Bartz".
A screenshot mostra o estado ATUAL da tela do computador.

Responda APENAS com JSON válido (sem markdown, sem texto fora do JSON):

{
  "acao": "click" | "wait" | "done" | "error",
  "x_pct": <número decimal 0.0 a 100.0 — posição horizontal na tela em porcentagem>,
  "y_pct": <número decimal 0.0 a 100.0 — posição vertical na tela em porcentagem>,
  "motivo": "<descreva o que vê na tela e por que está fazendo isso>"
}

AÇÕES:
- click → clique esquerdo em (x_pct, y_pct). Aponte para o CENTRO EXATO do botão/elemento.
- wait  → tela ainda carregando, nada para fazer agora.
- done  → o objetivo desta etapa JÁ FOI CONCLUÍDO (a tela mostra o resultado esperado).
- error → erro sem recuperação.

REGRAS IMPORTANTES:
- A imagem que você está vendo é APENAS A JANELA DO PROMOB (não a tela inteira).
- x_pct e y_pct são PORCENTAGENS DESSA JANELA (0.0=esquerda/topo, 100.0=direita/fundo).
- Exemplo: x_pct: 50.0 e y_pct: 50.0 é o meio exato da janela.
- Seja PRECISO ao apontar para o centro do elemento desejado.
- Se o objetivo da etapa já está cumprido na tela (ex: a janela já está aberta), retorne "done".

""";

            string instrucaoEtapa = etapa switch
            {
                Etapa.ClicarImportarProjeto => """
OBJETIVO: Abrir a janela de importação seguindo o caminho de menus: Arquivo -> Projetos -> Importar projeto.

DICA DE LOCALIZAÇÃO (IMPORTANTE):
- O menu "Arquivo" (texto) fica EXATAMENTE no canto superior esquerdo da janela, quase encostado na borda esquerda. No seu sistema, costuma ficar por volta de x_pct: 2.5%, y_pct: 5.5%.
- O menu "Ajuda" fica à DIREITA de "Arquivo". NÃO clique em "Ajuda" (que costuma ficar em x_pct: 7.0%).
- O menu que você quer é o PRIMEIRO (o mais à esquerda de todos).

COMO AGIR:
1. **Se você vê apenas a tela principal**: Clique no menu "Arquivo" (x_pct ~2.5%). Faça o passo 2.
2. Clique no botão "Importar projeto" (x_pct ~5% e y_pct ~10%).

3. **Se a janela "Importar projeto" já estiver aberta**: Retorne "done".

AÇÃO: Clique no menu ou opção necessária.
""",

                Etapa.ClicarBotaoTresPontos => """
OBJETIVO: Clicar no botão "..." (três pontos) que fica ao lado do campo "Caminho" na janela "Importar projeto".

COMO IDENTIFICAR:
- A janela "Importar projeto" deve estar aberta.
- O campo "Caminho" tem um botão pequeno "..." à direita dele.
- Se você vê a janela/diálogo "Abrir" do Windows já aberta (com lista de arquivos), retorne "done".

AÇÃO: Clique no centro do botão "..." ao lado do campo "Caminho".
""",

                Etapa.SelecionarArquivoEAbrir => """
OBJETIVO: Na janela "Abrir" do Windows, selecionar o PRIMEIRO arquivo da lista e clicar no botão "Abrir".

COMO FAZER (2 ações em sequência):
1. PRIMEIRO: Clique no PRIMEIRO arquivo que aparece na lista de arquivos (o que está no topo da lista).
   - Após clicar, o nome do arquivo deve aparecer no campo "Nome:" na parte inferior.
2. DEPOIS (na próxima chamada, quando o arquivo já estiver selecionado): Clique no botão "Abrir" que fica no canto inferior direito da janela.

VERIFICAÇÕES:
- Se o campo "Nome:" na parte inferior JÁ mostra um nome de arquivo E o arquivo na lista está destacado/selecionado (em azul), clique no botão "Abrir".
- Se a janela "Abrir" já fechou e você vê a janela "Importar projeto" com o campo "Caminho" preenchido, retorne "done".
- Se nenhum arquivo está selecionado, clique no primeiro arquivo da lista.

AÇÃO: Clique no primeiro arquivo OU no botão "Abrir" conforme o estado atual da tela.
""",

                Etapa.ClicarAvancar => """
OBJETIVO: Clicar no botão "Avançar" na janela "Importar projeto".

COMO IDENTIFICAR:
- A janela "Importar projeto" deve estar visível com o campo "Caminho" preenchido.
- O botão "Avançar" fica na parte inferior da janela, à direita do botão "Voltar".
- Se você vê um popup de "Atenção" ou "Confirmação" já aberto, retorne "done".

AÇÃO: Clique no centro do botão "Avançar".
""",

                Etapa.TratarPopupCancelar => """
OBJETIVO: Clicar no botão "Cancelar" no popup de "Atenção" que apareceu.

COMO IDENTIFICAR:
- Deve ter aparecido um popup/diálogo de "Atenção" ou "Confirmação".
- O botão "Cancelar" deve estar visível no popup.
- Se o popup já fechou e a tela voltou ao normal, retorne "done".

AÇÃO: Clique no centro do botão "Cancelar" no popup.
""",

                _ => "Analise a tela e decida a próxima ação."
            };

            return instrucaoBase + instrucaoEtapa;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSULTA À IA (OpenRouter)
        // ════════════════════════════════════════════════════════════════════
        static AcaoIA ConsultarIA(string b64, string prompt)
        {
            string url = "https://openrouter.ai/api/v1/chat/completions";

            var body = new
            {
                model = MODELO,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:image/png;base64,{b64}" } }
                        }
                    }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            string json = JsonSerializer.Serialize(body);

            for (int t = 1; t <= 4; t++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("Authorization", $"Bearer {API_KEY}");
                    request.Headers.Add("HTTP-Referer", "https://github.com/automacao-promob");
                    request.Headers.Add("X-Title", "Automacao Promob");
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var res = Http.SendAsync(request).GetAwaiter().GetResult();
                    string r = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if ((int)res.StatusCode == 429)
                    {
                        Warn($"Rate limit (429). Aguardando {15 * t}s...");
                        Thread.Sleep(15000 * t); continue;
                    }

                    if (!res.IsSuccessStatusCode)
                    {
                        Warn($"Erro {res.StatusCode}: {r[..Math.Min(200, r.Length)]}");
                        if (t < 4) { Thread.Sleep(5000); continue; }
                        return new AcaoIA { acao = "wait", motivo = "API indisponível" };
                    }

                    // Extrair content da resposta OpenRouter
                    using var doc = JsonDocument.Parse(r);
                    string? txt = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (string.IsNullOrEmpty(txt))
                        return new AcaoIA { acao = "wait", motivo = "Resposta vazia" };

                    // Limpeza de markdown
                    txt = txt.Replace("```json", "").Replace("```", "").Trim();
                    int fb = txt.IndexOf('{');
                    int lb = txt.LastIndexOf('}');
                    if (fb != -1 && lb > fb) txt = txt.Substring(fb, lb - fb + 1);

                    Console.WriteLine($"  [JSON] {txt[..Math.Min(120, txt.Length)]}");
                    return JsonSerializer.Deserialize<AcaoIA>(txt, JsonOpts)
                           ?? new AcaoIA { acao = "wait", motivo = "Parse falhou" };
                }
                catch (Exception ex)
                {
                    Warn($"Tentativa {t}/4: {ex.Message}");
                    if (t < 4) Thread.Sleep(5000);
                }
            }
            return new AcaoIA { acao = "wait", motivo = "Todas as tentativas falharam" };
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXECUTAR AÇÃO
        // ════════════════════════════════════════════════════════════════════
        static void ExecutarAcao(AcaoIA a, IntPtr hwnd)
        {
            switch (a.acao.ToLower())
            {
                case "click":
                    if (a.x_pct > 0 || a.y_pct > 0)
                    {
                        GetWindowRect(hwnd, out RECT rect);
                        int w = rect.Right - rect.Left;
                        int h = rect.Bottom - rect.Top;
                        
                        // Fallback caso a janela esteja minimizada
                        if (w <= 0 || h <= 0) 
                        {
                            w = GetSystemMetrics(0);
                            h = GetSystemMetrics(1);
                            rect.Left = 0;
                            rect.Top = 0;
                        }

                        int px = rect.Left + (int)(w * a.x_pct / 100.0);
                        int py = rect.Top + (int)(h * a.y_pct / 100.0);
                        
                        ClicarNaTela(px, py);
                        Console.WriteLine($"     🖱️  ({px},{py}) [pct: {a.x_pct:F1}%, {a.y_pct:F1}%]");
                    }
                    else
                    {
                        Warn("Clique sem coordenadas válidas.");
                    }
                    break;

                case "wait":
                    Thread.Sleep(2000);
                    break;

                case "done":
                case "error":
                    break;

                default:
                    Warn($"Ação desconhecida: {a.acao}");
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENCONTRAR JANELA DO PROMOB (FlaUI)
        // ════════════════════════════════════════════════════════════════════
        static Window? AguardarJanelaPromob(UIA3Automation automation, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var promobProc = System.Diagnostics.Process.GetProcesses()
                        .FirstOrDefault(p => p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                           !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase));

                    var desktop = automation.GetDesktop();
                    var janelas = desktop.FindAllChildren(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                    foreach (var j in janelas)
                    {
                        if (promobProc != null)
                        {
                            if (j.Properties.ProcessId.ValueOrDefault == promobProc.Id)
                            {
                                var name = j.Name ?? "";
                                if (EhJanelaPromob(name))
                                    return j.AsWindow();
                            }
                        }
                        else
                        {
                            var name = j.Name ?? "";
                            if (EhJanelaPromob(name))
                                return j.AsWindow();
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }

            return null;
        }

        static bool EhJanelaPromob(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return false;
            if (nome.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase)) return false;

            return nome.Contains("- Promob Studio", StringComparison.OrdinalIgnoreCase) ||
                   nome.Contains("Promob Studio Bartz", StringComparison.OrdinalIgnoreCase);
        }

        static void AtivarJanela(Window janela)
        {
            try
            {
                IntPtr hwnd = janela.Properties.NativeWindowHandle.ValueOrDefault;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_MAXIMIZE);
                    SetForegroundWindow(hwnd);
                }
                
                // Fallback FlaUI
                janela.SetForeground();
                janela.Focus();
            }
            catch { }

            Thread.Sleep(1000); // Dar tempo para a janela aparecer na tela
        }

        // ════════════════════════════════════════════════════════════════════
        //  SCREENSHOT
        // ════════════════════════════════════════════════════════════════════
        static string TirarScreenshot(IntPtr hwnd)
        {
            GetWindowRect(hwnd, out RECT rect);
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;

            if (w <= 0 || h <= 0)
            {
                w = GetSystemMetrics(0);
                h = GetSystemMetrics(1);
                rect.Left = 0;
                rect.Top = 0;
            }

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

            var reduzida = Redimensionar(bmp, 1280, 720);
            using var ms = new MemoryStream();
            reduzida.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        static void SalvarScreenshotDebug(string nome)
        {
            try
            {
                int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
                using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(0, 0, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

                string path = Path.Combine(PastaDebug, $"{nome}_{DateTime.Now:HHmmss}.png");
                bmp.Save(path, ImageFormat.Png);
                Info($"Screenshot salvo: {path}");
            }
            catch { }
        }

        static Bitmap Redimensionar(Bitmap src, int maxW, int maxH)
        {
            double r = Math.Min((double)maxW / src.Width, (double)maxH / src.Height);
            if (r >= 1.0) return new Bitmap(src);
            int nw = (int)(src.Width * r), nh = (int)(src.Height * r);
            var dst = new Bitmap(nw, nh);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, nw, nh);
            return dst;
        }

        // ════════════════════════════════════════════════════════════════════
        //  UTILITÁRIOS
        // ════════════════════════════════════════════════════════════════════
        static void ClicarNaTela(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(200);
            mouse_event(MOUSE_DOWN, 0, 0, 0, 0);
            Thread.Sleep(100);
            mouse_event(MOUSE_UP, 0, 0, 0, 0);
        }

        static void CarregarEnv()
        {
            string[] candidatos = { ".env", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env") };
            foreach (var path in candidatos)
            {
                if (!File.Exists(path)) continue;
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2 && parts[0].Trim() == "GEMINI_API_KEY")
                        API_KEY = parts[1].Trim().Trim('"').Trim('\'');
                }
                break;
            }
        }

        static void Banner(string msg) { Console.WriteLine($"\n{'═',0}{'═',50}\n  {msg}\n{'═',0}{'═',50}"); }
        static void Info(string msg)   { Console.WriteLine($"[INFO] {msg}"); }
        static void Ok(string msg)     { Console.WriteLine($"[✅  ] {msg}"); }
        static void Erro(string msg)   { Console.WriteLine($"[❌  ] {msg}"); }
        static void Warn(string msg)   { Console.WriteLine($"[⚠️  ] {msg}"); }
    }
}