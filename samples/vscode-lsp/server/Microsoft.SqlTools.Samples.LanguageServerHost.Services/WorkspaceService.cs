//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlTools.Hosting;
using Microsoft.SqlTools.Hosting.Contracts;
using Microsoft.SqlTools.Hosting.Extensibility;
using Microsoft.SqlTools.Hosting.Protocol;
using Microsoft.SqlTools.Hosting.Utility;
using Microsoft.SqlTools.Samples.LanguageServerHost.Services.Contracts;

namespace Microsoft.SqlTools.Samples.LanguageServerHost.Services
{
    [Export(typeof(IHostedService))]
    public class WorkspaceService : HostedService<WorkspaceService> 
    {
        /// <summary>
        /// Default, parameterless constructor.
        /// </summary>
        public WorkspaceService()
        {
            TextDocChangeCallbacks = new List<TextDocChangeCallback>();
            TextDocOpenCallbacks = new List<TextDocOpenCallback>();
            TextDocCloseCallbacks = new List<TextDocCloseCallback>();
        }

        public override void InitializeService(IServiceHost serviceHost)
        {
            // Create a workspace that will handle state for the session
            Workspace = new Workspace();

            serviceHost.SetAsyncEventHandler(DidChangeTextDocumentNotification.Type, HandleDidChangeTextDocumentNotification);
            serviceHost.SetAsyncEventHandler(DidOpenTextDocumentNotification.Type, HandleDidOpenTextDocumentNotification);
            serviceHost.SetAsyncEventHandler(DidCloseTextDocumentNotification.Type, HandleDidCloseTextDocumentNotification);

            // Register an initialization handler that sets the workspace path
            serviceHost.RegisterInitializeTask(async (parameters, context) =>
            {
                Logger.Instance.Write(LogLevel.Verbose, "Initializing workspace service");

                // TODO: cache client capabilities?

                if (Workspace != null)
                {
                    // we only support a single workspace path
                    if (parameters.WorkspaceFolders != null)
                    {
                        Workspace.WorkspacePath = parameters.WorkspaceFolders.First().Uri;
                    }
                    else
                    {
                        Workspace.WorkspacePath = parameters.RootUri;
                    }
                }
                await Task.FromResult(0);
            });

            // Register a shutdown request that disposes the workspace
            serviceHost.RegisterShutdownTask(async (parameters, context) =>
            {
                Logger.Instance.Write(LogLevel.Verbose, "Shutting down workspace service");

                if (Workspace != null)
                {
                    Workspace.Dispose();
                    Workspace = null;
                }
                await Task.FromResult(0);
            });
        }

        /// <summary>
        /// Adds a new task to be called when the text of a document changes.
        /// </summary>
        /// <param name="task">Delegate to call when the document changes</param>
        public void RegisterTextDocChangeCallback(TextDocChangeCallback task)
        {
            TextDocChangeCallbacks.Add(task);
        }

        /// <summary>
        /// Adds a new task to be called when a text document closes.
        /// </summary>
        /// <param name="task">Delegate to call when the document closes</param>
        public void RegisterTextDocCloseCallback(TextDocCloseCallback task)
        {
            TextDocCloseCallbacks.Add(task);
        }

        /// <summary>
        /// Adds a new task to be called when a file is opened
        /// </summary>
        /// <param name="task">Delegate to call when a document is opened</param>
        public void RegisterTextDocOpenCallback(TextDocOpenCallback task)
        {
            TextDocOpenCallbacks.Add(task);
        }

        #region Properties

        /// <summary>
        /// Workspace object for the service. Virtual to allow for mocking
        /// </summary>
        public virtual Workspace Workspace{ get; internal set; }

        /// <summary>
        /// Delegate for callbacks that occur when the current text document changes
        /// </summary>
        /// <param name="changedFiles">Array of files that changed</param>
        /// <param name="eventContext">Context of the event raised for the changed files</param>
        public delegate Task TextDocChangeCallback(TextDocument[] changedFiles, EventContext eventContext);

        /// <summary>
        /// Delegate for callbacks that occur when a text document is opened
        /// </summary>
        /// <param name="openFile">File that was opened</param>
        /// <param name="eventContext">Context of the event raised for the changed files</param>
        public delegate Task TextDocOpenCallback(TextDocument openFile, EventContext eventContext);

        /// <summary>
        /// Delegate for callbacks that occur when a text document is closed
        /// </summary>
        /// <param name="closedFile">File that was closed</param>
        /// <param name="eventContext">Context of the event raised for changed files</param>
        public delegate Task TextDocCloseCallback(TextDocument closedFile, EventContext eventContext);

        /// <summary>
        /// List of callbacks to call when the current text document changes
        /// </summary>
        private List<TextDocChangeCallback> TextDocChangeCallbacks { get; set; }

        /// <summary>
        /// List of callbacks to call when a text document is opened
        /// </summary>
        private List<TextDocOpenCallback> TextDocOpenCallbacks { get; set; }

        /// <summary>
        /// List of callbacks to call when a text document is closed
        /// </summary>
        private List<TextDocCloseCallback> TextDocCloseCallbacks { get; set; }

        #endregion

        /// <summary>
        /// Handles text document change events
        /// </summary>
        internal async Task HandleDidChangeTextDocumentNotification(
            DidChangeTextDocumentParams textChangeParams,
            EventContext eventContext)
        {
            Logger.Instance.Write(LogLevel.Verbose, "textDocument/didChange");

            try
            {
                StringBuilder msg = new StringBuilder();
                msg.Append("HandleDidChangeTextDocumentNotification");
                List<TextDocument> changedFiles = new List<TextDocument>();

                // A text change notification can batch multiple change requests
                foreach (var textChange in textChangeParams.ContentChanges)
                {
                    string fileUri = textChangeParams.TextDocument.Uri ?? textChangeParams.TextDocument.Uri;
                    msg.AppendLine(string.Format("  File: {0}", fileUri));

                    TextDocument changedFile = Workspace.GetFile(fileUri);
                    if (changedFile != null)
                    {
                        changedFile.ApplyChange(
                            GetFileChangeDetails(
                                textChange.Range.Value,
                                textChange.Text));

                        changedFiles.Add(changedFile);
                    }
                }

                Logger.Instance.Write(LogLevel.Verbose, msg.ToString());

                var handlers = TextDocChangeCallbacks.Select(t => t(changedFiles.ToArray(), eventContext));
                await Task.WhenAll(handlers);
            }
            catch (Exception ex)
            {
                Logger.Instance.Write(LogLevel.Error, "Unknown error " + ex.ToString());
                // Swallow exceptions here to prevent us from crashing
                // TODO: this probably means the file model is in a bad state or out of sync with the actual file; we should recover here
                await Task.FromResult(true);
            }
        }

        internal async Task HandleDidOpenTextDocumentNotification(DidOpenTextDocumentNotification openParams, EventContext eventContext)
        {
            try
            {
                Logger.Instance.Write(LogLevel.Verbose, "HandleDidOpenTextDocumentNotification");

                if (IsScmEvent(openParams.TextDocument.Uri))
                {
                    return;
                }

                // read the SQL file contents into the ScriptFile 
                TextDocument openedFile = Workspace.GetFileBuffer(openParams.TextDocument.Uri, openParams.TextDocument.Text);
                if (openedFile == null)
                {
                    return;
                }
                // Propagate the changes to the event handlers
                var textDocOpenTasks = TextDocOpenCallbacks.Select(t => t(openedFile, eventContext));

                await Task.WhenAll(textDocOpenTasks);
            }
            catch (Exception ex)
            {
                Logger.Instance.Write(LogLevel.Error, "Unknown error " + ex.ToString());
                // Swallow exceptions here to prevent us from crashing
                // TODO: this probably means the file model is in a bad state or out of sync with the actual file; we should recover here
                return;
            }
        }

        internal async Task HandleDidCloseTextDocumentNotification(DidCloseTextDocumentParams closeParams, EventContext eventContext)
        {
            try
            {
                Logger.Instance.Write(LogLevel.Verbose, "HandleDidCloseTextDocumentNotification");

                if (IsScmEvent(closeParams.TextDocument.Uri))
                {
                    return;
                }

                // Skip closing this file if the file doesn't exist
                var closedFile = Workspace.GetFile(closeParams.TextDocument.Uri);
                if (closedFile == null)
                {
                    return;
                }

                // Trash the existing document from our mapping
                Workspace.CloseFile(closedFile);

                // Send out a notification to other services that have subscribed to this event
                var textDocClosedTasks = TextDocCloseCallbacks.Select(t => t(closedFile, eventContext));
                await Task.WhenAll(textDocClosedTasks);
            }
            catch (Exception ex)
            {
                Logger.Instance.Write(LogLevel.Error, "Unknown error " + ex.ToString());
                // Swallow exceptions here to prevent us from crashing
                // TODO: this probably means the file model is in a bad state or out of sync with the actual file; we should recover here
                return;
            }
        }

        #region Private Helpers

        /// <summary>
        /// Switch from 0-based offsets to 1 based offsets
        /// </summary>
        /// <param name="changeRange"></param>
        /// <param name="insertString"></param>       
        private static DocumentChange GetFileChangeDetails(Range changeRange, string insertString)
        {
            // The protocol's positions are zero-based so add 1 to all offsets
            return new DocumentChange
            {
                InsertString = insertString,
                Line = changeRange.Start.Line + 1,
                Offset = changeRange.Start.Character + 1,
                EndLine = changeRange.End.Line + 1,
                EndOffset = changeRange.End.Character + 1
            };
        }

        private static bool IsScmEvent(string filePath)
        {
            // if the URI is prefixed with git: then we want to skip processing that file
            return filePath.StartsWith("git:");
        }

        #endregion
    }
}
