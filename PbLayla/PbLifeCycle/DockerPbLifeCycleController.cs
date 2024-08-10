using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Processing;

namespace PbLayla.PbLifeCycle;

public class DockerPbLifeCycleController : IPbLifeCycleController
{
    private readonly IOptions<DockerPbLifeCycleControllerOptions> m_options;
    private const string PbLaylaLabel = "pblayla";
    private const string PbLaylaAccountState = "pblayla.account_state";
    private readonly ILogger<DockerPbLifeCycleController> m_logger;

    public DockerPbLifeCycleController(IOptions<DockerPbLifeCycleControllerOptions> options, ILogger<DockerPbLifeCycleController> logger)
    {
        if(string.IsNullOrEmpty(options.Value.MountApiKeysPath))
            throw new ArgumentException("MountApiKeysPath is required");
        if(string.IsNullOrEmpty(options.Value.MountConfigsPath))
            throw new ArgumentException("MountConfigsPath is required");
        m_options = options;
        m_logger = logger;
    }

    private string GetPbLaylaAccountLabel(string accountName)
    {
        return FormattableString.Invariant($"pblayla.account.{accountName}");
    }

    public async Task<bool> StartPbAsync(string accountName, string configFileName, AccountState accountState, CancellationToken cancel = default)
    {
        try
        {
            using DockerClient client = new DockerClientConfiguration(
                    new Uri(m_options.Value.DockerHost))
                .CreateClient();
            await StopPbAsync(accountName, cancel);
            var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Image = m_options.Value.Image,
                Name = FormattableString.Invariant($"passivbot_{accountName}"),
                Labels = new Dictionary<string, string>()
                {
                    { PbLaylaLabel, PbLaylaLabel },
                    { PbLaylaAccountState, accountState.ToString() },
                    { GetPbLaylaAccountLabel(accountName), accountName },
                },
                HostConfig = new HostConfig()
                {
                    Binds = new List<string>()
                    {
                        FormattableString.Invariant($"{m_options.Value.MountConfigsPath}:{m_options.Value.ConfigsPath}"),
                        FormattableString.Invariant($"{m_options.Value.MountApiKeysPath}:{m_options.Value.ApiKeysPath}"),
                    },
                    RestartPolicy = new RestartPolicy
                    {
                        Name = RestartPolicyKind.UnlessStopped,
                    },
                },
                Cmd = new List<string>()
                {
                    "python",
                    "passivbot_multi.py",
                    FormattableString.Invariant($"configs/{configFileName}")
                },
            }, cancel);
            bool started = await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancel);
            if (started)
                m_logger.LogInformation("Started container '{ContainerId}' with config '{Config}' and account state '{AccountState}'", response.ID, configFileName, accountState);
            else
                m_logger.LogError("Failed to start container '{ContainerId}' with config '{Config}' and account state '{AccountState}'", response.ID, configFileName, accountState);
            return started;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to start passivbot with config '{Config}' and account state '{AccountState}'", configFileName, accountState);
            return false;
        }
    }

    public async Task<AccountState> FindStartedAccountStateAsync(string accountName, CancellationToken cancel = default)
    {
        try
        {
            using DockerClient client = new DockerClientConfiguration(
                    new Uri(m_options.Value.DockerHost))
                .CreateClient();
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>()
                {
                    {
                        "label",
                        new Dictionary<string, bool>()
                        {
                            { GetPbLaylaAccountLabel(accountName), true}
                        }
                    }
                }
            }, cancel);
            var container = containers.FirstOrDefault();
            if (container == null)
                return AccountState.Unknown;
            if (!container.Labels.TryGetValue(PbLaylaAccountState, out var accountStateStr))
                return AccountState.Unknown;
            if (!Enum.TryParse<AccountState>(accountStateStr, out var accountState))
                return AccountState.Unknown;
            return accountState;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to find started account state");
            return AccountState.Unknown;
        }
    }

    public async Task<bool> StopPbAsync(string accountName, CancellationToken cancel = default)
    {
        try
        {
            using DockerClient client = new DockerClientConfiguration(
                    new Uri(m_options.Value.DockerHost))
                .CreateClient();
            var containerIds = await FindPbLaylaContainers(client, accountName, cancel);
            foreach (var containerId in containerIds)
                await StopAndRemovePbAsync(client, containerId, cancel);
            return true;
        }
        catch (Exception e)
        {
            m_logger.LogWarning(e, "Failed to stop and remove containers");
            return false;
        }
    }

    private async Task<string[]> FindPbLaylaContainers(DockerClient client, string accountName, CancellationToken cancel)
    {
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>()
            {
                {
                    "label",
                    new Dictionary<string, bool>()
                    {
                        { GetPbLaylaAccountLabel(accountName), true}
                    }
                }
            }
        }, cancel);
        var containerIds = containers.Select(c => c.ID).ToArray();
        return containerIds;
    }

    private async Task StopAndRemovePbAsync(DockerClient client, string containerId, CancellationToken cancel)
    {
        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters
        {
            WaitBeforeKillSeconds = 2,
        }, cancel);
        await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = true,
        }, cancel);
        m_logger.LogInformation("Stopped and removed container {ContainerId}", containerId);
    }
}