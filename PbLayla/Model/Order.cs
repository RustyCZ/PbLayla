namespace PbLayla.Model;

public class Order
{
    public string OrderId { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public decimal? Price { get; set; }

    public decimal Quantity { get; set; }

    public OrderSide Side { get; set; }

    public PositionIdx? PositionMode { get; set; }

    public OrderStatus Status { get; set; }

    public decimal? AveragePrice { get; set; }

    public decimal? QuantityRemaining { get; set; }

    public decimal? ValueRemaining { get; set; }

    public decimal? QuantityFilled { get; set; }

    public decimal? ValueFilled { get; set; }

    public bool? ReduceOnly { get; set; }

    public DateTime CreateTime { get; set; }
}