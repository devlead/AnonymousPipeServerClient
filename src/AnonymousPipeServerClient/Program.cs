using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AnonymousPipeServerClient.Commands;
using Spectre.Console.Cli;
using Spectre.Cli.Extensions.DependencyInjection;

var serviceCollection = new ServiceCollection()
    .AddLogging(configure =>
            configure
                .AddSimpleConsole(opts =>
                {
                    opts.IncludeScopes = true;
                    opts.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                })
    );

using var registrar = new DependencyInjectionRegistrar(serviceCollection);
var app = new CommandApp(registrar);

app.Configure(
    config =>
    {
        config.ValidateExamples();

        config.AddCommand<ServerCommand>("server")
            .WithDescription("Server command.")
            .WithExample(new[] { "server" });

        config.AddCommand<ClientCommand>("client")
            .WithDescription("Client command.")
            .WithExample(new[] { "client", "<serverEndpoint>" });
    });

return await app.RunAsync(args);