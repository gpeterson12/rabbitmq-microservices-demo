using OrderService;

namespace OrderService.Tests;

public class RabbitMqReadinessGateTests
{
    [Fact]
    public void Gate_starts_not_ready_and_flips_once_marked()
    {
        var gate = new RabbitMqReadinessGate();

        Assert.False(gate.IsReady);

        gate.MarkReady();

        Assert.True(gate.IsReady);
    }

    [Fact]
    public void MarkReady_is_idempotent()
    {
        var gate = new RabbitMqReadinessGate();

        gate.MarkReady();
        gate.MarkReady();

        Assert.True(gate.IsReady);
    }
}
