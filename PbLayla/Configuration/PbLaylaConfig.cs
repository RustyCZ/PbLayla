namespace PbLayla.Configuration
{
    public class PbLaylaConfig
    {
        public List<Account> Accounts { get; set; } = new List<Account>();
        public RiskMonitorConfig RiskMonitor { get; set; } = new RiskMonitorConfig();
        public PbDocker Docker { get; set; } = new PbDocker();
        public Dori Dori { get; set; } = new Dori();
    }
}