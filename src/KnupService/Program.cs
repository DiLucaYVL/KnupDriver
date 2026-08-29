using KnupService;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "KnupDriverService";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

