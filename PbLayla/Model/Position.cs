﻿namespace PbLayla.Model
{
    public class Position
    {
        public string Symbol { get; set; } = string.Empty;

        public PositionSide Side { get; set; }

        public decimal Quantity { get; set; }

        public decimal? AveragePrice { get; set; }

        public TradeMode TradeMode { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }

        public decimal? UnrealizedPnl { get; set; }

        public Position Clone()
        {
            return new Position
            {
                Symbol = Symbol,
                Side = Side,
                Quantity = Quantity,
                AveragePrice = AveragePrice,
                TradeMode = TradeMode,
                CreateTime = CreateTime,
                UpdateTime = UpdateTime
            };
        }
    }
}