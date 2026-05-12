using Cake.Core.Diagnostics;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Fixtures;

public sealed class TestLogTests
{
    [Test]
    public async Task Write_Should_Capture_LogEntry()
    {
        var log = new TestLog();

        log.Write(Verbosity.Normal, LogLevel.Information, "hello {0}", "world");

        await Assert.That(log.Entries.Count).IsEqualTo(1);
        await Assert.That(log.Entries[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(log.Entries[0].Message).Contains("hello world");
    }

    [Test]
    public async Task HasMessage_Should_Return_True_When_Message_Contains_Substring()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Error, "something went wrong: disk full");

        await Assert.That(log.HasMessage(LogLevel.Error, "disk full")).IsTrue();
    }

    [Test]
    public async Task HasMessage_Should_Return_False_When_Message_Does_Not_Contain_Substring()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Information, "all good");

        await Assert.That(log.HasMessage(LogLevel.Error, "disk full")).IsFalse();
    }

    [Test]
    public async Task HasNoMessages_Should_Return_True_When_No_Entries_At_Level()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Information, "info only");

        await Assert.That(log.HasNoMessages(LogLevel.Error)).IsTrue();
    }

    [Test]
    public async Task HasNoMessages_Should_Return_False_When_Entries_Exist_At_Level()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Error, "fail");

        await Assert.That(log.HasNoMessages(LogLevel.Error)).IsFalse();
    }

    [Test]
    public async Task Write_Should_Filter_By_Verbosity()
    {
        var log = new TestLog { Verbosity = Verbosity.Quiet };

        log.Write(Verbosity.Verbose, LogLevel.Information, "should be filtered");

        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task ErrorCount_Should_Return_Count_Of_Error_Entries()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Error, "e1");
        log.Write(Verbosity.Normal, LogLevel.Error, "e2");
        log.Write(Verbosity.Normal, LogLevel.Information, "info");

        await Assert.That(log.ErrorCount).IsEqualTo(2);
    }

    [Test]
    public async Task WarningCount_And_InfoCount_Should_Return_Respective_Counts()
    {
        var log = new TestLog();
        log.Write(Verbosity.Normal, LogLevel.Warning, "w1");
        log.Write(Verbosity.Normal, LogLevel.Information, "i1");
        log.Write(Verbosity.Normal, LogLevel.Information, "i2");

        await Assert.That(log.WarningCount).IsEqualTo(1);
        await Assert.That(log.InfoCount).IsEqualTo(2);
    }
}
