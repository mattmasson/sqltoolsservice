//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Globalization;
using System.IO;

namespace Microsoft.SqlTools.Hosting.Utility
{
    /// <summary>
    /// The command-line options helper class.
    /// </summary>
    public class CommandOptions
    {
        /// <summary>
        /// Construct and parse command line options from the arguments array
        /// </summary>
        public CommandOptions(string[] args, string serviceName)
        {
            ServiceName = serviceName;
            ErrorMessage = string.Empty;
            Locale = string.Empty;

            try
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    string arg = args[i];
                    if (arg.StartsWith("--") || arg.StartsWith("-"))
                    {
                        // Extracting arguments and properties
                        arg = arg.Substring(1).ToLowerInvariant();
                        string argName = arg;

                        switch (argName)
                        {
                            case "-enable-logging":
                                EnableLogging = true;
                                break;
                            case "-log-dir":
                                SetLoggingDirectory(args[++i]);
                                break;
                            case "-locale":
                                SetLocale(args[++i]);
                                break;
                            case "h":
                            case "-help":
                                ShouldExit = true;
                                return;
                            case "-wait-for-debugger":
                                WaitForDebugger = true;
                                break;
                            default:
                                ErrorMessage += string.Format("Unknown argument \"{0}\"" + Environment.NewLine, argName);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage += ex.ToString();
                return;
            }
            finally
            {
                if (!string.IsNullOrEmpty(ErrorMessage) || ShouldExit)
                {
                    Console.WriteLine(Usage);
                    ShouldExit = true;
                }
            }
        }

        /// <summary>
        /// Contains any error messages during execution
        /// </summary>
        public string ErrorMessage { get; private set; }


        /// <summary>
        /// Whether diagnostic logging is enabled
        /// </summary>
        public bool EnableLogging { get; private set; }

        /// <summary>
        /// Gets the directory where log files are output.
        /// </summary>
        public string LoggingDirectory { get; private set; }

        /// <summary>
        /// Whether the program should exit immediately. Set to true when the usage is printed.
        /// </summary>
        public bool ShouldExit { get; private set; }

        /// <summary>
        /// The locale our we should instantiate this service in 
        /// </summary>
        public string Locale { get; private set; }

        /// <summary>
        /// Name of service that is receiving command options
        /// </summary>
        public string ServiceName { get; private set; }

        /// <summary>
        /// When specified the application will wait for a debugger to attach
        /// before proceeding with initialization.
        /// </summary>
        public bool WaitForDebugger { get; private set; }

        /// <summary>
        /// Get the usage string describing command-line arguments for the program
        /// </summary>
        public string Usage
        {
            get
            {
                var str = string.Format("{0}" + Environment.NewLine +
                    ServiceName + " " + Environment.NewLine +
                    "   Options:" + Environment.NewLine +
                    "        [--enable-logging]" + Environment.NewLine +
                    "        [--log-dir **] (default: current directory)" + Environment.NewLine +
                    "        [--help]" + Environment.NewLine +
                    "        [--locale **] (default: 'en')" + Environment.NewLine,
                    "        [--wait-for-debugger]" + Environment.NewLine,
                    ErrorMessage);
                return str;
            }
        }

        private void SetLoggingDirectory(string loggingDirectory)
        {
            if (string.IsNullOrWhiteSpace(loggingDirectory))
            {
                return;
            }

            this.LoggingDirectory = Path.GetFullPath(loggingDirectory);
        }

        public virtual void SetLocale(string locale)
        {
            try
            {
                LocaleSetter(locale);
            }
            catch (CultureNotFoundException)
            {
                // Ignore CultureNotFoundException since it only is thrown before Windows 10.  Windows 10,
                // along with macOS and Linux, pick up the default culture if an invalid locale is passed
                // into the CultureInfo constructor.
            }
        }

        /// <summary>
        /// Sets the Locale field used for testing and also sets the global CultureInfo used for
        /// culture-specific messages
        /// </summary>
        /// <param name="locale"></param>
        internal void LocaleSetter(string locale)
        {
            // Creating cultureInfo from our given locale
            CultureInfo language = new CultureInfo(locale);
            Locale = locale;

            // Setting our language globally 
            CultureInfo.CurrentCulture = language;
            CultureInfo.CurrentUICulture = language;
        }
    }
}
