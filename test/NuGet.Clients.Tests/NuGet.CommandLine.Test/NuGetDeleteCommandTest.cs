﻿using System;
using System.IO;
using System.Net;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetDeleteCommandTest
    {
        private const string ApiKeyHeader = "X-NuGet-ApiKey";
        private static readonly string NuGetExePath = Util.GetNuGetExePath();

        // Tests deleting a package from a source that is a file system directory.
        [Fact]
        public void DeleteCommand_DeleteFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));
                File.SetAttributes(packageFileName,
                    File.GetAttributes(packageFileName) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                File.WriteAllText(Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec"), "dummy text");
                Assert.True(Directory.Exists(packageVersionFolder.FullName));
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                //The specific version folder should be gone.
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFileFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                var dummyNuspec = Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec");
                File.WriteAllText(dummyNuspec, "dummy text");
                File.SetAttributes(dummyNuspec, File.GetAttributes(dummyNuspec) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        // Same as DeleteCommand_DeleteFromFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void DeleteCommand_DeleteFromFileSystemSourceUnixStyle()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var windowsSource = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string source = ((string)windowsSource).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", windowsSource);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    "-Source",
                    source,
                    "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    $"delete testPackage1 1.1.0 -Source {source} -NonInteractive",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromHttpSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var server = new MockServer())
            {
                server.Start();
                bool deleteRequestIsCalled = false;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    return HttpStatusCode.OK;
                });

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", server.Uri + "nuget", "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.True(deleteRequestIsCalled);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsThirdArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var server = new MockServer())
            {
                server.Delete.Add("/nuget/testPackage1/1.1", r =>
                {
                    var h = r.Headers[ApiKeyHeader];
                    if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return HttpStatusCode.Unauthorized;
                    }

                    return HttpStatusCode.OK;
                });

                server.Start();

                // Act
                var args = new[] {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    testApiKey,
                    "-Source",
                    server.Uri + "nuget",
                    "-NonInteractive"
                };

                var result = CommandRunner.Run(
                    NuGetExePath,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                server.Stop();

                // Assert
                Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Item2);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsNamedArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var server = new MockServer())
            {
                server.Delete.Add("/nuget/testPackage1/1.1", r =>
                {
                    var h = r.Headers[ApiKeyHeader];
                    if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return HttpStatusCode.Unauthorized;
                    }

                    return HttpStatusCode.OK;
                });

                server.Start();

                // Act
                var args = new[]
                {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    "should-be-ignored",  // The named argument is preferred over the positional argument.
                    "-ApiKey",
                    testApiKey,
                    "-Source",
                    server.Uri + "nuget",
                    "-NonInteractive"
                };

                var result = CommandRunner.Run(
                    NuGetExePath,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                server.Stop();

                // Assert
                Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Item2);
            }
        }

        [Theory]
        [InlineData("{0}index.json")] // package source url
        [InlineData("{0}push")] // delete package endpoint
        public void DeleteCommand_WithApiKeyFromConfig(string configKeyFormatString)
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var testFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                using (var server = new MockServer())
                {
                    // Server setup
                    var indexJson = Util.CreateIndexJson();

                    Util.AddFlatContainerResource(indexJson, server);
                    Util.AddPublishResource(indexJson, server);

                    server.Get.Add("/index.json", r =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 200;
                            response.ContentType = "text/javascript";
                            MockServer.SetResponseContent(response, indexJson.ToString());
                        });
                    });

                    server.Delete.Add("/push/testPackage1/1.1", r =>
                    {
                        var h = r.Headers[ApiKeyHeader];
                        if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return HttpStatusCode.Unauthorized;
                        }

                        return HttpStatusCode.OK;
                    });

                    server.Start();

                    var configKey = string.Format(configKeyFormatString, server.Uri);

                    var config = $@"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <packageSources>
        <add key='nuget.org' value='{server.Uri}index.json' protocolVersion='3' />
    </packageSources>
    <apikeys>
        <add key='{configKey}' value='{Configuration.EncryptionUtility.EncryptString(testApiKey)}' />
    </apikeys>
</configuration>";

                    var configFileName = Path.Combine(testFolder, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    // Act
                    var args = new[]
                    {
                        "delete",
                        "testPackage1",
                        "1.1.0",
                        "-Source",
                        "nuget.org",
                        "-ConfigFile",
                        configFileName,
                        "-NonInteractive"
                    };

                    var result = CommandRunner.Run(
                        NuGetExePath,
                        Directory.GetCurrentDirectory(),
                        string.Join(" ", args),
                        waitForExit: true);

                    server.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Item2);
                }
            }
        }
    }
}
