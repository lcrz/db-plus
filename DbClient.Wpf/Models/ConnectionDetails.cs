using System;

namespace DbClient.Wpf.Models
{
    /// <summary>
    /// Modelo que representa los detalles de configuración de una conexión a base de datos.
    /// </summary>
    public class ConnectionDetails
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Server { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "3306";
        public string Username { get; set; } = "root";
        public string Password { get; set; } = "";
        public string DatabaseName { get; set; } = "";

        /// <summary>
        /// Crea una copia de este perfil de conexión.
        /// </summary>
        public ConnectionDetails Clone()
        {
            return new ConnectionDetails
            {
                Id = this.Id,
                Name = this.Name,
                Server = this.Server,
                Port = this.Port,
                Username = this.Username,
                Password = this.Password,
                DatabaseName = this.DatabaseName
            };
        }
    }
}
