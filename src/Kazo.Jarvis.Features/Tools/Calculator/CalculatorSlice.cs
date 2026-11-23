using Kazo.Jarvis.Kernel.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kazo.Jarvis.Features.Tools.Calculator;

/// <summary>Registration of the tool <c>calculate</c> (ADR 0017, section 3).</summary>
public static class CalculatorSlice
{
    /// <summary>Adds the tool to the registry of the kernel.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCalculatorToolSlice(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITool, CalculatorTool>());
        return services;
    }
}
