﻿using System.Text.Json;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using Bybit.Net.Objects.Models.V5;
using Microsoft.Extensions.Logging;
using PbLayla.Helpers;
using PbLayla.Mapping;
using PbLayla.Model;
using Balance = PbLayla.Model.Balance;
using OrderSide = Bybit.Net.Enums.OrderSide;
using PbPosition = PbLayla.Model.Position;
using PbPositionSide = PbLayla.Model.PositionSide;
using PositionIdx = Bybit.Net.Enums.V5.PositionIdx;

namespace PbLayla.Exchanges;

public abstract class BybitPbFuturesRestClientBase : IPbFuturesRestClient
{
    private readonly IBybitRestClient m_bybitRestClient;
    private readonly Category m_category;
    private readonly ILogger<BybitPbFuturesRestClientBase> m_logger;

    protected BybitPbFuturesRestClientBase(IBybitRestClient bybitRestClient,
        ILogger<BybitPbFuturesRestClientBase> logger)
    {
        m_bybitRestClient = bybitRestClient;
        m_logger = logger;
        m_category = Category.Linear;
    }

    public abstract Task<Balance> GetBalancesAsync(CancellationToken cancel = default);

    public async Task<PbPosition[]> GetPositionsAsync(CancellationToken cancel = default)
    {
        var positions = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
        {
            List<PbPosition> positions = new List<PbPosition>();
            string? cursor = null;
            while (true)
            {
                var positionResult = await m_bybitRestClient.V5Api.Trading.GetPositionsAsync(
                    m_category,
                    settleAsset: Assets.QuoteAsset,
                    cursor: cursor,
                    limit: 200,
                    ct: cancel);
                if (!positionResult.GetResultOrError(out var data, out var error))
                    throw new InvalidOperationException(error.Message);
                foreach (var bybitPosition in data.List)
                {
                    var position = bybitPosition.ToPosition();
                    if (position == null)
                        m_logger.LogWarning($"Could not convert position for symbol: {bybitPosition.Symbol}");
                    else
                        positions.Add(position);
                }

                if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                    break;
                cursor = data.NextPageCursor;
            }

            return positions.ToArray();
        });

        return positions;
    }

    public async Task<bool> ClosePositionAsync(PbPosition position, CancellationToken cancel = default)
    {
        if (position.Side == PbPositionSide.Buy)
        {
            await ExchangePolicies<BybitResponse<BybitOrderId>>.RetryTooManyVisits
                .ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.CancelAllOrderAsync(
                            category: m_category,
                            symbol: position.Symbol,
                            ct: cancel));
            var sellOrderRes =
                await ExchangePolicies<BybitOrderId>.RetryTooManyVisits.ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                            category: m_category,
                            symbol: position.Symbol,
                            side: OrderSide.Sell,
                            type: NewOrderType.Market,
                            quantity: position.Quantity,
                            price: null,
                            positionIdx: PositionIdx.BuyHedgeMode,
                            reduceOnly: true,
                            timeInForce: TimeInForce.ImmediateOrCancel,
                            ct: cancel));
            if (!sellOrderRes.GetResultOrError(out var sellOrder, out var error))
            {
                m_logger.LogWarning($"{position.Symbol} Failed to place long take profit order: {error}");
                return false;
            }

            var result = await CheckOrderNotCancelled(position.Symbol, sellOrder.OrderId, cancel);
            return result;
        }
        else if (position.Side == PbPositionSide.Sell)
        {
            await ExchangePolicies<BybitResponse<BybitOrderId>>.RetryTooManyVisits
                .ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.CancelAllOrderAsync(
                            category: m_category,
                            symbol: position.Symbol,
                            ct: cancel));
            var buyOrderRes =
                await ExchangePolicies<BybitOrderId>.RetryTooManyVisits.ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                            category: m_category,
                            symbol: position.Symbol,
                            side: OrderSide.Buy,
                            type: NewOrderType.Market,
                            quantity: position.Quantity,
                            price: null,
                            positionIdx: PositionIdx.SellHedgeMode,
                            reduceOnly: true,
                            timeInForce: TimeInForce.ImmediateOrCancel,
                            ct: cancel));
            if (!buyOrderRes.GetResultOrError(out var buyOrder, out var error))
            {
                m_logger.LogWarning($"{position.Symbol} Failed to place short take profit order: {error}");
                return false;
            }

            var result = await CheckOrderNotCancelled(position.Symbol, buyOrder.OrderId, cancel);
            return result;
        }

        return false;
    }

    public async Task<Order[]> GetOrdersAsync(CancellationToken cancel = default)
    {
        var orders = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
        {
            List<Order> orders = new List<Order>();
            string? cursor = null;
            while (true)
            {
                var ordersResult = await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                    m_category,
                    settleAsset: Assets.QuoteAsset,
                    cursor: cursor,
                    ct: cancel);
                if (!ordersResult.GetResultOrError(out var data, out var error))
                    throw new InvalidOperationException(error.Message);
                orders.AddRange(data.List.Select(x => x.ToOrder()));
                if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                    break;
                cursor = data.NextPageCursor;
            }

            return orders.ToArray();
        });

        return orders;
    }

    public async Task CancelOrdersAsync(Order[] orders, CancellationToken cancel = default)
    {
        bool success = true;
        foreach (var order in orders)
        {
            try
            {
                await CancelOrderInnerAsync(order, cancel);
                await Task.Delay(100, cancel);
            }
            catch (InvalidOperationException)
            {
                success = false;
            }
        }
        if (!success)
            throw new InvalidOperationException("Failed to cancel orders");
    }

    protected async Task CancelOrderInnerAsync(Order order, CancellationToken cancel = default)
    {
        m_logger.LogInformation("Cancelling order {orderId}:{symbol}", order.OrderId, order.Symbol);
        var cancelOrderRes = await m_bybitRestClient.V5Api.Trading.CancelOrderAsync(
            category: m_category,
            symbol: order.Symbol,
            orderId: order.OrderId,
            ct: cancel);
        if (!cancelOrderRes.GetResultOrError(out _, out var error))
        {
            m_logger.LogWarning("Failed to cancel order {orderId}:{symbol}: {error}", order.OrderId, order.Symbol,
                error);
            throw new InvalidOperationException(error.Message);
        }

        m_logger.LogInformation("Order {orderId}:{symbol} cancelled", order.OrderId, order.Symbol);
    }

    public async Task<Ticker[]> GetTickersAsync(CancellationToken cancel = default)
    {
        var result = await m_bybitRestClient.V5Api.ExchangeData
            .GetLinearInverseTickersAsync(
                m_category, 
                null, 
                Assets.QuoteAsset, 
                null, 
                cancel);
        if (!result.GetResultOrError(out var data, out var error))
            throw new InvalidOperationException(error.Message);
        var tickers = data.List.Select(x => x.ToTicker()).ToArray();
        return tickers;
    }

    private async Task<bool> CheckOrderNotCancelled(string symbol, string orderId, CancellationToken cancel = default)
    {
        var orderStatusRes = await ExchangePolicies<BybitOrder>
            .RetryTooManyVisitsBybitResponse
            .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                category: m_category,
                symbol: symbol,
                orderId: orderId,
                ct: cancel));
        if (orderStatusRes.GetResultOrError(out var orderStatus, out _))
        {
            var order = orderStatus.List
                .FirstOrDefault(x => string.Equals(x.OrderId, orderId, StringComparison.Ordinal));
            if (order != null && order.Status == Bybit.Net.Enums.V5.OrderStatus.Cancelled)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> PlaceMarketSellHedgeOrderAsync(string symbol, decimal quantity, CancellationToken cancel = default)
    {
        var sellOrderRes = await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
            category: m_category,
            symbol: symbol,
            side: OrderSide.Sell,
            type: NewOrderType.Market,
            quantity: quantity,
            positionIdx: PositionIdx.SellHedgeMode,
            reduceOnly: false,
            timeInForce: TimeInForce.PostOnly,
            ct: cancel);

        if (!sellOrderRes.GetResultOrError(out _, out _))
            return false;

        return true;
    }

    public async Task<bool> ReduceSellHedgeAsync(string symbol, decimal quantity, CancellationToken cancel = default)
    {
        var buyOrderRes = await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
            category: m_category,
            symbol: symbol,
            side: OrderSide.Buy,
            type: NewOrderType.Market,
            quantity: quantity,
            positionIdx: PositionIdx.SellHedgeMode,
            reduceOnly: true,
            timeInForce: TimeInForce.PostOnly,
            ct: cancel);

        if (!buyOrderRes.GetResultOrError(out _, out _))
            return false;

        return true;
    }

    public virtual async Task<TransactionLog[]> GetTransactionLogsAsync(DateTime start, DateTime end, CancellationToken cancel = default)
    {
        var bybitTransactionLogs = new List<BybitTransactionLog>();
        string? cursor = null;
        while (true)
        {
            var result = await m_bybitRestClient.V5Api.Account.GetTransactionHistoryAsync(
                AccountType.Unified,
                Category.Linear,
                Assets.QuoteAsset,
                null,
                null,
                start,
                end,
                50,
                cursor,
                cancel);
            if (!result.GetResultOrError(out var data, out var error))
                throw new InvalidOperationException(error.Message);
            bybitTransactionLogs.AddRange(data.List);
            if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                break;
            cursor = data.NextPageCursor;
            await Task.Delay(100, cancel);
        }
        var transactionLogs = bybitTransactionLogs.Select(x => x.ToTransactionLog()).ToArray();
        return transactionLogs;
    }

    public async Task TransferProfitAsync(decimal quantity, string? fromMemberId, string? toMemberId, CancellationToken cancel)
    {
        if (!string.IsNullOrEmpty(fromMemberId) && !string.IsNullOrEmpty(toMemberId) && !string.Equals(fromMemberId, toMemberId))
        {
            // transfer between sub/main account
            var result = await m_bybitRestClient.V5Api.Account.CreateUniversalTransferAsync(Assets.QuoteAsset,
                quantity,
                fromMemberId,
                toMemberId,
                AccountType.Unified,
                AccountType.Fund,
                null,
                cancel);
            if (!result.GetResultOrError(out _, out var e))
            {
                m_logger.LogWarning("Failed to transfer profit: {error}", e.Message);
                throw new InvalidOperationException(e.Message);
            }
        }
        else
        {
            // internal transfer
            var result =await m_bybitRestClient.V5Api.Account.CreateInternalTransferAsync(Assets.QuoteAsset,
                quantity,
                AccountType.Unified,
                AccountType.Fund,
                null,
                cancel);
            if (!result.GetResultOrError(out _, out var e))
            {
                m_logger.LogWarning("Failed to transfer profit: {error}", e.Message);
                throw new InvalidOperationException(e.Message);
            }
        }
    }
}