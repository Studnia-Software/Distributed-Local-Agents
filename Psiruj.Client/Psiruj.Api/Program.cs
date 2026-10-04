using Psiruj.Api.Abstractions;
using Psiruj.Api.Providers;
using Psiruj.Api.Services;
using Psiruj.Api.Utils;
using Psiruj.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SystemMetricsListener>();

builder.Services.AddKeyedSingleton<IGpuMetricsProvider, NvidiaGpuMetricsProvider>("nvidia");
builder.Services.AddKeyedSingleton<IGpuMetricsProvider, WindowsGenericMetricsProvider>("windows_generic");
builder.Services.AddKeyedSingleton<IGpuMetricsProvider, AmdLinuxGpuMetricsProvider>("amd_linux");
builder.Services.AddKeyedSingleton<IGpuMetricsProvider, IntelLinuxGpuMetricsProvider>("intel_linux");
builder.Services.AddKeyedSingleton<IGpuMetricsProvider, NullGpuMetricsProvider>("null");

builder.Services.AddSingleton<IGpuMetricsProvider>(sp =>
{
    var gpuVendor = GpuDetector.DetectGpu();
    var isLinux = OperatingSystem.IsLinux();
    var isWindows = OperatingSystem.IsWindows();

    var key = gpuVendor switch
    {
        GpuVendor.Amd when isLinux => "amd_linux",
        GpuVendor.Nvidia => "nvidia",
        GpuVendor.Amd or GpuVendor.Intel when isWindows => "windows_generic",
        GpuVendor.Intel when isLinux => "intel_linux",
        _ => null
    };

    return sp.GetRequiredKeyedService<IGpuMetricsProvider>(key);
});

builder.Services.AddSingleton<IMetricsGathererService, MetricsGathererService>();

builder.Services.AddHostedService<MetricsPublisherService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();