using Bybit.Net.Objects.Models.V5;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using PbLayla.Helpers;
using Polly;
using Polly.Retry;

namespace PbLayla.Exchanges;

public static class ExchangePolicies
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger("Policies");
    private static readonly Random Random = new Random();
    private static readonly object Lock = new object();

    private static TimeSpan GetRandomDelay(int maxSeconds)
    {
        lock (Lock)
        {
            return TimeSpan.FromSeconds(Random.Next(5, maxSeconds));
        }
    }

    public static AsyncRetryPolicy RetryForever { get; } = Policy
        .Handle<Exception>(exception => exception is not OperationCanceledException)
        .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (exception, _) =>
        {
            if (exception != null)
                Logger.LogWarning(exception, "Error with Exchange API. Retrying...");
            else
                Logger.LogWarning("Error with Exchange API. Retrying...");
        });

    public static AsyncRetryPolicy<WebCallResult> RetryTooManyVisits { get; } = Policy
        .Handle<Exception>(exception => exception is not OperationCanceledException)
        .OrResult<WebCallResult>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
        .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
        {
            if (result.Exception != null)
                Logger.LogWarning(result.Exception, "Error with Exchange API. Retrying...");
            else
                Logger.LogWarning("Error with Exchange API. Retrying...");
            WaitWhenIpRateLimit(result.Result);
        });

    private static void WaitWhenIpRateLimit(WebCallResult? result)
    {
        if (result == null)
            return;
        if (result.Error != null &&
            result.Error.Code == (int)BybitErrorCodes.IpRateLimit)
        {
            Task.Delay(TimeSpan.FromMinutes(5)).Wait();
        }
    }
}

public static class ExchangePolicies<T>
{
    // ReSharper disable StaticMemberInGenericType
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger("Policies");
    private static readonly Random Random = new Random();
    private static readonly object Lock = new object();
    // ReSharper restore StaticMemberInGenericType

    private static TimeSpan GetRandomDelay(int maxSeconds)
    {
        lock (Lock)
        {
            return TimeSpan.FromSeconds(Random.Next(1, maxSeconds));
        }
    }

    public static AsyncRetryPolicy<WebCallResult<T>> RetryTooManyVisits { get; } = Policy
        .Handle<Exception>(exception => exception is not OperationCanceledException)
        .OrResult<WebCallResult<T>>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
        .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
        {
            if (result.Exception != null)
                Logger.LogWarning(result.Exception, "Error with Exchange API. Retrying with delay...");
            else
                Logger.LogWarning("Too many visits. Retrying with delay...");
            WaitWhenIpRateLimit(result.Result);
        });

    public static AsyncRetryPolicy<WebCallResult<BybitResponse<T>>> RetryTooManyVisitsBybitResponse { get; } = Policy
        .Handle<Exception>(exception => exception is not OperationCanceledException)
        .OrResult<WebCallResult<BybitResponse<T>>>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
        .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
        {
            if (result.Exception != null)
                Logger.LogWarning(result.Exception, "Error with Exchange API. Retrying with delay...");
            else
                Logger.LogWarning("Too many visits. Retrying with delay...");
            WaitWhenIpRateLimit(result.Result);
        });

    private static void WaitWhenIpRateLimit(WebCallResult<BybitResponse<T>>? result)
    {
        if (result == null)
            return;
        if (result.Error != null &&
            result.Error.Code == (int)BybitErrorCodes.IpRateLimit)
        {
            Task.Delay(TimeSpan.FromMinutes(5)).Wait();
        }
    }

    private static void WaitWhenIpRateLimit(WebCallResult<T>? result)
    {
        if (result == null)
            return;
        if (result.Error != null &&
            result.Error.Code == (int)BybitErrorCodes.IpRateLimit)
        {
            Task.Delay(TimeSpan.FromMinutes(5)).Wait();
        }
    }
}

public enum BybitErrorCodes
{
    LeverageNotChanged = 110043,
    PositionModeNotChanged = 110025,
    CrossModeNotModified = 110026,
    TooManyVisits = 10006,
    IpRateLimit = 10018,
}
