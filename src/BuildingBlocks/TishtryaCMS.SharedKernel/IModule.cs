using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace TishtryaCMS.SharedKernel;

/// <summary>
/// Contract for registering a modular monolith feature module.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
