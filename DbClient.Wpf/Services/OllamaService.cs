using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DbClient.Wpf.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ConnectionStorageService _connectionStorage;

        public OllamaService(ConnectionStorageService connectionStorage)
        {
            _connectionStorage = connectionStorage ?? throw new ArgumentNullException(nameof(connectionStorage));
            _httpClient = new HttpClient();
            // Timeout razonable para generación
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        public async Task<AiResponse> GenerateSqlAsync(string prompt, string schemaContext)
        {
            var settings = _connectionStorage.CurrentSettings;
            var systemPrompt = settings.OllamaSystemPrompt;

            var fullPrompt = $"Esquema de la Base de Datos:\n{schemaContext}\n\nSolicitud del usuario:\n{prompt}";

            // Asegurar que siempre se incluya la regla de nombres exactos para evitar alucinaciones de pluralización/singularización
            var finalSystemPrompt = systemPrompt;
            if (!finalSystemPrompt.Contains("REGLA DE NOMBRES EXACTOS") && !finalSystemPrompt.Contains("exact table and column names"))
            {
                finalSystemPrompt += "\n\nREGLA DE NOMBRES EXACTOS: Debes utilizar únicamente los nombres EXACTOS de las tablas y columnas provistos en el esquema. No asumas ni inventes nombres. Si en el esquema una tabla es 'factura' en singular o 'cliente' en singular, debes escribirla exactamente en singular en el SQL, sin pluralizarla a 'facturas' o 'clientes' (o viceversa).";
            }

            var apiResponse = await GenerateResponseAsync(finalSystemPrompt, fullPrompt);

            var sql = apiResponse.Text?.Trim();
            if (string.IsNullOrEmpty(sql))
            {
                apiResponse.Text = string.Empty;
                return apiResponse;
            }

            // Extraer el bloque de código SQL de markdown si existe
            var match = Regex.Match(sql, @"```sql\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                apiResponse.Text = match.Groups[1].Value.Trim();
                return apiResponse;
            }

            match = Regex.Match(sql, @"```\s*(.*?)\s*```", RegexOptions.Singleline);
            if (match.Success)
            {
                apiResponse.Text = match.Groups[1].Value.Trim();
                return apiResponse;
            }

            return apiResponse;
        }

        public async Task<AiResponse> GenerateResponseAsync(string systemPrompt, string userPrompt)
        {
            var settings = _connectionStorage.CurrentSettings;
            var endpointUrl = settings.OllamaEndpointUrl;
            var modelName = settings.OllamaModelName;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string logPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ai_prompt_log.txt");
                var providerInfo = settings.AiProvider == "AntigravityCli" ? "Antigravity CLI" : $"Ollama (Model: {modelName})";
                var logContent = $"=== FECHA: {DateTime.Now} ===\n\n=== PROVIDER: {providerInfo} ===\n\n=== SYSTEM PROMPT ===\n{systemPrompt}\n\n=== USER PROMPT ===\n{userPrompt}\n\n========================================================================\n\n";
                System.IO.File.AppendAllText(logPath, logContent, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al escribir log de prompt: {ex.Message}");
            }

            string responseContent = string.Empty;
            int? inputTokens = null;
            int? outputTokens = null;

            if (settings.AiProvider == "AntigravityCli")
            {
                var cliPath = settings.AntigravityCliPath;
                if (string.IsNullOrEmpty(cliPath))
                {
                    cliPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "agy", "bin", "agy.exe");
                }

                if (!System.IO.File.Exists(cliPath))
                {
                    throw new System.IO.FileNotFoundException($"No se encontró el ejecutable de Antigravity CLI en la ruta especificada: {cliPath}");
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cliPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--print");
                startInfo.ArgumentList.Add("--dangerously-skip-permissions");
                startInfo.ArgumentList.Add($"[SYSTEM INSTRUCTIONS]\n{systemPrompt}\n\n{userPrompt}");

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    throw new Exception("No se pudo iniciar el proceso de Antigravity CLI.");
                }

                // Iniciar la lectura asíncrona de los flujos para evitar interbloqueos en el buffer del sistema operativo
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(90000));
                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("La ejecución de Antigravity CLI superó el límite de 90 segundos. Por favor, asegúrate de estar autenticado ejecutando 'agy' en tu terminal.");
                }

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (process.ExitCode != 0 || (!string.IsNullOrWhiteSpace(stderr) && string.IsNullOrWhiteSpace(stdout)))
                {
                    var errMsg = string.IsNullOrWhiteSpace(stderr) ? "El CLI retornó una salida vacía con código de error." : stderr;
                    throw new Exception($"Antigravity CLI error: {errMsg}");
                }

                responseContent = stdout;
            }
            else
            {
                var payload = new
                {
                    model = modelName,
                    prompt = userPrompt,
                    system = systemPrompt,
                    stream = false,
                    options = new
                    {
                        num_ctx = 16384,
                        temperature = 0.0,
                        top_k = 1,
                        top_p = 0.1
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpointUrl, content);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    responseContent = responseElement.GetString();
                }

                if (doc.RootElement.TryGetProperty("prompt_eval_count", out var promptEvalCountElement))
                {
                    inputTokens = promptEvalCountElement.GetInt32();
                }
                if (doc.RootElement.TryGetProperty("eval_count", out var evalCountElement))
                {
                    outputTokens = evalCountElement.GetInt32();
                }
            }

            stopwatch.Stop();
            double durationSeconds = stopwatch.Elapsed.TotalSeconds;

            return new AiResponse
            {
                Text = responseContent,
                DurationSeconds = durationSeconds,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };
        }
    }
}
