namespace PbLayla.PbLifeCycle;

public class DockerPbLifeCycleControllerOptions
{
    public string DockerHost { get; set; } = Environment.OSVersion.Platform == PlatformID.Win32NT
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";

    public string Image { get; set; } = "passivbot:latest";

    public string ConfigsPath { get; set; } = "/passivbot/configs";

    public string ApiKeysPath { get; set; } = "/passivbot/api-keys.json";

    public string? MountConfigsPath { get; set; }

    public string? MountApiKeysPath { get; set; }
}