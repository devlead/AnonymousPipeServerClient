using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnonymousPipeServerClient.Commands.Settings;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace AnonymousPipeServerClient.Commands
{
    public class ClientCommand : AsyncCommand<ClientSettings>
    {
        public record ServerResponse(ServerVersion version, ServerCommand command);
        public record ServerVersion(short major, short minor, short build, short revision);
        public record ServerCommand(string command, Dictionary<string, string> commandParameters);

        private ILogger Logger { get; }

        public override async Task<int> ExecuteAsync(CommandContext context, ClientSettings settings)
        {
            try
            {
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, _) => cts.Cancel();

                if (!Debugger.IsAttached && settings.Debug)
                {
                    Debugger.Launch();
                }

                await using AnonymousPipeClientStream pipeClient = new(PipeDirection.In, settings.Server);

                foreach (var json in ChunkedJson(pipeClient, cts.Token))
                {

                    var serverVersion = JsonSerializer.Deserialize<ServerResponse>(json);
                    Logger.LogInformation("{serverVersion}", serverVersion);
                    await Task.Delay(1000, cts.Token);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Cancellation requested aborting program.");
                return 1;
            }
        }

        private static IEnumerable<byte[]> ChunkedJson(PipeStream pipeClient, CancellationToken cancellationToken)
        {
            static IEnumerable<byte> Chunk(Stream stream)
            {
                int current;
                while ((current = stream.ReadByte()) > 0)
                {
                    yield return (byte) current;
                }
            }

            using var bufferedStream = new BufferedStream(pipeClient, 1024);
            while (!cancellationToken.IsCancellationRequested && pipeClient.IsConnected)
            {
                yield return Chunk(bufferedStream).ToArray();
            }
        }

        public ClientCommand(ILogger<ClientCommand> logger)
        {
            Logger = logger;
            Logger.BeginScope(new { Environment.ProcessId});
        }
    }
}