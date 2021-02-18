using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnonymousPipeServerClient.Commands.Settings;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace AnonymousPipeServerClient.Commands
{
    public class ServerCommand : AsyncCommand<ServerSettings>
    {
        private readonly byte[] version = Encoding.UTF8.GetBytes("version");
        private readonly byte[] major = Encoding.UTF8.GetBytes("major");
        private readonly byte[] minor = Encoding.UTF8.GetBytes("minor");
        private readonly byte[] build = Encoding.UTF8.GetBytes("build");
        private readonly byte[] revision = Encoding.UTF8.GetBytes("revision");
        private readonly byte[] endOfRecord = {0};
        private ILogger Logger { get; }

        public override async Task<int> ExecuteAsync(CommandContext context, ServerSettings settings)
        {
            await using AnonymousPipeServerStream pipeServer = new(PipeDirection.Out, HandleInheritability.Inheritable);

            var clientHandleAsString = pipeServer.GetClientHandleAsString();
            Logger.LogInformation("Server started at {serverEndpoint}", clientHandleAsString);

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_,_) =>cancellationTokenSource.Cancel();

       

            Process? childProcess = default;
            try
            {
                childProcess = Process.Start(
                    new ProcessStartInfo
                    {
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        FileName = "AnonymousPipeServerClient.exe",
                        Arguments = $"client {(Debugger.IsAttached ? " --debug" : string.Empty)}",
                        EnvironmentVariables =
                        {
                            { nameof(AnonymousPipeServerClient), clientHandleAsString }
                        }
                    }
                );
                if (childProcess == null)
                {
                    return 1;
                }

                await using Utf8JsonWriter jsonWriter = new(
                    pipeServer,
                    new JsonWriterOptions { SkipValidation = true, Indented = false }
                );

                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (pipeServer.IsConnected)
                    {
                        // Fake version based on date
                        var versionDate = DateTime.UtcNow;
                        var timeOfDay = (short) ((versionDate - versionDate.Date).TotalSeconds / 3);

                        jsonWriter.WriteStartObject();
                        jsonWriter.WriteStartObject(version);
                        jsonWriter.WriteNumber(major, versionDate.Year);
                        jsonWriter.WriteNumber(minor, versionDate.Month);
                        jsonWriter.WriteNumber(build, versionDate.Day);
                        jsonWriter.WriteNumber(revision, timeOfDay);
                        jsonWriter.WriteEndObject();
                        jsonWriter.WriteEndObject();
                        await jsonWriter.FlushAsync(cancellationTokenSource.Token);
                        jsonWriter.Reset();

                        pipeServer.Write(endOfRecord);
                        await pipeServer.FlushAsync(cancellationTokenSource.Token);

                    }

                    await Task.Delay(1000, cancellationTokenSource.Token);
                }

                pipeServer.WaitForPipeDrain();
                await childProcess.WaitForExitAsync(cancellationTokenSource.Token);

                return 0;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Cancellation requested aborting program.");
                return 1;
            }
            finally
            {
                if ((childProcess?.HasExited ?? true) == false)
                {
                    childProcess.Kill();
                }
            }
        }

        public ServerCommand(ILogger<ServerCommand> logger)
        {
            Logger = logger;
            Logger.BeginScope(new { Environment.ProcessId });
        }
    }
}
