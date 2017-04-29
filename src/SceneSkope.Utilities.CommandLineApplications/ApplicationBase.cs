using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ApplicationInsights;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Loader;
using Serilog.Core;

namespace SceneSkope.Utilities.CommandLineApplications
{
    public abstract class ApplicationBase<TArgs> where TArgs : ArgumentsBase, new()
    {
        public void ApplicationMain(string[] args)
        {
            var parser = new CommandLineParser.CommandLineParser
            {
                AcceptSlash = true,
                ShowUsageOnEmptyCommandline = true,
                IgnoreCase = true,
                AcceptEqualSignSyntaxForValueArguments = true,
            };
            try
            {
                var arguments = new TArgs();
                parser.ExtractArgumentAttributes(arguments);
                parser.ParseCommandLine(args);
                if (arguments.Help)
                {
                    parser.ShowUsage();
                }
                else
                {
                    Run(arguments);
                }
            }
            catch (CommandLineParser.Exceptions.CommandLineException ex)
            {
                Console.WriteLine(ex.Message);
                foreach (var arg in args)
                {
                    Console.WriteLine(arg);
                }
                parser.ShowUsage();
            }
        }

        private LockFile _lockFile;
        private TelemetryClient _telemetryClient;

        private void Run(TArgs arguments)
        {
            if (!string.IsNullOrWhiteSpace(arguments.LockFile))
            {
                if ((_lockFile = LockFile.TryCreate(arguments.LockFile)) == null)
                {
                    return;
                }
            }
            else
            {
                _lockFile = null;
            }

            AssemblyLoadContext.Default.Unloading += _ => Unload();

            _telemetryClient = null;
            try
            {
                var tokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    tokenSource.Cancel();
                };
                var logConfiguration = new LoggerConfiguration();
                if (!string.IsNullOrWhiteSpace(arguments.Key))
                {
                    _telemetryClient = new TelemetryClient
                    {
                        InstrumentationKey = arguments.Key
                    };
                    logConfiguration = logConfiguration.WriteTo.ApplicationInsightsTraces(_telemetryClient);
                }

                if (!string.IsNullOrWhiteSpace(arguments.SeqHost))
                {
                    logConfiguration = logConfiguration
                        .WriteTo.Seq(arguments.SeqHost,
                        apiKey: arguments.SeqToken,
                        controlLevelSwitch: new LoggingLevelSwitch(),
                        compact: true);
                }

                if (!arguments.NoConsole)
                {
                    logConfiguration = logConfiguration.WriteTo.ColoredConsole();
                }

                var log = logConfiguration
                    .MinimumLevel.Information()
                    .CreateLogger();
                Log.Logger = log;
                Log.Information("Starting up");

                RunAsync(arguments, tokenSource.Token).GetAwaiter().GetResult();

            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error processing: {exception}", ex.Message);
            }
            finally
            {
                Unload();
            }
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private bool _exited;
        private void Unload()
        {
            if (!_exited)
            {
                _exited = true;
                Log.Information("Finished");
                Log.CloseAndFlush();
                if (_telemetryClient != null)
                {
                    _telemetryClient.Flush();
                    Thread.Sleep(1000);
                }
                _lockFile?.Dispose();
            }
        }

        protected abstract Task RunAsync(TArgs arguments, CancellationToken ct);
    }
}
