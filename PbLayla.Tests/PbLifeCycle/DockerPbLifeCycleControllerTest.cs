using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.PbLifeCycle;
using PbLayla.Processing;
using Xunit;

namespace PbLayla.Tests.PbLifeCycle;

[TestSubject(typeof(DockerPbLifeCycleController))]
public class DockerPbLifeCycleControllerTest
{

    [Fact(Skip = "Manual")]
    public async Task StartPbAsync()
    {
        const string mountConfigsPath = "";
        const string mountApiKeysPath = "";
        var options = new DockerPbLifeCycleControllerOptions
        {
            MountConfigsPath = mountConfigsPath,
            MountApiKeysPath = mountApiKeysPath
        };
        var logger = new Logger<DockerPbLifeCycleController>(new LoggerFactory());
        var sut = new DockerPbLifeCycleController(Options.Create(options), logger);

        const string config = "test.hjson";
        const string accountName = "test";
        var started = await sut.StartPbAsync(accountName, config, AccountState.Normal);
        var accountState = await sut.FindStartedAccountStateAsync(accountName);
        var stopped = await sut.StopPbAsync(accountName);

        Assert.True(started);
        Assert.True(stopped);
        Assert.Equal(AccountState.Normal, accountState);
    }
}