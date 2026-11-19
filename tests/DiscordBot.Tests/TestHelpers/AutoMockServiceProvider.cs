using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// A service provider that answers every interface or abstract-class request with a loose Moq
/// mock, and every <see cref="ILogger{TCategoryName}"/> with a null logger. Discord.Interactions
/// checks a module's constructor dependencies when it builds the module, so this lets a test
/// build real command modules (to read their registered shape) without the app's container.
/// Concrete types are resolved from <paramref name="concreteServices"/> or not at all.
/// </summary>
public sealed class AutoMockServiceProvider : IServiceProvider, IServiceScopeFactory, IServiceScope
{
    private readonly Dictionary<Type, object> _instances = new();

    /// <summary>
    /// Initializes a new instance with optional concrete services (for example a
    /// <c>DiscordSocketClient</c>) that cannot be mocked.
    /// </summary>
    public AutoMockServiceProvider(params object[] concreteServices)
    {
        foreach (var service in concreteServices)
        {
            _instances[service.GetType()] = service;
        }
    }

    /// <inheritdoc />
    public IServiceProvider ServiceProvider => this;

    /// <inheritdoc />
    public IServiceScope CreateScope() => this;

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory))
        {
            return this;
        }

        if (_instances.TryGetValue(serviceType, out var existing))
        {
            return existing;
        }

        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var logger = Activator.CreateInstance(
                typeof(NullLogger<>).MakeGenericType(serviceType.GetGenericArguments()))!;
            _instances[serviceType] = logger;
            return logger;
        }

        if (serviceType.IsInterface || serviceType.IsAbstract)
        {
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(serviceType))!;
            _instances[serviceType] = mock.Object;
            return mock.Object;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
