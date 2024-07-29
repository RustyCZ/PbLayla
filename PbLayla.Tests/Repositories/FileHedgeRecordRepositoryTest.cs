using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PbLayla.Model;
using PbLayla.PbLifeCycle;
using PbLayla.Repositories;
using Xunit;

namespace PbLayla.Tests.Repositories;

[TestSubject(typeof(FileHedgeRecordRepository))]
public class FileHedgeRecordRepositoryTest
{
    [Fact]
    public async Task HedgeRecordShouldBeSavedAsync()
    {
        var options = new FileHedgeRecordRepositoryOptions
        {
            AccountName = Guid.NewGuid().ToString("N"),
            FileDirectory = "TestDirectory",
            MaxHistory = TimeSpan.FromDays(14)
        };
        var logger = new Logger<DockerPbLifeCycleController>(new LoggerFactory());
        var repository = new FileHedgeRecordRepository(Options.Create(options), logger);
        var utcNow = DateTime.UtcNow;
        var position = new Position
        {
            Symbol = "TestSymbol",
            Quantity = 10.5m,
            UnrealizedPnl = -10,
            Side = PositionSide.Sell,
            UpdateTime = utcNow,
            CreateTime = utcNow,
            TradeMode = TradeMode.CrossMargin,
            AveragePrice = 5.5m,
        };

        await repository.AddClosedHedgePositionAsync(position);

        var recordsCount = await repository.ClosedHedgesCountAsync(TimeSpan.FromDays(1));
        Assert.Equal(1, recordsCount);
        var repository2 = new FileHedgeRecordRepository(Options.Create(options), logger);
        var recordsCount2 = await repository2.ClosedHedgesCountAsync(TimeSpan.FromDays(1));
        Assert.Equal(1, recordsCount2);
    }

    [Fact]
    public async Task HedgeRecordShouldNotBeCounted()
    {
        var options = new FileHedgeRecordRepositoryOptions
        {
            AccountName = Guid.NewGuid().ToString("N"),
            FileDirectory = "TestDirectory",
            MaxHistory = TimeSpan.FromDays(14)
        };
        var logger = new Logger<DockerPbLifeCycleController>(new LoggerFactory());
        var repository = new FileHedgeRecordRepository(Options.Create(options), logger);
        var utcNow = DateTime.UtcNow;
        var position = new Position
        {
            Symbol = "TestSymbol",
            Quantity = 10.5m,
            UnrealizedPnl = -10,
            Side = PositionSide.Sell,
            UpdateTime = utcNow,
            CreateTime = utcNow,
            TradeMode = TradeMode.CrossMargin,
            AveragePrice = 5.5m,
        };

        await repository.AddClosedHedgePositionAsync(position);
        await Task.Delay(TimeSpan.FromSeconds(2));
        utcNow = DateTime.UtcNow;
        var position2 = new Position
        {
            Symbol = "TestSymbol",
            Quantity = 10.5m,
            UnrealizedPnl = -10,
            Side = PositionSide.Sell,
            UpdateTime = utcNow,
            CreateTime = utcNow,
            TradeMode = TradeMode.CrossMargin,
            AveragePrice = 5.5m,
        };
        await repository.AddClosedHedgePositionAsync(position2);
        var recordsCount = await repository.ClosedHedgesCountAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, recordsCount);
        recordsCount = await repository.ClosedHedgesCountAsync(TimeSpan.FromDays(1));
        Assert.Equal(2, recordsCount);
    }
}