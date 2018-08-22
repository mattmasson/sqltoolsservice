﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlTools.Hosting.Utility;

namespace Microsoft.SqlTools.LanguageServerProtocol
{
    /// <summary>
    /// Contains the details and contents of an open file.
    /// </summary>
    public class TextDocument
    {
        #region Properties

        /// <summary>
        /// Offset value to add to any document range/position indexing.
        /// Used to support language service backends that use non-zero
        /// based position indexes. Sub-classes can override to use 
        /// an alternative index value (i.e. 1).
        /// </summary>
        virtual public int BaseOffset
        {
            get { return 0; }
        }

        /// <summary>
        /// Gets a unique string that identifies this file.  At this time,
        /// this property returns a normalized version of the value stored
        /// in the FilePath property.
        /// </summary>
        public string Id
        {
            get { return this.FilePath.ToLower(); }
        }

        /// <summary>
        /// Gets the path at which this file resides.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets or sets the path which the editor client uses to identify this file.
        /// Setter for testing purposes only
        /// virtual to allow mocking.
        /// </summary>
        public virtual string ClientFilePath { get; internal set; }

        /// <summary>
        /// Gets or sets a boolean that determines whether
        /// semantic analysis should be enabled for this file.
        /// For internal use only.
        /// </summary>
        internal bool IsAnalysisEnabled { get; set; }

        /// <summary>
        /// Gets a boolean that determines whether this file is
        /// in-memory or not (either unsaved or non-file content).
        /// </summary>
        public bool IsInMemory { get; private set; }

        /// <summary>
        /// Gets or sets a string containing the full contents of the file.
        /// Setter for testing purposes only
        /// </summary>
        public virtual string Contents
        {
            get
            {
                return string.Join("\r\n", FileLines);
            }
            set
            {
                FileLines = value != null ? value.Split('\n') : null;
            }
        }

        /// <summary>
        /// Gets a BufferRange that represents the entire content
        /// range of the file.
        /// </summary>
        public BufferRange FileRange { get; private set; }

/*
        /// <summary>
        /// Gets the list of syntax markers found by parsing this
        /// file's contents.
        /// </summary>
        public Marker[] SyntaxMarkers
        {
            get;
            private set;
        }
*/
        /// <summary>
        /// Gets the list of strings for each line of the file.
        /// </summary>
        internal IList<string> FileLines
        {
            get;
            private set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new TextDocument instance by reading file contents from
        /// the given TextReader.
        /// </summary>
        /// <param name="filePath">The path at which the script file resides.</param>
        /// <param name="clientFilePath">The path which the client uses to identify the file.</param>
        /// <param name="textReader">The TextReader to use for reading the file's contents.</param>
        /// <param name="isInMemory">whether this file is in-memory or not (either unsaved or non-file content)</param>
        public TextDocument(
            string filePath,
            string clientFilePath,
            TextReader textReader,
            bool isInMemory)
        {
            FilePath = filePath;
            ClientFilePath = clientFilePath;
            IsAnalysisEnabled = true;
            IsInMemory = isInMemory;

            SetFileContents(textReader.ReadToEnd());
        }

        /// <summary>
        /// Creates a new TextDocument instance with the specified file contents.
        /// </summary>
        /// <param name="filePath">The path at which the script file resides.</param>
        /// <param name="clientFilePath">The path which the client uses to identify the file.</param>
        /// <param name="initialBuffer">The initial contents of the script file.</param>
        public TextDocument(
            string filePath,
            string clientFilePath,
            string initialBuffer)
        {
            FilePath = filePath;
            ClientFilePath = clientFilePath;
            IsAnalysisEnabled = true;

            SetFileContents(initialBuffer);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a line from the file's contents.
        /// </summary>
        /// <param name="lineNumber">The line number in the file.</param>
        /// <returns>The complete line at the given line number.</returns>
        /// <seealso cref="BaseOffset"/>
        public string GetLine(int lineNumber)
        {
            Validate.IsWithinRange(
                "lineNumber", lineNumber,
                BaseOffset, FileLines.Count + BaseOffset);

            return FileLines[lineNumber - BaseOffset];
        }

        /// <summary>
        /// Gets the text under a specific range
        /// </summary>
        public string GetTextInRange(BufferRange range)
        {
            return string.Join(Environment.NewLine, GetLinesInRange(range));
        }

        /// <summary>
        /// Gets a range of lines from the file's contents. Virtual method to allow for
        /// mocking.
        /// </summary>
        /// <param name="bufferRange">The buffer range from which lines will be extracted.</param>
        /// <returns>An array of strings from the specified range of the file.</returns>
        /// <seealso cref="BaseOffset"/>
        public virtual string[] GetLinesInRange(BufferRange bufferRange)
        {
            ValidatePosition(bufferRange.Start);
            ValidatePosition(bufferRange.End);

            List<string> linesInRange = new List<string>();

            int startLine = bufferRange.Start.Line,
                endLine = bufferRange.End.Line;

            for (int line = startLine; line <= endLine; line++)
            {
                string currentLine = FileLines[line - BaseOffset];
                int startColumn =
                    line == startLine
                    ? bufferRange.Start.Column
                    : BaseOffset;
                int endColumn =
                    line == endLine
                    ? bufferRange.End.Column
                    : currentLine.Length + BaseOffset;

                currentLine =
                    currentLine.Substring(
                        startColumn - BaseOffset,
                        endColumn - startColumn);

                linesInRange.Add(currentLine);
            }

            return linesInRange.ToArray();
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the given position is outside
        /// of the file's buffer extents.
        /// </summary>
        /// <param name="bufferPosition">The position in the buffer to be validated.</param>
        public void ValidatePosition(BufferPosition bufferPosition)
        {
            ValidatePosition(bufferPosition.Line, bufferPosition.Column);
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the given position is outside
        /// of the file's buffer extents.
        /// </summary>
        /// <param name="line">The line to be validated.</param>
        /// <param name="column">The column to be validated.</param>
        /// <seealso cref="BaseOffset"/>
        public void ValidatePosition(int line, int column)
        {
            if (line < BaseOffset || line > FileLines.Count + BaseOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(line),
                    string.Format("Position is outside of file line range (BaseOffset = {0})", BaseOffset));
            }

            string lineString = FileLines[line - BaseOffset];
            int maxColumn = lineString.Length > 0 ? lineString.Length + BaseOffset : BaseOffset;

            if (column < BaseOffset || column > maxColumn)
            {
                throw new ArgumentOutOfRangeException(nameof(column), 
                    string.Format("Position is outside of column range for line {0} (BaseOffset = {1})", line, BaseOffset));
            }
        }

        /// <summary>
        /// Applies the provided FileChange to the file's contents
        /// </summary>
        /// <param name="fileChange">The FileChange to apply to the file's contents.</param>
        public void ApplyChange(DocumentChange fileChange)
        {
            ValidatePosition(fileChange.Line, fileChange.Offset);
            ValidatePosition(fileChange.EndLine, fileChange.EndOffset);

            // Break up the change lines
            string[] changeLines = fileChange.InsertString.Split('\n');

            // Get the first fragment of the first line
            string firstLineFragment =
                FileLines[fileChange.Line - BaseOffset]
                    .Substring(0, fileChange.Offset - BaseOffset);

            // Get the last fragment of the last line
            string endLine = FileLines[fileChange.EndLine - BaseOffset];
            string lastLineFragment =
                endLine.Substring(
                    fileChange.EndOffset - BaseOffset,
                    (FileLines[fileChange.EndLine - BaseOffset].Length - fileChange.EndOffset) + BaseOffset);

            // Remove the old lines
            for (int i = 0; i <= fileChange.EndLine - fileChange.Line; i++)
            {
                FileLines.RemoveAt(fileChange.Line - BaseOffset);
            }

            // Build and insert the new lines
            int currentLineNumber = fileChange.Line;
            for (int changeIndex = 0; changeIndex < changeLines.Length; changeIndex++)
            {
                // Since we split the lines above using \n, make sure to
                // trim the ending \r's off as well.
                string finalLine = changeLines[changeIndex].TrimEnd('\r');

                // Should we add first or last line fragments?
                if (changeIndex == 0)
                {
                    // Append the first line fragment
                    finalLine = firstLineFragment + finalLine;
                }
                if (changeIndex == changeLines.Length - BaseOffset)
                {
                    // Append the last line fragment
                    finalLine = finalLine + lastLineFragment;
                }

                FileLines.Insert(currentLineNumber - BaseOffset, finalLine);
                currentLineNumber++;
            }
        }

        /*
         // TODO: does this need to be moved to the ScriptFile base class?
         // TODO: does not take BaseOffset into account.


                /// <summary>
                /// Calculates the zero-based character offset of a given
                /// line and column position in the file.
                /// </summary>
                /// <param name="lineNumber">The 1-based line number from which the offset is calculated.</param>
                /// <param name="columnNumber">The 1-based column number from which the offset is calculated.</param>
                /// <returns>The zero-based offset for the given file position.</returns>
                public int GetOffsetAtPosition(int lineNumber, int columnNumber)
                {
                    Validate.IsWithinRange("lineNumber", lineNumber, 1, FileLines.Count);
                    Validate.IsGreaterThan("columnNumber", columnNumber, 0);

                    int offset = 0;

                    for (int i = 0; i < lineNumber; i++)
                    {
                        if (i == lineNumber - 1)
                        {
                            // Subtract 1 to account for 1-based column numbering
                            offset += columnNumber - 1;
                        }
                        else
                        {
                            // Add an offset to account for the current platform's newline characters
                            offset += FileLines[i].Length + Environment.NewLine.Length;
                        }
                    }

                    return offset;
                }
        */
        /// <summary>
        /// Calculates a FilePosition relative to a starting BufferPosition
        /// using the given line and column offset.
        /// </summary>
        /// <param name="originalPosition">The original BufferPosition from which an new position should be calculated.</param>
        /// <param name="lineOffset">The line offset added to the original position in this file.</param>
        /// <param name="columnOffset">The column offset added to the original position in this file.</param>
        /// <returns>A new FilePosition instance with the resulting line and column number.</returns>
        /// <seealso cref="BaseOffset"/>
        public DocumentPosition CalculatePosition(
            BufferPosition originalPosition,
            int lineOffset,
            int columnOffset)
        {
            int newLine = originalPosition.Line + lineOffset,
                newColumn = originalPosition.Column + columnOffset;

            ValidatePosition(newLine, newColumn);

            string scriptLine = FileLines[newLine - BaseOffset];
            newColumn = Math.Min(scriptLine.Length + BaseOffset, newColumn);

            return new DocumentPosition(this, newLine, newColumn);
        }

        /// <summary>
        /// Calculates the line and column number position based
        /// on the given buffer offset.
        /// </summary>
        /// <param name="bufferOffset">The buffer offset to convert.</param>
        /// <returns>A new BufferPosition containing the position of the offset.</returns>
        /// <seealso cref="BaseOffset"/>
        public BufferPosition GetPositionAtOffset(int bufferOffset)
        {
            BufferRange bufferRange =
                GetRangeBetweenOffsets(
                    bufferOffset, bufferOffset);

            return bufferRange.Start;
        }

        /// <summary>
        /// Calculates the line and column number range based on
        /// the given start and end buffer offsets.
        /// </summary>
        /// <param name="startOffset">The start offset of the range.</param>
        /// <param name="endOffset">The end offset of the range.</param>
        /// <returns>A new BufferRange containing the positions in the offset range.</returns>
        /// <seealso cref="BaseOffset"/>
        public BufferRange GetRangeBetweenOffsets(int startOffset, int endOffset)
        {
            bool foundStart = false;
            int currentOffset = 0;
            int searchedOffset = startOffset;

            BufferPosition startPosition = new BufferPosition(0, 0);    // TODO: is 0,0 correct or should it be -1,-1?
            BufferPosition endPosition = startPosition;

            int line = 0;
            while (line < FileLines.Count)
            {
                if (searchedOffset <= currentOffset + FileLines[line].Length)
                {
                    int column = searchedOffset - currentOffset;

                    // Have we already found the start position?
                    if (foundStart)
                    {
                        // Assign the end position and end the search
                        endPosition = new BufferPosition(line + BaseOffset, column + BaseOffset);
                        break;
                    }
                    else
                    {
                        startPosition = new BufferPosition(line + BaseOffset, column + BaseOffset);

                        // Do we only need to find the start position?
                        if (startOffset == endOffset)
                        {
                            endPosition = startPosition;
                            break;
                        }
                        else
                        {
                            // Since the end offset can be on the same line,
                            // skip the line increment and continue searching
                            // for the end position
                            foundStart = true;
                            searchedOffset = endOffset;
                            continue;
                        }
                    }
                }

                // Increase the current offset and include newline length
                currentOffset += FileLines[line].Length + Environment.NewLine.Length;
                line++;
            }

            return new BufferRange(startPosition, endPosition);
        }

        /// <summary>
        /// Set the script files contents
        /// </summary>
        /// <param name="fileContents"></param>
        public void SetFileContents(string fileContents)
        {
            // Split the file contents into lines and trim
            // any carriage returns from the strings.
            FileLines =
                fileContents
                    .Split('\n')
                    .Select(line => line.TrimEnd('\r'))
                    .ToList();
        }

        #endregion
    }
}
