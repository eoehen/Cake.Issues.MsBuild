﻿namespace Cake.Issues.MsBuild.LogFileFormat
{
    using System;
    using System.Collections.Generic;
    using Cake.Core.Diagnostics;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging.StructuredLogger;

    /// <summary>
    /// MsBuild binary log file format.
    /// </summary>
    internal class BinaryLogFileFormat : BaseMsBuildLogFileFormat
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryLogFileFormat"/> class.
        /// </summary>
        /// <param name="log">The Cake log instance.</param>
        public BinaryLogFileFormat(ICakeLog log)
            : base(log)
        {
        }

        /// <inheritdoc/>
        public override IEnumerable<IIssue> ReadIssues(
            MsBuildIssuesProvider issueProvider,
            IRepositorySettings repositorySettings,
            MsBuildIssuesSettings issueProviderSettings)
        {
#pragma warning disable SA1123 // Do not place regions within elements
            #region DupFinder Exclusion
#pragma warning restore SA1123 // Do not place regions within elements

            issueProvider.NotNull(nameof(issueProvider));
            repositorySettings.NotNull(nameof(repositorySettings));
            issueProviderSettings.NotNull(nameof(issueProviderSettings));

            #endregion

            var result = new List<IIssue>();

            var binLogReader = new BinLogReader();
            foreach (var record in binLogReader.ReadRecords(issueProviderSettings.LogFileContent))
            {
                var buildEventArgs = record.Args;

                IIssue issue = null;
                if (buildEventArgs is BuildErrorEventArgs buildError)
                {
                    issue = this.GetIssue(buildError, issueProvider, repositorySettings);
                }
                else if (buildEventArgs is BuildWarningEventArgs buildWarning)
                {
                    issue = this.GetIssue(buildWarning, issueProvider, repositorySettings);
                }

                if (issue == null)
                {
                    continue;
                }

                result.Add(issue);
            }

            return result;
        }

        /// <summary>
        /// Returns the column based on the value from a MsBuild log.
        /// </summary>
        /// <param name="column">Raw value from MsBuild log.</param>
        /// <returns>Column number or null if warning or error is not related to a file.</returns>
        private static int? GetColumn(int column)
        {
            // Convert negative column numbers or column number 0 to null
            if (column <= 0)
            {
                return null;
            }

            return column;
        }

        /// <summary>
        /// Returns the line based on the value from a MsBuild log.
        /// </summary>
        /// <param name="line">Raw value from MsBuild log.</param>
        /// <returns>Line number or null if warning or error is not related to a file.</returns>
        private static int? GetLine(int line)
        {
            // Convert negative line numbers or line number 0 to null
            if (line <= 0)
            {
                return null;
            }

            return line;
        }

        /// <summary>
        /// Returns an issue for a build error.
        /// </summary>
        /// <param name="buildError">Error for which the issue should be returned.</param>
        /// <param name="issueProvider">Issue provider instance.</param>
        /// <param name="repositorySettings">Repository settings to use.</param>
        /// <returns>Issue instance or null, if the <paramref name="buildError"/> could not be parsed.</returns>
        private IIssue GetIssue(
            BuildErrorEventArgs buildError,
            MsBuildIssuesProvider issueProvider,
            IRepositorySettings repositorySettings)
        {
            return
                this.GetIssue(
                    buildError.Message,
                    buildError.ProjectFile,
                    buildError.File,
                    buildError.LineNumber,
                    buildError.EndLineNumber,
                    buildError.ColumnNumber,
                    buildError.EndColumnNumber,
                    buildError.Code,
                    issueProvider,
                    repositorySettings);
        }

        /// <summary>
        /// Returns an issue for a build warning.
        /// </summary>
        /// <param name="buildWarning">Warning for which the issue should be returned.</param>
        /// <param name="issueProvider">Issue provider instance.</param>
        /// <param name="repositorySettings">Repository settings to use.</param>
        /// <returns>Issue instance or null, if the <paramref name="buildWarning"/> could not be parsed.</returns>
        private IIssue GetIssue(
            BuildWarningEventArgs buildWarning,
            MsBuildIssuesProvider issueProvider,
            IRepositorySettings repositorySettings)
        {
            return
                this.GetIssue(
                    buildWarning.Message,
                    buildWarning.ProjectFile,
                    buildWarning.File,
                    buildWarning.LineNumber,
                    buildWarning.EndLineNumber,
                    buildWarning.ColumnNumber,
                    buildWarning.EndColumnNumber,
                    buildWarning.Code,
                    issueProvider,
                    repositorySettings);
        }

        /// <summary>
        /// Returns an issue for values from an MsBuild log.
        /// </summary>
        /// <param name="issueProvider">Issue provider instance.</param>
        /// <param name="repositorySettings">Repository settings to use.</param>
        /// <returns>Issue instance or null, if the values could not be parsed.</returns>
        private IIssue GetIssue(
            string message,
            string projectFile,
            string file,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            string code,
            MsBuildIssuesProvider issueProvider,
            IRepositorySettings repositorySettings)
        {
            // Ignore warnings or errors without a message.
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var projectFileRelativePath = this.GetProject(projectFile, repositorySettings);

            // Read affected file from the warning or error.
            var (result, fileName) = this.TryGetFile(file, projectFile, repositorySettings);
            if (!result)
            {
                return null;
            }

            var line = GetLine(lineNumber);
            var endLine = GetLine(endLineNumber);
            var column = GetColumn(columnNumber);
            var endColumn = GetColumn(endColumnNumber);
            var rule = code;

            // Determine rule URL.
            Uri ruleUrl = null;
            if (!string.IsNullOrWhiteSpace(rule))
            {
                ruleUrl = MsBuildRuleUrlResolver.Instance.ResolveRuleUrl(rule);
            }

            // Build issue.
            return
                IssueBuilder
                    .NewIssue(message, issueProvider)
                    .WithPriority(IssuePriority.Warning)
                    .InProject(projectFileRelativePath, System.IO.Path.GetFileNameWithoutExtension(projectFileRelativePath))
                    .InFile(fileName, line, endLine, column, endColumn)
                    .OfRule(rule, ruleUrl)
                    .Create();
        }

        /// <summary>
        /// Determines the project from a value in a MsBuild log.
        /// </summary>
        /// <param name="project">Raw value from the MsBuild log.</param>
        /// <param name="repositorySettings">Repository settings to use.</param>
        /// <returns>Relative path to the project.</returns>
        private string GetProject(
            string project,
            IRepositorySettings repositorySettings)
        {
            // Validate project path and make relative to repository root.
            return this.ValidateFilePath(project, repositorySettings).FilePath;
        }

        /// <summary>
        /// Reads the affected file path from a value in a MsBuild log.
        /// </summary>
        /// <param name="fileName">Raw value for file path from MsBuild log.</param>
        /// <param name="project">Raw value for project path from the MsBuild log.</param>
        /// <param name="repositorySettings">Repository settings to use.</param>
        /// <returns>True if the file path could be parsed and the full path to the affected file.</returns>
        private (bool successful, string fileName) TryGetFile(
            string fileName,
            string project,
            IRepositorySettings repositorySettings)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (true, fileName);
            }

            // If not absolute path, combine with file path from project file.
            if (!fileName.IsFullPath())
            {
                var projectDirectory = System.IO.Path.GetDirectoryName(project);
                fileName = System.IO.Path.Combine(projectDirectory, fileName);
            }

            // Validate file path and make relative to repository root.
            return this.ValidateFilePath(fileName, repositorySettings);
        }
    }
}
