using System;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AnonymousPipeServerClient.Commands.Settings
{
    public class ClientSettings : CommandSettings
    {
        [CommandArgument(0, "[serverEndpoint]")]
        [Description("Server Endpoint")]
        public string Server { get; set; }

        [CommandOption("--debug")]
        [Description("Launch debugger if not attached.")]
        public bool Debug { get; set; }

#pragma warning disable 8618
        public ClientSettings()
#pragma warning restore 8618
        {
            if (Environment.GetEnvironmentVariable(nameof(AnonymousPipeServerClient)) is { } server)
            {
                Server = server;
            }
        }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(Server)
                ? ValidationResult.Error(
                    $"Server argument not specified nor in environment variable {nameof(AnonymousPipeServerClient)}.")
                : base.Validate();

    }
}