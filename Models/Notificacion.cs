namespace PlataformaCreditos.Models;

public class Notificacion
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public Guid MessageId { get; set; }

    public string Tipo { get; set; } = "SolicitudRegistrada";

    public int SolicitudId { get; set; }

    public string Contenido { get; set; } = string.Empty;

    public DateTime FechaEventoUtc { get; set; }

    public DateTime FechaRegistroUtc { get; set; }
}