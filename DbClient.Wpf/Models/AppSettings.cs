namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Modelo que representa la configuración general de la aplicación.
    /// </summary>
    public class AppSettings
    {
        private string _ollamaEndpointUrl;
        private string _ollamaModelName;
        private string _ollamaSystemPrompt;
        private string _aiProvider;
        private string _antigravityCliPath;

        public string AiProvider
        {
            get => string.IsNullOrEmpty(_aiProvider) ? "Ollama" : _aiProvider;
            set => _aiProvider = value;
        }

        public string AntigravityCliPath
        {
            get => string.IsNullOrEmpty(_antigravityCliPath)
                ? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "agy", "bin", "agy.exe")
                : _antigravityCliPath;
            set => _antigravityCliPath = value;
        }

        /// <summary>
        /// Ruta de la carpeta seleccionada por el usuario para guardar las conexiones.
        /// </summary>
        public string ConnectionsStorageFolder { get; set; }

        public string OllamaEndpointUrl
        {
            get => string.IsNullOrEmpty(_ollamaEndpointUrl) ? "http://localhost:11434/api/generate" : _ollamaEndpointUrl;
            set => _ollamaEndpointUrl = value;
        }

        public string OllamaModelName
        {
            get => string.IsNullOrEmpty(_ollamaModelName) ? "llama3" : _ollamaModelName;
            set => _ollamaModelName = value;
        }

        public string OllamaSystemPrompt
        {
            get => string.IsNullOrEmpty(_ollamaSystemPrompt)
                ? "Eres un asistente experto en bases de datos MySQL. Tu única tarea es traducir las solicitudes en lenguaje natural del usuario a una consulta SQL válida (únicamente sentencias SELECT) basándose en el esquema de tablas proporcionado.\nIMPORTANTE: El usuario te pedirá cosas como 'Muéstrame las facturas' o 'Busca los usuarios'. No intentes acceder a ninguna base de datos real ni respondas indicando que no puedes acceder. Tu única labor es escribir la consulta SQL SELECT correspondiente que la aplicación cliente ejecutará.\nREGLA DE SEGURIDAD ABSOLUTA: Solo puedes generar sentencias SELECT. Tienes estrictamente prohibido generar sentencias UPDATE, INSERT, DELETE, DROP, TRUNCATE, ALTER o cualquier otra instrucción que modifique datos o estructuras. Si el usuario pide modificar datos, debes responder con un comentario SQL indicando que no tienes los permisos para esa acción.\nREGLA DE NOMBRES EXACTOS: Debes utilizar únicamente los nombres EXACTOS de las tablas y columnas provistos en el esquema. No asumas ni inventes nombres. Si en el esquema una tabla es 'factura' en singular o 'cliente' en singular, debes escribirla exactamente en singular en el SQL, sin pluralizarla a 'facturas' o 'clientes' (o viceversa).\nREGLA DE SINTAXIS CRÍTICA: Al usar múltiples JOINs, asegúrate de que el orden de las tablas unidas sea lógico. Nunca hagas referencia al alias de una tabla en la cláusula ON de un JOIN si esa tabla aún no ha sido declarada en el FROM o en un JOIN anterior. Por ejemplo, en lugar de 'FROM a JOIN b ON a.id = c.a_id JOIN c ON ...', escribe 'FROM a JOIN c ON a.id = c.a_id JOIN b ON ...' (declarando c antes de usar su alias).\nDevuelve únicamente el código SQL limpio, sin bloques de código Markdown (```sql), sin explicaciones adicionales ni saludos."
                : _ollamaSystemPrompt;
            set => _ollamaSystemPrompt = value;
        }

        private string _jsonCopyMode;
        public string JsonCopyMode
        {
            get => string.IsNullOrEmpty(_jsonCopyMode) ? "KeyOnly" : _jsonCopyMode;
            set => _jsonCopyMode = value;
        }
    }
}
