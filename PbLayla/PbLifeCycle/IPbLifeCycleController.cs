using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PbLayla.Processing;

namespace PbLayla.PbLifeCycle;

public interface IPbLifeCycleController
{
    Task<bool> StartPbAsync(string accountName, string configFileName, AccountState accountState, CancellationToken cancel = default);
    Task<AccountState> FindStartedAccountStateAsync(string accountName, CancellationToken cancel = default);
    Task<bool> StopPbAsync(string accountName, CancellationToken cancel = default);
}