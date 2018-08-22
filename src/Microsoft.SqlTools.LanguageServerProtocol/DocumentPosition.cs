//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.SqlTools.LanguageServerProtocol
{
    /// <summary>
    /// Provides details and operations for a buffer position in a
    /// specific file.
    /// </summary>
    public class DocumentPosition : BufferPosition
    {
        #region Private Fields

        private TextDocument document;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new DocumentPosition instance for the 1-based line and
        /// column numbers in the specified file.
        /// </summary>
        /// <param name="document">The document in which the position is located.</param>
        /// <param name="line">The line number in the file.</param>
        /// <param name="column">The column number in the file.</param>
        public DocumentPosition(
            TextDocument document,
            int line,
            int column)
                : base(line, column)
        {
            this.document = document;
        }

        /// <summary>
        /// Creates a new FilePosition instance for the specified file by
        /// copying the specified BufferPosition
        /// </summary>
        /// <param name="document">The document in which the position is located.</param>
        /// <param name="copiedPosition">The original BufferPosition from which the line and column will be copied.</param>
        public DocumentPosition(
            TextDocument document,
            BufferPosition copiedPosition)
                 : this(document, copiedPosition.Line, copiedPosition.Column)
        {
            document.ValidatePosition(copiedPosition);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a FilePosition relative to this position by adding the
        /// provided line and column offset relative to the contents of
        /// the current file.
        /// </summary>
        /// <param name="lineOffset">The line offset to add to this position.</param>
        /// <param name="columnOffset">The column offset to add to this position.</param>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        public DocumentPosition AddOffset(int lineOffset, int columnOffset)
        {
            return document.CalculatePosition(
                this,
                lineOffset,
                columnOffset);
        }

        /// <summary>
        /// Gets a FilePosition for the line and column position
        /// of the beginning of the current line after any initial
        /// whitespace for indentation.
        /// </summary>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        /// <seealso cref="TextDocument.BaseOffset"/>
        public DocumentPosition GetLineStart()
        {
            string scriptLine = document.FileLines[Line - document.BaseOffset];

            int lineStartColumn = document.BaseOffset;
            for (int i = 0; i < scriptLine.Length; i++)
            {
                if (!char.IsWhiteSpace(scriptLine[i]))
                {
                    lineStartColumn = i + document.BaseOffset;
                    break;
                }
            }

            return new DocumentPosition(document, Line, lineStartColumn);
        }

        /// <summary>
        /// Gets a FilePosition for the line and column position
        /// of the end of the current line.
        /// </summary>
        /// <returns>A new FilePosition instance for the calculated position.</returns>
        /// <seealso cref="TextDocument.BaseOffset"/>
        public DocumentPosition GetLineEnd()
        {
            string scriptLine = document.FileLines[Line - document.BaseOffset];
            return new DocumentPosition(document, Line, scriptLine.Length + document.BaseOffset);
        }

        #endregion

    }
}

