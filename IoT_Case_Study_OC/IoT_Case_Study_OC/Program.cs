using Agent.Interfaces;
using Agent.Models;
using Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        var opcConnectionString = configuration["OPC_CONNECTION_STRING"];
        var azureConnectionString = configuration["AZURE_IOT_CONNECTION_STRING"];

        if (opcConnectionString == null || azureConnectionString == null)
        {
            Console.Error.WriteLine("OPC_CONNECTION_STRING or AZURE_IOT_CONNECTION_STRING is not provided.");
            return;
        }

        var deviceIdentifiers = configuration.GetSection("DEVICES").GetChildren()
    .Select(d =>
    {
        var deviceName = d["deviceName"];
        var opcNodeId = d["opcNodeId"];
        var azureDeviceId = d["azureDeviceId"];

        if (opcNodeId == null || azureDeviceId == null || deviceName == null)
        {
            Console.Error.WriteLine($"{d} DON'T HAVE ENOUGH CONFIG DATA. IT WILL BE MISSED");
            return null;
        }

        return new DeviceIdentifier(
            deviceName,
            opcNodeId,
            azureDeviceId);
    })
    .Where(d => d != null).ToList();

        services.AddSingleton<IOpcManager>(provider => new OpcManagerService(opcConnectionString, deviceIdentifiers));
        services.AddSingleton<IIoTManager>(provider => new IoTManagerService(azureConnectionString, deviceIdentifiers));
        services.AddSingleton<IDeviceManager>(provider =>
            new DeviceManagerService(
                provider.GetRequiredService<IOpcManager>(),
                provider.GetRequiredService<IIoTManager>(),
                deviceIdentifiers
            ));
    })
    .Build();

var deviceManager = host.Services.GetRequiredService<IDeviceManager>();
await deviceManager.StartAsync();

Console.ReadLine();
deviceManager.Stop();
