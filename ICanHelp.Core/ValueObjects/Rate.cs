using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{

        public sealed class Rate
        {
            public double Value { get; }

            public Rate(double value)
            {
                if (value < 1 || value > 5)
                    throw new ArgumentOutOfRangeException(nameof(value), "Rating must be between 1 and 5.");

                Value = value;
            }

            public static Rate Create(double value)
            {
                return new Rate(value);
            }

            public override string ToString() => $"{Value} star{(Value > 1 ? "s" : "")}";

            // Optional: Equality logic
            public override bool Equals(object obj) =>
                obj is Rate other && Value.Equals(other.Value);

        public override int GetHashCode() => Value.GetHashCode();
        }

}
