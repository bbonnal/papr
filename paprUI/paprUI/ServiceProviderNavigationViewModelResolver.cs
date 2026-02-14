using System;
using Microsoft.Extensions.DependencyInjection;
using rUI.Avalonia.Desktop;

namespace paprUI;

/// <summary>
/// Resolves navigation ViewModels from the app DI container.
/// </summary>
public sealed class ServiceProviderNavigationViewModelResolver(IServiceProvider serviceProvider) : INavigationViewModelResolver
{
    public object Resolve(Type viewModelType)
        => serviceProvider.GetService(viewModelType)
           ?? throw new InvalidOperationException($"ViewModel type '{viewModelType.FullName}' is not registered in DI.");
}
