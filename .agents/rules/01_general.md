---
trigger: always_on
---

# Reglas de Arquitectura del Proyecto: Cliente de Base de Datos

- Lenguaje y Framework: C# (WPF o Avalonia UI), .NET 8+.
- Patrón de Diseño: MVVM (Model-View-ViewModel) e Inyección de Dependencias.
- Arquitectura de Plugins: El núcleo de la aplicación NO DEBE tener referencias directas a librerías específicas de bases de datos (como MySqlConnector).
- Contrato Obligatorio: Toda interacción con la base de datos debe realizarse a través de la interfaz `IDatabasePlugin`.
- Esta interfaz debe definir métodos para: Connect(), Disconnect(), GetDatabases(), GetTables(string databaseName), GetColumns(string tableName), y ExecuteQuery(string query).
- Las implementaciones concretas (ej. `MySqlPlugin`) deben aislarse en sus propias clases o proyectos de biblioteca.
- UI: La interfaz principal debe tener un panel lateral (TreeView) para exploración de objetos y un panel central/derecho con pestañas (TabControl) para resultados y edición SQL.
- Los controles tipo combobox deben permitir escribir para filtrar de forma rapida los resultados.
