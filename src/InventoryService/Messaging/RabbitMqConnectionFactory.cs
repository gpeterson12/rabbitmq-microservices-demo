using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Messaging;

namespace InventoryService.Messaging;

public interface IRabbitMqConnectionFactory
{
    Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionFactory> logger)
    : IRabbitMqConnectionFactory
{
    private const int InitialBackoffMilliseconds = 500;
    private const int MaxBackoffMilliseconds = 30_000;

    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqConnectionFactory> _logger = logger;

    public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };

        var backoffMilliseconds = InitialBackoffMilliseconds;
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                _logger.LogInformation(
                    "Connecting to RabbitMQ at {HostName}:{Port}, virtual host '{VirtualHost}' (attempt {Attempt})",
                    _options.HostName, _options.Port, _options.VirtualHost, attempt);

                var connection = await factory.CreateConnectionAsync(cancellationToken);

                _logger.LogInformation(
                    "Connected to RabbitMQ at {HostName}:{Port} after {Attempt} attempt(s)",
                    _options.HostName, _options.Port, attempt);

                return connection;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                backoffMilliseconds = Math.Min(backoffMilliseconds * 2, MaxBackoffMilliseconds);
                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt} failed, retrying in {BackoffMilliseconds} ms",
                    attempt, backoffMilliseconds);

                await Task.Delay(backoffMilliseconds, cancellationToken);
            }
        }
    }
}
