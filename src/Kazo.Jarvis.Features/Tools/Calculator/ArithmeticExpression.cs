using System.Globalization;
using Kazo.Jarvis.Kernel.Tools;

namespace Kazo.Jarvis.Features.Tools.Calculator;

/// <summary>
/// An arithmetic expression read and computed by this code only: numbers, <c>+ - * / % ^</c>, parentheses, the
/// constants <c>pi</c> and <c>e</c> and a closed list of functions. Nothing else is understood, so no text a model
/// writes can run anything. Computed in <see cref="decimal"/>: 0.1 + 0.2 is 0.3.
/// </summary>
internal static class ArithmeticExpression
{
    /// <summary>Longest expression read.</summary>
    public const int MaximumLength = 500;

    /// <summary>Deepest nesting of parentheses and signs, so a hostile expression cannot exhaust the stack.</summary>
    private const int MaximumDepth = 64;

    /// <summary>Computes <paramref name="expression"/>.</summary>
    /// <param name="expression">The expression, for instance <c>12*7+3</c>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ToolException">The expression is not understood, divides by zero or leaves the range of numbers.</exception>
    public static decimal Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ToolException("The expression is empty.");
        }

        if (expression.Length > MaximumLength)
        {
            throw new ToolException($"The expression is longer than {MaximumLength} characters.");
        }

        try
        {
            var parser = new Parser(Normalize(expression));
            var value = parser.Sum(0);
            parser.ExpectEnd();
            return value;
        }
        catch (OverflowException)
        {
            throw new ToolException("The result is too large.");
        }
        catch (DivideByZeroException)
        {
            throw new ToolException("Division by zero.");
        }
    }

    /// <summary>The value written for the model: invariant, without trailing zeros, at most 12 decimals.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its text, for instance <c>87</c> or <c>0.3</c>.</returns>
    public static string Format(decimal value)
    {
        var rounded = Math.Round(value, 12, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>The signs a model or a transcription may write: ×, ÷, the typographic minus, the decimal comma between two digits.</summary>
    private static string Normalize(string expression)
    {
        var text = expression.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace('–', '-').Replace("**", "^", StringComparison.Ordinal);
        var characters = text.ToCharArray();
        for (var i = 1; i < characters.Length - 1; i++)
        {
            if (characters[i] == ',' && char.IsAsciiDigit(characters[i - 1]) && char.IsAsciiDigit(characters[i + 1]))
            {
                characters[i] = '.';
            }
        }

        return new string(characters);
    }

    private sealed class Parser(string text)
    {
        private int position;

        public void ExpectEnd()
        {
            SkipSpaces();
            if (position < text.Length)
            {
                throw new ToolException($"Unexpected '{text[position]}' at position {position + 1}.");
            }
        }

        public decimal Sum(int depth)
        {
            var value = Product(depth);
            while (true)
            {
                if (Take('+'))
                {
                    value += Product(depth);
                }
                else if (Take('-'))
                {
                    value -= Product(depth);
                }
                else
                {
                    return value;
                }
            }
        }

        private decimal Product(int depth)
        {
            var value = Signed(depth);
            while (true)
            {
                if (Take('*'))
                {
                    value *= Signed(depth);
                }
                else if (Take('/'))
                {
                    value /= Signed(depth);
                }
                else if (Take('%'))
                {
                    value %= Signed(depth);
                }
                else
                {
                    return value;
                }
            }
        }

        // A sign binds less than a power: -2^2 is -4.
        private decimal Signed(int depth)
        {
            if (depth > MaximumDepth)
            {
                throw new ToolException("The expression is nested too deeply.");
            }

            if (Take('-'))
            {
                return -Signed(depth + 1);
            }

            return Take('+') ? Signed(depth + 1) : Power(depth);
        }

        // Right to left: 2^3^2 is 2^9.
        private decimal Power(int depth)
        {
            var value = Atom(depth);
            return Take('^') ? Raise(value, Signed(depth + 1)) : value;
        }

        private decimal Atom(int depth)
        {
            SkipSpaces();
            if (position >= text.Length)
            {
                throw new ToolException("The expression ends too soon.");
            }

            if (Take('('))
            {
                var inner = Sum(depth + 1);
                if (!Take(')'))
                {
                    throw new ToolException("A parenthesis is not closed.");
                }

                return inner;
            }

            var current = text[position];
            if (char.IsAsciiDigit(current) || current == '.')
            {
                return Number();
            }

            if (char.IsAsciiLetter(current))
            {
                var start = position;
                while (position < text.Length && char.IsAsciiLetter(text[position]))
                {
                    position++;
                }

                return Named(text[start..position].ToLowerInvariant(), depth);
            }

            throw new ToolException($"Unexpected '{current}' at position {position + 1}.");
        }

        private decimal Number()
        {
            var start = position;
            while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.'))
            {
                position++;
            }

            // An exponent such as 1e3, only when digits follow: "2e" alone is 2 times the constant e, refused below.
            if (position < text.Length && text[position] is 'e' or 'E')
            {
                var after = position + 1;
                if (after < text.Length && text[after] is '+' or '-')
                {
                    after++;
                }

                if (after < text.Length && char.IsAsciiDigit(text[after]))
                {
                    position = after;
                    while (position < text.Length && char.IsAsciiDigit(text[position]))
                    {
                        position++;
                    }
                }
            }

            var literal = text[start..position];
            return decimal.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ToolException($"'{literal}' is not a number.");
        }

        private decimal Named(string name, int depth)
        {
            switch (name)
            {
                case "pi":
                    return (decimal)Math.PI;
                case "e":
                    return (decimal)Math.E;
            }

            if (!Take('('))
            {
                throw new ToolException($"'{name}' is not understood.");
            }

            var argument = Sum(depth + 1);
            if (!Take(')'))
            {
                throw new ToolException("A parenthesis is not closed.");
            }

            return name switch
            {
                "sqrt" => argument < 0 ? throw new ToolException("The square root of a negative number is not a real number.") : FromDouble(Math.Sqrt((double)argument)),
                "abs" => Math.Abs(argument),
                "round" => Math.Round(argument, MidpointRounding.AwayFromZero),
                "floor" => Math.Floor(argument),
                "ceil" => Math.Ceiling(argument),
                _ => throw new ToolException($"The function '{name}' is not understood."),
            };
        }

        private static decimal Raise(decimal value, decimal exponent)
        {
            // Whole exponents stay exact; the others go through floating point.
            if (exponent == Math.Truncate(exponent) && Math.Abs(exponent) <= 1000)
            {
                var result = 1m;
                for (var i = 0; i < (int)Math.Abs(exponent); i++)
                {
                    result *= value;
                }

                return exponent < 0 ? 1m / result : result;
            }

            return FromDouble(Math.Pow((double)value, (double)exponent));
        }

        private static decimal FromDouble(double value) =>
            double.IsNaN(value) ? throw new ToolException("The result is not a real number.")
            : double.IsInfinity(value) ? throw new OverflowException()
            : (decimal)value;

        private bool Take(char expected)
        {
            SkipSpaces();
            if (position < text.Length && text[position] == expected)
            {
                position++;
                return true;
            }

            return false;
        }

        private void SkipSpaces()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }
    }
}
