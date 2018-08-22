//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlTools.Hosting.Utility;
using Microsoft.SqlTools.LanguageServerProtocol.Contracts.ClientCapabilities;

namespace Microsoft.SqlTools.LanguageServerProtocol
{
    /// <summary>
    /// Manages a "workspace" of documents that are open for a particular editing session.
    /// Also helps to navigate references between files within the workspace.
    /// </summary>
    public class Workspace : IDisposable
    {
        #region Private Fields

        private const string UntitledScheme = "untitled";
        private static readonly HashSet<string> fileUriSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file",
            UntitledScheme,
            //"tsqloutput"  // should be set in script sub-class?
        };

        private readonly Dictionary<string, TextDocument> workspaceDocuments = new Dictionary<string, TextDocument>();

        #endregion

        #region Properties

        /// <summary>
        /// All documents contained within the workspace. Can be overridden to specialize the Document type.
        /// </summary>
        virtual protected Dictionary<string, TextDocument> Documents
        {
            get { return workspaceDocuments; }
        }

        /// <summary>
        /// Set of known schemes used to determine whether a given URI points to a file.
        /// Workspace subclasses may choose to add to this list if they support custom file schemes.
        /// The hashset should use <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        virtual protected HashSet<string> FileUriSchemes { get { return fileUriSchemes; } }

        /// <summary>
        /// Gets or sets the root path of the workspace.
        /// </summary>
        public string WorkspacePath { get; set; }

        /// <summary>
        /// Gets or sets the ClientCapabilities set during initialization.
        /// </summary>
        virtual public ClientCapabilities ClientCapabilities { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the Workspace class with default ClientCapabilities.
        /// </summary>
        internal Workspace()
            : this(new ClientCapabilities())
        {
            
        }

        /// <summary>
        /// Creates a new instance of the Workspace class.
        /// </summary>
        /// <param name="clientCapabilities">Capabilities of the client connected to this workspace</param>
        internal Workspace(ClientCapabilities clientCapabilities)
        {
            ClientCapabilities = clientCapabilities;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if a given URI is contained in a workspace 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Flag indicating if the file is tracked in workspace</returns>
        public bool ContainsFile(string path)
        {
            Validate.IsNotNullOrWhitespaceString("path", path);

            // Resolve the full file path 
            ResolvedFile resolvedFile = this.ResolveFilePath(path);
            string keyName = resolvedFile.LowercaseFilePath;

            TextDocument document = null;
            return this.Documents.TryGetValue(keyName, out document);
        }

        /// <summary>
        /// Gets an open file in the workspace. If the file isn't open but
        /// exists on the filesystem, load and return it. Virtual method to
        /// allow for mocking
        /// </summary>
        /// <param name="path">The file path at which the document resides.</param>
        /// <exception cref="FileNotFoundException">
        /// <paramref name="path"/> is not found.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="path"/> contains a null or empty string.
        /// </exception>
        public virtual TextDocument GetFile(string path)
        {
            Validate.IsNotNullOrWhitespaceString("path", path);
            if (IsNonFileUri(path))
            {
                return null;
            }

            // Resolve the full file path 
            ResolvedFile resolvedFile = this.ResolveFilePath(path);
            string keyName = resolvedFile.LowercaseFilePath;

            // Make sure the file isn't already loaded into the workspace
            TextDocument document = null;
            if (!this.Documents.TryGetValue(keyName, out document))
            {
                if (IsUntitled(resolvedFile.FilePath)
                    || !resolvedFile.CanReadFromDisk
                    || !File.Exists(resolvedFile.FilePath))
                {
                    // It's either not a registered untitled file, or not a valid file on disk
                    // so any attempt to read from disk will fail.
                    return null;
                }
                // This method allows FileNotFoundException to bubble up 
                // if the file isn't found.
                using (FileStream fileStream = new FileStream(resolvedFile.FilePath, FileMode.Open, FileAccess.Read))
                using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    document = new TextDocument(resolvedFile.FilePath, path, streamReader, isInMemory: false);

                    this.Documents.Add(keyName, document);
                }

                Logger.Instance.Write(LogLevel.Verbose, "Opened file on disk: " + resolvedFile.FilePath);
            }

            return document;
        }

        private ResolvedFile ResolveFilePath(string path)
        {
            bool canReadFromDisk = false;
            if (!IsPathInMemoryOrNonFileUri(path))
            {
                if (path.StartsWith(@"file://"))
                {
                    // VS Code encodes the ':' character in the drive name, which can lead to problems parsing
                    // the URI, so unencode it if present. See https://github.com/Microsoft/vscode/issues/2990
                    path = path.Replace("%3A/", ":/", StringComparison.OrdinalIgnoreCase);

                    // Client sent the path in URI format, extract the local path and trim
                    // any extraneous slashes
                    Uri fileUri = new Uri(path);
                    path = fileUri.LocalPath;
                    if (path.StartsWith("//") || path.StartsWith("\\\\") || path.StartsWith("/"))
                    {
                        path = path.Substring(1);
                    }
                }

                // Clients could specify paths with escaped space, [ and ] characters which .NET APIs
                // will not handle.  These paths will get appropriately escaped just before being passed
                // into the SqlTools engine.
                path = UnescapePath(path);

                // switch to unix path separators on non-Windows platforms
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    path = path.Replace('\\', '/');
                }

                // Get the absolute file path
                ResolvedFile resolvedFile = ResolvedFile.TryGetFullPath(path);
                path = resolvedFile.FilePath;
                canReadFromDisk = resolvedFile.CanReadFromDisk;
            }

            Logger.Instance.Write(LogLevel.Verbose, "Resolved path: " + path);

            return new ResolvedFile(path, canReadFromDisk);
        }

        /// <summary>
        /// Unescapes any escaped [, ] or space characters. Typically use this before calling a
        /// .NET API that doesn't understand PowerShell escaped chars.
        /// </summary>
        /// <param name="path">The path to unescape.</param>
        /// <returns>The path with the ` character before [, ] and spaces removed.</returns>
        public static string UnescapePath(string path)
        {
            if (!path.Contains("`"))
            {
                return path;
            }

            return Regex.Replace(path, @"`(?=[ \[\]])", "");
        }

        /// <summary>
        /// Gets a new TextDocument instance which is identified by the given file
        /// path and initially contains the given buffer contents.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="initialBuffer"></param>
        /// <returns></returns>
        public TextDocument GetFileBuffer(string path, string initialBuffer)
        {
            Validate.IsNotNullOrWhitespaceString("path", path);
            if (IsNonFileUri(path))
            {
                return null;
            }

            // Resolve the full file path 
            ResolvedFile resolvedFile = this.ResolveFilePath(path);
            string keyName = resolvedFile.LowercaseFilePath;

            // Make sure the file isn't already loaded into the workspace
            TextDocument document = null;
            if (!this.Documents.TryGetValue(keyName, out document))
            {
                document = new TextDocument(resolvedFile.FilePath, path, initialBuffer);

                this.Documents.Add(keyName, document);

                Logger.Instance.Write(LogLevel.Verbose, "Opened file as in-memory buffer: " + resolvedFile.FilePath);
            }

            return document;
        }

        /// <summary>
        /// Gets an array of all opened TextDocuments in the workspace.
        /// </summary>
        /// <returns>An array of all opened TextDocuments in the workspace.</returns>
        public TextDocument[] GetOpenedFiles()
        {
            return Documents.Values.ToArray();
        }

        /// <summary>
        /// Closes a currently open file with the given path.
        /// </summary>
        /// <param name="document">The file path at which the script resides.</param>
        public void CloseFile(TextDocument document)
        {
            Validate.IsNotNull("document", document);

            this.Documents.Remove(document.Id);
        }

        internal string GetBaseFilePath(string path)
        {
            if (IsPathInMemoryOrNonFileUri(path))
            {
                // If the file is in memory, use the workspace path
                return this.WorkspacePath;
            }

            if (!Path.IsPathRooted(path))
            {
                // TODO: Assert instead?
                throw new InvalidOperationException(
                    string.Format(
                        "Must provide a full path for originalScriptPath: {0}",
                        path));
            }

            // Get the directory of the file path
            return Path.GetDirectoryName(path);
        }

        internal string ResolveRelativePath(string baseFilePath, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                return relativePath;
            }

            // Get the directory of the original script file, combine it
            // with the given path and then resolve the absolute file path.
            string combinedPath =
                Path.GetFullPath(
                    Path.Combine(
                        baseFilePath,
                        relativePath));

            return combinedPath;
        }
        internal static bool IsPathInMemoryOrNonFileUri(string path)
        {
            string scheme = GetScheme(path);
            if (!string.IsNullOrEmpty(scheme))
            {
                return !scheme.Equals("file");
            }
            return false;
        }

        public static string GetScheme(string uri)
        {
            string windowsFilePattern = @"^(?:[\w]\:|\\)";
            if (Regex.IsMatch(uri, windowsFilePattern))
            {
                // Handle windows paths, these conflict with other "URI" handling
                return null;
            }

            // Match anything that starts with xyz:, as VSCode send URIs in the format untitled:, git: etc.
            string pattern = "^([a-z][a-z0-9+.-]*):";
            Match match = Regex.Match(uri, pattern);
            if (match != null && match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private bool IsNonFileUri(string path)
        {
            string scheme = GetScheme(path);
            if (!string.IsNullOrEmpty(scheme))
            {
                return !fileUriSchemes.Contains(scheme); ;
            }
            return false;
        }

        private bool IsUntitled(string path)
        {
            string scheme = GetScheme(path);
            if (scheme != null && scheme.Length > 0)
            {
                return string.Compare(UntitledScheme, scheme, StringComparison.OrdinalIgnoreCase) == 0;
            }
            return false;
        }

        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion        
    }
}
