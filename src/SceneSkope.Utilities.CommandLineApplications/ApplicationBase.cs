using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Serilog;
using Serilog.Core;

namespace SceneSkope.Utilities.CommandLineApplications
{
    public class ApplicationBase
    {
        public static void OnExit(Action onExit)
        {
            var assemblyLoadContextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
            if (assemblyLoadContextType != null)
            {
                var currentLoadContext = assemblyLoadContextType.GetTypeInfo().GetProperty("Default").GetValue(null, null);
                var unloadingEvent = currentLoadContext.GetType().GetTypeInfo().GetEvent("Unloading");
                var delegateType = typeof(Action<>).MakeGenericType(assemblyLoadContextType);
#pragma warning disable RCS1163 // Unused parameter.
#pragma warning disable IDE0039 // Use local function
                Action<object> lambda = (context) => onExit();
#pragma warning restore IDE0039 // Use local function
#pragma warning restore RCS1163 // Unused parameter.
                unloadingEvent.AddEventHandler(currentLoadContext, lambda.GetMethodInfo().CreateDelegate(delegateType, lambda.Target));
                return;
            }

            var appDomainType = Type.GetType("System.AppDomain, mscorlib");
            if (appDomainType != null)
            {
                var currentAppDomain = appDomainType.GetTypeInfo().GetProperty("CurrentDomain").GetValue(null, null);
                var processExitEvent = currentAppDomain.GetType().GetTypeInfo().GetEvent("ProcessExit");
#pragma warning disable IDE0039 // Use local function
                EventHandler lambda = (sender, e) => onExit();
#pragma warning restore IDE0039 // Use local function
                processExitEvent.AddEventHandler(currentAppDomain, lambda);
                return;
                // Note that .NETCore has a private System.AppDomain which lacks the ProcessExit event.
                // That's why we test for AssemblyLoadContext first!
            }

            var isNetCore = (Type.GetType("System.Object, System.Runtime") != null);
            if (isNetCore) throw new Exception("Before calling this function, declare a variable of type 'System.Runtime.Loader.AssemblyLoadContext' from NuGet package 'System.Runtime.Loader'");
            else throw new Exception("Neither mscorlib nor System.Runtime.Loader is referenced");
        }

        internal static string[] PreProcessArgs(string[] args)
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

        internal protected LockFile _lockFile;
        internal protected TelemetryClient _telemetryClient;
        internal protected bool _exited;

        internal protected void Unload()
        {
            if (!_exited)
            {
                _exited = true;
                Log.Debug("Finished");
                Log.CloseAndFlush();
                if (_telemetryClient != null)
                {
                    _telemetryClient.Flush();
                    Thread.Sleep(1000);
                }
                _lockFile?.Dispose();
            }
        }
    }

    public abstract class ApplicationBase<TArgs> : ApplicationBase where TArgs : ArgumentsBase, new()
    {
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

            OnExit(Unload);

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
                    logConfiguration = logConfiguration.WriteTo.Console();
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

                Log.Logger = logConfiguration
                    .MinimumLevel.Information()
                    .Enrich.WithProperty("Application", Assembly.GetEntryAssembly().GetName().Name)
                    .Enrich.WithDemystifiedStackTraces()
                    .CreateLogger();
                Log.Debug("Starting up");

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

        protected abstract Task RunAsync(TArgs arguments, CancellationToken ct);
    }
}
