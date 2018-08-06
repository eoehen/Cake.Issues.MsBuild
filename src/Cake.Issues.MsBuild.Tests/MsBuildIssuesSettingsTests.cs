﻿namespace Cake.Issues.MsBuild.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Cake.Core.IO;
    using Cake.Issues.MsBuild.LogFileFormat;
    using Cake.Testing;
    using Shouldly;
    using Testing;
    using Xunit;

    public sealed class MsBuildIssuesSettingsTests
    {
        public sealed class TheCtor
        {
            [Fact]
            public void Should_Throw_If_LogFilePath_Is_Null()
            {
                // Given
                FilePath logFilePath = null;
                var format = new XmlFileLoggerLogFileFormat(new FakeLog());

                // When
                var result = Record.Exception(() => new MsBuildIssuesSettings(logFilePath, format));

                // Then
                result.IsArgumentNullException("logFilePath");
            }

            [Fact]
            public void Should_Throw_If_LogContent_Is_Null()
            {
                // Given
                byte[] logFileContent = null;
                var format = new XmlFileLoggerLogFileFormat(new FakeLog());

                // When
                var result = Record.Exception(() => new MsBuildIssuesSettings(logFileContent, format));

                // Then
                result.IsArgumentNullException("logFileContent");
            }

            [Fact]
            public void Should_Throw_If_LogContent_Is_Empty()
            {
                // Given
                byte[] logFileContent = Array.Empty<byte>();
                var format = new XmlFileLoggerLogFileFormat(new FakeLog());

                // When
                var result = Record.Exception(() => new MsBuildIssuesSettings(logFileContent, format));

                // Then
                result.IsArgumentException("logFileContent");
            }

            [Fact]
            public void Should_Set_LogContent()
            {
                // Given
                var logFileContent = "Foo".ToByteArray();
                var format = new XmlFileLoggerLogFileFormat(new FakeLog());

                // When
                var settings = new MsBuildIssuesSettings(logFileContent, format);

                // Then
                settings.LogFileContent.ShouldBe(logFileContent);
            }

            [Fact]
            public void Should_Set_LogContent_From_LogFilePath()
            {
                // Given
                var format = new XmlFileLoggerLogFileFormat(new FakeLog());
                using (var tempFile = new ResourceTempFile("Cake.Issues.MsBuild.Tests.Testfiles.XmlFileLoggerLogFileFormat.FullLog.xml"))
                {
                    // When
                    var settings = new MsBuildIssuesSettings(tempFile.FileName, format);

                    // Then
                    settings.LogFileContent.ShouldBe(tempFile.Content);
                }
            }
        }
    }
}
