using Android.App;
using Android.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Android.Startup;
using Remotely.Shared.Services;

namespace Remotely.Desktop.Android;

[Application]
public class MainApplication : Application
{
    private IServiceProvider? _serviceProvider;

    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) { }

    public static IServiceProvider? Services { get; private set; }

    public override void OnCreate()
    {
        base.OnCreate();

        var services = new ServiceCollection();

        services.AddRemoteControlAndroid(this);

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;
    }
}
