using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record Price
    {
            public decimal Amount { get; }
            public string Currency { get; }

            public Price(decimal amount, string currency)
            {
                if (amount < 0)
                {
                    throw new ArgumentException("Amount cannot be negative.", nameof(amount));
                }

                if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                {
                    throw new ArgumentException("Currency must be a valid 3-letter ISO code.", nameof(currency));
                }

                Amount = amount;
                Currency = currency.ToUpperInvariant();
            }

            // Arithmetic operators
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
                    throw new InvalidOperationException("Cannot operate on different currencies.");
                }
            }

            public override string ToString() => $"{Amount:0.00} {Currency}";
        public static implicit operator decimal(Price value) => value.Amount;
        public static implicit operator Price(decimal value) => new(value);
    }

}
