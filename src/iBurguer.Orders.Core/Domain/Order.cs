using System.Text.Json.Serialization;
using iBurguer.Orders.Core.Abstractions;
using static iBurguer.Orders.Core.Exceptions;

namespace iBurguer.Orders.Core.Domain;

public class Order : Entity<Guid>, IAggregateRoot
{
    private IList<OrderTracking> _trackings = new List<OrderTracking>();
    private IList<OrderItem> _items;

    public Guid Id { get; set; } = Guid.NewGuid();
    public OrderNumber Number { get; init; }
    public OrderType Type { get; init; }
    public PickupCode PickupCode { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public Guid? BuyerId { get; init; }
    public DateTime CreatedAt { get; init; }
    
    private Order() {}

    public Order(OrderNumber number, OrderType type, PaymentMethod paymentMethod, Guid? buyerId, IList<OrderItem> items)
    {
        InvalidOrderNumber.ThrowIfNull(number);
        LeastOneOrderItem.ThrowIf(!items.Any());
        
        Number = number;
        Type = type;
        PickupCode = PickupCode.Generate();
        PaymentMethod = paymentMethod;
        BuyerId = buyerId;
        CreatedAt = DateTime.Now;

        foreach (var item in items)
        {
            item.SetOrder(this);
        }
        
        _items = items;
        _trackings.Add(new OrderTracking(OrderStatus.WaitingForPayment, this));
    }

    public IReadOnlyCollection<OrderTracking> Trackings => _trackings.AsReadOnly();

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Money Total => _items.Sum(i => i.Subtotal);

    public bool IsPaid => _trackings.Any(t => t.OrderStatus != OrderStatus.WaitingForPayment && t.OrderStatus != OrderStatus.Canceled);

    public OrderStatus CurrentStatus => _trackings.OrderByDescending(s => s.When).First().OrderStatus;
    
    [JsonIgnore]
    public OrderTracking CurrentTracking => _trackings.OrderByDescending(s => s.When).First();

    public void Confirm()
    {
        CannotToConfirmOrder.ThrowIf(CurrentStatus != OrderStatus.WaitingForPayment);

        _trackings.Add(new OrderTracking(OrderStatus.Confirmed, this));
    }

    public void Start()
    {
        CannotToStartOrder.ThrowIf(CurrentStatus != OrderStatus.Confirmed);

        _trackings.Add(new OrderTracking(OrderStatus.InProgress, this));
    }

    public void Complete()
    {
        CannotToCompleteOrder.ThrowIf(CurrentStatus != OrderStatus.InProgress);

        _trackings.Add(new OrderTracking(OrderStatus.ReadyForPickup, this));
    }

    public void Deliver()
    {
        CannotToDeliverOrder.ThrowIf(CurrentStatus != OrderStatus.ReadyForPickup);

        _trackings.Add(new OrderTracking(OrderStatus.PickedUp, this));
    }

    public void Cancel()
    {
        CannotToCancelOrder.ThrowIf(CurrentStatus != OrderStatus.WaitingForPayment && CurrentStatus != OrderStatus.Confirmed);

        _trackings.Add(new OrderTracking(OrderStatus.Canceled, this));
    }
}