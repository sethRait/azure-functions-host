﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Workers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Workers
{
    public class WorkerConsoleLogServiceTests
    {
        private IScriptEventManager _eventManager;
        private IProcessRegistry _processRegistry;
        private TestLogger _testUserLogger = new TestLogger("Host.Function.Console");
        private TestLogger _testSystemLogger = new TestLogger("Worker.rpcWorkerProcess");
        private WorkerConsoleLogService _workerConsoleLogService;
        private WorkerConsoleLogSource _workerConsoleLogSource;
        private Mock<IServiceProvider> _serviceProviderMock;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WorkerConsoleLogService_ConsoleLogs_LogLevel_Expected(bool useStdErrForErrorLogsOnly)
        {
            // Arrange
            _workerConsoleLogSource = new WorkerConsoleLogSource();
            _eventManager = new ScriptEventManager();
            _processRegistry = new EmptyProcessRegistry();
            _workerConsoleLogService = new WorkerConsoleLogService(_testUserLogger, _workerConsoleLogSource);
            _serviceProviderMock = new Mock<IServiceProvider>(MockBehavior.Strict);

            WorkerProcess workerProcess = new TestWorkerProcess(_eventManager, _processRegistry, _testSystemLogger, _workerConsoleLogSource, null, _serviceProviderMock.Object, useStdErrForErrorLogsOnly);
            workerProcess.ParseErrorMessageAndLog("Test Message No keyword");
            workerProcess.ParseErrorMessageAndLog("Test Error Message");
            workerProcess.ParseErrorMessageAndLog("Test Warning Message");
            workerProcess.ParseErrorMessageAndLog("LanguageWorkerConsoleLog[Test Worker Message No keyword]");
            workerProcess.ParseErrorMessageAndLog("LanguageWorkerConsoleLog[Test Worker Error Message]");
            workerProcess.ParseErrorMessageAndLog("LanguageWorkerConsoleLog[Test Worker Warning Message]");

            // Act
            _ = _workerConsoleLogService.ProcessLogs().ContinueWith(t => { });
            await _workerConsoleLogService.StopAsync(System.Threading.CancellationToken.None);
            var userLogs = _testUserLogger.GetLogMessages();
            var systemLogs = _testSystemLogger.GetLogMessages();

            // Assert
            Assert.True(userLogs.Count == 3);
            Assert.True(systemLogs.Count == 3);

            VerifyLogLevel(userLogs, "Test Error Message", LogLevel.Error);
            VerifyLogLevel(systemLogs, "[Test Worker Error Message]", LogLevel.Error);
            VerifyLogLevel(userLogs, "Test Warning Message", LogLevel.Warning);
            VerifyLogLevel(systemLogs, "[Test Worker Warning Message]", LogLevel.Warning);

            if (useStdErrForErrorLogsOnly)
            {
                VerifyLogLevel(userLogs, "Test Message No keyword", LogLevel.Error);
                VerifyLogLevel(systemLogs, "[Test Worker Message No keyword]", LogLevel.Error);
            }
            else
            {
                VerifyLogLevel(userLogs, "Test Message No keyword", LogLevel.Information);
                VerifyLogLevel(systemLogs, "[Test Worker Message No keyword]", LogLevel.Information);
            }
        }

        private static void VerifyLogLevel(IList<LogMessage> allLogs, string msg, LogLevel expectedLevel)
        {
            var message = allLogs.Where(l => l.FormattedMessage.Contains(msg)).FirstOrDefault();
            Assert.NotNull(message);
            Assert.DoesNotContain(WorkerConstants.LanguageWorkerConsoleLogPrefix, message.FormattedMessage);
            Assert.Equal(expectedLevel, message.Level);
        }
    }
}
