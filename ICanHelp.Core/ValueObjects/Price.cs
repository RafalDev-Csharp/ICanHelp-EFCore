using System.Globalization;
using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record Price
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Price(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new DomainRuleException("Price amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainRuleException("Currency must be a 3-letter ISO code.");
        }

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Price Pln(decimal amount) => new(amount, "PLN");

    public static Price operator +(Price left, Price right)
    {
        EnsureSameCurrency(left, right);
        return new Price(left.Amount + right.Amount, left.Currency);
    }

    public static Price operator -(Price left, Price right)
    {
        EnsureSameCurrency(left, right);
        return new Price(left.Amount - right.Amount, left.Currency);
    }

    private static void EnsureSameCurrency(Price left, Price right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainRuleException("Cannot operate on prices in different currencies.");
        }
    }

    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
}
