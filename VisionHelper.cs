using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace AutomacaoPromobTeste{
    /// <summary>
    /// Helper de visão para a automação:
    /// - captura screenshot
    /// - consulta modelo multimodal
    /// - decide se a tela está pronta
    /// - pode localizar elementos por coordenadas
    /// </summary>
    static class VisionHelper{
        // ────────────────────────────────────────────────────────────────────
        // Configuração
        const string OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions";
        const string MODELO = "google/gemini-2.0-flash-001";

        const int MAX_TENTATIVAS_API = 3;
        const int TIMEOUT_HTTP_SEGUNDOS = 30;

        // Limites de imagem: menor payload = menos latência/custo
        const int MAX_IMG_W = 1024;
        const int MAX_IMG_H = 576;
        const long JPEG_QUALITY = 70L;

        // Evita spam de capturas idênticas muito rápidas
        static readonly object Sync = new();

        static string _apiKey = "";
        static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(TIMEOUT_HTTP_SEGUNDOS)
        };

        // Cache simples da última captura para reaproveitar em loops muito próximos
        static DateTime _ultimaCapturaTs = DateTime.MinValue;
        static string? _ultimaCapturaBase64;
        static Rectangle? _ultimaAreaCaptura;

        // ────────────────────────────────────────────────────────────────────
        // Win32
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int n);

        // ────────────────────────────────────────────────────────────────────
        // Inicialização
        public static void Inicializar()
        {
            _apiKey = CarregarApiKey();

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Console.WriteLine("[VISION] ⚠️ API key não encontrada. Vision desabilitado.");
            }
            else
            {
                Console.WriteLine("[VISION] ✅ API key carregada. Vision habilitado.");
            }
        }

        public static bool Habilitado => !string.IsNullOrWhiteSpace(_apiKey);

        // ────────────────────────────────────────────────────────────────────
        // API principal
        public static bool AguardarEstadoTela(
            string descricao,
            int maxTentativas = 10,
            int intervaloMs = 1200,
            int fallbackMs = 1500,
            Rectangle? area = null)
        {
            var res = EsperarIA(descricao, maxTentativas, intervaloMs, fallbackMs, area);
            return res.Pronto;
        }

        public static Point? LocalizarElemento(
            string elemento,
            int maxTentativas = 4,
            int intervaloMs = 1200,
            Rectangle? area = null)
        {
            if (!Habilitado)
                return null;

            string prompt =
                $"Localize o centro de: {elemento}. " +
                "Retorne coordenadas x e y relativas à imagem, em escala de 0 a 1000.";

            for (int i = 1; i <= maxTentativas; i++)
            {
                if (i > 1)
                    Thread.Sleep(intervaloMs);

                try
                {
                    string b64 = TirarScreenshot(area);
                    var res = PerguntarIA(b64, prompt, isDiscovery: true);

                    if (res.X.HasValue && res.Y.HasValue)
                    {
                        Rectangle bounds = area ?? BoundsTelaInteira();

                        int realX = bounds.Left + (res.X.Value * bounds.Width / 1000);
                        int realY = bounds.Top + (res.Y.Value * bounds.Height / 1000);

                        Console.WriteLine($"  [VISION] Elemento '{elemento}' localizado em: {realX}, {realY}");
                        return new Point(realX, realY);
                    }

                    Console.WriteLine($"  [VISION] Tentativa {i}/{maxTentativas}: elemento não localizado.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [VISION] Tentativa {i}/{maxTentativas}: erro ao localizar elemento: {ex.Message}");
                }
            }

            return null;
        }

        // ────────────────────────────────────────────────────────────────────
        // Núcleo de espera
        static RespostaVisao EsperarIA(
            string descricao,
            int maxTentativas,
            int intervaloMs,
            int fallbackMs,
            Rectangle? area)
        {
            if (!Habilitado)
            {
                Console.WriteLine($"  [VISION] (desabilitado) aguardando fallback de {fallbackMs}ms.");
                Thread.Sleep(fallbackMs);

                // Mais seguro: não afirmar que está pronto sem verificação real
                return new RespostaVisao
                {
                    Pronto = false,
                    Motivo = "Vision desabilitado; apenas fallback por tempo aplicado"
                };
            }

            Console.WriteLine($"  [VISION] 👁️ Aguardando: \"{descricao}\"");

            for (int i = 1; i <= maxTentativas; i++)
            {
                if (i > 1)
                    Thread.Sleep(intervaloMs);

                try
                {
                    string b64 = TirarScreenshot(area);
                    var resultado = PerguntarIA(b64, descricao);

                    Console.WriteLine(
                        $"  [VISION] Tentativa {i}/{maxTentativas}: " +
                        $"{(resultado.Pronto ? "✅ PRONTO" : "⏳ aguardando")} — {resultado.Motivo}");

                    if (resultado.Pronto)
                        return resultado;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [VISION] ⚠️ Erro na tentativa {i}: {ex.Message}");
                }
            }

            Console.WriteLine($"  [VISION] ❌ Timeout aguardando: \"{descricao}\"");
            return new RespostaVisao
            {
                Pronto = false,
                Motivo = "Timeout da análise visual"
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // Consulta ao modelo
        static RespostaVisao PerguntarIA(
            string screenshotBase64,
            string descricaoEsperada,
            bool isDiscovery = false)
        {
            string prompt =
                "Você é um assistente de automação que analisa screenshots.\n\n" +
                $"TAREFA: {descricaoEsperada}\n\n" +
                "Responda APENAS com JSON válido, sem markdown e sem texto extra.\n" +
                (isDiscovery
                    ? "- x: inteiro de 0 a 1000\n" +
                      "- y: inteiro de 0 a 1000\n" +
                      "- motivo: descrição breve em português\n" +
                      "- se não encontrar o elemento, omita x e y\n"
                    : "- pronto: true se a condição está satisfeita; false caso contrário\n" +
                      "- motivo: descrição breve em português\n");

            var requestBody = new
            {
                model = MODELO,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{screenshotBase64}"
                                }
                            }
                        }
                    }
                },
                temperature = 0.0,
                max_tokens = 160
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);

            for (int t = 1; t <= MAX_TENTATIVAS_API; t++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, OPENROUTER_URL);
                    request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                    request.Headers.Add("HTTP-Referer", "https://github.com/automacao-promob");
                    request.Headers.Add("X-Title", "Automacao Promob Vision");
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    using var response = Http.SendAsync(request).GetAwaiter().GetResult();
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if ((int)response.StatusCode == 429)
                    {
                        int espera = 5000 * t;
                        Console.WriteLine($"  [VISION] ⚠️ Rate limit (429). Aguardando {espera}ms...");
                        Thread.Sleep(espera);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string resumo = responseBody.Length > 180
                            ? responseBody[..180]
                            : responseBody;

                        Console.WriteLine($"  [VISION] ⚠️ API erro {response.StatusCode}: {resumo}");

                        if (t < MAX_TENTATIVAS_API)
                        {
                            Thread.Sleep(1500 * t);
                            continue;
                        }

                        return new RespostaVisao
                        {
                            Pronto = false,
                            Motivo = $"API indisponível ({response.StatusCode})"
                        };
                    }

                    string? content = ExtrairContent(responseBody);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return new RespostaVisao
                        {
                            Pronto = false,
                            Motivo = "Resposta vazia da API"
                        };
                    }

                    string json = ExtrairJson(content);

                    var resultado = JsonSerializer.Deserialize<RespostaVisao>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    return resultado ?? new RespostaVisao
                    {
                        Pronto = false,
                        Motivo = "Falha ao desserializar JSON"
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [VISION] ⚠️ Tentativa {t}/{MAX_TENTATIVAS_API}: {ex.Message}");

                    if (t < MAX_TENTATIVAS_API)
                        Thread.Sleep(1500 * t);
                }
            }

            return new RespostaVisao
            {
                Pronto = false,
                Motivo = "Todas as tentativas da API falharam"
            };
        }

        static string? ExtrairContent(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;

            var first = choices[0];

            if (!first.TryGetProperty("message", out var message))
                return null;

            if (!message.TryGetProperty("content", out var contentNode))
                return null;

            if (contentNode.ValueKind == JsonValueKind.String)
                return contentNode.GetString();

            // Alguns providers podem retornar array
            if (contentNode.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();

                foreach (var item in contentNode.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textNode))
                        sb.Append(textNode.GetString());
                }

                return sb.ToString();
            }

            return null;
        }

        static string ExtrairJson(string content)
        {
            content = content.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("```", "")
                             .Trim();

            int firstBrace = content.IndexOf('{');
            int lastBrace = content.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
                return content.Substring(firstBrace, lastBrace - firstBrace + 1);

            return content;
        }

        // ────────────────────────────────────────────────────────────────────
        // Screenshot
        static string TirarScreenshot(Rectangle? area = null)
        {
            lock (Sync)
            {
                var agora = DateTime.UtcNow;

                // Reaproveita captura muito recente da mesma área
                if (_ultimaCapturaBase64 != null &&
                    _ultimaAreaCaptura == area &&
                    (agora - _ultimaCapturaTs).TotalMilliseconds < 400)
                {
                    return _ultimaCapturaBase64;
                }

                Rectangle bounds = area ?? BoundsTelaInteira();

                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return "";

                using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                using var reduzida = Redimensionar(bmp, MAX_IMG_W, MAX_IMG_H);

                string base64 = SalvarComoJpegBase64(reduzida, JPEG_QUALITY);

                _ultimaCapturaTs = agora;
                _ultimaCapturaBase64 = base64;
                _ultimaAreaCaptura = area;

                return base64;
            }
        }

        static Rectangle BoundsTelaInteira()
        {
            int w = GetSystemMetrics(0);
            int h = GetSystemMetrics(1);
            return new Rectangle(0, 0, w, h);
        }

        static Bitmap Redimensionar(Bitmap src, int maxW, int maxH)
        {
            double ratio = Math.Min((double)maxW / src.Width, (double)maxH / src.Height);

            if (ratio >= 1.0)
                return new Bitmap(src);

            int nw = Math.Max(1, (int)(src.Width * ratio));
            int nh = Math.Max(1, (int)(src.Height * ratio));

            var dst = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);

            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, nw, nh);

            return dst;
        }

        static string SalvarComoJpegBase64(Bitmap bmp, long quality)
        {
            using var ms = new MemoryStream();

            var codec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (codec == null)
            {
                bmp.Save(ms, ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }

            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            bmp.Save(ms, codec, ep);

            return Convert.ToBase64String(ms.ToArray());
        }

        // ────────────────────────────────────────────────────────────────────
        // .env
        static string CarregarApiKey()
        {
            string[] candidatos =
            {
                ".env",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env")
            };

            foreach (var path in candidatos)
            {
                if (!File.Exists(path))
                    continue;

                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2 && parts[0].Trim() == "GEMINI_API_KEY")
                        return parts[1].Trim().Trim('"').Trim('\'');
                }
            }

            return "";
        }

        // ────────────────────────────────────────────────────────────────────
        // Modelo de resposta
        class RespostaVisao
        {
            [JsonPropertyName("pronto")]
            public bool Pronto { get; set; }

            [JsonPropertyName("x")]
            public int? X { get; set; }

            [JsonPropertyName("y")]
            public int? Y { get; set; }

            [JsonPropertyName("motivo")]
            public string Motivo { get; set; } = "";
        }
    }
}