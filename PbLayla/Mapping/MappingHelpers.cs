using PbLayla.Model;

namespace PbLayla.Mapping
{
    public static class MappingHelpers
    {
        public static Balance ToBalance(this Bybit.Net.Objects.Models.V5.BybitAssetBalance balance)
        {
            return new Balance(
                balance.Equity,
                balance.WalletBalance,
                balance.UnrealizedPnl,
                balance.RealizedPnl);
        }

        public static Position? ToPosition(this Bybit.Net.Objects.Models.V5.BybitPosition value)
        {
            var position = new Position
            {
                AveragePrice = value.AveragePrice,
                Quantity = value.Quantity,
                Side = value.Side.ToPositionSide(),
                Symbol = value.Symbol,
                TradeMode = value.TradeMode.ToTradeMode(),
                CreateTime = value.CreateTime ?? DateTime.UtcNow,
                UpdateTime = value.UpdateTime ?? DateTime.UtcNow,
                UnrealizedPnl = value.UnrealizedPnl
            };

            if (position.UpdateTime < position.CreateTime)
                position.UpdateTime = position.CreateTime;

            return position;
        }

        public static TradeType ToTradeType(this Bybit.Net.Enums.TradeType value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.TradeType.Trade:
                    return TradeType.Trade;
                case Bybit.Net.Enums.TradeType.AdlTrade:
                    return TradeType.AdlTrade;
                case Bybit.Net.Enums.TradeType.Funding:
                    return TradeType.Funding;
                case Bybit.Net.Enums.TradeType.BustTrade:
                    return TradeType.BustTrade;
                case Bybit.Net.Enums.TradeType.Settle:
                    return TradeType.Settle;
                case Bybit.Net.Enums.TradeType.Delivery:
                    return TradeType.Delivery;
                default:
                    return TradeType.Unknown;
            }
        }

        public static PositionSide ToPositionSide(this Bybit.Net.Enums.PositionSide? value)
        {
            if (!value.HasValue)
                return PositionSide.None;
            switch (value)
            {
                case Bybit.Net.Enums.PositionSide.Buy:
                    return PositionSide.Buy;
                case Bybit.Net.Enums.PositionSide.Sell:
                    return PositionSide.Sell;
                case Bybit.Net.Enums.PositionSide.None:
                    return PositionSide.None;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static TradeMode ToTradeMode(this Bybit.Net.Enums.TradeMode value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.TradeMode.CrossMargin:
                    return TradeMode.CrossMargin;
                case Bybit.Net.Enums.TradeMode.Isolated:
                    return TradeMode.Isolated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static Ticker ToTicker(this Bybit.Net.Objects.Models.V5.BybitLinearInverseTicker value)
        {
            return new Ticker
            {
                BestAskPrice = value.BestAskPrice,
                BestBidPrice = value.BestBidPrice,
                LastPrice = value.LastPrice,
                Timestamp = DateTime.UtcNow,
                FundingRate = value.FundingRate,
                Symbol = value.Symbol
            };
        }

        public static Order ToOrder(this Bybit.Net.Objects.Models.V5.BybitOrder value)
        {
            return new Order
            {
                Symbol = value.Symbol,
                Price = value.Price,
                AveragePrice = value.AveragePrice,
                OrderId = value.OrderId,
                PositionMode = value.PositionIdx.ToPositionMode(),
                Quantity = value.Quantity,
                Side = value.Side.ToOrderSide(),
                QuantityFilled = value.QuantityFilled,
                QuantityRemaining = value.QuantityRemaining,
                Status = value.Status.ToOrderStatus(),
                ValueFilled = value.ValueFilled,
                ValueRemaining = value.ValueRemaining,
                ReduceOnly = value.ReduceOnly,
                CreateTime = value.CreateTime,
            };
        }

        public static OrderSide ToOrderSide(this Bybit.Net.Enums.OrderSide value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.OrderSide.Buy:
                    return OrderSide.Buy;
                case Bybit.Net.Enums.OrderSide.Sell:
                    return OrderSide.Sell;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static PositionIdx? ToPositionMode(this Bybit.Net.Enums.V5.PositionIdx? value)
        {
            if (value == null)
                return null;
            switch (value)
            {
                case Bybit.Net.Enums.V5.PositionIdx.OneWayMode:
                    return PositionIdx.OneWayMode;
                case Bybit.Net.Enums.V5.PositionIdx.BuyHedgeMode:
                    return PositionIdx.BuyHedgeMode;
                case Bybit.Net.Enums.V5.PositionIdx.SellHedgeMode:
                    return PositionIdx.SellHedgeMode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static OrderStatus ToOrderStatus(this Bybit.Net.Enums.V5.OrderStatus value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.V5.OrderStatus.Created:
                    return OrderStatus.Created;
                case Bybit.Net.Enums.V5.OrderStatus.New:
                    return OrderStatus.New;
                case Bybit.Net.Enums.V5.OrderStatus.Rejected:
                    return OrderStatus.Rejected;
                case Bybit.Net.Enums.V5.OrderStatus.PartiallyFilled:
                    return OrderStatus.PartiallyFilled;
                case Bybit.Net.Enums.V5.OrderStatus.PartiallyFilledCanceled:
                    return OrderStatus.PartiallyFilledCanceled;
                case Bybit.Net.Enums.V5.OrderStatus.Filled:
                    return OrderStatus.Filled;
                case Bybit.Net.Enums.V5.OrderStatus.Cancelled:
                    return OrderStatus.Cancelled;
                case Bybit.Net.Enums.V5.OrderStatus.Untriggered:
                    return OrderStatus.Untriggered;
                case Bybit.Net.Enums.V5.OrderStatus.Triggered:
                    return OrderStatus.Triggered;
                case Bybit.Net.Enums.V5.OrderStatus.Deactivated:
                    return OrderStatus.Deactivated;
                case Bybit.Net.Enums.V5.OrderStatus.Active:
                    return OrderStatus.Active;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }
}