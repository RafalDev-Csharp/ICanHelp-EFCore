using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record CustomDateTime
    {
        public DateTime Value { get; }

        public CustomDateTime(DateTime value)
        {
            // Walidacja daty. defalut DateTime.MinValue;
            if (value == default)
            {
                throw new ArgumentException("DateTime cannot be default.", nameof(value));
            }

            // Walidacja zakresu (nie pozwalamy na daty dalej niz rok w przyszlosc i daty wczorajszej)
            if (value < DateTime.UtcNow.AddYears(1) || value > DateTime.UtcNow.AddDays(-1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "DateTime must be within a valid range.");
            }

            // Walidacja czasu (godziny od 0 do 23, minuty od 0 do 59)
            if (value.Hour < 0 || value.Hour > 23 || value.Minute < 0 || value.Minute > 59)
            {
                throw new ArgumentException("Invalid time component in DateTime.", nameof(value));
            }

            Value = value;
        }

        // Konwersje implicit
        public static implicit operator CustomDateTime(DateTime value) => new(value);
        public static implicit operator DateTime(CustomDateTime customDateTime) => customDateTime.Value;

        // Formatowanie
        public override string ToString() => Value.ToString("yyyy-MM-dd HH:mm:ss");
    }

}
