using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PbLayla.Processing;

public interface IRiskMonitor
{
    Task ExecuteAsync(CancellationToken cancel = default);
}