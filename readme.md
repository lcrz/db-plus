# DbClient - DB+

Un cliente de base de datos moderno, ligero y altamente extensible desarrollado en **C# (.NET 10.0 windows)** utilizando **WPF** y siguiendo el patrón de diseño **MVVM (Model-View-ViewModel)**.

Este cliente de base de datos destaca por su arquitectura desacoplada basada en plugins, lo que permite integrar soporte para nuevos motores de bases de datos de forma modular y sin alterar el núcleo de la aplicación.

---

## Características Clave

- **Arquitectura Basada en Plugins:** El núcleo de la aplicación no depende de ninguna biblioteca específica de bases de datos (como `MySqlConnector`). Todo motor de base de datos se comunica mediante la interfaz `IDatabasePlugin`.
- **Interfaz Moderna y Oscura:** UI estilizada y optimizada para el flujo de trabajo de desarrolladores.
- **Explorador Jerárquico (Sidebar):** Visualización mediante un `TreeView` para navegar rápidamente por bases de datos, tablas, columnas e índices.
- **Editor SQL Inteligente:** Editor con pestañas (`TabControl`) que soporta autocompletado y previsualización de sentencias SQL.
- **Filtros Rápidos en Grilla:** Controles combobox y campos de texto con filtrado ágil para buscar registros sobre la marcha.
- **Diagramas Entidad-Relación (ER):** Generación interactiva y visualización de esquemas de bases de datos.

---

## Arquitectura del Proyecto

El proyecto está dividido en tres capas principales:

1. **DbClient.Core:** Biblioteca de clases que define los contratos y abstracciones principales, incluyendo la interfaz fundamental `IDatabasePlugin` y los modelos del esquema de base de datos (`TableSchema`, `ColumnSchema`, etc.).
2. **DbClient.Plugins.MySql:** Implementación concreta del plugin para bases de datos MySQL/MariaDB, aislada de la interfaz gráfica y del core de la aplicación.
3. **DbClient.Wpf:** Aplicación de interfaz gráfica desarrollada en WPF, encargada de la renderización del explorador, editor de consultas, diagramas ER y la grilla de datos.

---

## Requisitos

- **SDK de .NET 10.0** (o posterior)
- **Windows OS** (Debido a la dependencia nativa de WPF)

---

## Ejecución y Despliegue

### Ejecutar la aplicación en desarrollo

Para compilar y arrancar la aplicación de manera local:

```bash
dotnet run --project DbClient.Wpf
```

### Publicar la aplicación en un ejecutable único (Single File)

Para generar un ejecutable autocontenido y optimizado listo para producción (`Release`) en Windows x64:

```bash
dotnet publish DbClient.Wpf -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

> **Ubicación de la publicación:**  
> `DbClient.Wpf/bin/Release/net10.0-windows/win-x64/publish/`

---

## Cómo Agregar un Nuevo Plugin de Base de Datos

Para añadir soporte para otro motor de base de datos (por ejemplo, PostgreSQL o SQLite):

1. Crea un nuevo proyecto de biblioteca de clases (ej. `DbClient.Plugins.PostgreSql`).
2. Agrega una referencia al proyecto `DbClient.Core`.
3. Implementa la interfaz `IDatabasePlugin`:
   ```csharp
   public class PostgreSqlPlugin : IDatabasePlugin
   {
       public bool Connect(string connectionString) { ... }
       public void Disconnect() { ... }
       public List<string> GetDatabases() { ... }
       public List<TableSchema> GetTables(string databaseName) { ... }
       public QueryResult ExecuteQuery(string query) { ... }
       // ... implementar los demás métodos requeridos
   }
   ```
4. Compila y coloca el ensamblado resultante `.dll` en el directorio de plugins de la aplicación.
