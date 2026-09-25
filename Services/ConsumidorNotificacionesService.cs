using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Mensajes;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Services;

public sealed class ConsumidorNotificacionesService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsumidorNotificacionesService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _connectionString;
    private readonly string _queueName;

    public ConsumidorNotificacionesService(
        IConfiguration configuration,
        ILogger<ConsumidorNotificacionesService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _connectionString = _configuration["RabbitMq:ConnectionString"] ?? string.Empty;
        _queueName = _configuration["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";
    }

    private bool EstaHabilitado
        => bool.TryParse(_configuration["RabbitMq:ConsumerEnabled"], out var habilitado) && habilitado;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!EstaHabilitado)
        {
            _logger.LogInformation("Consumidor de notificaciones DESACTIVADO (RabbitMq:ConsumerEnabled=false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogInformation("Consumidor de notificaciones sin conexion configurada (RabbitMq:ConnectionString vacio).");
            return;
        }

        _logger.LogInformation("Consumidor de notificaciones ACTIVADO, escuchando la cola {Queue}.", _queueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirEnBucleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el consumidor de notificaciones; se reintenta la conexion en 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirEnBucleAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_connectionString),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        using var conexion = factory.CreateConnection();
        using var canal = conexion.CreateModel();

        canal.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        canal.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var contador = new ConsumidorSolicitudMensaje(canal, _scopeFactory, _logger);
        canal.BasicConsume(queue: _queueName, autoAck: false, consumer: contador);

        _logger.LogInformation("Suscripcion a la cola {Queue} establecida.", _queueName);

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        conexion.ConnectionShutdown += (_, _) => tcs.TrySetResult(null);

        await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, stoppingToken));
    }

    private sealed class ConsumidorSolicitudMensaje : EventingBasicConsumer
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;

        public ConsumidorSolicitudMensaje(IModel model, IServiceScopeFactory scopeFactory, ILogger logger)
            : base(model)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            Received += ProcesarMensaje;
        }

        private void ProcesarMensaje(object? sender, BasicDeliverEventArgs e)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                var evento = JsonSerializer.Deserialize<SolicitudRegistradaEvent>(
                    e.Body.Span, JsonOptions);

                if (evento is null || string.IsNullOrWhiteSpace(evento.UsuarioId))
                {
                    _logger.LogError("Mensaje {DeliveryTag} con cuerpo invalido; se descarta sin requeue.", e.DeliveryTag);
                    Model.BasicNack(e.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                var yaExiste = db.Notificaciones.Any(n => n.MessageId == evento.MessageId);
                if (yaExiste)
                {
                    _logger.LogInformation(
                        "Mensaje {MessageId} ya procesado (idempotencia); ACK sin reinsertar.",
                        evento.MessageId);
                    Model.BasicAck(e.DeliveryTag, multiple: false);
                    return;
                }

                var notificacion = new Notificacion
                {
                    UsuarioId = evento.UsuarioId,
                    MessageId = evento.MessageId,
                    Tipo = evento.Tipo,
                    SolicitudId = evento.SolicitudId,
                    Contenido = $"Tu solicitud N° {evento.SolicitudId} fue registrada en estado Pendiente.",
                    FechaEventoUtc = evento.FechaEventoUtc,
                    FechaRegistroUtc = DateTime.UtcNow
                };

                db.Notificaciones.Add(notificacion);

                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Mensaje {MessageId} rechazado por el indice unico (posible duplicado concurrente); se trata como procesado y se ACK.",
                        evento.MessageId);
                    Model.BasicAck(e.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogInformation(
                    "Notificacion #{NotificacionId} persistida para {UsuarioId} (MessageId={MessageId}); ACK.",
                    notificacion.Id, evento.UsuarioId, evento.MessageId);

                Model.BasicAck(e.DeliveryTag, multiple: false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Mensaje {DeliveryTag} no es JSON valido; se descarta sin requeue.", e.DeliveryTag);
                Model.BasicNack(e.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fallo el guardado de la notificacion del mensaje {DeliveryTag}; NACK con requeue para reintentar.",
                    e.DeliveryTag);
                Model.BasicNack(e.DeliveryTag, multiple: false, requeue: true);
            }
        }
    }
}