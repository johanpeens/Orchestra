using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using TeamPortal.Mock;

namespace TeamPortal.Web.Tests.Common;

/// <summary>
/// Implements <see cref="IAsyncLifetime"/> so xUnit tears the container down
/// asynchronously. Several MudBlazor services (KeyInterceptorService,
/// PointerEventsNoneService) implement only IAsyncDisposable, and a synchronous
/// Dispose on the provider throws rather than disposing them.
/// </summary>
public abstract class ComponentTestBase : BunitContext, IAsyncLifetime
{
    protected ComponentTestBase()
    {
        Services.AddMudServices();

        Services.AddMockData(o =>
        {
            o.ReadDelay = TimeSpan.Zero;
            o.WriteDelay = TimeSpan.Zero;
        });

        // Pages that ask whether an optional asset has been dropped into wwwroot
        // need this. A test run has no wwwroot, so the provider says nothing is
        // there - which is the state a fresh checkout is actually in.
        Services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());

        // MudBlazor calls JS interop during render - popovers, scroll listeners,
        // resize observers. Strict mode fails every one of them, so every
        // MudBlazor component test needs Loose.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

/// <summary>
/// Enough of <see cref="IWebHostEnvironment"/> for a component to ask whether a
/// file exists. Both providers are null providers: in a test there is no content
/// root and no wwwroot, and pretending otherwise would make a test pass for a
/// reason that has nothing to do with the component.
/// </summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "TeamPortal.Web.Tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string EnvironmentName { get; set; } = "Development";
}
