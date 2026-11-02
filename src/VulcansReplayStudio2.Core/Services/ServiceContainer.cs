using System;

namespace VulcansReplayStudio2.Core.Services;

public interface IServiceProvider
{
    T Get<T>();
    void Register<T>(T instance);
}

public class ServiceContainer : IServiceProvider, IDisposable
{
    private readonly System.Collections.Generic.Dictionary<Type, object> _services = new();

    public T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out var obj) && obj is T t) return t;
        throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }

    public void Register<T>(T instance)
    {
        _services[typeof(T)] = instance!;
    }

    public void Dispose()
    {
        foreach (var s in _services.Values)
        {
            if (s is IDisposable d) d.Dispose();
        }
        _services.Clear();
    }
}