//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.SqlTools.Hosting;
using Microsoft.SqlTools.Hosting.Channels;
using Microsoft.SqlTools.Hosting.Extensibility;
using Microsoft.SqlTools.Hosting.Utility;
using Microsoft.SqlTools.LanguageServerProtocol;
using Microsoft.SqlTools.LanguageServerProtocol.Contracts.ServerCapabilities;
using Microsoft.SqlTools.Samples.LanguageServerHost.Services;

namespace Microsoft.SqlTools.Samples.LanguageServerHost
{
    class Program
    {
        private const string ServiceName = "LanguageServerHost";
        // turn on Verbose logging during early development
        private const LogLevel MinimumLogLevel = LogLevel.Verbose;

        static void Main(string[] args)
        {
            try
            {
                // read command-line arguments
                string applicationName = Path.ChangeExtension(ServiceName, ".exe");
                CommandOptions commandOptions = new CommandOptions(args, applicationName);
                if (commandOptions.ShouldExit)
                {
                    return;
                }

                // we are in debug mode
                if (commandOptions.WaitForDebugger)
                {
                    int maxWait = 30;
                    int waitCount = 0;
                    while (!Debugger.IsAttached && waitCount < maxWait)
                    {
                        Thread.Sleep(500);
                        waitCount++;
                    }
                }

                // configure log file
                string logFilePath = ServiceName;
                if (!string.IsNullOrWhiteSpace(commandOptions.LoggingDirectory))
                {
                    logFilePath = Path.Combine(commandOptions.LoggingDirectory, logFilePath);
                }
                
                Logger.Instance.Initialize(logFilePath: logFilePath, minimumLogLevel: MinimumLogLevel, isEnabled: commandOptions.EnableLogging);
                Logger.Instance.Write(LogLevel.Normal, "Starting language server host");

                // list all service assemblies that should be loaded by the host
                string[] searchPath = new string[] {
                    "Microsoft.SqlTools.Hosting.v2.dll",
                    //"Microsoft.SqlTools.CoreServices.dll",
                    "Microsoft.SqlTools.LanguageServerProtocol.dll",
                    "Microsoft.SqlTools.Samples.LanguageServerHost.Services.dll",
                };

                ExtensionServiceProvider serviceProvider = ExtensionServiceProvider.CreateFromAssembliesInDirectory(Path.GetDirectoryName(typeof(Program).Assembly.Location), searchPath);
                serviceProvider.RegisterSingleService(SettingsService<LanguageServerSettings>.Instance);

                ExtensibleServiceHost host = new ExtensibleServiceHost(serviceProvider, new StdioServerChannel());

                // TODO: can server capabilities be automatically determined by the registered services?
                host.ServerCapabilities = new ServerCapabilities
                {
                    TextDocumentSync = TextDocumentSyncKind.Incremental
                };

                Logger.Instance.Write(LogLevel.Verbose, "Starting wait loop");

                host.Start();
                host.WaitForExit();
            }
            catch (Exception e)
            {
                Logger.Instance.Write(LogLevel.Error, string.Format("An unhandled exception occurred: {0}", e));
                Environment.Exit(1);
            }
        }
    }
}
