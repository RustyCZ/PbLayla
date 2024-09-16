using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PbLayla.Configuration;

public class Dori
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ExecutionFailInterval { get; set; } = TimeSpan.FromMinutes(1);
}
