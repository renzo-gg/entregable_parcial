using System.Text.Json;
using PlataformaCreditos.Mensajes;
using RabbitMQ.Client;

namespace PlataformaCreditos.Services;

public interface IProductorNotificacionSolicitud
{
    Task PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId, CancellationToken cancellationToken = default);
}

public sealed class ProductorNotificacionSolicitud : IProductorNotificacionSolicitud, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductorNotificacionSolicitud> _logger;
    private readonly object _lock = new();
    private readonly string _connectionString;
    private readonly string _queueName;

    private IConnection? _connection;
    private IModel? _channel;

    public ProductorNotificacionSolicitud(IConfiguration configuration, ILogger<ProductorNotificacionSolicitud> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = _configuration["RabbitMq:ConnectionString"] ?? string.Empty;
        _queueName = _configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";
    }

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId, CancellationToken cancellationToken = default)
    {
        if (!EstaConfigurado)
        {
            _logger.LogDebug("RabbitMQ sin configurar (RabbitMq:ConnectionString vacio); no se publica el evento.");
            return;
        }

        var evento = new SolicitudRegistradaEvent(
            Tipo: "SolicitudRegistrada",
            MessageId: Guid.NewGuid(),
            SolicitudId: solicitudId,
            UsuarioId: usuarioId,
            FechaEventoUtc: DateTime.UtcNow);

        try
        {
            var channel = ObtenerCanal();

            var propiedades = channel.CreateBasicProperties();
            propiedades.DeliveryMode = 2;
            propiedades.ContentType = "application/json";
            propiedades.Type = evento.Tipo;
            propiedades.MessageId = evento.MessageId.ToString();
            propiedades.Timestamp = new AmqpTimestamp(new DateTimeOffset(evento.FechaEventoUtc).ToUnixTimeSeconds());

            var cuerpo = JsonSerializer.SerializeToUtf8Bytes(evento, JsonOptions);

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: true,
                basicProperties: propiedades,
                body: cuerpo);

            var confirmado = await Task.Run(
                () => channel.WaitForConfirms(),
                cancellationToken);

            if (!confirmado)
            {
                throw new InvalidOperationException(
                    $"RabbitMQ no confirmo el mensaje {evento.Tipo} de la solicitud {solicitudId}.");
            }

            _logger.LogInformation(
                "Mensaje {Tipo} MessageId={MessageId} SolicitudId={SolicitudId} confirmado por RabbitMQ.",
                evento.Tipo, evento.MessageId, evento.SolicitudId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudo publicar/confirmar el mensaje {Tipo} de la solicitud {SolicitudId} en RabbitMQ.",
                "SolicitudRegistrada", solicitudId);
        }
    }

    private IModel ObtenerCanal()
    {
        lock (_lock)
        {
            if (_connection is not null && _connection.IsOpen && _channel is not null && _channel.IsOpen)
            {
                return _channel;
            }

            _channel?.Dispose();
            _connection?.Dispose();

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_connectionString),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ConfirmSelect();

            _channel.QueueDeclare(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            return _channel;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}