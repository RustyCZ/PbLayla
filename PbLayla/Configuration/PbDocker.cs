using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PbLayla.Configuration;

public class PbDocker
{
    public string DockerHost { get; set; } = Environment.OSVersion.Platform == PlatformID.Win32NT
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";

    public string Image { get; set; } = "passivbot:latest";

    public string ConfigsPath { get; set; } = "/passivbot/configs";

    public string ApiKeysPath { get; set; } = "/passivbot/api-keys.json";

    public string? MountConfigsPath { get; set; } = "/home/passivbot/configs";

    public string? MountApiKeysPath { get; set; } = "/home/passivbot/api-keys.json";
}
