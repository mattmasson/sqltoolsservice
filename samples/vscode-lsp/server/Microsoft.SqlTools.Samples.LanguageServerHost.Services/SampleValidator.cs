using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SqlTools.CoreServices.Workspace;
using Microsoft.SqlTools.Hosting;
using Microsoft.SqlTools.Hosting.Extensibility;
using Microsoft.SqlTools.Hosting.Protocol;
using Microsoft.SqlTools.Hosting.Utility;
using Microsoft.SqlTools.Samples.LanguageServerHost.Services.Contracts;

namespace Microsoft.SqlTools.Samples.LanguageServerHost.Services
{
    [Export(typeof(IHostedService))]
    public class SampleValidator : HostedService<SampleValidator>
    {
        private readonly Regex Pattern = new Regex(@"\b[A-Z]{2,}\b", RegexOptions.Multiline | RegexOptions.Compiled);

        private WorkspaceService WorkspaceService { get; set; }

        private SettingsService<LanguageServerSettings> SettingsService { get; set; }

        public override void InitializeService(IServiceHost serviceHost)
        {
            // get a handle on the SettingsService
            SettingsService = this.ServiceProvider.GetService<SettingsService<LanguageServerSettings>>();
            Debug.Assert(SettingsService != null, "SettingsService instance not set");

            SettingsService.RegisterConfigChangeCallback(HandleDidChangeConfigurationNotification);

            // get a handle on the WorkspaceService 
            WorkspaceService = this.ServiceProvider.GetService<WorkspaceService>();
            Debug.Assert(WorkspaceService != null, "WorkspaceService instance not set");

            // register a callback to handle document changes
            WorkspaceService.RegisterTextDocChangeCallback(HandleDidChangeTextDocumentNotification);

            // Register an initialization handler that retrieves the initial settings
            serviceHost.RegisterInitializeTask(async (parameters, context) =>
            {
                Logger.Instance.Write(LogLevel.Verbose, "Initializing service");

                context.SendEvent(
                    DidChangeConfigurationNotification<LanguageServerSettings>.Type,
                    new DidChangeConfigurationParams<LanguageServerSettings>
                    {
                        Settings = new LanguageServerSettings()
                    }
                );

                await Task.FromResult(0);
            });
        }

        public Task HandleDidChangeConfigurationNotification(
            LanguageServerSettings newSettings,
            LanguageServerSettings oldSettings,
            EventContext eventContext)
        {
            // TODO: handle configuration changes
            return Task.FromResult(true);
        }

        /// <summary>
        /// Handles text document change events
        /// </summary>
        /// <param name="textChangeParams"></param>
        /// <param name="eventContext"></param>
        public async Task HandleDidChangeTextDocumentNotification(TextDocument[] changedFiles, EventContext eventContext)
        {
            try
            {
                await Task.WhenAll(changedFiles.Select(doc => ValidateTextDocument(doc, eventContext)));
            }
            catch (Exception ex)
            {
                Logger.Instance.Write(LogLevel.Error, "Unknown error " + ex.ToString());
                // TODO: need mechanism return errors from event handlers
                throw;
            }
        }

        public Task ValidateTextDocument(TextDocument document, EventContext context)
        {
            int problems = 0;
            List<Diagnostic> diagnostics = new List<Diagnostic>();

            foreach (Match m in Pattern.Matches(document.Contents))
            {
                problems++;

                BufferPosition start = document.GetPositionAtOffset(m.Index);
                BufferPosition end = document.GetPositionAtOffset(m.Index + m.Value.Length);

                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Code = "ex",
                    Message = string.Format("'{0}' is all uppercase.", m.Value),
                    Range = new Range
                    {
                        Start = new Position { Line = start.Line - document.BaseOffset, Character = start.Column - document.BaseOffset },
                        End = new Position { Line = end.Line - document.BaseOffset, Character = end.Column - document.BaseOffset },
                    }
                });

                // TODO: check client capability for related information

                // TODO: check problem count against settings
            }

            context.SendEvent(
                PublishDiagnosticsNotification.Type,
                new PublishDiagnosticsNotification
                {
                    Uri = document.Id,
                    Diagnostics = diagnostics.ToArray()
                }
            );

            return Task.FromResult(true);
        }
    }
}
