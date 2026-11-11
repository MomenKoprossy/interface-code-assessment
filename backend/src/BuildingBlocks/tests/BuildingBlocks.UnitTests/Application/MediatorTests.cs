using AwesomeAssertions;
using BuildingBlocks.Application;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.UnitTests.Application;

public class MediatorTests
{
    public sealed record Ping(string Value) : IQuery<string>;

    private sealed class PingHandler(List<string> log) : IRequestHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping request, CancellationToken cancellationToken)
        {
            log.Add("handler");
            return ValueTask.FromResult($"pong:{request.Value}");
        }
    }

    private sealed class Recording(List<string> log, string name) : IPipelineBehavior<Ping, string>
    {
        public async ValueTask<string> Handle(Ping request, MessageHandlerDelegate<Ping, string> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            var response = await next(request, cancellationToken);
            log.Add($"{name}:after");
            return response;
        }
    }

    private readonly List<string> _log = [];

    private ISender CreateSender(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<Ping, string>>(new PingHandler(_log));
        configure?.Invoke(services);
        return new Mediator(services.BuildServiceProvider());
    }

    [Fact]
    public async Task Send_routes_to_handler_inside_behaviors_first_registered_outermost()
    {
        var sender = CreateSender(s =>
        {
            s.AddSingleton<IPipelineBehavior<Ping, string>>(new Recording(_log, "outer"));
            s.AddSingleton<IPipelineBehavior<Ping, string>>(new Recording(_log, "inner"));
        });

        var response = await sender.Send(new Ping("x"), TestContext.Current.CancellationToken);

        response.Should().Be("pong:x");
        _log.Should().Equal("outer:before", "inner:before", "handler", "inner:after", "outer:after");
    }

    [Fact]
    public async Task ValidationBehavior_throws_all_failures_and_skips_handler()
    {
        var sender = CreateSender(s =>
        {
            s.AddSingleton<IValidator<Ping>>(new InlineValidator<Ping> { v => v.RuleFor(x => x.Value).NotEmpty() });
            s.AddSingleton<IValidator<Ping>>(new InlineValidator<Ping> { v => v.RuleFor(x => x.Value).MinimumLength(3) });
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        var act = async () => await sender.Send(new Ping(""), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().HaveCount(2);
        _log.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidationBehavior_passes_valid_request_to_handler()
    {
        var sender = CreateSender(s =>
        {
            s.AddSingleton<IValidator<Ping>>(new InlineValidator<Ping> { v => v.RuleFor(x => x.Value).NotEmpty() });
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        var response = await sender.Send(new Ping("ok"), TestContext.Current.CancellationToken);

        response.Should().Be("pong:ok");
    }

    [Fact]
    public void AddPlatformMediator_registers_logging_then_validation_and_no_exception_logging_behavior()
    {
        var services = new ServiceCollection().AddPlatformMediator(typeof(MediatorTests).Assembly);

        services.Where(d => d.ServiceType == typeof(IPipelineBehavior<,>)).Select(d => d.ImplementationType)
            .Should().Equal(typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>));
    }
}
