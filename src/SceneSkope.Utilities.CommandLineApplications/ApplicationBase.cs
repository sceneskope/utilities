﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Serilog;
using Serilog.Core;

namespace SceneSkope.Utilities.CommandLineApplications
{
    public abstract class ApplicationBase<TArgs> where TArgs : ArgumentsBase, new()
    {
#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
        private static string[] PreProcessArgs(string[] args)
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.
        {
            var converted = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("@"))
                {
                    var fileName = arg.Substring(1);
                    if (File.Exists(fileName))
                    {
                        converted.AddRange(File.ReadAllLines(fileName));
                    }
                    else
                    {
                        converted.Add(arg);
                    }
                }
                else
                {
                    converted.Add(arg);
                }
            }
            return converted.ToArray();
        }

        public void ApplicationMain(string[] args)
        {
            string[] processedArgs = null;
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
                processedArgs = PreProcessArgs(args);
                parser.ParseCommandLine(processedArgs);
                if (arguments.Help)
                {
                    parser.ShowUsage();
                }
                else if (parser.ParsingSucceeded)
                {
                    Run(arguments);
                }
            }
            catch (CommandLineParser.Exceptions.CommandLineException ex)
            {
                Console.WriteLine(ex.Message);
                foreach (var arg in args)
                {
                    Console.WriteLine($"Argument: {arg}");
                }
                if ((processedArgs != null) && (processedArgs != args))
                {
                    foreach (var arg in processedArgs)
                    {
                        Console.WriteLine($"Processed: {arg}");
                    }
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
                if (!string.IsNullOrWhiteSpace(arguments.LogFile))
                {
                    if (arguments.LogFile.IndexOf("{Date}", StringComparison.OrdinalIgnoreCase) == -1)
                    {
                        throw new ArgumentException("LogFile must contain {Date} in it's name");
                    }
                    logConfiguration = logConfiguration
                        .WriteTo.RollingFile(arguments.LogFile);
                }

                var log = logConfiguration
                    .MinimumLevel.Information()
                    .Enrich.WithProperty("Application", Assembly.GetEntryAssembly().GetName().Name)
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
                Log.Fatal(ex, "Error processing: {Exception}", ex.Message);
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
